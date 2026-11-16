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

    private GameObject canvasPromptGO;
    private Text textoEstado;
    private Text promptPoseText;
    private Text promptResultadoText;
    private Coroutine promptCoroutineActual;

    // Estado de conexion (para el indicador de la esquina superior derecha, siempre visible)
    private Text textoConexion;
    private bool huboRecibido = false;
    private float ultimoRecibidoTiempo = 0f;
    private string ultimoRecibidoMsg = "";
    private bool huboEnviado = false;
    private float ultimoEnviadoTiempo = 0f;
    private string ultimoEnviadoMsg = "";

    // Deteccion de desconexion: mandamos un PING propio cada INTERVALO_PING segundos (haya
    // o no otra actividad) para tener siempre trafico con que medir si la otra PC sigue ahi.
    // Si pasan mas de UMBRAL_DESCONEXION segundos sin recibir NADA (ni PING ni ningun otro
    // mensaje), asumimos que la otra PC se desconecto.
    private const float INTERVALO_PING = 1f;
    private const float UMBRAL_DESCONEXION = 4f;
    private float proximoPingTiempo = 0f;
    private Text textoAlertaDesconexion;
    private bool conectadoPrevio = true;

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
        ConstruirUIConexion();

        Debug.Log($"[CrossPC_KinectBridge] Escuchando a la PC del Oculus en el puerto UDP {listenPort}. Enviando a {remoteIP}:{remotePort}.");
    }

    // En Unity 2022 "Arial.ttf" como recurso incorporado ya no existe (fue reemplazado
    // por "LegacyRuntime.ttf") y devuelve null en silencio, dejando el Text sin fuente
    // (por eso no se veia NINGUN texto de este script, aunque los GameObjects estuvieran
    // activos). Probamos primero el nombre nuevo y si no esta, caemos al viejo.
    private Font ObtenerFuenteUI()
    {
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    // Canvas aparte, SIEMPRE activo (no depende de canvasMapeoGO ni canvasPromptGO), con
    // sorting order alto para quedar por encima de todo: un indicador chico en la esquina
    // superior derecha para confirmar a simple vista que los datos realmente estan viajando
    // entre las dos PCs en ambos sentidos (y hace cuanto fue el ultimo mensaje de cada lado).
    private void ConstruirUIConexion()
    {
        var canvasGO = new GameObject("ConexionStatus_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var go = new GameObject("TextoConexion", typeof(RectTransform));
        go.transform.SetParent(canvasGO.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-20f, -20f);
        rt.sizeDelta = new Vector2(680f, 110f);

        textoConexion = go.AddComponent<Text>();
        textoConexion.font = ObtenerFuenteUI();
        textoConexion.fontSize = 20;
        textoConexion.fontStyle = FontStyle.Bold;
        textoConexion.alignment = TextAnchor.UpperRight;
        textoConexion.color = Color.white;
        textoConexion.text = "PC A -> B: sin datos aun\nB -> PC A: sin datos aun";

        // Banner grande de alerta (oculto hasta que se detecte una desconexion real).
        var alertaGO = new GameObject("TextoAlertaDesconexion", typeof(RectTransform));
        alertaGO.transform.SetParent(canvasGO.transform, false);
        var alertaRT = alertaGO.GetComponent<RectTransform>();
        alertaRT.anchorMin = new Vector2(0.5f, 1f);
        alertaRT.anchorMax = new Vector2(0.5f, 1f);
        alertaRT.pivot = new Vector2(0.5f, 1f);
        alertaRT.anchoredPosition = new Vector2(0f, -20f);
        alertaRT.sizeDelta = new Vector2(1400f, 90f);

        textoAlertaDesconexion = alertaGO.AddComponent<Text>();
        textoAlertaDesconexion.font = ObtenerFuenteUI();
        textoAlertaDesconexion.fontSize = 42;
        textoAlertaDesconexion.fontStyle = FontStyle.Bold;
        textoAlertaDesconexion.alignment = TextAnchor.UpperCenter;
        textoAlertaDesconexion.color = new Color(1f, 0.3f, 0.3f);
        textoAlertaDesconexion.text = "¡ALERTA! JUGADOR A DESCONECTADO - Esperando a que se reconecte...";
        alertaGO.SetActive(false);
    }

    // Se llama todos los frames (haya o no mensajes nuevos) para que el "hace X.Xs" del
    // indicador se siga actualizando en vivo, no solo cuando llega algo.
    private void ActualizarTextoConexion()
    {
        if (textoConexion == null) return;

        string recibido = huboRecibido
            ? $"{ultimoRecibidoMsg} (hace {(Time.time - ultimoRecibidoTiempo):F1}s)"
            : "sin datos aun";
        string enviado = huboEnviado
            ? $"{ultimoEnviadoMsg} (hace {(Time.time - ultimoEnviadoTiempo):F1}s)"
            : "sin datos aun";

        bool recibidoOk = huboRecibido && (Time.time - ultimoRecibidoTiempo) < 5f;
        bool enviadoOk = huboEnviado && (Time.time - ultimoEnviadoTiempo) < 5f;

        textoConexion.text = $"PC A -> B: {recibido}\nB -> PC A: {enviado}";
        textoConexion.color = (recibidoOk && enviadoOk) ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.75f, 0.3f);
    }

    // Se llama todos los frames: prende/apaga el banner de alerta segun si hace mas de
    // UMBRAL_DESCONEXION segundos que no llega nada de la PC A, y corta cualquier prompt
    // de pose en curso justo en el momento en que se detecta la desconexion (no en cada
    // frame despues). La musica de la PC A no depende de esto, sigue sonando igual.
    private void ActualizarAlertaDesconexion()
    {
        if (textoAlertaDesconexion == null) return;

        if (!huboRecibido)
        {
            textoAlertaDesconexion.gameObject.SetActive(false);
            return;
        }

        bool conectadoAhora = (Time.time - ultimoRecibidoTiempo) < UMBRAL_DESCONEXION;
        textoAlertaDesconexion.gameObject.SetActive(!conectadoAhora);

        if (conectadoPrevio && !conectadoAhora)
        {
            if (promptCoroutineActual != null) StopCoroutine(promptCoroutineActual);
            if (promptPoseText != null) promptPoseText.gameObject.SetActive(false);
            if (promptResultadoText != null) promptResultadoText.gameObject.SetActive(false);
        }

        conectadoPrevio = conectadoAhora;
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

        // Fondo opaco a pantalla completa: sin esto, al ocultar la pantalla de mapeo
        // (PoseMapperUI) se veia la escena 3D de atras (la consola DJ, igual a lo que
        // ve el Jugador A). Con este fondo, la pantalla del Jugador B siempre muestra
        // una pantalla propia y limpia, nunca la vista 3D del otro jugador.
        var fondoGO = new GameObject("FondoPantallaJuego", typeof(RectTransform));
        fondoGO.transform.SetParent(canvasGO.transform, false);
        var fondoRT = fondoGO.GetComponent<RectTransform>();
        fondoRT.anchorMin = Vector2.zero;
        fondoRT.anchorMax = Vector2.one;
        fondoRT.offsetMin = Vector2.zero;
        fondoRT.offsetMax = Vector2.zero;
        var fondoImg = fondoGO.AddComponent<Image>();
        fondoImg.color = new Color(0.05f, 0.05f, 0.09f, 1f);

        // Texto de estado general (entre rondas del pipeline): "esperando al jugador A",
        // y al terminar, el score final. Mientras hay una pose pedida, este texto se
        // oculta y le cede el lugar a promptPoseText/promptResultadoText de mas abajo.
        var estadoGO = new GameObject("TextoEstado", typeof(RectTransform));
        estadoGO.transform.SetParent(canvasGO.transform, false);
        var estadoRT = estadoGO.GetComponent<RectTransform>();
        estadoRT.anchorMin = new Vector2(0.5f, 0.5f);
        estadoRT.anchorMax = new Vector2(0.5f, 0.5f);
        estadoRT.pivot = new Vector2(0.5f, 0.5f);
        estadoRT.anchoredPosition = Vector2.zero;
        estadoRT.sizeDelta = new Vector2(1700f, 400f);

        textoEstado = estadoGO.AddComponent<Text>();
        textoEstado.font = ObtenerFuenteUI();
        textoEstado.fontSize = 55;
        textoEstado.fontStyle = FontStyle.Bold;
        textoEstado.alignment = TextAnchor.MiddleCenter;
        textoEstado.color = Color.white;
        textoEstado.text = "Esperando a que el Jugador A presione Play...";

        var promptGO = new GameObject("PromptPoseText", typeof(RectTransform));
        promptGO.transform.SetParent(canvasGO.transform, false);
        var rt = promptGO.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 80f);
        rt.sizeDelta = new Vector2(1500f, 300f);

        promptPoseText = promptGO.AddComponent<Text>();
        promptPoseText.font = ObtenerFuenteUI();
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
        promptResultadoText.font = ObtenerFuenteUI();
        promptResultadoText.fontSize = 60;
        promptResultadoText.fontStyle = FontStyle.Bold;
        promptResultadoText.alignment = TextAnchor.MiddleCenter;
        promptResultadoText.color = Color.white;
        promptResultadoText.text = "";
        resultGO.SetActive(false);

        canvasPromptGO = canvasGO;
        canvasGO.SetActive(false);
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

        if (toProcess != null)
        {
            foreach (string message in toProcess)
            {
                HandleMessage(message);
            }
        }

        ActualizarTextoConexion();
        ActualizarAlertaDesconexion();

        if (Time.time >= proximoPingTiempo)
        {
            SendOnce("PING");
            proximoPingTiempo = Time.time + INTERVALO_PING;
        }
    }

    private void HandleMessage(string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length == 0) return;

        huboRecibido = true;
        ultimoRecibidoTiempo = Time.time;
        ultimoRecibidoMsg = parts[0];

        switch (parts[0])
        {
            case "PING":
                break; // solo sirve para confirmar que la PC A sigue conectada

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

    // Llamado desde PoseMapperUI apenas termina la calibracion del jugador nuevo: muestra
    // esta pantalla (propia, con fondo opaco) con el mensaje de espera hasta que el
    // Jugador A arranque el pipeline.
    public void MostrarPantallaJuego()
    {
        if (canvasPromptGO == null) return;
        canvasPromptGO.SetActive(true);

        if (promptPoseText != null) promptPoseText.gameObject.SetActive(false);
        if (promptResultadoText != null) promptResultadoText.gameObject.SetActive(false);

        if (textoEstado != null)
        {
            textoEstado.gameObject.SetActive(true);
            textoEstado.color = Color.white;
            textoEstado.text = "Esperando a que el Jugador A presione Play...";
        }
    }

    // Llamado desde PoseMapperUI cuando se vuelve a traer la pantalla de mapeo/debug
    // (tecla M), para que no quede superpuesta con esta.
    public void OcultarPantallaJuego()
    {
        if (canvasPromptGO != null) canvasPromptGO.SetActive(false);
    }

    private void HandlePipelineWindowOpen()
    {
        Debug.Log("<color=yellow>[CrossPC_KinectBridge] PIPELINE: arranco la ventana de evaluacion en PC A.</color>");

        if (textoEstado != null) textoEstado.gameObject.SetActive(false);
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

        if (textoEstado != null) textoEstado.gameObject.SetActive(false);
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

        if (promptCoroutineActual != null) StopCoroutine(promptCoroutineActual);
        if (promptPoseText != null) promptPoseText.gameObject.SetActive(false);
        if (promptResultadoText != null) promptResultadoText.gameObject.SetActive(false);

        if (textoEstado != null)
        {
            textoEstado.gameObject.SetActive(true);
            textoEstado.color = new Color(0.6f, 0.85f, 1f);
            textoEstado.text = $"Pipeline terminado.\nScore final: {scoreStr}";
        }
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

            huboEnviado = true;
            ultimoEnviadoTiempo = Time.time;
            ultimoEnviadoMsg = message.Split('|')[0];
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
