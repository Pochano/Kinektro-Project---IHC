using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MockKinectSource : MonoBehaviour, IJointDataSource
{
    [Header("Velocidad de movimiento simulado (m/s)")]
    public float moveSpeed = 1.0f;

    private Dictionary<string, Vector3> jointPositions = new Dictionary<string, Vector3>();
    private string mockHandStateRight = "Open";
    private string mockHandStateLeft = "Open";

    void Start()
    {
        jointPositions["Head"] = new Vector3(0f, 1.6f, 2f);
        jointPositions["Neck"] = new Vector3(0f, 1.45f, 2f);
        jointPositions["SpineShoulder"] = new Vector3(0f, 1.35f, 2f);
        jointPositions["SpineMid"] = new Vector3(0f, 1.0f, 2f);
        jointPositions["SpineBase"] = new Vector3(0f, 0.7f, 2f);

        jointPositions["ShoulderLeft"] = new Vector3(0.2f, 1.35f, 2f);
        jointPositions["ElbowLeft"] = new Vector3(0.35f, 1.1f, 2f);
        jointPositions["HandLeft"] = new Vector3(0.4f, 0.9f, 2f);

        jointPositions["ShoulderRight"] = new Vector3(-0.2f, 1.35f, 2f);
        jointPositions["ElbowRight"] = new Vector3(-0.35f, 1.1f, 2f);
        jointPositions["HandRight"] = new Vector3(-0.4f, 0.9f, 2f);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector3 rightHand = jointPositions["HandRight"];

        float rdx = 0f, rdy = 0f, rdz = 0f;

        // --- TECLADO NUMÉRICO (NUMPAD) ---
        if (keyboard.numpad8Key.isPressed) rdy += 1f;
        if (keyboard.numpad2Key.isPressed) rdy -= 1f;
        if (keyboard.numpad4Key.isPressed) rdx -= 1f;
        if (keyboard.numpad6Key.isPressed) rdx += 1f;
        if (keyboard.numpad9Key.isPressed) rdz += 1f;
        if (keyboard.numpad3Key.isPressed) rdz -= 1f;

        rightHand += new Vector3(rdx, rdy, rdz) * moveSpeed * Time.deltaTime;
        jointPositions["HandRight"] = rightHand;

        bool fistPressed = keyboard.numpad5Key.isPressed ||
                            keyboard.numpad0Key.isPressed ||
                            keyboard.numpadEnterKey.isPressed;

        mockHandStateRight = fistPressed ? "Closed" : "Open";
    }

    public bool TryGetJointPosition(string jointName, out Vector3 pos)
    {
        return jointPositions.TryGetValue(jointName, out pos);
    }

    public string GetHandState(string handJointName)
    {
        if (handJointName == "HandRight") return mockHandStateRight;
        if (handJointName == "HandLeft") return mockHandStateLeft;
        return "Unknown";
    }
}