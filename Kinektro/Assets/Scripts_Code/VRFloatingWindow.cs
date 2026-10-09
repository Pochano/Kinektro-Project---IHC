using UnityEngine;
using UnityEngine.InputSystem;

public class VRFloatingWindow : MonoBehaviour
{
    [Header("Contenedores de Ventana")]
    public GameObject expandedPanel;   // Canvas_Biblioteca
    public GameObject minimizedSphere;  // Minimized_Biblioteca

    [Header("Ajustes de Posicionamiento al Minimizar")]
    [Tooltip("Desplazamiento de la esfera respecto al panel")]
    public Vector3 sphereOffset = new Vector3(0.5f, 0f, 0f);

    [Header("Ajustes de Altura Mínima al Expandir")]
    public bool lockToInitialHeight = true;

    [Header("Estado Inicial")]
    public bool startMinimized = true; // Por defecto inicia como esfera

    private float initialY;

    private void Awake()
    {
        if (expandedPanel != null)
        {
            initialY = expandedPanel.transform.position.y;
        }
    }

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

    private void Update()
    {
        // Simulación por teclado con el Input System: Tecla '.'
        if (Keyboard.current != null && Keyboard.current.periodKey.wasPressedThisFrame)
        {
            ToggleWindow();
        }
    }

    /// <summary>
    /// Oculta el panel grande y muestra únicamente la esfera
    /// </summary>
    public void MinimizeWindow()
    {
        if (expandedPanel != null) expandedPanel.SetActive(false);
        if (minimizedSphere != null)
        {
            if (expandedPanel != null)
            {
                minimizedSphere.transform.position = expandedPanel.transform.position +
                                                    (expandedPanel.transform.rotation * sphereOffset);
                Vector3 euler = expandedPanel.transform.rotation.eulerAngles;
                minimizedSphere.transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
            }
            minimizedSphere.SetActive(true);
        }
        Debug.Log("[VRFloatingWindow] Ventana minimizada a esfera.");
    }

    /// <summary>
    /// Oculta la esfera y abre la ventana flotante mirando al usuario
    /// </summary>
    public void ExpandWindow()
    {
        if (minimizedSphere != null) minimizedSphere.SetActive(false);

        if (expandedPanel != null)
        {
            Vector3 targetPosition = (minimizedSphere != null) ? minimizedSphere.transform.position : expandedPanel.transform.position;

            if (lockToInitialHeight)
            {
                targetPosition.y = initialY;
            }

            expandedPanel.transform.position = targetPosition;

            Transform cameraTransform = Camera.main != null ? Camera.main.transform : null;
            if (cameraTransform != null)
            {
                Vector3 directionToCamera = cameraTransform.position - expandedPanel.transform.position;
                directionToCamera.y = 0f;

                if (directionToCamera != Vector3.zero)
                {
                    expandedPanel.transform.rotation = Quaternion.LookRotation(directionToCamera);
                }
            }

            expandedPanel.SetActive(true);
            Debug.Log("[VRFloatingWindow] Ventana expandida.");
        }

        // --- NOTIFICACIÓN AL TUTORIAL ---
        // Avance de paso en el tutorial al abrir la biblioteca
        if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep == TutorialStep.SelectTrack)
        {
            Debug.Log("[Tutorial] Biblioteca abierta correctamente.");
        }
    }

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