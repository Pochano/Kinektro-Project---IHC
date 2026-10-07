using System;
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FMODUnity;

public class FuserBeatbarUI : MonoBehaviour
{
    [Header("Configuración Visual")]
    public RectTransform ticksContainer;
    public GameObject tickPrefab;
    public float pixelsPerBeat = 140f;
    public int totalTicksToPool = 28;

    [Header("Grosor y Altura de las Barras")]
    public float normalTickWidth = 12f;
    public float normalTickHeight = 45f;
    public float accentTickWidth = 22f;
    public float accentTickHeight = 70f;

    [Header("Ajuste Fino Visual (Píxeles)")]
    [Tooltip("Ajuste visual en píxeles hacia adelante (+) o atrás (-). Usar para centrar la barra perfectamente sobre el puntero.")]
    public float visualPixelOffset = 0f;

    [Header("Estado Sincronizado por FMOD")]
    public float currentBPM = 120f;
    public bool isPlaying = false;
    public int currentTimelineBeat = 0;
    public int currentTimelineBar = 0;

    private List<RectTransform> pooledTicks = new List<RectTransform>();

    // Variables internas de FMOD Callback
    private FMOD.Studio.EventInstance activeInstance;
    private FMOD.Studio.EVENT_CALLBACK beatCallback;
    private GCHandle timelineHandle;
    private TimelineInfo timelineInfo = new TimelineInfo();

    // Variables del Reloj Híbrido
    private float lastCallbackBeatPosition = 0f;
    private float lastCallbackSystemTime = 0f;
    private float currentContinuousBeat = 0f;

    [StructLayout(LayoutKind.Sequential)]
    public class TimelineInfo
    {
        public int currentBeat = 0;
        public int currentBar = 0;
        public float currentBPM = 120f;
        public int positionMs = 0;
        public bool updatedThisFrame = false;
    }

    private void Start()
    {
        InitializeTicksPool();
    }

    private void Update()
    {
        if (!isPlaying) return;

        if (activeInstance.isValid() && timelineInfo != null)
        {
            currentTimelineBeat = timelineInfo.currentBeat;
            currentTimelineBar = timelineInfo.currentBar;
            if (timelineInfo.currentBPM > 0) currentBPM = timelineInfo.currentBPM;

            float beatsPerSecond = currentBPM / 60f;

            // Si el Callback de FMOD registró un nuevo Beat/Bar
            if (timelineInfo.updatedThisFrame)
            {
                timelineInfo.updatedThisFrame = false;

                // Calculamos la posición base exacta en Beats reportada por FMOD
                lastCallbackBeatPosition = (timelineInfo.positionMs / 1000f) * beatsPerSecond;
                lastCallbackSystemTime = Time.unscaledTime;
            }

            // Avance continuo entre Callbacks usando el reloj de tiempo real de Unity
            float timeSinceLastCallback = Time.unscaledTime - lastCallbackSystemTime;
            float projectedBeatPosition = lastCallbackBeatPosition + (timeSinceLastCallback * beatsPerSecond);

            // Suavizado suave si hay pequeñas discrepancias
            currentContinuousBeat = Mathf.Lerp(currentContinuousBeat, projectedBeatPosition, Time.deltaTime * 30f);

            UpdateTicksPosition(currentContinuousBeat);
        }
    }

    private void InitializeTicksPool()
    {
        if (ticksContainer == null || tickPrefab == null) return;

        foreach (Transform child in ticksContainer) Destroy(child.gameObject);
        pooledTicks.Clear();

        int halfPool = totalTicksToPool / 2;
        for (int i = -halfPool; i < halfPool; i++)
        {
            GameObject newTick = Instantiate(tickPrefab, ticksContainer);
            newTick.SetActive(true);

            RectTransform rt = newTick.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                pooledTicks.Add(rt);
            }
        }
    }

    private void UpdateTicksPosition(float currentBeatPosition)
    {
        if (pooledTicks.Count == 0) return;

        float totalSpan = pooledTicks.Count * pixelsPerBeat;
        float halfSpan = totalSpan / 2f;

        for (int i = 0; i < pooledTicks.Count; i++)
        {
            float baseOffset = (i - (pooledTicks.Count / 2f)) * pixelsPerBeat;

            // Posición X con el offset de píxeles visual aplicado
            float rawX = baseOffset - (currentBeatPosition * pixelsPerBeat) + visualPixelOffset;
            float moduloX = Mathf.Repeat(rawX + halfSpan, totalSpan) - halfSpan;

            pooledTicks[i].anchoredPosition = new Vector2(moduloX, 0f);

            // Redondeo exacto respecto al centro (posición cero)
            int beatIndex = Mathf.RoundToInt(currentBeatPosition + ((moduloX - visualPixelOffset) / pixelsPerBeat));

            // Beat 1 del compás (cada 4 beats) = Barra gruesa principal
            if (beatIndex % 4 == 0)
            {
                pooledTicks[i].sizeDelta = new Vector2(accentTickWidth, accentTickHeight);
            }
            else
            {
                pooledTicks[i].sizeDelta = new Vector2(normalTickWidth, normalTickHeight);
            }
        }
    }

    public void LinkFMODInstance(FMOD.Studio.EventInstance instance)
    {
        UnlinkFMODInstance();

        if (!instance.isValid()) return;

        activeInstance = instance;
        timelineInfo = new TimelineInfo();

        timelineHandle = GCHandle.Alloc(timelineInfo, GCHandleType.Pinned);
        activeInstance.setUserData(GCHandle.ToIntPtr(timelineHandle));

        beatCallback = new FMOD.Studio.EVENT_CALLBACK(FMODBeatEventCallback);
        activeInstance.setCallback(beatCallback, FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_BEAT);

        // Reset inicial del contador
        activeInstance.getTimelinePosition(out int startMs);
        lastCallbackBeatPosition = (startMs / 1000f) * (currentBPM / 60f);
        lastCallbackSystemTime = Time.unscaledTime;
        currentContinuousBeat = lastCallbackBeatPosition;
    }

    public void UnlinkFMODInstance()
    {
        if (activeInstance.isValid())
        {
            activeInstance.setCallback(null);
        }

        if (timelineHandle.IsAllocated)
        {
            timelineHandle.Free();
        }
    }

    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    private static FMOD.RESULT FMODBeatEventCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameterPtr)
    {
        FMOD.Studio.EventInstance instance = new FMOD.Studio.EventInstance(instancePtr);
        instance.getUserData(out IntPtr timelineInfoPtr);

        if (timelineInfoPtr != IntPtr.Zero)
        {
            GCHandle timelineHandle = GCHandle.FromIntPtr(timelineInfoPtr);
            TimelineInfo timelineInfo = (TimelineInfo)timelineHandle.Target;

            if (type == FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_BEAT)
            {
                var parameter = (FMOD.Studio.TIMELINE_BEAT_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.TIMELINE_BEAT_PROPERTIES));
                timelineInfo.currentBeat = parameter.beat;
                timelineInfo.currentBar = parameter.bar;
                timelineInfo.currentBPM = parameter.tempo;

                instance.getTimelinePosition(out timelineInfo.positionMs);
                timelineInfo.updatedThisFrame = true;
            }
        }
        return FMOD.RESULT.OK;
    }

    public void SetPlayingState(bool playing)
    {
        isPlaying = playing;

        if (!playing)
        {
            UnlinkFMODInstance();
        }

        if (ticksContainer != null)
        {
            ticksContainer.gameObject.SetActive(playing);
        }
    }

    private void OnDestroy()
    {
        UnlinkFMODInstance();
    }
}