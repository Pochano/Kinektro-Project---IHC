using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class MockKinectSource : MonoBehaviour, IJointDataSource
{
    [Header("Posicion del muneco en el mundo (arrastra esto en Play Mode para")]
    [Tooltip("ubicarlo donde quieras, por ejemplo detras de la consola. Se puede" +
             " cambiar en vivo mientras estas en Play.")]
    public Vector3 originOffset = new Vector3(0f, 0f, 1.2f);

    [Header("Posicion Y de la mano cuando esta 'levantada' (prueba), relativa al origen")]
    public float raisedHandY = 1.9f;

    [Header("Posicion Y de la mano en reposo (abajo), relativa al origen")]
    public float idleHandY = 0.9f;

    [Header("Velocidad de levantado/bajado de la mano (m/s), para que se vea como un")]
    [Tooltip("movimiento real en vez de un salto instantaneo")]
    public float raiseSpeed = 0.8f;

    // Posiciones LOCALES (relativas a originOffset). Start() las llena una sola vez;
    // Update() las recalcula sumando originOffset, por lo que originOffset se puede
    // mover en vivo (incluso en Play Mode) y el muneco entero se desplaza con el.
    private Dictionary<string, Vector3> localJointPositions = new Dictionary<string, Vector3>();
    private Dictionary<string, Vector3> jointPositions = new Dictionary<string, Vector3>();
    private string mockHandStateRight = "Open";
    private string mockHandStateLeft = "Open";

    void Start()
    {
        localJointPositions["Head"] = new Vector3(0f, 1.6f, 0f);
        localJointPositions["Neck"] = new Vector3(0f, 1.45f, 0f);
        localJointPositions["SpineShoulder"] = new Vector3(0f, 1.35f, 0f);
        localJointPositions["SpineMid"] = new Vector3(0f, 1.0f, 0f);
        localJointPositions["SpineBase"] = new Vector3(0f, 0.7f, 0f);

        localJointPositions["ShoulderLeft"] = new Vector3(0.2f, 1.35f, 0f);
        localJointPositions["ElbowLeft"] = new Vector3(0.35f, 1.1f, 0f);
        localJointPositions["HandLeft"] = new Vector3(0.4f, idleHandY, 0f);

        localJointPositions["ShoulderRight"] = new Vector3(-0.2f, 1.35f, 0f);
        localJointPositions["ElbowRight"] = new Vector3(-0.35f, 1.1f, 0f);
        localJointPositions["HandRight"] = new Vector3(-0.4f, idleHandY, 0f);

        RecomputeWorldPositions();
    }

    void RecomputeWorldPositions()
    {
        foreach (var kvp in localJointPositions)
        {
            jointPositions[kvp.Key] = kvp.Value + originOffset;
        }
    }

    void Update()
    {
        var keyboard = Keyboard.current;

        bool raiseRight = false, fistRight = false, raiseLeft = false, fistLeft = false;

        if (keyboard != null)
        {
            // --- ESQUEMA DE 4 TECLAS DE FUNCION (F1-F4): no aparecen en NINGUN .cs ni
            // .inputactions del proyecto (revisado el proyecto completo, incluidas TODAS
            // las muestras de XR Interaction Toolkit), y funcionan igual en cualquier
            // teclado sin depender del numpad.
            // F1 = mano DERECHA arriba (levantada)   | F2 = mano DERECHA puno cerrado
            // F3 = mano IZQUIERDA arriba (levantada) | F4 = mano IZQUIERDA puno cerrado
            raiseRight = keyboard.f1Key.isPressed;
            fistRight = keyboard.f2Key.isPressed;
            raiseLeft = keyboard.f3Key.isPressed;
            fistLeft = keyboard.f4Key.isPressed;
        }

        Vector3 rightHandLocal = localJointPositions["HandRight"];
        float targetRightY = raiseRight ? raisedHandY : idleHandY;
        rightHandLocal.y = Mathf.MoveTowards(rightHandLocal.y, targetRightY, raiseSpeed * Time.deltaTime);
        localJointPositions["HandRight"] = rightHandLocal;
        mockHandStateRight = fistRight ? "Closed" : "Open";

        Vector3 leftHandLocal = localJointPositions["HandLeft"];
        float targetLeftY = raiseLeft ? raisedHandY : idleHandY;
        leftHandLocal.y = Mathf.MoveTowards(leftHandLocal.y, targetLeftY, raiseSpeed * Time.deltaTime);
        localJointPositions["HandLeft"] = leftHandLocal;
        mockHandStateLeft = fistLeft ? "Closed" : "Open";

        // Se recalcula TODOS los frames para que mover originOffset en el Inspector
        // (incluso en Play Mode) desplace el muneco entero al instante.
        RecomputeWorldPositions();
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
