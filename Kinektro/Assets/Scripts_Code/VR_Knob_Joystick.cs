using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public enum KnobType
{
    Gain,
    EQLow,
    EQMid,
    EQHigh
}

public class VR_Knob_Joystick : MonoBehaviour, IResaltable
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
    [Range(1f, 100f)]
    public float sensitivity = 45.0f;

    [Header("Canal Asignado")]
    public int channelNumber = 1;

    [Range(10f, 200f)]
    public float smoothness = 100.0f;

    public float CurrentNormalizedValue => currentNormalizedValue;

    [Header("Resaltado del Pipeline (Jugador A)")]
    public Renderer targetRenderer;
    public Color offColor = Color.white;
    public Color resaltadoColor = new Color(0f, 1f, 0.2f, 1f);

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

        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();

        // Color oscuro original de reposo (ignora el blanco guardado en el Inspector)
        offColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        resaltadoColor = new Color(0f, 1f, 0.2f, 1f);
        SetKnobColor(offColor);
    }

    private void Start()
    {
        ResetKnobToDefault();
    }

    private void Update()
    {
        if (isGrabbed && interactorTransform != null)
        {
            Vector3 currentHandPosWorld = interactorTransform.position;
            Vector3 deltaHandWorld = currentHandPosWorld - lastHandPosition;

            Vector3 worldRight = (Camera.main != null) ? Camera.main.transform.right : Vector3.right;
            float deltaX = Vector3.Dot(deltaHandWorld, worldRight);

            lastHandPosition = currentHandPosWorld;

            targetNormalizedValue += deltaX * sensitivity;
            targetNormalizedValue = Mathf.Clamp01(targetNormalizedValue);

            currentNormalizedValue = Mathf.Lerp(currentNormalizedValue, targetNormalizedValue, Time.deltaTime * smoothness);
            ApplyRotationAndAudio(currentNormalizedValue);
        }
        else
        {
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

        if (DJPipelineEvaluator.Instance != null)
        {
            DJControlTarget target = GetPipelineTarget();
            if (target != DJControlTarget.None)
            {
                DJPipelineEvaluator.Instance.SubmitInput(target, value);
            }
        }

        if (BasicTutorialManager.Instance != null)
        {
            BasicTutorialManager.Instance.OnKnobHighMoved(value);
        }

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.OnKnobRotated();
        }
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

    private Coroutine flashRoutine;

    public void SetResaltado(bool activo)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        SetKnobColor(offColor);
    }

    public void SetProximidadColor(float cercania)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        // Exponente 2.5f: mantiene el rojo en la mayor parte del recorrido y transiciona a verde solo muy cerca del objetivo
        float factor = Mathf.Pow(Mathf.Clamp01(cercania), 2.5f);
        Color colorLejos = new Color(1f, 0.08f, 0.08f, 1f);
        SetKnobColor(Color.Lerp(colorLejos, resaltadoColor, factor));
    }


    public void FlashSuccess()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashGreenRoutine());
    }

    private System.Collections.IEnumerator FlashGreenRoutine()
    {
        SetKnobColor(resaltadoColor);
        yield return new WaitForSeconds(0.45f);
        SetKnobColor(offColor);
        flashRoutine = null;
    }

    private void SetKnobColor(Color newColor)
    {
        if (targetRenderer == null) return;

        Material[] mats = targetRenderer.materials;
        bool colorApplied = false;

        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i].name.Contains("Color_Sync") || mats[i].name.Contains("Sync"))
            {
                mats[i].color = newColor;
                if (mats[i].HasProperty("_EmissionColor")) mats[i].SetColor("_EmissionColor", newColor);
                colorApplied = true;
                break;
            }
        }

        if (!colorApplied && mats.Length > 0)
        {
            mats[0].color = newColor;
            if (mats[0].HasProperty("_EmissionColor")) mats[0].SetColor("_EmissionColor", newColor);
        }

        targetRenderer.materials = mats;
    }

    private DJControlTarget GetPipelineTarget()
    {
        if (channelNumber == 1)
        {
            switch (knobType)
            {
                case KnobType.Gain: return DJControlTarget.Gain_Ch1;
                case KnobType.EQLow: return DJControlTarget.EqLow_Ch1;
                case KnobType.EQMid: return DJControlTarget.EqMid_Ch1;
                case KnobType.EQHigh: return DJControlTarget.EqHigh_Ch1;
            }
        }
        else if (channelNumber == 2)
        {
            switch (knobType)
            {
                case KnobType.Gain: return DJControlTarget.Gain_Ch2;
                case KnobType.EQLow: return DJControlTarget.EqLow_Ch2;
                case KnobType.EQMid: return DJControlTarget.EqMid_Ch2;
                case KnobType.EQHigh: return DJControlTarget.EqHigh_Ch2;
            }
        }
        return DJControlTarget.None;
    }
}