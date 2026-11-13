using UnityEngine;
using System.Collections;
using TMPro;

public class DJPipelineEvaluator : MonoBehaviour
{
    public static DJPipelineEvaluator Instance { get; private set; }

    [Header("Referencias")]
    public DJ_Manager djManager;
    public DJPipelineChart activeChart;

    [Header("UI de Cuenta Regresiva (Opcional)")]
    public TextMeshProUGUI countdownText;

    [Header("Puntuación")]
    public int currentScore = 0;
    public int currentCombo = 0;

    private float songTimer = 0f;
    private int lastPrintedSecond = -1; // <--- VARIABLE NUEVA PARA RASTREAR EL SEGUNDO
    private bool isRunning = false;
    private bool isCountingDown = false;

    private void Awake()
    {
        Instance = this;
    }

    public void StartCountdown()
    {
        if (isRunning || isCountingDown) return;
        StartCoroutine(CountdownRoutine());
    }

    private IEnumerator CountdownRoutine()
    {
        isCountingDown = true;

        if (countdownText != null) countdownText.gameObject.SetActive(true);

        if (countdownText != null) countdownText.text = "3";
        Debug.Log("<color=yellow>[PIPELINE] 3...</color>");
        yield return new WaitForSeconds(1.0f);

        if (countdownText != null) countdownText.text = "2";
        Debug.Log("<color=yellow>[PIPELINE] 2...</color>");
        yield return new WaitForSeconds(1.0f);

        if (countdownText != null) countdownText.text = "1";
        Debug.Log("<color=yellow>[PIPELINE] 1...</color>");
        yield return new WaitForSeconds(1.0f);

        if (countdownText != null) countdownText.text = "¡GO!";
        Debug.Log("<color=green>[PIPELINE] ¡GO! Iniciando evaluación.</color>");

        isCountingDown = false;
        StartPipeline();

        yield return new WaitForSeconds(0.5f);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    public void StartPipeline()
    {
        songTimer = 0f;
        lastPrintedSecond = -1; // Reiniciar contador de segundos
        isRunning = true;

        if (activeChart != null)
        {
            foreach (var node in activeChart.nodes)
            {
                node.isEvaluated = false;
            }
        }
    }

    private void Update()
    {
        if (!isRunning || activeChart == null) return;

        songTimer += Time.deltaTime;

        // --- IMPRIMIR CADA SEGUNDO EN LA CONSOLA ---
        int currentSecond = Mathf.FloorToInt(songTimer);
        if (currentSecond > lastPrintedSecond)
        {
            lastPrintedSecond = currentSecond;
            Debug.Log($"<color=cyan>[TIEMPO] {lastPrintedSecond}s</color>");
        }

        // Evaluar nodos expirados (MISS)
        for (int i = 0; i < activeChart.nodes.Count; i++)
        {
            PipelineNode node = activeChart.nodes[i];

            if (!node.isEvaluated && songTimer > (node.targetTime + node.timeWindow))
            {
                node.isEvaluated = true;
                OnNodeMissed(node);
            }
        }
    }

    public void SubmitInput(DJControlTarget control, float rawNormalizedValue)
    {
        if (!isRunning || activeChart == null) return;

        int playerLevel = Mathf.RoundToInt(Mathf.Clamp01(rawNormalizedValue) * 9f) + 1;

        for (int i = 0; i < activeChart.nodes.Count; i++)
        {
            PipelineNode node = activeChart.nodes[i];

            if (node.isEvaluated) continue;

            if (node.targetControl == control)
            {
                bool timeValid = Mathf.Abs(songTimer - node.targetTime) <= node.timeWindow;
                bool levelValid = Mathf.Abs(playerLevel - node.targetLevel) <= node.levelTolerance;

                if (timeValid && levelValid)
                {
                    node.isEvaluated = true;
                    OnNodeHit(node, playerLevel);
                    break;
                }
            }
        }
    }

    private void OnNodeHit(PipelineNode node, int levelReached)
    {
        currentCombo++;
        currentScore += 100 * currentCombo;
        Debug.Log($"<color=green>[HIT!]</color> Control: {node.targetControl} | Nivel Objetivo: {node.targetLevel} | Alcanzado: {levelReached} | Combo: {currentCombo}");
    }

    private void OnNodeMissed(PipelineNode node)
    {
        currentCombo = 0;
        Debug.Log($"<color=red>[MISS!]</color> Tiempo agotado para {node.targetControl} (Nivel pedido: {node.targetLevel})");
    }
}
