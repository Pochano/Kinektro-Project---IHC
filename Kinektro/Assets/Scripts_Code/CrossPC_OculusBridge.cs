using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Puente UDP del lado PC A (Oculus). Esta PC corre FMOD/DJ_Manager/FXSelector_Ch1/Ch2
/// (el audio real). Recibe de la PC del Kinect (PC B) los gestos ya procesados
/// (mano levantada/bajada, altura continua 0-1) y el efecto elegido por voz, y los
/// aplica directamente sobre los componentes locales. Tambien puede avisarle a la PC B
/// cuando hay una "ventana" abierta para elegir efectos y cuales estan disponibles.
///
/// No reemplaza el canal UDP local Kinect-middleware -> Unity (KinectUdpReceiver, puerto
/// 5005 en la PC del Kinect): este es un canal NUEVO y separado, entre las dos PCs.
///
/// Protocolo (texto UTF8, un mensaje por datagrama, campos separados por '|'):
///   PC B -> PC A (este script escucha):
///     HAND_RAISED|LEFT            HAND_RAISED|RIGHT
///     HAND_LOWERED|LEFT           HAND_LOWERED|RIGHT
///     HAND_HEIGHT|LEFT|0.73       HAND_HEIGHT|RIGHT|0.40   (continuo, cada frame)
///     SELECT_EFFECT|<canal>|<slot>                          (1=canal1/izq, 2=canal2/der; slot 1-5)
///   PC A -> PC B (este script envia, ver SendWindowOpen/SendWindowClose/SendFader):
///     WINDOW_OPEN|<canal>|<slot1,slot2,...>
///     WINDOW_CLOSE|<canal>
///     FADER|<canal>|<valor0a1>
/// </summary>
public class CrossPC_OculusBridge : MonoBehaviour
{
    [Header("Red")]
    [Tooltip("Puerto local en el que esta PC (Oculus) escucha mensajes de la PC del Kinect.")]
    public int listenPort = 6005;
    [Tooltip("IP de la PC del Kinect (PC B) en la red local.")]
    public string remoteIP = "192.168.1.50";
    [Tooltip("Puerto en el que la PC del Kinect escucha (debe coincidir con listenPort de CrossPC_KinectBridge en esa PC).")]
    public int remotePort = 6006;
    [Tooltip("Cuantas veces se repite el envio de un mensaje discreto importante (WINDOW_OPEN/WINDOW_CLOSE/SELECT_EFFECT), para compensar la falta de confirmacion de UDP.")]
    public int redundantSends = 3;

    [Header("Referencias locales (misma escena, PC A)")]
    public FXSelector fxSelectorCh1;
    public FXSelector fxSelectorCh2;
    public DJ_Manager djManager;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool running = false;

    private readonly Queue<string> inboundQueue = new Queue<string>();
    private readonly object queueLock = new object();

    void Start()
    {
        udpClient = new UdpClient(listenPort);
        running = true;
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log($"[CrossPC_OculusBridge] Escuchando a la PC del Kinect en el puerto UDP {listenPort}. Enviando a {remoteIP}:{remotePort}.");
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
            case "HAND_RAISED":
                if (parts.Length >= 2) HandleHandRaised(parts[1]);
                break;

            case "HAND_LOWERED":
                if (parts.Length >= 2) HandleHandLowered(parts[1]);
                break;

            case "HAND_HEIGHT":
                if (parts.Length >= 3) HandleHandHeight(parts[1], parts[2]);
                break;

            case "SELECT_EFFECT":
                if (parts.Length >= 3) HandleSelectEffect(parts[1], parts[2]);
                break;

            default:
                Debug.LogWarning($"[CrossPC_OculusBridge] Mensaje no reconocido: {message}");
                break;
        }
    }

    private void HandleHandRaised(string side)
    {
        Debug.Log($"[CrossPC_OculusBridge] RX: HAND_RAISED|{side}");
        if (side == "LEFT" && fxSelectorCh1 != null) fxSelectorCh1.ActivateSelected();
        else if (side == "RIGHT" && fxSelectorCh2 != null) fxSelectorCh2.ActivateSelected();
    }

    private void HandleHandLowered(string side)
    {
        Debug.Log($"[CrossPC_OculusBridge] RX: HAND_LOWERED|{side}");
        if (side == "LEFT" && fxSelectorCh1 != null) fxSelectorCh1.Deactivate();
        else if (side == "RIGHT" && fxSelectorCh2 != null) fxSelectorCh2.Deactivate();
    }

    private void HandleHandHeight(string side, string valueStr)
    {
        if (djManager == null) return;
        if (!float.TryParse(valueStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value01)) return;

        value01 = Mathf.Clamp01(value01);

        if (side == "LEFT" && fxSelectorCh1 != null)
        {
            djManager.SetFXIntensity(1, fxSelectorCh1.ArmedEffect, value01);
        }
        else if (side == "RIGHT" && fxSelectorCh2 != null)
        {
            djManager.SetFXIntensity(2, fxSelectorCh2.ArmedEffect, value01);
        }
    }

    private void HandleSelectEffect(string channelStr, string slotStr)
    {
        if (!int.TryParse(channelStr, out int channel)) return;
        if (!int.TryParse(slotStr, out int slot)) return;

        if (channel == 1 && fxSelectorCh1 != null) fxSelectorCh1.SelectEffect(slot);
        else if (channel == 2 && fxSelectorCh2 != null) fxSelectorCh2.SelectEffect(slot);

        Debug.Log($"[CrossPC_OculusBridge] Efecto seleccionado por voz desde PC B: canal={channel} slot={slot}");
    }

    // --- Envio hacia la PC del Kinect (llamar desde el flujo del DJ en PC A) ---

    /// <summary>Avisa a la PC B que se abrio una ventana para elegir efectos en este canal, y cuales estan disponibles (1=Reverb,2=Delay,3=Distortion,4=LowPass,5=Flanger).</summary>
    public void SendWindowOpen(int channel, int[] availableSlots)
    {
        string slotsCsv = string.Join(",", availableSlots);
        SendReliable($"WINDOW_OPEN|{channel}|{slotsCsv}");
    }

    public void SendWindowClose(int channel)
    {
        SendReliable($"WINDOW_CLOSE|{channel}");
    }

    /// <summary>Envio continuo opcional (sin redundancia) de un valor de fader/knob a la PC B, por si su pantalla lo quiere reflejar.</summary>
    public void SendFader(int channel, float value01)
    {
        SendOnce($"FADER|{channel}|{Mathf.Clamp01(value01).ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }

    private void SendReliable(string message)
    {
        Debug.Log($"[CrossPC_OculusBridge] Enviando (x{Mathf.Max(1, redundantSends)}): {message}");
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
            Debug.LogWarning($"[CrossPC_OculusBridge] No se pudo enviar '{message}' a {remoteIP}:{remotePort}: {e.Message}");
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
