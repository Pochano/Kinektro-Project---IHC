using UnityEngine;
using TMPro;
using UnityEngine.UI;

public enum TutorialStep
{
    Welcome,            // 1. Bienvenida
    ShelfExplanation,   // 2. Explicación de estantería
    SelectTrack,        // 3. Seleccionar pista de la librería
    GrabVinyl,          // 4. Coger el disco
    PlaceOnTurntable,   // 5. Colocar en el plato
    PlayPause,          // 6. Probar Play/Pause
    CueButton,          // 7. Probar Cue
    LoopButton,         // 8. Probar Loop (3 etapas)
    PitchFader,         // 9. Mover Fader de Pitch
    KnobsEQ,            // 10. Probar Knobs de EQ
    FXButton,           // 11. Presionar botón morado FX
    SelectFX,           // 12. Seleccionar efecto favorito
    KinectHandMove,     // 13. Mover la mano (Kinect)
    Completed           // Tutorial Finalizado
}

public class TutorialManager : MonoBehaviour
{
    [Header("UI del Tutorial")]
    public GameObject tutorialCanvas;
    public TextMeshProUGUI instructionText;
    public Button nextButton; // Útil para pasos puramente explicativos (Pasos 1 y 2)

    [Header("Estado Actual")]
    public TutorialStep currentStep = TutorialStep.Welcome;

    // Sub-estados para verificaciones compuestas
    private bool playPressed = false;
    private bool pausePressed = false;
    private bool cuePressed = false;
    private int loopStateTracker = 0; // 0=Off, 1=Recording, 2=Active

    public static TutorialManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (tutorialCanvas != null) tutorialCanvas.SetActive(false);
        if (nextButton != null) nextButton.onClick.AddListener(OnNextButtonClicked);
    }

    public void StartTutorial()
    {
        if (tutorialCanvas != null) tutorialCanvas.SetActive(true);
        SetStep(TutorialStep.Welcome);
    }

    public void SetStep(TutorialStep step)
    {
        currentStep = step;
        if (nextButton != null) nextButton.gameObject.SetActive(false);

        switch (currentStep)
        {
            case TutorialStep.Welcome:
                UpdateInstruction("¡Bienvenido a Kinektro VR!\n\nEste tutorial te enseñará las herramientas básicas de la mesa de DJ.");
                if (nextButton != null) nextButton.gameObject.SetActive(true);
                break;

            case TutorialStep.ShelfExplanation:
                UpdateInstruction("Mira la estantería a tu lado.\nPosee 6 bases magnéticas para sostener los vinilos que generes desde la librería.");
                if (nextButton != null) nextButton.gameObject.SetActive(true);
                break;

            case TutorialStep.SelectTrack:
                UpdateInstruction("PASO 1:\nAbre la librería flotante (tecla '.' o tocando la esfera) y selecciona una pista.");
                break;

            case TutorialStep.GrabVinyl:
                UpdateInstruction("PASO 2:\nToma el disco recién generado de la estantería usando el gatillo (Grip).");
                break;

            case TutorialStep.PlaceOnTurntable:
                UpdateInstruction("PASO 3:\nLleva el disco hacia uno de los platos (Turntables) para cargarlo.");
                break;

            case TutorialStep.PlayPause:
                UpdateInstruction("PASO 4:\nPresiona el botón de PLAY en el plato para reproducir, vuelve a presionarlo para pausar, y una vez más para reanudar.");
                playPressed = false;
                pausePressed = false;
                break;

            case TutorialStep.CueButton:
                UpdateInstruction("PASO 5:\nPresiona el botón CUE para reiniciar la pista al inicio y luego dale a PLAY.");
                cuePressed = false;
                break;

            case TutorialStep.LoopButton:
                UpdateInstruction("PASO 6:\nPrueba el botón de LOOP en 3 etapas:\n1. Marca Entrada\n2. Marca Salida (Activar)\n3. Presiona de nuevo para Apagar.");
                loopStateTracker = 0;
                break;

            case TutorialStep.PitchFader:
                UpdateInstruction("PASO 7:\nMantiene presionado el Grip sobre la palanca de PITCH/Tempo y muévela hacia arriba o abajo.");
                break;

            case TutorialStep.KnobsEQ:
                UpdateInstruction("PASO 8:\nToma una perilla de Ecualización (Low, Mid, High) y gírala a la izquierda o derecha.");
                break;

            case TutorialStep.FXButton:
                UpdateInstruction("PASO 9:\nPresiona el botón morado de FX para activar el módulo de efectos en el canal.");
                break;

            case TutorialStep.SelectFX:
                UpdateInstruction("PASO 10:\nSelecciona en la interfaz el efecto que más te guste (Reverb, Delay, Flanger...).");
                break;

            case TutorialStep.KinectHandMove:
                UpdateInstruction("PASO 11:\nMueve tu mano verticalmente hacia arriba y hacia abajo para controlar la modulación (Kinect/Hand Tracking).");
                break;

            case TutorialStep.Completed:
                UpdateInstruction("¡Felicidades! Has completado el tutorial de Kinektro.\n\n¡Ya estás listo para mezclar!");
                if (nextButton != null)
                {
                    nextButton.gameObject.SetActive(true);
                    nextButton.GetComponentInChildren<TextMeshProUGUI>().text = "Finalizar";
                }
                break;
        }
    }

    private void UpdateInstruction(string text)
    {
        if (instructionText != null) instructionText.text = text;
    }

    private void OnNextButtonClicked()
    {
        if (currentStep == TutorialStep.Welcome)
        {
            SetStep(TutorialStep.ShelfExplanation);
        }
        else if (currentStep == TutorialStep.ShelfExplanation)
        {
            SetStep(TutorialStep.SelectTrack);
        }
        else if (currentStep == TutorialStep.Completed)
        {
            if (tutorialCanvas != null) tutorialCanvas.SetActive(false);
        }
    }

    // --- MÉTODOS DE VERIFICACIÓN (Llamados por tus scripts del sistema) ---

    public void OnTrackSelectedFromLibrary()
    {
        if (currentStep == TutorialStep.SelectTrack) SetStep(TutorialStep.GrabVinyl);
    }

    public void OnVinylGrabbed()
    {
        if (currentStep == TutorialStep.GrabVinyl) SetStep(TutorialStep.PlaceOnTurntable);
    }

    public void OnVinylPlacedOnTurntable()
    {
        if (currentStep == TutorialStep.PlaceOnTurntable) SetStep(TutorialStep.PlayPause);
    }

    public void OnPlayToggle(bool isPlayingNow)
    {
        if (currentStep == TutorialStep.PlayPause)
        {
            if (isPlayingNow && !playPressed) playPressed = true;
            else if (!isPlayingNow && playPressed) pausePressed = true;
            else if (isPlayingNow && playPressed && pausePressed)
            {
                SetStep(TutorialStep.CueButton);
            }
        }
        else if (currentStep == TutorialStep.CueButton && cuePressed && isPlayingNow)
        {
            SetStep(TutorialStep.LoopButton);
        }
    }

    public void OnCuePressed()
    {
        if (currentStep == TutorialStep.CueButton)
        {
            cuePressed = true;
        }
    }

    public void OnLoopStateChanged(int stateIndex)
    {
        if (currentStep == TutorialStep.LoopButton)
        {
            if (stateIndex == 1 && loopStateTracker == 0) loopStateTracker = 1;      // Recording
            else if (stateIndex == 2 && loopStateTracker == 1) loopStateTracker = 2; // Active
            else if (stateIndex == 0 && loopStateTracker == 2)                       // Off
            {
                SetStep(TutorialStep.PitchFader);
            }
        }
    }

    public void OnPitchFaderMoved()
    {
        if (currentStep == TutorialStep.PitchFader) SetStep(TutorialStep.KnobsEQ);
    }

    public void OnKnobRotated()
    {
        if (currentStep == TutorialStep.KnobsEQ) SetStep(TutorialStep.FXButton);
    }

    public void OnFXButtonToggle()
    {
        if (currentStep == TutorialStep.FXButton) SetStep(TutorialStep.SelectFX);
    }

    public void OnFXSelected()
    {
        if (currentStep == TutorialStep.SelectFX) SetStep(TutorialStep.KinectHandMove);
    }

    public void OnKinectHandMoved()
    {
        if (currentStep == TutorialStep.KinectHandMove) SetStep(TutorialStep.Completed);
    }
}