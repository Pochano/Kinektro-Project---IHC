using UnityEngine;
using UnityEngine.Events;

public class GestureDetector : MonoBehaviour
{
    public KinectSourceProvider sourceProvider;
    private IJointDataSource dataSource;

    [Header("Umbrales (ajustables)")]
    public float raiseMarginMeters = 0.1f;
    public float swipeSpeedThreshold = 1.5f;

    [Header("Eventos - Mano Derecha")]
    public UnityEvent onRightHandRaised;
    public UnityEvent onRightHandLowered;
    public UnityEvent onRightSwipe;

    [Header("Eventos - Mano Izquierda")]
    public UnityEvent onLeftHandRaised;
    public UnityEvent onLeftHandLowered;
    public UnityEvent onLeftSwipe;

    [Header("Eventos - Ambas Manos")]
    public UnityEvent onBothHandsRaised;
    public UnityEvent onBothHandsLowered;

    private class HandTracker
    {
        public bool isRaised = false;
        public Vector3 lastPos;
        public bool hasLastPos = false;
    }

    private readonly HandTracker rightHand = new HandTracker();
    private readonly HandTracker leftHand = new HandTracker();
    private bool bothHandsWereRaised = false;

    void Start()
    {
        if (sourceProvider != null)
        {
            dataSource = sourceProvider.GetActiveSource();
        }

        if (dataSource == null)
        {
            Debug.LogError("GestureDetector: no se pudo obtener una fuente de datos valida desde el SourceProvider.");
        }
    }

    void Update()
    {
        if (dataSource == null) return;

        if (!dataSource.TryGetJointPosition("Head", out Vector3 headPos))
        {
            return; //referencai de cabeza
        }

        bool rightRaised = ProcessHand("HandRight", headPos, rightHand, onRightHandRaised, onRightHandLowered, onRightSwipe, "derecha");
        bool leftRaised = ProcessHand("HandLeft", headPos, leftHand, onLeftHandRaised, onLeftHandLowered, onLeftSwipe, "izquierda");

        bool bothRaisedNow = rightRaised && leftRaised;

        if (bothRaisedNow && !bothHandsWereRaised)
        {
            bothHandsWereRaised = true;
            onBothHandsRaised.Invoke();
            Debug.Log("Gesto: AMBAS manos levantadas");
        }
        else if (!bothRaisedNow && bothHandsWereRaised)
        {
            bothHandsWereRaised = false;
            onBothHandsLowered.Invoke();
            Debug.Log("Gesto: ya no estan ambas manos levantadas");
        }
    }

    // Procesa una mano: detecta si esta levantada (vs la cabeza) y si se mueve rapido estando levantada.
    // Devuelve true si esa mano esta levantada en este frame.
    private bool ProcessHand(string jointName, Vector3 headPos, HandTracker tracker, UnityEvent onRaised, UnityEvent onLowered, UnityEvent onSwipe, string etiqueta)
    {
        if (!dataSource.TryGetJointPosition(jointName, out Vector3 handPos))
        {
            return tracker.isRaised;
        }

        bool isRaisedNow = handPos.y > headPos.y + raiseMarginMeters;

        if (isRaisedNow && !tracker.isRaised)
        {
            tracker.isRaised = true;
            onRaised.Invoke();
            Debug.Log("Gesto: mano " + etiqueta + " levantada");
        }
        else if (!isRaisedNow && tracker.isRaised)
        {
            tracker.isRaised = false;
            onLowered.Invoke();
            Debug.Log("Gesto: mano " + etiqueta + " bajada");
        }

        if (tracker.hasLastPos)
        {
            float speed = (handPos - tracker.lastPos).magnitude / Time.deltaTime;

            if (tracker.isRaised && speed > swipeSpeedThreshold)
            {
                onSwipe.Invoke();
                Debug.Log("Gesto: mano " + etiqueta + " levantada + movimiento rapido (velocidad: " + speed.ToString("F2") + ")");
            }
        }

        tracker.lastPos = handPos;
        tracker.hasLastPos = true;

        return tracker.isRaised;
    }
}