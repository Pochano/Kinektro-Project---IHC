using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class KinectUdpReceiver : MonoBehaviour, IJointDataSource
{
    public int port = 5005;

    [Tooltip("Tilda esto si el Kinect reporta las manos 'en espejo' (levantas la derecha y el juego reacciona como si fuera la izquierda, o viceversa). Invierte Left/Right de todos los joints y del estado de las manos antes de usarlos.")]
    public bool swapLeftRight = false;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool running = false;

    private readonly Dictionary<string, Vector3> jointPositions = new Dictionary<string, Vector3>();
    private readonly object dataLock = new object();
    private string latestMessage = null;

    private string handStateLeft = "Unknown";
    private string handStateRight = "Unknown";

    void Start()
    {
        udpClient = new UdpClient(port);
        running = true;
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        Debug.Log("Escuchando datos del Kinect en el puerto UDP " + port);
    }

    private void ReceiveLoop()
    {
        IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udpClient.Receive(ref anyIP);
                string message = System.Text.Encoding.UTF8.GetString(data);

                lock (dataLock)
                {
                    latestMessage = message;
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
        string message = null;

        lock (dataLock)
        {
            if (latestMessage != null)
            {
                message = latestMessage;
                latestMessage = null;
            }
        }

        if (message != null)
        {
            ParseMessage(message);
        }
    }

    private void ParseMessage(string message)
    {
        string[] joints = message.Split(';');

        foreach (string jointEntry in joints)
        {
            if (string.IsNullOrEmpty(jointEntry)) continue;

            string[] parts = jointEntry.Split(',');
            if (parts.Length == 0) continue;

            if (parts[0] == "HANDSTATE")
            {
                if (parts.Length >= 3)
                {
                    if (swapLeftRight)
                    {
                        handStateLeft = parts[2];
                        handStateRight = parts[1];
                    }
                    else
                    {
                        handStateLeft = parts[1];
                        handStateRight = parts[2];
                    }
                }
                continue;
            }

            if (parts.Length != 4) continue;

            string jointName = MapJointName(parts[0]);
            float x = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            float y = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            float z = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);

            jointPositions[jointName] = new Vector3(x, y, z);
        }

        if (jointPositions.ContainsKey("HandRight"))
        {
            Debug.Log("Mano derecha: " + jointPositions["HandRight"]);
        }

        // === TEMPORAL: diagnostico de joints disponibles, quitar despues de verificar ===
        Debug.Log("[DIAG] Joints disponibles ahora mismo: " + string.Join(", ", jointPositions.Keys));
    }

    // Invierte el sufijo Left/Right de un nombre de joint (ej. HandLeft <-> HandRight) cuando
    // swapLeftRight esta tildado, para corregir Kinects que reportan las manos "en espejo".
    private string MapJointName(string rawName)
    {
        if (!swapLeftRight) return rawName;
        if (rawName.EndsWith("Left")) return rawName.Substring(0, rawName.Length - 4) + "Right";
        if (rawName.EndsWith("Right")) return rawName.Substring(0, rawName.Length - 5) + "Left";
        return rawName;
    }

    public Vector3 GetJointPosition(string jointName)
    {
        if (jointPositions.ContainsKey(jointName))
        {
            return jointPositions[jointName];
        }
        return Vector3.zero;
    }

    public bool TryGetJointPosition(string jointName, out Vector3 pos)
    {
        return jointPositions.TryGetValue(jointName, out pos);
    }

    public string GetHandState(string handJointName)
    {
        if (handJointName == "HandLeft") return handStateLeft;
        if (handJointName == "HandRight") return handStateRight;
        return "Unknown";
    }

    void OnApplicationQuit()
    {
        running = false;
        udpClient.Close();

        if (receiveThread != null && receiveThread.IsAlive)
        {
            receiveThread.Join(500);
        }
    }
}
