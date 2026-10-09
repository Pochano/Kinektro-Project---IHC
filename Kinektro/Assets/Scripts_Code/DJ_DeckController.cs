using UnityEngine;

public class DJ_DeckController : MonoBehaviour
{
    [Header("Configuración de Canal")]
    [Tooltip("1 para el plato izquierdo (Ch1), 2 para el plato derecho (Ch2)")]
    public int channelNumber = 1;

    [Header("Referencias Principales")]
    public DJ_Manager djManager;

    [Header("Configuración de Pista")]
    public int defaultSongIndex = 0;
    public int currentSongIndex = -1;

    [Header("Botón Play/Pause 3D")]
    public VR_Boton playButton;

    [Header("Fader de Pitch Asociado")]
    [Tooltip("Arrastra aquí el GameObject de la palanca de tempo del canal")]
    public GameObject pitchFader;

    [Header("Visuales del Botón Loop")]
    public MeshRenderer loopButtonRenderer;
    public Color colorOff = Color.gray;
    public Color colorRecording = Color.yellow;
    public Color colorActive = Color.green;

    private bool isPlaying = false;

    void Start()
    {
        if (djManager == null) djManager = FindObjectOfType<DJ_Manager>();
    }

    public void LoadVinylTrack(int trackID)
    {
        currentSongIndex = trackID - 1;
        Debug.Log($"[Deck Ch{channelNumber}] Pista cargada: Índice {currentSongIndex} (TrackID: {trackID})");
    }

    public void UnloadVinylTrack()
    {
        currentSongIndex = -1;
        isPlaying = false;
        Debug.Log($"[Deck Ch{channelNumber}] Vinilo removido.");
    }

    public void OnPlayButtonPressed()
    {
        if (djManager == null) return;

        int songToPlay = (currentSongIndex >= 0) ? currentSongIndex : defaultSongIndex;

        if (!isPlaying)
        {
            djManager.PlaySong(channelNumber, songToPlay);
            isPlaying = true;
        }
        else
        {
            djManager.TogglePlayPause(channelNumber);
            isPlaying = false;
        }

        // Notificación al Tutorial
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnPlayToggle(isPlaying);
    }

    public void OnCueButtonPressed()
    {
        if (djManager == null) return;

        djManager.StopSong(channelNumber);
        isPlaying = false;

        if (playButton != null)
        {
            playButton.ForceUnpress();
        }

        // Notificación al Tutorial
        if (TutorialManager.Instance != null) TutorialManager.Instance.OnCuePressed();

        Debug.Log($"[Deck Ch{channelNumber}] CUE: Canción detenida.");
    }

    // --- CONTROL DE LOOP 3 ESTADOS ---

    public void OnLoopButtonPressed()
    {
        if (djManager == null) return;

        DJ_Manager.LoopState currentLoopState = djManager.HandleLoopButton(channelNumber);
        UpdateLoopButtonVisual(currentLoopState);

        // Notificación al Tutorial usando la variable declarada arriba
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.OnLoopStateChanged((int)currentLoopState);
        }
    }

    private void UpdateLoopButtonVisual(DJ_Manager.LoopState state)
    {
        if (loopButtonRenderer == null) return;

        switch (state)
        {
            case DJ_Manager.LoopState.Off:
                loopButtonRenderer.material.color = colorOff;
                break;
            case DJ_Manager.LoopState.RecordingIn:
                loopButtonRenderer.material.color = colorRecording;
                break;
            case DJ_Manager.LoopState.Active:
                loopButtonRenderer.material.color = colorActive;
                break;
        }
    }

    // --- RESET DE TEMPO / PITCH ---

    public void OnResetTempoButtonPressed()
    {
        if (djManager == null) return;

        // 1. Reset de audio FMOD y refresco de pantalla UI BPM
        djManager.ResetTempo(channelNumber);

        // 2. Reset físico del fader 3D
        if (pitchFader != null)
        {
            var faderScript = pitchFader.GetComponent<VR_Fader>();
            if (faderScript != null)
            {
                faderScript.ResetFaderToDefault();
            }
            else
            {
                pitchFader.SendMessage("ResetFaderToDefault", SendMessageOptions.DontRequireReceiver);
            }
        }

        Debug.Log($"[Deck Ch{channelNumber}] Tempo, Fader 3D y UI de BPM reseteados a neutro.");
    }
}