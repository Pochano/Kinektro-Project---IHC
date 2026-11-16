using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PipelineLaneNoteUI : MonoBehaviour
{
    [Header("Componentes UI")]
    public Image backgroundBadge;
    public Image controlIcon;
    public TextMeshProUGUI levelText;

    private PipelineNode node;
    private float leadTime;
    private RectTransform rectTransform;
    private Vector2 spawnPos;
    private Vector2 hitZoneRightEdge;
    private Vector2 hitZoneLeftEdge;
    private float fastApproachSpeed;
    private bool isResolved = false;

    public void Initialize(PipelineNode targetNode, ControlVisualEntry visual, float anticipationTime, Vector2 startAnchoredPos, RectTransform hitZoneRect)
    {
        node = targetNode;
        leadTime = anticipationTime;
        spawnPos = startAnchoredPos;
        rectTransform = GetComponent<RectTransform>();

        if (visual != null)
        {
            if (backgroundBadge != null) backgroundBadge.color = visual.badgeColor;
            if (controlIcon != null && visual.icon != null) controlIcon.sprite = visual.icon;
        }

        if (levelText != null)
        {
            levelText.text = (node.targetLevel == 10 && node.levelTolerance == 0) ? "ACT" : node.targetLevel.ToString();
        }

        // Calculamos los bordes izquierdo y derecho reales de LaneHitZone
        Vector2 hitCenter = hitZoneRect != null ? hitZoneRect.anchoredPosition : Vector2.zero;
        float zoneHalfWidth = hitZoneRect != null ? (hitZoneRect.rect.width * 0.5f) : 50f;
        float noteHalfWidth = rectTransform != null ? (rectTransform.rect.width * 0.5f) : 25f;

        // Borde derecho: punto exacto donde la nota entra en contacto con LaneHitZone
        hitZoneRightEdge = new Vector2(hitCenter.x + zoneHalfWidth + noteHalfWidth, hitCenter.y);
        // Borde izquierdo: punto exacto donde la nota termina de salir de LaneHitZone
        hitZoneLeftEdge = new Vector2(hitCenter.x - zoneHalfWidth - noteHalfWidth, hitCenter.y);

        // Guardamos la velocidad rápida original (píxeles/segundo) para reutilizarla al salir
        float approachDistance = Mathf.Abs(spawnPos.x - hitZoneRightEdge.x);
        fastApproachSpeed = (leadTime > 0f) ? (approachDistance / leadTime) : 200f;

        rectTransform.anchoredPosition = spawnPos;
    }

    private void Update()
    {
        if (isResolved || DJPipelineEvaluator.Instance == null || !DJPipelineEvaluator.Instance.IsRunning) return;

        float currentTime = DJPipelineEvaluator.Instance.CurrentSongTime;
        float windowStart = node.targetTime;
        float windowEnd = node.targetTime + node.timeWindow;

        if (currentTime < windowStart)
        {
            // FASE 1: Aproximación rápida desde SpawnPoint hasta el borde derecho de LaneHitZone
            float timeRemaining = windowStart - currentTime;
            float progress = 1f - Mathf.Clamp01(timeRemaining / leadTime);
            rectTransform.anchoredPosition = Vector2.Lerp(spawnPos, hitZoneRightEdge, progress);
        }
        else if (currentTime <= windowEnd && node.timeWindow > 0f)
        {
            // FASE 2: Cruce lento dentro de LaneHitZone durante exactamente 'timeWindow' segundos
            float windowProgress = (currentTime - windowStart) / node.timeWindow;
            rectTransform.anchoredPosition = Vector2.Lerp(hitZoneRightEdge, hitZoneLeftEdge, windowProgress);
        }
        else
        {
            // FASE 3 (Miss): Salió por el borde izquierdo y recupera su velocidad rápida normal
            float overtime = currentTime - windowEnd;
            rectTransform.anchoredPosition = new Vector2(hitZoneLeftEdge.x - (fastApproachSpeed * overtime), hitZoneLeftEdge.y);
        }
    }

    public void ResolveVisual(bool isHit)
    {
        if (isResolved) return;
        isResolved = true;

        if (backgroundBadge != null)
        {
            backgroundBadge.color = isHit ? Color.green : Color.red;
        }

        // Si fue Miss, dejamos que siga saliendo rápido un instante antes de destruirse
        Destroy(gameObject, isHit ? 0.25f : 0.6f);
    }

    public PipelineNode GetNode() => node;
}