using UnityEngine;
using FMODUnity;

public class DJ_Manager : MonoBehaviour
{
    [Header("Master Beatbar Visualizer")]
    public FuserBeatbarUI fuserBeatbarUI;

    [Header("Referencias a Platos (Turntables)")]
    public TurntableDeck deckChannel1;
    public TurntableDeck deckChannel2;

    [Header("Botones de Canciones (Compatibilidad)")]
    public VR_Boton[] songButtons;

    [Header("Eventos FMOD (Lista de Canciones)")]
    public EventReference[] songEvents;

    [Header("Referencias a Perillas")]
    public VR_Knob_Joystick[] djKnobs;

    [Header("Estado Canal 1")]
    [Range(0f, 1f)] public float currentCh1Volume = 1f;
    [Range(0f, 1f)] public float currentCh1Gain = 1f;
    public float currentCh1Pitch = 1f;
    private FMOD.Studio.EventInstance songInstanceCh1;
    public int currentSongIndexCh1 = -1;

    [Header("Estado Canal 2")]
    [Range(0f, 1f)] public float currentCh2Volume = 1f;
    [Range(0f, 1f)] public float currentCh2Gain = 1f;
    public float currentCh2Pitch = 1f;
    private FMOD.Studio.EventInstance songInstanceCh2;
    public int currentSongIndexCh2 = -1;

    [Header("Crossfader (0 = Ch1 Full, 0.5 = Centro, 1 = Ch2 Full)")]
    [Range(0f, 1f)] public float crossfaderValue = 0.5f;

    // --- MÁQUINA DE ESTADOS DE LOOP ---
    public enum LoopState { Off, RecordingIn, Active }

    [Header("Estado Loop Canal 1")]
    public LoopState loopStateCh1 = LoopState.Off;
    private int loopStartMsCh1 = -1;
    private int loopEndMsCh1 = -1;

    [Header("Estado Loop Canal 2")]
    public LoopState loopStateCh2 = LoopState.Off;
    private int loopStartMsCh2 = -1;
    private int loopEndMsCh2 = -1;

    void Start()
    {
        if (!RuntimeManager.HasBankLoaded("Master"))
        {
            RuntimeManager.LoadBank("Master", true);
        }
    }

    void Update()
    {
        ProcessLoop(1, songInstanceCh1, loopStateCh1 == LoopState.Active, loopStartMsCh1, loopEndMsCh1);
        ProcessLoop(2, songInstanceCh2, loopStateCh2 == LoopState.Active, loopStartMsCh2, loopEndMsCh2);
    }

    private void ProcessLoop(int channel, FMOD.Studio.EventInstance instance, bool isLoopActive, int startMs, int endMs)
    {
        if (!isLoopActive || !instance.isValid() || startMs < 0 || endMs <= startMs) return;

        instance.getTimelinePosition(out int currentPos);
        if (currentPos >= endMs)
        {
            instance.setTimelinePosition(startMs);
        }
    }

    // --- CONTROL DEL LOOP EN 3 ESTADOS ---

    public LoopState HandleLoopButton(int channel)
    {
        FMOD.Studio.EventInstance instance = (channel == 1) ? songInstanceCh1 : songInstanceCh2;
        if (!instance.isValid()) return LoopState.Off;

        instance.getTimelinePosition(out int currentPos);

        if (channel == 1)
        {
            switch (loopStateCh1)
            {
                case LoopState.Off:
                    loopStartMsCh1 = currentPos;
                    loopStateCh1 = LoopState.RecordingIn;
                    Debug.Log($"[Ch1 Loop] Entrada A fijada en: {loopStartMsCh1} ms. Esperando Salida B...");
                    return LoopState.RecordingIn;

                case LoopState.RecordingIn:
                    if (currentPos > loopStartMsCh1)
                    {
                        loopEndMsCh1 = currentPos;
                        loopStateCh1 = LoopState.Active;
                        Debug.Log($"[Ch1 Loop] Salida B fijada en: {loopEndMsCh1} ms. ¡BUCLE ACTIVADO!");
                        return LoopState.Active;
                    }
                    return LoopState.RecordingIn;

                case LoopState.Active:
                    loopStateCh1 = LoopState.Off;
                    loopStartMsCh1 = -1;
                    loopEndMsCh1 = -1;
                    Debug.Log($"[Ch1 Loop] Bucle DESACTIVADO. Canción continúa desde posición actual.");
                    return LoopState.Off;
            }
        }
        else if (channel == 2)
        {
            switch (loopStateCh2)
            {
                case LoopState.Off:
                    loopStartMsCh2 = currentPos;
                    loopStateCh2 = LoopState.RecordingIn;
                    Debug.Log($"[Ch2 Loop] Entrada A fijada en: {loopStartMsCh2} ms. Esperando Salida B...");
                    return LoopState.RecordingIn;

                case LoopState.RecordingIn:
                    if (currentPos > loopStartMsCh2)
                    {
                        loopEndMsCh2 = currentPos;
                        loopStateCh2 = LoopState.Active;
                        Debug.Log($"[Ch2 Loop] Salida B fijada en: {loopEndMsCh2} ms. ¡BUCLE ACTIVADO!");
                        return LoopState.Active;
                    }
                    return LoopState.RecordingIn;

                case LoopState.Active:
                    loopStateCh2 = LoopState.Off;
                    loopStartMsCh2 = -1;
                    loopEndMsCh2 = -1;
                    Debug.Log($"[Ch2 Loop] Bucle DESACTIVADO. Canción continúa desde posición actual.");
                    return LoopState.Off;
            }
        }

        return LoopState.Off;
    }

    // --- CONTROL DEL MASTER BEATBAR ---

    public void UpdateMasterBeatbar()
    {
        if (fuserBeatbarUI == null) return;

        if (deckChannel1 != null && deckChannel1.currentVinyl != null && songInstanceCh1.isValid())
        {
            fuserBeatbarUI.LinkFMODInstance(songInstanceCh1);
            fuserBeatbarUI.SetPlayingState(true);
        }
        else if (deckChannel2 != null && deckChannel2.currentVinyl != null && songInstanceCh2.isValid())
        {
            fuserBeatbarUI.LinkFMODInstance(songInstanceCh2);
            fuserBeatbarUI.SetPlayingState(true);
        }
        else
        {
            fuserBeatbarUI.SetPlayingState(false);
        }
    }

    // --- MÉTODOS COMPATIBLES CON SCRIPTS ANTIGUOS ---

    public void PlaySong(int songIndex) => PlaySong(1, songIndex);
    public void TogglePlayPause() => TogglePlayPause(1);
    public void StopCurrentSong() => StopSong(1);

    public void SetGain(float value) => SetGainCh1(value);
    public void SetVolume(float value) => SetVolumeCh1(value);
    public void SetEQLow(float value) => SetEQLow(1, value);
    public void SetEQMid(float value) => SetEQMid(1, value);
    public void SetEQHigh(float value) => SetEQHigh(1, value);

    public void SetKinectParameter(string parameterName, float value)
    {
        if (songInstanceCh1.isValid()) songInstanceCh1.setParameterByName(parameterName, value);
        if (songInstanceCh2.isValid()) songInstanceCh2.setParameterByName(parameterName, value);
    }

    public void FastForward() => SeekTimeline(1, 15000);
    public void Rewind() => SeekTimeline(1, -15000);

    private void SeekTimeline(int channel, int deltaMs)
    {
        FMOD.Studio.EventInstance instance = (channel == 1) ? songInstanceCh1 : songInstanceCh2;
        if (!instance.isValid()) return;

        instance.getTimelinePosition(out int currentPos);
        int targetPos = Mathf.Max(0, currentPos + deltaMs);

        instance.setTimelinePosition(targetPos);
        RuntimeManager.StudioSystem.flushCommands();
    }

    public void ResetAllKnobs()
    {
        if (djKnobs == null) return;
        foreach (var knob in djKnobs)
        {
            if (knob != null) knob.ResetKnobToDefault();
        }
    }

    // --- CONTROL DE PITCH / TEMPO ---

    public void SetPitchCh1(float faderNormalizedValue) => SetPitch(1, faderNormalizedValue);
    public void SetPitchCh2(float faderNormalizedValue) => SetPitch(2, faderNormalizedValue);

    public void SetPitch(int channel, float faderNormalizedValue)
    {
        float speedMultiplier = Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(faderNormalizedValue));

        if (channel == 1)
        {
            currentCh1Pitch = speedMultiplier;
            if (songInstanceCh1.isValid()) songInstanceCh1.setPitch(currentCh1Pitch);
        }
        else if (channel == 2)
        {
            currentCh2Pitch = speedMultiplier;
            if (songInstanceCh2.isValid()) songInstanceCh2.setPitch(currentCh2Pitch);
        }

        UpdateMasterBeatbar();
    }

    public void ResetTempo(int channel)
    {
        SetPitch(channel, 0.5f);
        UpdateMasterBeatbar();
        Debug.Log($"[Ch{channel}] Tempo reseteado a 1.0x y UI actualizada.");
    }

    // --- REPRODUCCIÓN MULTI-CANAL ---

    public void PlaySong(int channel, int songIndex)
    {
        if (songEvents == null || songIndex < 0 || songIndex >= songEvents.Length || songEvents[songIndex].IsNull)
            return;

        if (channel == 1)
        {
            if (currentSongIndexCh1 == songIndex && songInstanceCh1.isValid())
            {
                TogglePlayPause(1);
                return;
            }

            if (songButtons != null)
            {
                for (int i = 0; i < songButtons.Length; i++)
                {
                    if (i != songIndex && songButtons[i] != null) songButtons[i].ForceUnpress();
                }
            }

            StopSong(1);
            currentSongIndexCh1 = songIndex;
            songInstanceCh1 = RuntimeManager.CreateInstance(songEvents[songIndex]);
            songInstanceCh1.setPitch(currentCh1Pitch);
            songInstanceCh1.start();
            ResetAllKnobs();
        }
        else if (channel == 2)
        {
            if (currentSongIndexCh2 == songIndex && songInstanceCh2.isValid())
            {
                TogglePlayPause(2);
                return;
            }

            StopSong(2);
            currentSongIndexCh2 = songIndex;
            songInstanceCh2 = RuntimeManager.CreateInstance(songEvents[songIndex]);
            songInstanceCh2.setPitch(currentCh2Pitch);
            songInstanceCh2.start();
        }

        UpdateAudioOutputVolumes();
        UpdateMasterBeatbar();
    }

    public void TogglePlayPause(int channel)
    {
        FMOD.Studio.EventInstance instance = (channel == 1) ? songInstanceCh1 : songInstanceCh2;

        if (!instance.isValid() && channel == 1)
        {
            PlaySong(1, 0);
            return;
        }

        if (instance.isValid())
        {
            instance.getPaused(out bool isPaused);
            instance.setPaused(!isPaused);
        }
    }

    public void StopSong(int channel)
    {
        FMOD.Studio.EventInstance instance = (channel == 1) ? songInstanceCh1 : songInstanceCh2;

        if (instance.isValid())
        {
            instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            instance.release();
        }

        if (channel == 1)
        {
            currentSongIndexCh1 = -1;
            songInstanceCh1 = default;
            loopStateCh1 = LoopState.Off;
        }
        else if (channel == 2)
        {
            currentSongIndexCh2 = -1;
            songInstanceCh2 = default;
            loopStateCh2 = LoopState.Off;
        }

        UpdateMasterBeatbar();
    }

    // --- CONTROLES DE VOLUMEN Y CROSSFADER ---

    public void SetVolumeCh1(float value) { currentCh1Volume = Mathf.Clamp01(value); UpdateAudioOutputVolumes(); }
    public void SetVolumeCh2(float value) { currentCh2Volume = Mathf.Clamp01(value); UpdateAudioOutputVolumes(); }
    public void SetGainCh1(float value) { currentCh1Gain = Mathf.Clamp01(value); UpdateAudioOutputVolumes(); }
    public void SetGainCh2(float value) { currentCh2Gain = Mathf.Clamp01(value); UpdateAudioOutputVolumes(); }

    public void SetCrossfader(float value)
    {
        crossfaderValue = Mathf.Clamp01(value);
        UpdateAudioOutputVolumes();
    }

    private void UpdateAudioOutputVolumes()
    {
        float crossCh1Factor = Mathf.Clamp01((1f - crossfaderValue) * 2f);
        float crossCh2Factor = Mathf.Clamp01(crossfaderValue * 2f);

        float finalCh1 = currentCh1Volume * currentCh1Gain * crossCh1Factor;
        float finalCh2 = currentCh2Volume * currentCh2Gain * crossCh2Factor;

        if (songInstanceCh1.isValid()) songInstanceCh1.setVolume(finalCh1);
        if (songInstanceCh2.isValid()) songInstanceCh2.setVolume(finalCh2);
    }

    // --- EQ MULTI-CANAL ---

    public void SetEQLow(int channel, float value) => ApplyEQParameter(channel, "Low", value);
    public void SetEQMid(int channel, float value) => ApplyEQParameter(channel, "Mid", value);
    public void SetEQHigh(int channel, float value) => ApplyEQParameter(channel, "High", value);

    private void ApplyEQParameter(int channel, string paramName, float normalizedValue)
    {
        FMOD.Studio.EventInstance instance = (channel == 1) ? songInstanceCh1 : songInstanceCh2;
        if (!instance.isValid()) return;

        float clampedValue = Mathf.Clamp01(normalizedValue);
        float dbValue = (clampedValue < 0.5f)
            ? Mathf.Lerp(-80f, 0f, clampedValue * 2f)
            : Mathf.Lerp(0f, 6f, (clampedValue - 0.5f) * 2f);

        instance.setParameterByName(paramName, dbValue);
    }

    private void OnDestroy()
    {
        StopSong(1);
        StopSong(2);
    }
}