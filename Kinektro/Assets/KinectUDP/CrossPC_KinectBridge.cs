using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

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

        Debug.Log($"[CrossPC_KinectBridge] Escuchando a la PC del Oculus en el puerto UDP {listenPort}. Enviando a {remoteIP}:{remotePort}.");
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
