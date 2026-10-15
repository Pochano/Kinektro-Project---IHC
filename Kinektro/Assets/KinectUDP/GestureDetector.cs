using UnityEngine;
using UnityEngine.Events;

public class GestureDetector : MonoBehaviour
{
    public KinectSourceProvider sourceProvider;
    private IJointDataSource dataSource;

    [Header("Intensidad progresiva de FX (altura de mano, continua)")]
    [Tooltip("DJ_Manager de la escena, para alimentar los parametros de intensidad por efecto.")]
    public DJ_Manager djManager;
    [Tooltip("FXSelector del canal 1 (mano izquierda).")]
    public FXSelector fxSelectorCh1;
    [Tooltip("FXSelector del canal 2 (mano derecha).")]
    public FXSelector fxSelectorCh2;
    [Tooltip("Margen extra sobre la cabeza para 'mano totalmente arriba' (metros), igual que en kinectv2.")]
    public float intensityHeadMargin = 0.3f;
    private int debugLogFrameCounter = 0;

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

        UpdateFXIntensity(headPos);
    }

    // Altura normalizada 0-1 de cada mano (SpineBase = abajo, Head+margen = arriba, igual que
    // kinectv2), enviada cada frame al efecto armado de su canal (izquierda->Ch1, derecha->Ch2).
    // Mutua exclusion (los demas efectos del canal quedan en 0) la resuelve DJ_Manager.SetFXIntensity.
    private void UpdateFXIntensity(Vector3 headPos)
    {
        if (djManager == null) return;
        if (!dataSource.TryGetJointPosition("SpineBase", out Vector3 spinePos)) return;

        float lowY = spinePos.y;
        float highY = headPos.y + intensityHeadMargin;

        if (fxSelectorCh1 != null && dataSource.TryGetJointPosition("HandLeft", out Vector3 leftHandPos))
        {
            float leftHeight = Mathf.Clamp01(Mathf.InverseLerp(lowY, highY, leftHandPos.y));
            int ch1Effect = fxSelectorCh1.IsActive ? fxSelectorCh1.ArmedEffect : 0;
            djManager.SetFXIntensity(1, ch1Effect, leftHeight);

            // === TEMPORAL: log de QA, quitar despues de verificar ===
            debugLogFrameCounter++;
            if (debugLogFrameCounter >= 20)
            {
                debugLogFrameCounter = 0;
                Debug.Log($"[FXIntensity DEBUG] Ch1 armedEffect={ch1Effect} leftHeight={leftHeight:F2} -> escala 0-100: {leftHeight * 100f:F1}");
            }
        }

        if (fxSelectorCh2 != null && dataSource.TryGetJointPosition("HandRight", out Vector3 rightHandPos))
        {
            float rightHeight = Mathf.Clamp01(Mathf.InverseLerp(lowY, highY, rightHandPos.y));
            int ch2Effect = fxSelectorCh2.IsActive ? fxSelectorCh2.ArmedEffect : 0;
            djManager.SetFXIntensity(2, ch2Effect, rightHeight);
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