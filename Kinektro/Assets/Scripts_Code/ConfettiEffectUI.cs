using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Efecto de confeti para UI (Canvas): al llamar <see cref="Explotar"/>, lanza varios
/// cuadraditos de colores desde una posicion dada, con velocidad y rotacion aleatoria,
/// que caen con una gravedad simulada y se desvanecen. Pensado para el "festejo" visual
/// cuando el Jugador B logra la pose pedida a tiempo (estilo Just Dance).
///
/// No depende de ningun asset externo: <see cref="spriteConfeti"/> se asigna por codigo
/// desde Resources/UI/ConfettiDot.png (un circulo solido generado localmente).
/// </summary>
public class ConfettiEffectUI : MonoBehaviour
{
    [Header("Configuracion")]
    public Sprite spriteConfeti;
    public int cantidadPorExplosion = 26;
    public float duracionSegundos = 1.1f;
    public float velocidadMinima = 350f;
    public float velocidadMaxima = 750f;
    public float tamanoMinimo = 14f;
    public float tamanoMaximo = 28f;

    private static readonly Color[] PaletaColores =
    {
        new Color(1f, 0.25f, 0.4f),   // rosa/rojo
        new Color(1f, 0.75f, 0.1f),   // amarillo/naranja
        new Color(0.3f, 0.9f, 0.5f),  // verde
        new Color(0.3f, 0.65f, 1f),   // celeste
        new Color(0.75f, 0.4f, 1f),   // violeta
        new Color(1f, 0.45f, 0.75f),  // fucsia
    };

    /// <summary>Hace estallar una explosion de confeti centrada en la posicion anclada
    /// (anchoredPosition) dada, dentro del mismo RectTransform que este componente.</summary>
    public void Explotar(Vector2 posicionAnclada)
    {
        if (spriteConfeti == null) return;

        for (int i = 0; i < cantidadPorExplosion; i++)
        {
            CrearParticula(posicionAnclada);
        }
    }

    private void CrearParticula(Vector2 origen)
    {
        var go = new GameObject("ConfettiParticula", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = go.GetComponent<RectTransform>();
        float tam = Random.Range(tamanoMinimo, tamanoMaximo);
        rt.sizeDelta = new Vector2(tam, tam);
        rt.anchoredPosition = origen;

        var img = go.AddComponent<Image>();
        img.sprite = spriteConfeti;
        img.raycastTarget = false;
        img.color = PaletaColores[Random.Range(0, PaletaColores.Length)];

        // Direccion mayormente hacia arriba/costados (como un estallido), nunca hacia abajo
        // al salir, para que se note bien antes de que la "gravedad" la haga caer.
        float angulo = Random.Range(0f, Mathf.PI * 2f);
        float velocidad = Random.Range(velocidadMinima, velocidadMaxima);
        Vector2 direccion = new Vector2(Mathf.Cos(angulo), Mathf.Abs(Mathf.Sin(angulo)) * 1.1f + 0.25f);
        Vector2 velocidadInicial = direccion.normalized * velocidad;

        var particula = go.AddComponent<ConfettiParticula>();
        particula.Inicializar(rt, img, velocidadInicial, duracionSegundos, Random.Range(-540f, 540f));
    }
}

/// <summary>Comportamiento individual de cada particula de confeti: tiro parabolico simple
/// (gravedad hacia abajo), rotacion constante y fade-out sobre su tiempo de vida. Se crea
/// y se destruye solo, no hace falta manejarla desde afuera.</summary>
public class ConfettiParticula : MonoBehaviour
{
    private RectTransform rt;
    private Image img;
    private Vector2 velocidad;
    private float vidaRestante;
    private float vidaTotal;
    private float velocidadRotacion;
    private const float GRAVEDAD = -1400f;

    public void Inicializar(RectTransform rectTransform, Image imagen, Vector2 velocidadInicial, float duracion, float rotacionPorSegundo)
    {
        rt = rectTransform;
        img = imagen;
        velocidad = velocidadInicial;
        vidaTotal = duracion;
        vidaRestante = duracion;
        velocidadRotacion = rotacionPorSegundo;
    }

    private void Update()
    {
        if (rt == null) { Destroy(gameObject); return; }

        velocidad.y += GRAVEDAD * Time.deltaTime;
        rt.anchoredPosition += velocidad * Time.deltaTime;
        rt.Rotate(0f, 0f, velocidadRotacion * Time.deltaTime);

        vidaRestante -= Time.deltaTime;
        if (img != null)
        {
            float alpha = Mathf.Clamp01(vidaRestante / (vidaTotal * 0.4f));
            var c = img.color;
            c.a = alpha;
            img.color = c;
        }

        if (vidaRestante <= 0f) Destroy(gameObject);
    }
}
