using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fondo "a lo Just Dance" para la pantalla del Jugador B: en vez de un color solido fijo,
/// el color base del fondo recorre el circulo de matices (hue) lentamente, y unas
/// "manchas" de color grandes y suaves flotan de un lado a otro encima, para que la
/// pantalla se sienta viva y alegre en vez de estatica.
///
/// No necesita ningun asset externo: las manchas usan un sprite circular con degrade
/// generado localmente (Resources/UI/BlobSoft.png).
/// </summary>
public class FondoAnimadoUI : MonoBehaviour
{
    [Header("Color base (se anima solo, no hace falta tocarlo)")]
    public Image fondoBase;
    [Range(0f, 1f)] public float saturacionBase = 0.55f;
    [Range(0f, 1f)] public float brilloBase = 0.9f;
    [Tooltip("Vueltas completas al circulo de color por segundo. Chico a proposito para que el cambio sea un pulso lento, no un estroboscopio.")]
    public float velocidadCicloColor = 0.03f;

    [Header("Manchas de color flotantes")]
    public Sprite spriteMancha;
    public RectTransform contenedorManchas;
    public int cantidadManchas = 5;
    [Range(0f, 1f)] public float opacidadManchas = 0.35f;

    private Image[] manchas;
    private Vector2[] centroManchas;
    private float[] faseManchas;
    private float[] velocidadManchas;
    private float[] radioManchas;
    private float[] faseColorManchas;

    private void Start()
    {
        if (fondoBase != null)
        {
            fondoBase.color = Color.HSVToRGB(0f, saturacionBase, brilloBase);
        }

        if (spriteMancha == null || contenedorManchas == null) return;

        manchas = new Image[cantidadManchas];
        centroManchas = new Vector2[cantidadManchas];
        faseManchas = new float[cantidadManchas];
        velocidadManchas = new float[cantidadManchas];
        radioManchas = new float[cantidadManchas];
        faseColorManchas = new float[cantidadManchas];

        for (int i = 0; i < cantidadManchas; i++)
        {
            var go = new GameObject("Mancha_" + i, typeof(RectTransform));
            go.transform.SetParent(contenedorManchas, false);
            var rt = go.GetComponent<RectTransform>();
            float tam = Random.Range(500f, 950f);
            rt.sizeDelta = new Vector2(tam, tam);

            var img = go.AddComponent<Image>();
            img.sprite = spriteMancha;
            img.raycastTarget = false;
            manchas[i] = img;

            centroManchas[i] = new Vector2(Random.Range(-700f, 700f), Random.Range(-400f, 400f));
            faseManchas[i] = Random.Range(0f, Mathf.PI * 2f);
            velocidadManchas[i] = Random.Range(0.08f, 0.2f);
            radioManchas[i] = Random.Range(150f, 320f);
            faseColorManchas[i] = Random.Range(0f, 1f);
        }
    }

    private void Update()
    {
        float hueBase = (Time.time * velocidadCicloColor) % 1f;

        if (fondoBase != null)
        {
            fondoBase.color = Color.HSVToRGB(hueBase, saturacionBase, brilloBase);
        }

        if (manchas == null) return;

        for (int i = 0; i < manchas.Length; i++)
        {
            if (manchas[i] == null) continue;
            var rt = manchas[i].rectTransform;
            float t = Time.time * velocidadManchas[i] + faseManchas[i];
            Vector2 offset = new Vector2(Mathf.Cos(t) * radioManchas[i], Mathf.Sin(t * 0.8f) * radioManchas[i] * 0.6f);
            rt.anchoredPosition = centroManchas[i] + offset;

            float hueMancha = (hueBase + 0.5f + faseColorManchas[i] * 0.3f) % 1f;
            Color c = Color.HSVToRGB(hueMancha, Mathf.Clamp01(saturacionBase + 0.2f), 1f);
            c.a = opacidadManchas;
            manchas[i].color = c;
        }
    }
}
