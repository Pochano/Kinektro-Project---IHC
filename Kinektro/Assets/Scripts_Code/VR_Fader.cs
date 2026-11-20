using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class FaderValueChangedEvent : UnityEvent<float> { }

public class VR_Fader : MonoBehaviour, IResaltable
{
    public enum FaderAxis { X_Axis, Y_Axis, Z_Axis }

    [Header("Malla y Movimiento")]
    public Transform faderMesh;
    public FaderAxis slideAxis = FaderAxis.Z_Axis;
    public bool invertAxis = false;

    [Header("Límites de Recorrido (Escala Local x100)")]
    public float minLimit = -3.0f;
    public float maxLimit = 3.0f;

    [Header("Sensibilidad y Peso (Fricción)")]
    [Range(0.1f, 1.0f)]
    public float dragSensitivity = 0.35f;

    [Range(1f, 30f)]
    public float frictionDamping = 8.0f;

    [Header("Valor Actual")]
    [Range(0f, 1f)]
    public float currentValue = 0.5f;

    [Header("Eventos")]
    public FaderValueChangedEvent OnValueChanged;

    [Header("Resaltado del Pipeline (Jugador A)")]
    public Renderer targetRenderer;
    public Color offColor = Color.white;
    public Color resaltadoColor = new Color(0f, 1f, 0.2f, 1f);

    [Header("Pipeline Target")]
    public DJControlTarget pipelineTarget = DJControlTarget.None;

    private Vector3 initialLocalPos;
    private Transform originalParent;
    private float currentOffset;
    private bool isResetting = false;

    void Awake()
    {
        if (faderMesh == null) faderMesh = transform;
        initialLocalPos = faderMesh.localPosition;
        originalParent = transform.parent;
        currentOffset = 0f;

        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();

        // Color oscuro original de reposo
        offColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        resaltadoColor = new Color(0f, 1f, 0.2f, 1f);
        SetFaderColor(offColor);
    }

    void LateUpdate()
    {
        if (transform.parent != originalParent)
        {
            transform.SetParent(originalParent, true);
        }

        if (isResetting)
        {
            ApplyOffsetToMesh(currentOffset);
            isResetting = false;
            return;
        }

        Vector3 rawTargetLocal = faderMesh.localPosition;
        float rawOffset = GetCurrentOffset(rawTargetLocal);

        float deltaOffset = (rawOffset - currentOffset) * dragSensitivity;
        float targetOffset = Mathf.Clamp(currentOffset + deltaOffset, minLimit, maxLimit);

        currentOffset = Mathf.Lerp(currentOffset, targetOffset, Time.deltaTime * frictionDamping);

        ApplyOffsetToMesh(currentOffset);

        float rawValue = Mathf.InverseLerp(minLimit, maxLimit, currentOffset);
        float calculatedValue = invertAxis ? (1f - rawValue) : rawValue;

        if (calculatedValue < 0.001f) calculatedValue = 0f;
        if (calculatedValue > 0.999f) calculatedValue = 1f;

        if (Mathf.Abs(currentValue - calculatedValue) > 0.001f)
        {
            currentValue = calculatedValue;
            OnValueChanged?.Invoke(currentValue);

            if (DJPipelineEvaluator.Instance != null && pipelineTarget != DJControlTarget.None)
            {
                DJPipelineEvaluator.Instance.SubmitInput(pipelineTarget, currentValue);
            }
        }

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.OnPitchFaderMoved();
        }
    }

    private float GetCurrentOffset(Vector3 localPos)
    {
        if (slideAxis == FaderAxis.X_Axis) return localPos.x - initialLocalPos.x;
        if (slideAxis == FaderAxis.Y_Axis) return localPos.y - initialLocalPos.y;
        return localPos.z - initialLocalPos.z;
    }

    private void ApplyOffsetToMesh(float offset)
    {
        Vector3 fixedPos = initialLocalPos;
        if (slideAxis == FaderAxis.X_Axis) fixedPos.x += offset;
        else if (slideAxis == FaderAxis.Y_Axis) fixedPos.y += offset;
        else if (slideAxis == FaderAxis.Z_Axis) fixedPos.z += offset;

        if (faderMesh != null)
        {
            faderMesh.localPosition = fixedPos;
        }
    }

    public void ResetFaderToDefault()
    {
        currentValue = 0.5f;
        currentOffset = 0f;
        ApplyOffsetToMesh(currentOffset);
        isResetting = true;
        OnValueChanged?.Invoke(currentValue);
        Debug.Log($"[VR_Fader] Reseteado al centro exacto original ({currentValue}).");
    }

    private Coroutine flashRoutine;

    public void SetResaltado(bool activo)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        SetFaderColor(offColor);
    }
    public void SetProximidadColor(float cercania)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        // Exponente 2.5f: mantiene el rojo en la mayor parte del recorrido y transiciona a verde solo muy cerca del objetivo
        float factor = Mathf.Pow(Mathf.Clamp01(cercania), 2.5f);
        Color colorLejos = new Color(1f, 0.08f, 0.08f, 1f);
        SetFaderColor(Color.Lerp(colorLejos, resaltadoColor, factor));
    }

    public void FlashSuccess()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashGreenRoutine());
    }

    private System.Collections.IEnumerator FlashGreenRoutine()
    {
        SetFaderColor(resaltadoColor);
        yield return new WaitForSeconds(0.45f);
        SetFaderColor(offColor);
        flashRoutine = null;
    }

    private void SetFaderColor(Color newColor)
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
}