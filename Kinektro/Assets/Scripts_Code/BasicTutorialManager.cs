using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using FMODUnity;
using FMOD.Studio;

public enum BasicTutorialStep
{
    Welcome,            // 1. Bienvenida
    PlayAndCue,         // 2. Play y Cue
    KnobHigh,           // 3. Knob High
    VolumeFader,        // 4. Fader de Volumen
    LoopAndTempo,       // 5. Loop, Tempo Fader y Reset
    Completed           // 6. Cierre
}

public class BasicTutorialManager : MonoBehaviour
{
    public static BasicTutorialManager Instance { get; private set; }

    [Header("UI del Tutorial")]
    public GameObject tutorialCanvas;
    public TextMeshProUGUI instructionText;
    public Button nextButton;

    [Header("Audio FMOD de Prueba")]
    public EventReference defaultSongEvent;
    private EventInstance songInstance;

    [Header("Referencias a Objetos de la Jerarquía (Escena_2)")]
    public GameObject playButtonObj;       // Ch1_Play
    public GameObject cueButtonObj;        // Ch1_Cue
    public GameObject knobHighObj;         // Ch1_High
    public GameObject volumeFaderObj;      // Ch1_Volumen
    public GameObject loopButtonObj;       // Ch1_Loop
    public GameObject tempoFaderObj;       // Ch1_Pitch
    public GameObject resetTempoButtonObj; // Ch1_Reinicio

    [Header("Ajustes de Spawn")]
    public float spawnDuration = 0.4f;

    [Header("Estado Actual")]
    public BasicTutorialStep currentStep = BasicTutorialStep.Welcome;

    // LÓGICA DE LOOP DE 3 ESTADOS (Idéntica a DJ_Manager)
    public enum LoopState { Off, RecordingIn, Active }
    private LoopState loopState = LoopState.Off;
    private int loopStartMs = -1;
    private int loopEndMs = -1;

    // Banderas de validación de tareas
    private bool playTested = false;
    private bool cueTested = false;
    private bool knobUsed = false;
    private bool volumeMoved = false;
    private bool loopTested = false;
    private bool tempoMoved = false;
    private bool tempoResetPressed = false;

    // Cooldown para evitar doble disparo inmediato en VR
    private float lastInteractionTime = 0f;
    private float interactionCooldown = 0.2f;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextButtonClicked);

        SetStep(BasicTutorialStep.Welcome);
    }

    private void Update()
    {
        // PROCESAR BUCLE EN TIEMPO REAL (Lógica fluida sin tirones)
        if (loopState == LoopState.Active && songInstance.isValid() && loopStartMs >= 0 && loopEndMs > loopStartMs)
        {
            songInstance.getTimelinePosition(out int currentPos);
            if (currentPos >= loopEndMs)
            {
                songInstance.setTimelinePosition(loopStartMs);
            }
        }
    }

    public void SetStep(BasicTutorialStep step)
    {
        currentStep = step;

        HideAllControls();

        if (nextButton != null) nextButton.gameObject.SetActive(false);

        switch (currentStep)
        {
            case BasicTutorialStep.Welcome:
                UpdateInstruction("¡Bienvenido al Tutorial Básico de Kinektro!\n\nAprenderás a usar los botones de transporte, ecualización, volumen y tempo antes de pasar a la consola completa.");
                ShowNextButton("Next");
                break;

            case BasicTutorialStep.PlayAndCue:
                StartDefaultSong(); // Inicia la canción si no está sonando

                SpawnObject(playButtonObj);
                SpawnObject(cueButtonObj);

                UpdateInstruction("PASO 1: REPRODUCCIÓN Y PAUSA\n\n\n1. Presiona PLAY para pausar o reproducir en cualquier momento.\n\n\n\n2. Presiona CUE para regresar al inicio de la canción.");
                break;

            case BasicTutorialStep.KnobHigh:
                EnsureAudioIsPlaying();
                SpawnObject(knobHighObj);
                UpdateInstruction("PASO 2: ECUALIZACIÓ\n\n(KNOB HIGH)\n\n\n\nGira la perilla HIGH para ajustar la presencia de las frecuencias agudas.");
                break;

            case BasicTutorialStep.VolumeFader:
                EnsureAudioIsPlaying();
                ResetHighKnobToDefault(); // Resetea solo el efecto EQ, mantiene el tiempo de la canción

                SpawnObject(volumeFaderObj);
                UpdateInstruction("PASO 3: VOLUMEN DE CANAL\n\n\nDesplaza el Fader de Volumen hacia arriba o abajo para modificar la intensidad.");
                break;

            case BasicTutorialStep.LoopAndTempo:
                EnsureAudioIsPlaying();
                ResetVolumeToDefault();  // Resetea el volumen al 50%
                ClearLoopSettingsOnly(); // Desactiva el loop sin reiniciar la canción a 0ms

                SpawnObject(loopButtonObj);
                SpawnObject(tempoFaderObj);
                SpawnObject(resetTempoButtonObj);

                UpdateInstruction("PASO 4: LOOP Y CONTROL DE TEMPO\n\n\n\n1. Presiona LOOP\n(1ro marcar A, 2do cerrar B, 3ro desactivar).\n\n2. Mueve la palanca TEMPO.\n\n3. Presiona REINICIO de Tempo.");
                break;

            case BasicTutorialStep.Completed:
                UpdateInstruction("¡Felicidades! Has completado el entrenamiento básico de controles.\n\n¡Ya estás listo para pasar a la consola principal!");
                ShowNextButton("Next");
                break;
        }
    }

    private void SpawnObject(GameObject target)
    {
        if (target == null) return;

        var vrBtn = target.GetComponent<VR_Boton>();
        if (vrBtn != null) vrBtn.ForceUnpress();

        target.SetActive(true);
        StartCoroutine(AnimateSpawnCoroutine(target.transform));
    }

    private IEnumerator AnimateSpawnCoroutine(Transform targetTransform)
    {
        Vector3 initialScale = targetTransform.localScale;
        Vector3 minScale = Vector3.one * 0.001f;
        targetTransform.localScale = minScale;

        float elapsedTime = 0f;
        while (elapsedTime < spawnDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsedTime / spawnDuration);
            targetTransform.localScale = Vector3.Lerp(minScale, initialScale, t);
            yield return null;
        }

        targetTransform.localScale = initialScale;
    }

    private void HideAllControls()
    {
        if (playButtonObj != null) playButtonObj.SetActive(false);
        if (cueButtonObj != null) cueButtonObj.SetActive(false);
        if (knobHighObj != null) knobHighObj.SetActive(false);
        if (volumeFaderObj != null) volumeFaderObj.SetActive(false);
        if (loopButtonObj != null) loopButtonObj.SetActive(false);
        if (tempoFaderObj != null) tempoFaderObj.SetActive(false);
        if (resetTempoButtonObj != null) resetTempoButtonObj.SetActive(false);
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
            if (tmpText != null) tmpText.text = label;
        }
    }

    public void OnNextButtonClicked()
    {
        if (currentStep == BasicTutorialStep.Welcome)
        {
            SetStep(BasicTutorialStep.PlayAndCue);
        }
        else if (currentStep == BasicTutorialStep.PlayAndCue)
        {
            EnsureAudioIsPlaying();
            SetStep(BasicTutorialStep.KnobHigh);
        }
        else if (currentStep == BasicTutorialStep.KnobHigh)
        {
            SetStep(BasicTutorialStep.VolumeFader);
        }
        else if (currentStep == BasicTutorialStep.VolumeFader)
        {
            SetStep(BasicTutorialStep.LoopAndTempo);
        }
        else if (currentStep == BasicTutorialStep.LoopAndTempo)
        {
            SetStep(BasicTutorialStep.Completed);
        }
        else if (currentStep == BasicTutorialStep.Completed)
        {
            StopAudioAndClean();
            SceneManager.LoadScene("Escena_3");
        }
    }

    // --- MANEJO DE REPRODUCCIÓN FMOD ---

    private void StartDefaultSong()
    {
        if (!defaultSongEvent.IsNull && !songInstance.isValid())
        {
            songInstance = RuntimeManager.CreateInstance(defaultSongEvent);
            songInstance.setVolume(0.5f);
            songInstance.start();
        }
    }

    public void EnsureAudioIsPlaying()
    {
        if (songInstance.isValid())
        {
            songInstance.getPaused(out bool isPaused);
            if (isPaused)
            {
                songInstance.setPaused(false);
            }
        }
    }

    private void ResetHighKnobToDefault()
    {
        if (knobHighObj != null)
        {
            var knobScript = knobHighObj.GetComponent<VR_Knob_Joystick>();
            if (knobScript != null) knobScript.ResetKnobToDefault();
        }

        if (songInstance.isValid())
        {
            songInstance.setParameterByName("High", 0f);
        }
    }

    private void ResetVolumeToDefault()
    {
        if (volumeFaderObj != null)
        {
            var faderScript = volumeFaderObj.GetComponent<VR_Fader>();
            if (faderScript != null) faderScript.ResetFaderToDefault();
        }

        if (songInstance.isValid())
        {
            songInstance.setVolume(0.5f);
        }
    }

    public void ClearLoopSettingsOnly()
    {
        loopState = LoopState.Off;
        loopStartMs = -1;
        loopEndMs = -1;

        // NO se fuerza setTimelinePosition(0) aquí para permitir reproducción fluida continua
        if (loopButtonObj != null)
        {
            var vrBtn = loopButtonObj.GetComponent<VR_Boton>();
            if (vrBtn != null)
            {
                vrBtn.ForceUnpress();
                vrBtn.SetButtonColor(vrBtn.offColor); // Restablece el color del aro Color_Sync
            }
        }
    }

    // --- INTERACCIONES DE BOTONES CORREGIDAS ---

    public void OnPlayPressed()
    {
        if (!songInstance.isValid()) return;

        if (Time.time - lastInteractionTime < interactionCooldown) return;
        lastInteractionTime = Time.time;

        // Alterna entre pausar y reproducir según el estado REAL de FMOD
        songInstance.getPaused(out bool isCurrentlyPaused);
        bool newPausedState = !isCurrentlyPaused;
        songInstance.setPaused(newPausedState);

        if (currentStep == BasicTutorialStep.PlayAndCue)
        {
            playTested = true;
            CheckPlayCueCompletion();
        }
    }

    public void OnCuePressed()
    {
        if (!songInstance.isValid()) return;

        if (Time.time - lastInteractionTime < interactionCooldown) return;
        lastInteractionTime = Time.time;

        songInstance.setTimelinePosition(0);
        songInstance.setPaused(true);

        if (playButtonObj != null)
        {
            var playBtnScript = playButtonObj.GetComponent<VR_Boton>();
            if (playBtnScript != null) playBtnScript.ForceUnpress();
        }

        if (currentStep == BasicTutorialStep.PlayAndCue)
        {
            cueTested = true;
            CheckPlayCueCompletion();
        }
    }

    private void CheckPlayCueCompletion()
    {
        if (playTested && cueTested)
        {
            ShowNextButton("Next");
        }
    }

    public void OnKnobHighMoved(float value)
    {
        if (songInstance.isValid())
        {
            float dbValue = (value < 0.5f) ? Mathf.Lerp(-80f, 0f, value * 2f) : Mathf.Lerp(0f, 6f, (value - 0.5f) * 2f);
            songInstance.setParameterByName("High", dbValue);
        }

        if (currentStep == BasicTutorialStep.KnobHigh && !knobUsed)
        {
            knobUsed = true;
            ShowNextButton("Next");
        }
    }

    public void OnVolumeFaderMoved(float value)
    {
        if (songInstance.isValid())
        {
            songInstance.setVolume(Mathf.Clamp01(value));
        }

        if (currentStep == BasicTutorialStep.VolumeFader && !volumeMoved)
        {
            volumeMoved = true;
            ShowNextButton("Next");
        }
    }

    public void OnLoopPressed()
    {
        if (!songInstance.isValid()) return;

        if (Time.time - lastInteractionTime < interactionCooldown) return;
        lastInteractionTime = Time.time;

        songInstance.getTimelinePosition(out int currentPos);
        var loopBtnScript = (loopButtonObj != null) ? loopButtonObj.GetComponent<VR_Boton>() : null;

        switch (loopState)
        {
            case LoopState.Off:
                loopStartMs = currentPos;
                loopState = LoopState.RecordingIn;

                if (loopBtnScript != null) loopBtnScript.SetButtonColor(loopBtnScript.inColor);
                Debug.Log($"[Tutorial Loop] Entrada (A) marcada en {loopStartMs} ms.");
                break;

            case LoopState.RecordingIn:
                if (currentPos >= loopStartMs + 300)
                {
                    loopEndMs = currentPos;
                    loopState = LoopState.Active;

                    if (loopBtnScript != null) loopBtnScript.SetButtonColor(loopBtnScript.activeColor);
                    Debug.Log($"[Tutorial Loop] Salida (B) marcada en {loopEndMs} ms. ¡LOOP ACTIVO!");

                    if (currentStep == BasicTutorialStep.LoopAndTempo)
                    {
                        loopTested = true;
                        CheckLoopAndTempoCompletion();
                    }
                }
                break;

            case LoopState.Active:
                ClearLoopSettingsOnly();
                Debug.Log("[Tutorial Loop] Loop DESACTIVADO.");
                break;
        }
    }

    public void OnTempoFaderMoved(float value)
    {
        if (songInstance.isValid())
        {
            float speedMultiplier = Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(value));
            songInstance.setPitch(speedMultiplier);
        }

        if (currentStep == BasicTutorialStep.LoopAndTempo)
        {
            tempoMoved = true;
            CheckLoopAndTempoCompletion();
        }
    }

    public void OnResetTempoPressed()
    {
        if (!songInstance.isValid()) return;

        if (Time.time - lastInteractionTime < interactionCooldown) return;
        lastInteractionTime = Time.time;

        songInstance.setPitch(1.0f);

        // Busca el VR_Fader directamente o en cualquier objeto hijo (por si asignaste Ch1_Pitch en lugar de Ch1_Pitch_Head)
        if (tempoFaderObj != null)
        {
            var faderScript = tempoFaderObj.GetComponentInChildren<VR_Fader>();
            if (faderScript != null)
            {
                faderScript.ResetFaderToDefault();
            }
            else
            {
                Debug.LogWarning("[Tutorial] No se encontró el script VR_Fader en tempoFaderObj ni en sus hijos.");
            }
        }

        if (currentStep == BasicTutorialStep.LoopAndTempo)
        {
            if (tempoMoved)
            {
                tempoResetPressed = true;
                CheckLoopAndTempoCompletion();
            }
        }
    }

    private void CheckLoopAndTempoCompletion()
    {
        if (loopTested && tempoMoved && tempoResetPressed)
        {
            ShowNextButton("Next");
        }
    }

    private void StopAudioAndClean()
    {
        if (songInstance.isValid())
        {
            songInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            songInstance.release();
        }
    }

    private void OnDestroy()
    {
        StopAudioAndClean();
    }
}