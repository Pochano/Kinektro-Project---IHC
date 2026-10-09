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
    [Tooltip("Reduce la sensibilidad del movimiento de la mano. Valores más bajos requieren gestos más amplios.")]
    public float dragSensitivity = 0.35f;

    [Range(1f, 30f)]
    [Tooltip("Pesadez física/fricción del fader. Valores más bajos hacen que se sienta más pesado y denso.")]
    public float frictionDamping = 8.0f;

    [Header("Valor Actual")]
    [Range(0f, 1f)]
    public float currentValue = 1f;

    [Header("Eventos")]
    public FaderValueChangedEvent OnValueChanged;

    private Vector3 initialLocalPos;
    private Transform originalParent;
    private float currentOffset;

    void Awake()
    {
        if (faderMesh == null) faderMesh = transform;
        initialLocalPos = faderMesh.localPosition;
        originalParent = transform.parent;
        currentOffset = GetCurrentOffset(faderMesh.localPosition);
    }

    void LateUpdate()
    {
        // 1. Evitar des-padreo
        if (transform.parent != originalParent)
        {
            transform.SetParent(originalParent, true);
        }

        // 2. Obtener posición objetivo cruda enviada por XR Grab Interactable
        Vector3 rawTargetLocal = faderMesh.localPosition;
        float rawOffset = GetCurrentOffset(rawTargetLocal);

        // 3. Aplicar factor de sensibilidad para atenuar saltos bruscos
        float deltaOffset = (rawOffset - currentOffset) * dragSensitivity;
        float targetOffset = Mathf.Clamp(currentOffset + deltaOffset, minLimit, maxLimit);

        // 4. Inercia/Fricción: suavizado con resistencia física
        currentOffset = Mathf.Lerp(currentOffset, targetOffset, Time.deltaTime * frictionDamping);

        // 5. Aplicar la posición final filtrada a la malla
        Vector3 fixedPos = initialLocalPos;
        if (slideAxis == FaderAxis.X_Axis) fixedPos.x += currentOffset;
        else if (slideAxis == FaderAxis.Y_Axis) fixedPos.y += currentOffset;
        else if (slideAxis == FaderAxis.Z_Axis) fixedPos.z += currentOffset;

        faderMesh.localPosition = fixedPos;

        // 6. Calcular valor normalizado 0.0 a 1.0
        float rawValue = Mathf.InverseLerp(minLimit, maxLimit, currentOffset);
        float calculatedValue = invertAxis ? (1f - rawValue) : rawValue;

        if (calculatedValue < 0.001f) calculatedValue = 0f;
        if (calculatedValue > 0.999f) calculatedValue = 1f;

        // 7. Notificar cambio de valor a FMOD
        if (Mathf.Abs(currentValue - calculatedValue) > 0.001f)
        {
            currentValue = calculatedValue;
            OnValueChanged?.Invoke(currentValue);
        }

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnPitchFaderMoved();

    }

    private float GetCurrentOffset(Vector3 localPos)
    {
        if (slideAxis == FaderAxis.X_Axis) return localPos.x - initialLocalPos.x;
        if (slideAxis == FaderAxis.Y_Axis) return localPos.y - initialLocalPos.y;
        return localPos.z - initialLocalPos.z;
    }

    /// <summary>
    /// Devuelve el fader físico al centro neutral (0.5), actualiza su posición 3D y notifica el evento
    /// </summary>
    public void ResetFaderToDefault()
    {
        // Posición central neutral (mitad de minLimit y maxLimit)
        float targetNormalized = 0.5f;
        currentValue = targetNormalized;

        // Recalcular offset neutro
        float targetOffset = Mathf.Lerp(minLimit, maxLimit, invertAxis ? (1f - targetNormalized) : targetNormalized);
        currentOffset = targetOffset;

        // Forzar posición física 3D inmediata
        Vector3 fixedPos = initialLocalPos;
        if (slideAxis == FaderAxis.X_Axis) fixedPos.x += currentOffset;
        else if (slideAxis == FaderAxis.Y_Axis) fixedPos.y += currentOffset;
        else if (slideAxis == FaderAxis.Z_Axis) fixedPos.z += currentOffset;

        if (faderMesh != null)
        {
            faderMesh.localPosition = fixedPos;
        }

        // Disparar evento para sincronizar con FMOD y UI
        OnValueChanged?.Invoke(currentValue);

        Debug.Log($"[VR_Fader] Reseteado al centro ({currentValue}).");
    }
}