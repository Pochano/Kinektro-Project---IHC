using UnityEngine;

public class VRFloatingWindow : MonoBehaviour
{
    [Header("Contenedores de Ventana")]
    public GameObject expandedPanel;  // Panel Canvas/UI completo de la biblioteca
    public GameObject minimizedSphere; // Malla 3D de la esfera/bolita

    [Header("Estado Inicial")]
    public bool startMinimized = false;

    private void Start()
    {
        if (startMinimized)
        {
            MinimizeWindow();
        }
        else
        {
            ExpandWindow();
        }
    }

    /// <summary>
    /// Oculta la ventana y activa la esfera minimizada
    /// </summary>
    public void MinimizeWindow()
    {
        if (expandedPanel != null) expandedPanel.SetActive(false);
        if (minimizedSphere != null) minimizedSphere.SetActive(true);
        Debug.Log("[VRFloatingWindow] Ventana minimizada a esfera.");
    }

    /// <summary>
    /// Restaura la ventana flotante completa
    /// </summary>
    public void ExpandWindow()
    {
        if (expandedPanel != null) expandedPanel.SetActive(true);
        if (minimizedSphere != null) minimizedSphere.SetActive(false);
        Debug.Log("[VRFloatingWindow] Ventana expandida.");
    }

    /// <summary>
    /// Alterna el estado de la ventana
    /// </summary>
    public void ToggleWindow()
    {
        bool isExpanded = expandedPanel != null && expandedPanel.activeSelf;
        if (isExpanded)
        {
            MinimizeWindow();
        }
        else
        {
            ExpandWindow();
        }
    }
}