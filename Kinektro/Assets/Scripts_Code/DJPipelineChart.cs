using UnityEngine;
using System.Collections.Generic;

public enum DJControlTarget
{
    None,
    // --- CANAL 1 ---
    VolFader_Ch1,
    TempoFader_Ch1,
    Gain_Ch1,
    EqLow_Ch1,
    EqMid_Ch1,
    EqHigh_Ch1,
    PlayButton_Ch1,
    CueButton_Ch1,
    LoopToggle_Ch1,
    ResetTempo_Ch1,
    LoadVinyl_Ch1,

    // --- CANAL 2 ---
    VolFader_Ch2,
    TempoFader_Ch2,
    Gain_Ch2,
    EqLow_Ch2,
    EqMid_Ch2,
    EqHigh_Ch2,
    PlayButton_Ch2,
    CueButton_Ch2,
    LoopToggle_Ch2,
    ResetTempo_Ch2,
    LoadVinyl_Ch2,

    // --- GLOBAL ---
    Crossfader
}

[System.Serializable]
public class PipelineNode
{
    [Tooltip("Tiempo en segundos del audio donde se evalúa la acción")]
    public float targetTime;

    [Tooltip("Control que el jugador debe manipular")]
    public DJControlTarget targetControl;

    [Tooltip("Nivel requerido en escala de 1 a 10 (Para botones/vinilos usa 10)")]
    [Range(1, 10)]
    public int targetLevel = 5;

    [Tooltip("Margen de error en niveles (ej. 1 = acepta ±1 nivel)")]
    [Range(0, 3)]
    public int levelTolerance = 1;

    [Tooltip("Ventana de tiempo en segundos (ej. 0.5s)")]
    public float timeWindow = 0.5f;

    [HideInInspector] public bool isSpawned = false;
    [HideInInspector] public bool isEvaluated = false;
}

[CreateAssetMenu(fileName = "NewPipelineChart", menuName = "Kinektro/Pipeline Chart")]
public class DJPipelineChart : ScriptableObject
{
    public string trackName;
    public List<PipelineNode> nodes = new List<PipelineNode>();
}