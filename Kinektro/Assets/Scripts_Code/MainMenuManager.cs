using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [Header("Paneles de UI")]
    public GameObject mainMenuCanvas;       // Objeto Menu

    [Header("Botones del Menú (UI Estándar)")]
    public Button playButton;
    public Button tutorialButton;

    // Propiedad estática para saber en la Escena 3 si venimos en Modo Tutorial o Juego Libre
    public static bool IsTutorialActive { get; private set; } = false;

    private void Start()
    {
        // Al arrancar en la Escena 1, nos aseguramos de que el menú flotante esté visible
        if (mainMenuCanvas != null) mainMenuCanvas.SetActive(true);

        if (playButton != null) playButton.onClick.AddListener(OnPlayPressed);
        if (tutorialButton != null) tutorialButton.onClick.AddListener(OnTutorialPressed);
    }

    /// <summary>
    /// Evento del botón PLAY: Va directo a la Consola Completa en Modo Libre (Escena_3).
    /// </summary>
    public void OnPlayPressed()
    {
        IsTutorialActive = false;
        Debug.Log("[MainMenu] Cargando Escena_3 en Modo Juego Libre...");
        SceneManager.LoadScene("Escena_3");
    }

    /// <summary>
    /// Evento del botón TUTORIAL: Va a la Escena_Tutorial (Componentes Individuales).
    /// </summary>
    public void OnTutorialPressed()
    {
        IsTutorialActive = true;
        Debug.Log("[MainMenu] Cargando Escena_2...");
        SceneManager.LoadScene("Escena_2"); // <--- Aquí cambias a Escena_2
    }
}