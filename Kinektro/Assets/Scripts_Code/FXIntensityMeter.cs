using UnityEngine;

/// <summary>
/// Barra vertical (eje Y local) que muestra la intensidad 0-1 del efecto
/// armado en un canal como una "llama" morada que se LLENA de abajo hacia
/// arriba (no una linea que viaja, sino un relleno que crece), con brillo
/// (emission) que aumenta con la intensidad para que se vea energico y
/// llamativo. Pensada para usarse junto a GestureDetector.UpdateFXIntensity,
/// que ya calcula ese valor 0-1 cada frame (leftHeight/rightHeight).
/// </summary>
public class FXIntensityMeter : MonoBehaviour
{
    [Tooltip("Transform del relleno (la 'llama') que crece/decrece. Debe tener su pivote pensado para estirarse desde abajo: este script mueve y escala su eje Y local.")]
    public Transform fill;

    [Tooltip("Posicion local Y de la base del relleno (abajo del todo, intensidad 0).")]
    public float bottomY = 0.1f;

    [Tooltip("Posicion local Y del tope del relleno (arriba del todo, intensidad 1).")]
    public float topY = 2.1f;

    [Tooltip("Grosor minimo del relleno cuando la intensidad es 0, para que nunca desaparezca del todo.")]
    public float minFillHeight = 0.04f;

    [Header("Colores (morado energico, con brillo)")]
    public Color trackColor = new Color(0.18f, 0f, 0.3f, 1f);

    [Tooltip("Color del relleno cuando la intensidad es baja.")]
    public Color fillColorLow = new Color(0.45f, 0f, 0.75f, 1f);

    [Tooltip("Color del relleno cuando la intensidad es alta (mas vivo/rosado, como una llama).")]
    public Color fillColorHigh = new Color(0.95f, 0.35f, 1f, 1f);

    [Tooltip("Multiplicador de brillo (emission) con intensidad 1. A mayor valor, mas 'encendido' se ve arriba.")]
    public float maxEmissionIntensity = 3.5f;

    private float fillWidth, fillDepth;
    private Material fillMat;
    private Material trackMat;
    private bool cached;

    private void Awake()
    {
        var trackRenderer = GetComponent<Renderer>();
        if (trackRenderer != null)
        {
            trackMat = trackRenderer.material;
            trackMat.color = trackColor;
        }

        if (fill != null)
        {
            var fillRenderer = fill.GetComponent<Renderer>();
            if (fillRenderer != null)
            {
                fillMat = fillRenderer.material;
                fillMat.EnableKeyword("_EMISSION");
                fillMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            fillWidth = fill.localScale.x;
            fillDepth = fill.localScale.z;
            cached = true;
        }
    }

    /// <summary>Llamado cada frame (desde GestureDetector) con el valor 0-1 de altura de mano.</summary>
    public void SetValue01(float value01)
    {
        if (fill == null) return;
        if (!cached)
        {
            fillWidth = fill.localScale.x;
            fillDepth = fill.localScale.z;
            cached = true;
        }

        float t = Mathf.Clamp01(value01);
        float totalHeight = topY - bottomY;
        float fillHeight = Mathf.Max(minFillHeight, totalHeight * t);

        Vector3 pos = fill.localPosition;
        pos.y = bottomY + fillHeight * 0.5f;
        fill.localPosition = pos;

        Vector3 scale = fill.localScale;
        scale.y = fillHeight;
        fill.localScale = scale;

        if (fillMat != null)
        {
            Color baseColor = Color.Lerp(fillColorLow, fillColorHigh, t);
            fillMat.color = baseColor;
            fillMat.SetColor("_EmissionColor", baseColor * Mathf.Lerp(0.1f, maxEmissionIntensity, t));
        }
    }
}
