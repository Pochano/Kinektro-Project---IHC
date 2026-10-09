using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public enum KnobType
{
    Gain,
    EQLow,
    EQMid,
    EQHigh
}

public class VR_Knob_Joystick : MonoBehaviour
{
    [Header("Referencias")]
    public KnobType knobType = KnobType.Gain;
    public DJ_Manager djManager;

    [Header("Transform de la Perilla 3D")]
    public Transform knobModel;
    public Vector3 rotationAxis = new Vector3(0, 0, 1);
    public float minAngle = -135f;
    public float maxAngle = 135f;

    [Header("Control de Precision y Fluidez")]
    [Tooltip("Sensibilidad aumentada: Un valor de 35 a 50 permite recorrer todo el rango con un movimiento de solo 5-10 cm")]
    [Range(1f, 100f)]
    public float sensitivity = 45.0f;

    [Header("Canal Asignado")]
    public int channelNumber = 1; // Ajustar a 1 o 2 en el Inspector

    [Tooltip("Rapidez de respuesta visual (80-100 elimina el retardo pegajoso)")]
    [Range(10f, 200f)]
    public float smoothness = 100.0f;

    private Quaternion initialLocalRotation;
    private float targetNormalizedValue = 0.5f;
    private float currentNormalizedValue = 0.5f;
    private bool isGrabbed = false;

    private Transform interactorTransform;
    private Vector3 lastHandPosition;

    private void Awake()
    {
        if (knobModel == null) knobModel = transform;
        initialLocalRotation = knobModel.localRotation;
    }

    private void Start()
    {
        ResetKnobToDefault();
    }

    private void Update()
    {
        if (isGrabbed && interactorTransform != null)
        {
            // 1. Posición del mando en mundo real (metros)
            Vector3 currentHandPosWorld = interactorTransform.position;
            Vector3 deltaHandWorld = currentHandPosWorld - lastHandPosition;

            // 2. Eje horizontal de la cámara o mundo
            Vector3 worldRight = (Camera.main != null) ? Camera.main.transform.right : Vector3.right;
            float deltaX = Vector3.Dot(deltaHandWorld, worldRight);

            lastHandPosition = currentHandPosWorld;

            // 3. Calculamos la meta directa sin la traba del Lerp intermedio
            targetNormalizedValue += deltaX * sensitivity;
            targetNormalizedValue = Mathf.Clamp01(targetNormalizedValue);

            // 4. Interpolación ultra reactiva pero fluida
            currentNormalizedValue = Mathf.Lerp(currentNormalizedValue, targetNormalizedValue, Time.deltaTime * smoothness);
            ApplyRotationAndAudio(currentNormalizedValue);
        }
        else
        {
            // Cuando se suelta, suaviza de forma imperceptible hasta reposar en el punto final
            if (Mathf.Abs(currentNormalizedValue - targetNormalizedValue) > 0.001f)
            {
                currentNormalizedValue = Mathf.Lerp(currentNormalizedValue, targetNormalizedValue, Time.deltaTime * smoothness);
                RotateMesh(currentNormalizedValue);
            }
        }
    }

    public void SetValue(float normalizedValue)
    {
        targetNormalizedValue = Mathf.Clamp01(normalizedValue);
        currentNormalizedValue = targetNormalizedValue;
        ApplyRotationAndAudio(currentNormalizedValue);
    }

    private void RotateMesh(float value)
    {
        float targetAngle = Mathf.Lerp(minAngle, maxAngle, value);
        knobModel.localRotation = initialLocalRotation * Quaternion.AngleAxis(targetAngle, rotationAxis);
    }

    private void ApplyRotationAndAudio(float value)
    {
        RotateMesh(value);
        ApplyValueToDJManager(value);

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnKnobRotated();

    }

    private void ApplyValueToDJManager(float value)
    {
        if (djManager == null) return;

        switch (knobType)
        {
            case KnobType.Gain:
                if (channelNumber == 1) djManager.SetGainCh1(value);
                else djManager.SetGainCh2(value);
                break;
            case KnobType.EQLow:
                djManager.SetEQLow(channelNumber, value);
                break;
            case KnobType.EQMid:
                djManager.SetEQMid(channelNumber, value);
                break;
            case KnobType.EQHigh:
                djManager.SetEQHigh(channelNumber, value);
                break;
        }
    }

    public void ResetKnobToDefault()
    {
        targetNormalizedValue = 0.5f;
        currentNormalizedValue = 0.5f;
        RotateMesh(0.5f);
    }

    public void OnSelectEntered(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        interactorTransform = args.interactorObject.transform;
        lastHandPosition = interactorTransform.position;
    }

    public void OnSelectExited(SelectExitEventArgs args)
    {
        isGrabbed = false;
        interactorTransform = null;
    }
}