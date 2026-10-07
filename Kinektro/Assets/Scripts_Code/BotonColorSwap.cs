using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class BotonColorToggle : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private bool estaActivo = false;

    [Header("Colores del Botón")]
    public Color colorInactivo = Color.red;
    public Color colorActivo = Color.green;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        ActualizarColor();
    }

    public void AlternarEstado()
    {
        estaActivo = !estaActivo;
        ActualizarColor();
    }

    private void ActualizarColor()
    {
        if (meshRenderer != null)
        {
            meshRenderer.material.color = estaActivo ? colorActivo : colorInactivo;
        }
    }
}