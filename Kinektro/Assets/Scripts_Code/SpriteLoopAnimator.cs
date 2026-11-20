using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reproduce en loop una lista de sprites sobre un Image, dandole vida a un icono que
/// de otra forma quedaria como una foto congelada - una leve animacion de "respirar"/
/// balanceo, el mismo efecto que tienen los personajes de Just Dance: nunca quedan del
/// todo quietos, aunque esten sosteniendo una pose.
///
/// Se usa con <see cref="SetCuadros"/> para cambiar que animacion esta reproduciendo
/// (por ejemplo, al pasar de una pose pedida a la siguiente). Si se le pasa un arreglo
/// vacio o null, simplemente deja de animar y el Image se queda con el ultimo sprite
/// que tenia puesto.
/// </summary>
[RequireComponent(typeof(Image))]
public class SpriteLoopAnimator : MonoBehaviour
{
    [Tooltip("Velocidad de reproduccion del loop. Los cuadros fueron generados para una vuelta completa cada ~1 segundo a 12 fps.")]
    public float cuadrosPorSegundo = 12f;

    private Image img;
    private Sprite[] cuadros;
    private Coroutine loop;

    private void Awake()
    {
        img = GetComponent<Image>();
    }

    /// <summary>Cambia (o detiene, si se pasa null/vacio) la animacion en loop actual.</summary>
    public void SetCuadros(Sprite[] nuevosCuadros)
    {
        cuadros = nuevosCuadros;

        if (loop != null)
        {
            StopCoroutine(loop);
            loop = null;
        }

        if (isActiveAndEnabled && cuadros != null && cuadros.Length > 0)
        {
            loop = StartCoroutine(Reproducir());
        }
    }

    private void OnEnable()
    {
        if (loop == null && cuadros != null && cuadros.Length > 0)
        {
            loop = StartCoroutine(Reproducir());
        }
    }

    private void OnDisable()
    {
        if (loop != null)
        {
            StopCoroutine(loop);
            loop = null;
        }
    }

    private IEnumerator Reproducir()
    {
        var espera = new WaitForSeconds(1f / Mathf.Max(1f, cuadrosPorSegundo));
        int i = 0;
        while (true)
        {
            if (cuadros == null || cuadros.Length == 0) yield break;
            img.sprite = cuadros[i % cuadros.Length];
            i++;
            yield return espera;
        }
    }
}
