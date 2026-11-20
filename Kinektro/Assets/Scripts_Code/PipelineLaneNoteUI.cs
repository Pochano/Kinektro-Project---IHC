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
    private RectTransform containerRect;
    private Vector2 spawnPos;
    private Vector2 hitZoneRightEdge;
    private Vector2 hitZoneLeftEdge;
    private float fastApproachSpeed;
    private float leftExitBoundary;
    private bool isResolved = false;

    public void Initialize(PipelineNode targetNode, ControlVisualEntry visual, float anticipationTime, Vector2 startAnchoredPos, RectTransform hitZoneRect)
    {
        node = targetNode;
        leadTime = anticipationTime;
        spawnPos = startAnchoredPos;
        rectTransform = GetComponent<RectTransform>();
        containerRect = transform.parent as RectTransform;
        transform.localScale = Vector3.one;

        if (visual != null)
        {
            if (backgroundBadge != null) backgroundBadge.color = visual.badgeColor;
            if (controlIcon != null && visual.icon != null)
            {
                controlIcon.sprite = visual.icon;
                controlIcon.color = Color.white; // Sin filtro mientras viaja
            }
        }

        if (levelText != null)
        {
            levelText.text = (node.targetLevel == 10 && node.levelTolerance == 0) ? "ACT" : node.targetLevel.ToString();
        }

        Vector2 hitCenter = hitZoneRect != null ? hitZoneRect.anchoredPosition : Vector2.zero;
        float zoneHalfWidth = hitZoneRect != null ? (hitZoneRect.rect.width * 0.5f) : 50f;
        float noteHalfWidth = rectTransform != null ? (rectTransform.rect.width * 0.5f) : 25f;

        hitZoneRightEdge = new Vector2(hitCenter.x + zoneHalfWidth + noteHalfWidth, hitCenter.y);
        hitZoneLeftEdge = new Vector2(hitCenter.x - zoneHalfWidth - noteHalfWidth, hitCenter.y);

        float approachDistance = Mathf.Abs(spawnPos.x - hitZoneRightEdge.x);
        fastApproachSpeed = (leadTime > 0f) ? (approachDistance / leadTime) : 200f;

        float containerHalfWidth = (containerRect != null) ? (containerRect.rect.width * 0.5f) : 500f;
        leftExitBoundary = -containerHalfWidth - noteHalfWidth;

        rectTransform.anchoredPosition = spawnPos;
    }

    private void Update()
    {
        if (DJPipelineEvaluator.Instance == null) return;

        float currentTime = DJPipelineEvaluator.Instance.CurrentSongTime;
        float windowStart = node.targetTime;
        float windowEnd = node.targetTime + node.timeWindow;

        if (!isResolved && currentTime < windowStart)
        {
            float timeRemaining = windowStart - currentTime;
            float progress = 1f - Mathf.Clamp01(timeRemaining / leadTime);
            rectTransform.anchoredPosition = Vector2.Lerp(spawnPos, hitZoneRightEdge, progress);
        }
        else if (!isResolved && currentTime <= windowEnd && node.timeWindow > 0f)
        {
            float windowProgress = (currentTime - windowStart) / node.timeWindow;
            rectTransform.anchoredPosition = Vector2.Lerp(hitZoneRightEdge, hitZoneLeftEdge, windowProgress);
        }
        else
        {
            Vector2 pos = rectTransform.anchoredPosition;
            pos.x -= fastApproachSpeed * Time.deltaTime;
            rectTransform.anchoredPosition = pos;

            if (pos.x <= leftExitBoundary)
            {
                Destroy(gameObject);
            }
        }
    }

    public void ResolveVisual(bool isHit)
    {
        if (isResolved) return;
        isResolved = true;

        if (isHit)
        {
            // Filtro verde manteniendo la silueta del icono
            Color verdeHit = new Color(0.15f, 1f, 0.25f, 1f);
            if (controlIcon != null) controlIcon.color = verdeHit;
            if (backgroundBadge != null) backgroundBadge.color = new Color(0.05f, 0.45f, 0.12f, 1f);
        }
        else
        {
            // Filtro rojo + ligero aumento de tamaño para resaltar el Miss
            Color rojoMiss = new Color(1f, 0.15f, 0.15f, 1f);
            if (controlIcon != null) controlIcon.color = rojoMiss;
            if (backgroundBadge != null) backgroundBadge.color = new Color(0.5f, 0.05f, 0.05f, 1f);
            transform.localScale = Vector3.one * 1.15f;
        }
    }

    public PipelineNode GetNode() => node;
}