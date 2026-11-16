using UnityEngine;
using System.Collections.Generic;

public enum DJControlTarget
{
    VolFader_Ch1,
    VolFader_Ch2,
    Crossfader,
    EqLow_Ch1,
    EqMid_Ch1,
    EqHigh_Ch1,
    TempoFader_Ch1,
    PlayButton_Ch1,
    CueButton_Ch1,
    LoopToggle_Ch1
}

[System.Serializable]
public class PipelineNode
{
    [Tooltip("Tiempo en segundos del audio donde se evalúa la acción")]
    public float targetTime;

    [Tooltip("Control que el jugador debe manipular")]
    public DJControlTarget targetControl;

    [Tooltip("Nivel requerido en escala de 1 a 10")]
    [Range(1, 10)]
    public int targetLevel = 5;

    [Tooltip("Margen de error en niveles (ej. 1 = acepta ±1 nivel)")]
    [Range(0, 3)]
    public int levelTolerance = 1;

    [Tooltip("Ventana de tiempo en segundos (ej. 0.5s)")]
    public float timeWindow = 0.5f;

    [HideInInspector] public bool isEvaluated = false;

    // Estado en tiempo de ejecucion (no se guarda en el asset): si ya se prendio el
    // resaltado del control fisico para este nodo, para no reencenderlo en cada frame.
    [System.NonSerialized] public bool resaltadoActivado = false;
}

// CORRECCIÓN AQUÍ: 'fileName' y 'menuName' con 'n' minúscula
[CreateAssetMenu(fileName = "NewPipelineChart", menuName = "Kinektro/Pipeline Chart")]
public class DJPipelineChart : ScriptableObject
{
    public string trackName;
    public List<PipelineNode> nodes = new List<PipelineNode>();
}