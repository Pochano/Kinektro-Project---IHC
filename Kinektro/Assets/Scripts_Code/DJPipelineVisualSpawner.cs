using UnityEngine;
using System.Collections.Generic;

public class DJPipelineVisualSpawner : MonoBehaviour
{
    [Header("Configuración Visual")]
    public DJControlVisualConfig visualConfig;
    [Tooltip("Segundos de anticipación con los que aparece la nota antes de tocar el borde derecho de HitZone")]
    public float leadTime = 3.0f;

    [Header("Prefab de Nota")]
    public PipelineLaneNoteUI laneNotePrefab;

    [Header("Puntos de Anclaje en FuserBeatbarUI")]
    [Tooltip("Contenedor padre dentro del Canvas de FuserBeatbarUI")]
    public RectTransform notesContainer;
    [Tooltip("Punto derecho donde nacen las notas del carril")]
    public RectTransform laneSpawnPoint;
    [Tooltip("Zona de impacto (RectTransform con el ancho de la ventana)")]
    public RectTransform laneHitZone;

    private List<PipelineLaneNoteUI> activeLaneNotes = new List<PipelineLaneNoteUI>();

    private void Start()
    {
        if (DJPipelineEvaluator.Instance != null)
        {
            DJPipelineEvaluator.Instance.OnPipelineStarted += ClearAllNotes;
            DJPipelineEvaluator.Instance.OnNodeHitEvent += HandleNodeHit;
            DJPipelineEvaluator.Instance.OnNodeMissedEvent += HandleNodeMissed;
        }
    }

    private void OnDestroy()
    {
        if (DJPipelineEvaluator.Instance != null)
        {
            DJPipelineEvaluator.Instance.OnPipelineStarted -= ClearAllNotes;
            DJPipelineEvaluator.Instance.OnNodeHitEvent -= HandleNodeHit;
            DJPipelineEvaluator.Instance.OnNodeMissedEvent -= HandleNodeMissed;
        }
    }

    private void Update()
    {
        var evaluator = DJPipelineEvaluator.Instance;
        if (evaluator == null || !evaluator.IsRunning || evaluator.activeChart == null) return;

        float currentTime = evaluator.CurrentSongTime;
        var nodes = evaluator.activeChart.nodes;

        for (int i = 0; i < nodes.Count; i++)
        {
            PipelineNode node = nodes[i];

            if (!node.isSpawned && !node.isEvaluated && currentTime >= (node.targetTime - leadTime))
            {
                node.isSpawned = true;
                SpawnNodeVisual(node);
            }
        }
    }

    private void SpawnNodeVisual(PipelineNode node)
    {
        if (laneNotePrefab == null || notesContainer == null) return;

        ControlVisualEntry visual = (visualConfig != null) ? visualConfig.GetConfig(node.targetControl) : null;

        PipelineLaneNoteUI noteObj = Instantiate(laneNotePrefab, notesContainer);
        Vector2 startPos = laneSpawnPoint != null ? laneSpawnPoint.anchoredPosition : new Vector2(400f, 0f);

        noteObj.Initialize(node, visual, leadTime, startPos, laneHitZone);
        activeLaneNotes.Add(noteObj);
    }

    private void HandleNodeHit(PipelineNode node, int levelReached) => ResolveNote(node, true);
    private void HandleNodeMissed(PipelineNode node) => ResolveNote(node, false);

    private void ResolveNote(PipelineNode node, bool isHit)
    {
        for (int i = activeLaneNotes.Count - 1; i >= 0; i--)
        {
            if (activeLaneNotes[i] == null) { activeLaneNotes.RemoveAt(i); continue; }
            if (activeLaneNotes[i].GetNode() == node)
            {
                activeLaneNotes[i].ResolveVisual(isHit);
                return;
            }
        }
    }

    private void ClearAllNotes()
    {
        foreach (var n in activeLaneNotes) if (n != null) Destroy(n.gameObject);
        activeLaneNotes.Clear();
    }
}