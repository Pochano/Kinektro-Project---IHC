using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class KinectUdpReceiver : MonoBehaviour, IJointDataSource
{
    public int port = 5005;

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
                    handStateLeft = parts[1];
                    handStateRight = parts[2];
                }
                continue;
            }

            if (parts.Length != 4) continue;

            string jointName = parts[0];
            float x = float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            float y = float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
            float z = float.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);

            jointPositions[jointName] = new Vector3(x, y, z);
        }

        if (jointPositions.ContainsKey("HandRight"))
        {
            Debug.Log("Mano derecha: " + jointPositions["HandRight"]);
        }
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
