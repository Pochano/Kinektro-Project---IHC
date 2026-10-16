using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class FaderValueChangedEvent : UnityEvent<float> { }

public class VR_Fader : MonoBehaviour
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

    private Vector3 initialLocalPos;
    private Transform originalParent;
    private float currentOffset;
    private bool isResetting = false; // Bloquea tirones del LateUpdate al resetear

    void Awake()
    {
        if (faderMesh == null) faderMesh = transform;
        initialLocalPos = faderMesh.localPosition;
        originalParent = transform.parent;

        // En el inicio, el offset relativo es 0 (posición neutral/central)
        currentOffset = 0f;
    }

    void LateUpdate()
    {
        if (transform.parent != originalParent)
        {
            transform.SetParent(originalParent, true);
        }

        // Si se acaba de resetear, forzamos la posición centrada inicial y saltamos este frame
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

    /// <summary>
    /// Regresa el fader exactamente a su posición inicial en la escena (centro 0.5)
    /// </summary>
    public void ResetFaderToDefault()
    {
        currentValue = 0.5f;

        // Vuelve a la posición local original del Start (offset 0)
        currentOffset = 0f;

        // Aplica directamente la posición a la malla
        ApplyOffsetToMesh(currentOffset);

        // Activa la bandera para evitar interferencia en este fotograma
        isResetting = true;

        OnValueChanged?.Invoke(currentValue);

        Debug.Log($"[VR_Fader] Reseteado al centro exacto original ({currentValue}).");
    }
}