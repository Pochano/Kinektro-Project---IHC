using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Paneles de UI")]
    public GameObject mainMenuCanvas;       // Menu
    public GameObject librarySphere;        // Minimized_Biblioteca
    public GameObject libraryPanel;         // Canvas_Biblioteca

    [Header("Botones del Menú (UI Estándar)")]
    public Button playButton;
    public Button tutorialButton;

    [Header("Botones 3D / XR")]
    public GameObject playButton3D;
    public GameObject tutorialButton3D;

    [Header("Referencias de Managers")]
    public TutorialManager tutorialManager;

    private void Start()
    {
        // Al arrancar, mostramos el menú principal y ocultamos la biblioteca
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);
        if (librarySphere != null) librarySphere.SetActive(false);
        if (libraryPanel != null) libraryPanel.SetActive(false);

        if (playButton != null) playButton.onClick.AddListener(OnPlayPressed);
        if (tutorialButton != null) tutorialButton.onClick.AddListener(OnTutorialPressed);
    }

    public void OnPlayPressed()
    {
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(false);

        // Habilitar la esfera minimizada para arrancar (el script VRFloatingWindow se encarga de manejar el panel)
        if (librarySphere != null) librarySphere.SetActive(true);

        Debug.Log("[MainMenu] Modo Juego Libre iniciado.");
    }

    public void OnTutorialPressed()
    {
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(false);

        // Al iniciar el tutorial, mostramos la esfera para que el usuario la toque o abra
        if (librarySphere != null) librarySphere.SetActive(true);

        if (tutorialManager != null)
        {
            tutorialManager.StartTutorial();
        }

        Debug.Log("[MainMenu] Modo Tutorial iniciado.");
    }
}