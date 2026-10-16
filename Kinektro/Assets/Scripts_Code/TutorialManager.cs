using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum TutorialStep
{
    Welcome,            // 1. Saludo y bienvenida
    SelectTrack,        // Compatibilidad con VRFloatingWindow
    OpenLibrary,        // 2. Girar a la derecha, explorar biblioteca flotante y seleccionar pista
    GrabAndPlaceVinyl,  // 3. Explicación de estantería izquierda, coger vinilo y colocarlo en el plato
    FreeMixExplanation, // 4. Libertad para usar los controles vistos en Escena 2
    KinectFXButtons,    // 5. Presionar botón morado FX (Ch1 o Ch2) -> Activa modo Kinect
    FXButton,           // Compatibilidad
    SelectFXEffect,     // 6. Seleccionar un efecto en la interfaz
    SelectFX,           // Compatibilidad
    KinectHandRaise,    // 7. Mirar al frente y levantar la mano según canal
    KinectHandMove,     // Compatibilidad con KinectGestureFX
    Completed           // 8. Eso es todo el tutorial, ¡disfruta y experimenta!
}

public class TutorialManager : MonoBehaviour
{
    [Header("UI del Tutorial")]
    public GameObject tutorialCanvas;
    public TextMeshProUGUI instructionText;
    public Button nextButton;

    [Header("Referencias a Botones Morados de FX (Kinect)")]
    public VR_Boton fxButtonCh1;
    public VR_Boton fxButtonCh2;

    [Header("Colores de Estado FX / Kinect")]
    public Color kinectActiveColor = new Color(0.6f, 0f, 1f); // Morado brillante

    [Header("Estado Actual")]
    public TutorialStep currentStep = TutorialStep.Welcome;

    public static TutorialManager Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (nextButton != null)
        {
            // Evento UI Tradicional / Clic
            nextButton.onClick.RemoveAllListeners();
            nextButton.onClick.AddListener(OnNextButtonClicked);

            // Vinculación con XR Simple Interactable si está presente
            XRSimpleInteractable interactable = nextButton.GetComponent<XRSimpleInteractable>();
            if (interactable != null)
            {
                interactable.selectEntered.RemoveAllListeners();
                interactable.selectEntered.AddListener((args) => OnNextButtonClicked());
                interactable.activated.RemoveAllListeners();
                interactable.activated.AddListener((args) => OnNextButtonClicked());
            }
        }

        StartTutorial();
    }

    public void StartTutorial()
    {
        if (tutorialCanvas != null) tutorialCanvas.SetActive(true);
        SetStep(TutorialStep.Welcome);
    }

    public void SetStep(TutorialStep step)
    {
        currentStep = step;

        switch (currentStep)
        {
            case TutorialStep.Welcome:
                UpdateInstruction("¡Hola! Te damos la bienvenida a la discoteca de Kinektro VR.");
                ShowNextButton("Next");
                break;

            case TutorialStep.OpenLibrary:
            case TutorialStep.SelectTrack:
                UpdateInstruction("PASO 1:\nGira a tu derecha. Encontrarás la biblioteca flotante.\nNavega entre las pestañas y selecciona una pista.");
                HideNextButton();
                break;

            case TutorialStep.GrabAndPlaceVinyl:
                UpdateInstruction("PASO 2:\nA tu costado izquierdo se generará el vinilo en la estantería.\nExtiende tu mano, tómalo y colócalo sobre la mesa de DJ (Plato).");
                HideNextButton();
                break;

            case TutorialStep.FreeMixExplanation:
                UpdateInstruction("PASO 3:\n¡Excelente! Ahora puedes experimentar libremente usando todo lo aprendido en la escena previa (Play, Cue, Loop, Pitch, EQ).");
                ShowNextButton("Next");
                break;

            case TutorialStep.KinectFXButtons:
            case TutorialStep.FXButton:
                UpdateInstruction("PASO 4: MODO KINECT\nPresiona cualquiera de los botones morados de FX ubicados a los costados del panel de mezcla para activar el modo Kinect.");
                HideNextButton();
                break;

            case TutorialStep.SelectFXEffect:
            case TutorialStep.SelectFX:
                UpdateInstruction("PASO 5: SELECCIÓN DE EFECTO\nElige en la pantalla el efecto de tu preferencia (Reverb, Delay, Flanger, etc.).");
                HideNextButton();
                break;

            case TutorialStep.KinectHandRaise:
            case TutorialStep.KinectHandMove:
                UpdateInstruction("PASO 6: CONTROL POR GESTOS\nMira hacia al frente y levanta la mano del canal correspondiente para modular el efecto en tiempo real.");
                ShowNextButton("Next");
                break;

            case TutorialStep.Completed:
                UpdateInstruction("PASO 7:\n¡Eso es todo el tutorial!\n\nDisfruta y experimenta con la consola.");
                ShowNextButton("Finish");
                break;
        }
    }

    private void UpdateInstruction(string text)
    {
        if (instructionText != null) instructionText.text = text;
    }

    private void ShowNextButton(string label)
    {
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(true);

            TextMeshProUGUI tmpText = nextButton.GetComponentInChildren<TextMeshProUGUI>();
            if (tmpText != null)
            {
                tmpText.text = label;
            }
            else
            {
                Text legacyText = nextButton.GetComponentInChildren<Text>();
                if (legacyText != null) legacyText.text = label;
            }
        }
    }

    private void HideNextButton()
    {
        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(false);
        }
    }

    public void OnNextButtonClicked()
    {
        Debug.Log($"[TutorialManager] Next presionado en: {currentStep}");

        switch (currentStep)
        {
            case TutorialStep.Welcome:
                SetStep(TutorialStep.OpenLibrary);
                break;

            case TutorialStep.FreeMixExplanation:
                SetStep(TutorialStep.KinectFXButtons);
                break;

            case TutorialStep.KinectHandRaise:
            case TutorialStep.KinectHandMove:
                SetStep(TutorialStep.Completed);
                break;

            case TutorialStep.Completed:
                if (tutorialCanvas != null) tutorialCanvas.SetActive(false);
                break;
        }
    }

    // --- MANEJO DE EVENTOS DE ACCIÓN REAL ---

    public void OnTrackSelectedFromLibrary()
    {
        if (currentStep == TutorialStep.OpenLibrary || currentStep == TutorialStep.SelectTrack)
        {
            SetStep(TutorialStep.GrabAndPlaceVinyl);
        }
    }

    public void OnVinylPlacedOnTurntable()
    {
        if (currentStep == TutorialStep.GrabAndPlaceVinyl)
        {
            SetStep(TutorialStep.FreeMixExplanation);
        }
    }

    public void OnFXButtonToggle(int channel)
    {
        if (currentStep == TutorialStep.KinectFXButtons || currentStep == TutorialStep.FXButton)
        {
            if (channel == 1 && fxButtonCh1 != null) fxButtonCh1.SetButtonColor(kinectActiveColor);
            if (channel == 2 && fxButtonCh2 != null) fxButtonCh2.SetButtonColor(kinectActiveColor);

            SetStep(TutorialStep.SelectFXEffect);
        }
    }

    public void OnFXSelected()
    {
        if (currentStep == TutorialStep.SelectFXEffect || currentStep == TutorialStep.SelectFX)
        {
            SetStep(TutorialStep.KinectHandRaise);
        }
    }

    public void OnKinectHandMoved()
    {
        if (currentStep == TutorialStep.KinectHandRaise || currentStep == TutorialStep.KinectHandMove)
        {
            SetStep(TutorialStep.Completed);
        }
    }

    // --- MÉTODOS DE COMPATIBILIDAD ---

    public void OnFXButtonToggle() => OnFXButtonToggle(1);
    public void OnVinylGrabbed() { }
    public void OnPlayToggle(bool isPlayingNow) { }
    public void OnCuePressed() { }
    public void OnLoopStateChanged(int stateIndex) { }
    public void OnPitchFaderMoved() { }
    public void OnKnobRotated() { }
}