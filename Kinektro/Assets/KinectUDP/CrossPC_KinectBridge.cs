using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Puente UDP del lado PC B (Kinect). Esta PC NO tiene DJ_Manager/FMOD: solo tiene el
/// Kinect (via KinectUdpReceiver, canal LOCAL existente en el puerto 5005) y el
/// GestureDetector ya calculando gestos sobre esos datos. Este script reenvia esos
/// gestos (mano levantada/bajada + altura continua) a la PC A (Oculus), y expone el
/// metodo SelectEffectByVoice para que un reconocedor de voz (ver VoiceEffectSelector.cs)
/// le diga a la PC A que efecto armar en cada canal.
///
/// Cableado en el Inspector de esta escena (PC B):
///   GestureDetector.onLeftHandRaised    -> este componente . OnLeftHandRaised()
///   GestureDetector.onLeftHandLowered   -> este componente . OnLeftHandLowered()
///   GestureDetector.onRightHandRaised   -> este componente . OnRightHandRaised()
///   GestureDetector.onRightHandLowered  -> este componente . OnRightHandLowered()
///   GestureDetector.onLeftHandHeightChanged  -> este componente . OnLeftHandHeightChanged(float)
///   GestureDetector.onRightHandHeightChanged -> este componente . OnRightHandHeightChanged(float)
///
/// En esta escena, el GestureDetector puede dejar djManager/fxSelectorCh1/fxSelectorCh2/meterCh1/
/// meterCh2 SIN asignar (null): el patch en GestureDetector.cs hace que las alturas se sigan
/// calculando y emitiendo igual, sin esos componentes locales que solo existen en PC A.
///
/// Protocolo: ver comentario en CrossPC_OculusBridge.cs (es el mismo, visto desde el otro lado).
/// </summary>
public class CrossPC_KinectBridge : MonoBehaviour
{
    [Header("Red")]
    [Tooltip("Puerto local en el que esta PC (Kinect) escucha mensajes de la PC del Oculus.")]
    public int listenPort = 6006;
    [Tooltip("IP de la PC del Oculus (PC A) en la red local.")]
    public string remoteIP = "192.168.1.40";
    [Tooltip("Puerto en el que la PC del Oculus escucha (debe coincidir con listenPort de CrossPC_OculusBridge en esa PC).")]
    public int remotePort = 6005;
    [Tooltip("Cuantas veces se repite el envio de un mensaje discreto importante (SELECT_EFFECT), para compensar la falta de confirmacion de UDP.")]
    public int redundantSends = 3;

    [Header("Comportamiento")]
    [Tooltip("Si esta activo, SelectEffectByVoice ignora (solo loguea advertencia) una seleccion de voz si la PC A no abrio ventana para ese canal. Si esta desactivado, siempre se envia.")]
    public bool requireWindowOpenForVoiceSelection = false;

    [Header("Prompts de pose para el Pipeline (Jugador B)")]
    [Tooltip("PoseIdentifier de esta escena, para saber en vivo que pose esta haciendo el jugador B. Si se deja vacio, se busca automaticamente con FindObjectOfType al recibir el primer PIPELINE_HIT.")]
    public PoseIdentifier poseIdentifier;
    [Tooltip("Cuantos segundos tiene el jugador B para lograr la pose pedida antes de marcarse como FALLASTE.")]
    public float segundosParaHacerGesto = 3f;

    // Que pose le mostramos al jugador B segun que control toco el jugador A. Editable
    // desde el Inspector - cambia esto si tus poses grabadas tienen otros nombres, o si
    // querés repartir los controles entre las poses de otra forma.
    [System.Serializable]
    public struct MapeoControlPose
    {
        public DJControlTarget control;
        public string nombrePose;
    }

    [Tooltip("Mapeo de que pose debe hacer el jugador B segun el control que acerto el jugador A. Los nombres de pose tienen que coincidir EXACTO con los nombres guardados en PoseMapperUI.")]
    public List<MapeoControlPose> mapeoControlAPose = new List<MapeoControlPose>
    {
        // Los primeros 3 son los que de verdad usa Pipeline_Prueba1.asset (el chart de
        // prueba ya existente) - quedan repartidos en las 3 poses "activas" que tenes
        // grabadas, asi la demo pide gestos distintos en cada nodo en vez de repetir
        // siempre el mismo. El resto queda mapeado por si agregas mas nodos despues.
        new MapeoControlPose { control = DJControlTarget.VolFader_Ch1,    nombrePose = "BrazosArriba" },
        new MapeoControlPose { control = DJControlTarget.EqLow_Ch1,       nombrePose = "DerechaArriba" },
        new MapeoControlPose { control = DJControlTarget.PlayButton_Ch1,  nombrePose = "IzquierdaArriba" },
        new MapeoControlPose { control = DJControlTarget.CueButton_Ch1,   nombrePose = "DerechaArriba" },
        new MapeoControlPose { control = DJControlTarget.LoopToggle_Ch1,  nombrePose = "IzquierdaArriba" },
        new MapeoControlPose { control = DJControlTarget.VolFader_Ch2,    nombrePose = "DerechaArriba" },
        new MapeoControlPose { control = DJControlTarget.Crossfader,      nombrePose = "IzquierdaArriba" },
        new MapeoControlPose { control = DJControlTarget.EqMid_Ch1,       nombrePose = "DerechaArriba" },
        new MapeoControlPose { control = DJControlTarget.EqHigh_Ch1,      nombrePose = "IzquierdaArriba" },
        new MapeoControlPose { control = DJControlTarget.TempoFader_Ch1,  nombrePose = "BrazosArriba" },
    };

    private Text promptPoseText;
    private Text promptResultadoText;
    private Coroutine promptCoroutineActual;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool running = false;

    private readonly Queue<string> inboundQueue = new Queue<string>();
    private readonly object queueLock = new object();

    // Estado de "ventana abierta" reportado por la PC A, por canal.
    private readonly Dictionary<int, bool> windowOpenByChannel = new Dictionary<int, bool>();
    private readonly Dictionary<int, HashSet<int>> availableEffectsByChannel = new Dictionary<int, HashSet<int>>();
    private readonly Dictionary<int, float> lastFaderByChannel = new Dictionary<int, float>();

    void Start()
    {
        udpClient = new UdpClient(listenPort);
        running = true;
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        ConstruirUIPrompt();

        Debug.Log($"[CrossPC_KinectBridge] Escuchando a la PC del Oculus en el puerto UDP {listenPort}. Enviando a {remoteIP}:{remotePort}.");
    }

    // Canvas minimo armado por codigo (igual estilo que PoseMapperUI/DJPipelineEvaluator):
    // un texto grande centrado para "HACE: <pose>" y, debajo, el resultado (BIEN/FALLASTE)
    // una vez que se acaba el tiempo o el jugador logra la pose. Arranca oculto.
    private void ConstruirUIPrompt()
    {
        var canvasGO = new GameObject("PipelinePrompt_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        var promptGO = new GameObject("PromptPoseText", typeof(RectTransform));
        promptGO.transform.SetParent(canvasGO.transform, false);
        var rt = promptGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 80f);
        rt.sizeDelta = new Vector2(1500f, 300f);

        promptPoseText = promptGO.AddComponent<Text>();
        promptPoseText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        promptPoseText.fontSize = 90;
        promptPoseText.fontStyle = FontStyle.Bold;
        promptPoseText.alignment = TextAnchor.MiddleCenter;
        promptPoseText.color = new Color(1f, 0.85f, 0.2f);
        promptPoseText.text = "";
        promptGO.SetActive(false);

        var resultGO = new GameObject("PromptResultadoText", typeof(RectTransform));
        resultGO.transform.SetParent(canvasGO.transform, false);
        var rt2 = resultGO.GetComponent<RectTransform>();
        rt2.anchorMin = new Vector2(0.5f, 0.5f);
        rt2.anchorMax = new Vector2(0.5f, 0.5f);
        rt2.pivot = new Vector2(0.5f, 0.5f);
        rt2.anchoredPosition = new Vector2(0f, -140f);
        rt2.sizeDelta = new Vector2(1000f, 140f);

        promptResultadoText = resultGO.AddComponent<Text>();
        promptResultadoText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        promptResultadoText.fontSize = 60;
        promptResultadoText.fontStyle = FontStyle.Bold;
        promptResultadoText.alignment = TextAnchor.MiddleCenter;
        promptResultadoText.color = Color.white;
        promptResultadoText.text = "";
        resultGO.SetActive(false);
    }

    private void ReceiveLoop()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref anyIP);
                string message = Encoding.UTF8.GetString(data);

                lock (queueLock)
                {
                    inboundQueue.Enqueue(message);
                }
            }
            catch (Exception)
            {
                // el socket se cerro o hubo un error de red; salimos silenciosamente
            }
        }
    }

    void Update()
    {
        List<string> toProcess = null;

        lock (queueLock)
        {
            if (inboundQueue.Count > 0)
            {
                toProcess = new List<string>(inboundQueue);
                inboundQueue.Clear();
            }
        }

        if (toProcess == null) return;

        foreach (string message in toProcess)
        {
            HandleMessage(message);
        }
    }

    private void HandleMessage(string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length == 0) return;

        switch (parts[0])
        {
            case "WINDOW_OPEN":
                if (parts.Length >= 3) HandleWindowOpen(parts[1], parts[2]);
                break;

            case "WINDOW_CLOSE":
                if (parts.Length >= 2) HandleWindowClose(parts[1]);
                break;

            case "FADER":
                if (parts.Length >= 3) HandleFader(parts[1], parts[2]);
                break;

            case "PIPELINE_WINDOW_OPEN":
                HandlePipelineWindowOpen();
                break;

            case "PIPELINE_HIT":
                if (parts.Length >= 3) HandlePipelineHit(parts[1], parts[2]);
                break;

            case "PIPELINE_WINDOW_CLOSE":
                if (parts.Length >= 2) HandlePipelineWindowClose(parts[1]);
                break;

            default:
                Debug.LogWarning($"[CrossPC_KinectBridge] Mensaje no reconocido: {message}");
                break;
        }
    }

    private void HandleWindowOpen(string channelStr, string slotsCsv)
    {
        if (!int.TryParse(channelStr, out int channel)) return;

        var slots = new HashSet<int>();
        foreach (string s in slotsCsv.Split(','))
        {
            if (int.TryParse(s, out int slot)) slots.Add(slot);
        }

        windowOpenByChannel[channel] = true;
        availableEffectsByChannel[channel] = slots;

        Debug.Log($"[CrossPC_KinectBridge] Ventana ABIERTA canal {channel}. Efectos disponibles: {string.Join(",", slots)}");
    }

    private void HandleWindowClose(string channelStr)
    {
        if (!int.TryParse(channelStr, out int channel)) return;

        windowOpenByChannel[channel] = false;
        Debug.Log($"[CrossPC_KinectBridge] Ventana CERRADA canal {channel}.");
    }

    private void HandleFader(string channelStr, string valueStr)
    {
        if (!int.TryParse(channelStr, out int channel)) return;
        if (!float.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value01)) return;

        lastFaderByChannel[channel] = Mathf.Clamp01(value01);
    }

    // --- Pipeline de DJPipelineEvaluator (PC A): por ahora solo logueamos, es la prueba
    // chica para confirmar que el dato llega bien antes de conectarlo con gestos reales. ---

    private void HandlePipelineWindowOpen()
    {
        Debug.Log("<color=yellow>[CrossPC_KinectBridge] PIPELINE: arranco la ventana de evaluacion en PC A.</color>");
    }

    private void HandlePipelineHit(string controlStr, string comboStr)
    {
        Debug.Log($"<color=green>[CrossPC_KinectBridge] PIPELINE HIT recibido: control={controlStr} combo={comboStr}</color>");

        if (!Enum.TryParse(controlStr, out DJControlTarget control))
        {
            Debug.LogWarning($"[CrossPC_KinectBridge] Control de pipeline desconocido: '{controlStr}'.");
            return;
        }

        string poseEsperada = null;
        foreach (var mapeo in mapeoControlAPose)
        {
            if (mapeo.control == control) { poseEsperada = mapeo.nombrePose; break; }
        }

        if (string.IsNullOrEmpty(poseEsperada))
        {
            Debug.LogWarning($"[CrossPC_KinectBridge] No hay pose mapeada para el control '{control}'. Revisa mapeoControlAPose en el Inspector.");
            return;
        }

        if (poseIdentifier == null) poseIdentifier = FindObjectOfType<PoseIdentifier>();
        if (poseIdentifier == null)
        {
            Debug.LogWarning("[CrossPC_KinectBridge] No se encontro un PoseIdentifier en la escena - no puedo chequear si el jugador B logra la pose.");
        }

        if (promptCoroutineActual != null) StopCoroutine(promptCoroutineActual);
        promptCoroutineActual = StartCoroutine(PromptGestoRoutine(poseEsperada));
    }

    // Muestra "HACE: <pose>" en pantalla y chequea, cuadro a cuadro, si PoseIdentifier
    // reconoce esa misma pose antes de que se acabe segundosParaHacerGesto. Al terminar
    // (por exito o por tiempo agotado) muestra el resultado un segundo y lo oculta.
    private IEnumerator PromptGestoRoutine(string poseEsperada)
    {
        if (promptPoseText == null) yield break;

        promptResultadoText.gameObject.SetActive(false);
        promptPoseText.gameObject.SetActive(true);
        promptPoseText.text = $"HACE:\n{poseEsperada}";

        float restante = segundosParaHacerGesto;
        bool logrado = false;

        while (restante > 0f)
        {
            if (poseIdentifier != null && poseIdentifier.PoseActualReconocida == poseEsperada)
            {
                logrado = true;
                break;
            }
            restante -= Time.deltaTime;
            yield return null;
        }

        promptPoseText.gameObject.SetActive(false);
        promptResultadoText.gameObject.SetActive(true);
        promptResultadoText.text = logrado ? "¡BIEN!" : "FALLASTE";
        promptResultadoText.color = logrado ? new Color(0.3f, 1f, 0.45f) : new Color(1f, 0.3f, 0.3f);

        yield return new WaitForSeconds(1f);
        promptResultadoText.gameObject.SetActive(false);
    }

    private void HandlePipelineWindowClose(string scoreStr)
    {
        Debug.Log($"<color=magenta>[CrossPC_KinectBridge] PIPELINE: se cerro la ventana. Score final recibido: {scoreStr}.</color>");
    }

    /// <summary>Util para que la UI/voz de esta PC sepa si puede ofrecer seleccion de efecto ahora mismo.</summary>
    public bool IsWindowOpen(int channel) => windowOpenByChannel.TryGetValue(channel, out bool open) && open;

    public IReadOnlyCollection<int> GetAvailableEffects(int channel) =>
        availableEffectsByChannel.TryGetValue(channel, out var slots) ? (IReadOnlyCollection<int>)slots : Array.Empty<int>();

    public float GetLastFader(int channel) => lastFaderByChannel.TryGetValue(channel, out float v) ? v : 0f;

    // --- Callbacks para cablear en el Inspector desde GestureDetector (eventos de esta PC) ---

    public void OnLeftHandRaised() => SendReliable("HAND_RAISED|LEFT");
    public void OnLeftHandLowered() => SendReliable("HAND_LOWERED|LEFT");
    public void OnRightHandRaised() => SendReliable("HAND_RAISED|RIGHT");
    public void OnRightHandLowered() => SendReliable("HAND_LOWERED|RIGHT");

    public void OnLeftHandHeightChanged(float value01) => SendOnce($"HAND_HEIGHT|LEFT|{value01.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    public void OnRightHandHeightChanged(float value01) => SendOnce($"HAND_HEIGHT|RIGHT|{value01.ToString(System.Globalization.CultureInfo.InvariantCulture)}");

    /// <summary>Llamar desde el reconocedor de voz (ver VoiceEffectSelector.cs) cuando el usuario de esta PC dice el nombre de un efecto. slot: 1=Reverb,2=Delay,3=Distortion,4=LowPass,5=Flanger.</summary>
    public void SelectEffectByVoice(int channel, int slot)
    {
        if (requireWindowOpenForVoiceSelection && !IsWindowOpen(channel))
        {
            Debug.LogWarning($"[CrossPC_KinectBridge] Seleccion de voz canal={channel} slot={slot} ignorada: la ventana de efectos no esta abierta.");
            return;
        }

        SendReliable($"SELECT_EFFECT|{channel}|{slot}");
        Debug.Log($"[CrossPC_KinectBridge] Efecto seleccionado por voz: canal={channel} slot={slot}");
    }

    private void SendReliable(string message)
    {
        Debug.Log($"[CrossPC_KinectBridge] Enviando (x{Mathf.Max(1, redundantSends)}): {message}");
        for (int i = 0; i < Mathf.Max(1, redundantSends); i++)
        {
            SendOnce(message);
        }
    }

    private void SendOnce(string message)
    {
        if (udpClient == null) return;

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            udpClient.Send(data, data.Length, remoteIP, remotePort);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[CrossPC_KinectBridge] No se pudo enviar '{message}' a {remoteIP}:{remotePort}: {e.Message}");
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        udpClient?.Close();

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(500);
        }
    }
}
