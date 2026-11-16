using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class DJPipelineEvaluator : MonoBehaviour
{
    public static DJPipelineEvaluator Instance { get; private set; }

    [Header("Referencias")]
    public DJ_Manager djManager;
    public DJPipelineChart activeChart;
    [Tooltip("Puente UDP de esta PC (A) hacia la PC B. Se usa para avisarle cuando arranca/termina la ventana del pipeline y cada vez que el jugador A acierta una interaccion (para que B sepa cuando hacer un gesto).")]
    public CrossPC_OculusBridge bridge;

    [System.Serializable]
    public struct MapeoControlObjeto
    {
        public DJControlTarget control;
        [Tooltip("Arrastra aca el VR_Boton, VR_Fader o VR_Knob_Joystick correspondiente a este control.")]
        public MonoBehaviour objetoControl;
    }
    [Header("Resaltado de controles fisicos (Jugador A)")]
    [Tooltip("Por cada control del chart, el objeto fisico (boton/fader/knob) que hay que iluminar cuando se acerca su turno.")]
    public List<MapeoControlObjeto> mapeoControlAObjeto = new List<MapeoControlObjeto>();
    [Tooltip("Cuantos segundos antes de targetTime se enciende el resaltado del control.")]
    public float anticipacionResaltado = 1.5f;

    [Header("UI de Cuenta Regresiva (Opcional)")]
    public TextMeshProUGUI countdownText;

    // Texto de score/combo siempre visible (arriba a la izquierda). No es [Header]
    // publico a proposito: se arma solo por codigo en Awake(), no hace falta asignarlo
    // a mano en el Inspector.
    private TextMeshProUGUI scoreComboText;

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
        ConstruirUIBasica();
        ActualizarScoreComboUI();
    }

    // Si no se asigno "countdownText" a mano en el Inspector, armamos por codigo un
    // Canvas minimo con la cuenta regresiva (centro, grande) y un marcador de
    // Score/Combo siempre visible (arriba a la izquierda) - asi se ve algo real en
    // pantalla al probar, sin depender de que alguien lo cablee manualmente.
    private void ConstruirUIBasica()
    {
        var canvasGO = new GameObject("Pipeline_HUD_Canvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        if (countdownText == null)
        {
            var countdownGO = new GameObject("CountdownText", typeof(RectTransform));
            countdownGO.transform.SetParent(canvasGO.transform, false);
            var rt = countdownGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(600, 300);

            var tmp = countdownGO.AddComponent<TextMeshProUGUI>();
            tmp.font = TMP_Settings.defaultFontAsset;
            tmp.fontSize = 180;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.85f, 0.2f);
            tmp.text = "";
            countdownGO.SetActive(false);

            countdownText = tmp;
        }

        var scoreGO = new GameObject("ScoreComboText", typeof(RectTransform));
        scoreGO.transform.SetParent(canvasGO.transform, false);
        var scoreRT = scoreGO.GetComponent<RectTransform>();
        scoreRT.anchorMin = new Vector2(0f, 1f);
        scoreRT.anchorMax = new Vector2(0f, 1f);
        scoreRT.pivot = new Vector2(0f, 1f);
        scoreRT.anchoredPosition = new Vector2(30f, -30f);
        scoreRT.sizeDelta = new Vector2(500f, 90f);

        scoreComboText = scoreGO.AddComponent<TextMeshProUGUI>();
        scoreComboText.font = TMP_Settings.defaultFontAsset;
        scoreComboText.fontSize = 36;
        scoreComboText.fontStyle = FontStyles.Bold;
        scoreComboText.alignment = TextAlignmentOptions.TopLeft;
        scoreComboText.color = Color.white;
    }

    // Refleja currentScore/currentCombo en el texto de arriba a la izquierda. Se llama
    // cada vez que cualquiera de los dos cambia (HIT, MISS, y al arrancar).
    private void ActualizarScoreComboUI()
    {
        if (scoreComboText != null)
            scoreComboText.text = $"Score: {currentScore}\nCombo: {currentCombo}";
    }

    // Permite arrancar el pipeline a mano desde el Inspector (click derecho sobre el
    // componente en Play Mode > "TEST: Iniciar Pipeline"), sin necesidad del casco ni
    // de apretar el boton StartPipeline fisicamente - util para probar el envio de
    // datos a la PC B sin hardware VR a mano.
    [ContextMenu("TEST: Iniciar Pipeline (StartCountdown)")]
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
        bridge?.SendPipelineWindowOpen();

        yield return new WaitForSeconds(0.5f);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    // Busca, entre los objetos mapeados en el Inspector, el que corresponde a un
    // DJControlTarget dado, y lo devuelve como IResaltable (sea VR_Boton, VR_Fader o
    // VR_Knob_Joystick - no nos importa cual, solo que sepa prenderse/apagarse).
    private IResaltable BuscarResaltable(DJControlTarget control)
    {
        foreach (var mapeo in mapeoControlAObjeto)
        {
            if (mapeo.control == control) return mapeo.objetoControl as IResaltable;
        }
        return null;
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
                node.resaltadoActivado = false;
                BuscarResaltable(node.targetControl)?.SetResaltado(false);
            }
        }

        ActualizarScoreComboUI();
    }

    private void Update()
    {
        // Atajos de teclado para poder probar el pipeline sin VR/Kinect a mano - pensados
        // para el build standalone de demostracion, donde no hay Inspector para el menu
        // contextual de prueba (eso solo existe dentro del Editor): P arranca el pipeline
        // (igual que el boton fisico StartPipeline), H fuerza un HIT en el proximo nodo
        // pendiente del chart (sin validar tiempo/nivel), asi se puede mostrar un
        // PIPELINE_HIT real viajando entre los dos procesos sin tocar nada de VR.
        if (Keyboard.current != null)
        {
            if (Keyboard.current.pKey.wasPressedThisFrame) StartCountdown();
            if (isRunning && Keyboard.current.hKey.wasPressedThisFrame) ForzarHitDeProximoNodo();
        }

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

            if (!node.isEvaluated && !node.resaltadoActivado && songTimer >= (node.targetTime - anticipacionResaltado))
            {
                node.resaltadoActivado = true;
                BuscarResaltable(node.targetControl)?.SetResaltado(true);
            }

            if (!node.isEvaluated && songTimer > (node.targetTime + node.timeWindow))
            {
                node.isEvaluated = true;
                OnNodeMissed(node);
            }
        }

        CheckChartFinished();
    }

    // Si ya no queda ningun nodo sin evaluar, el chart termino: avisamos a la PC B
    // (PIPELINE_WINDOW_CLOSE) y dejamos de correr el cronometro.
    private void CheckChartFinished()
    {
        if (!isRunning || activeChart == null) return;

        for (int i = 0; i < activeChart.nodes.Count; i++)
        {
            if (!activeChart.nodes[i].isEvaluated) return;
        }

        isRunning = false;
        bridge?.SendPipelineWindowClose(currentScore);
        Debug.Log($"<color=magenta>[PIPELINE] Chart terminado. Score final: {currentScore}</color>");
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

    // Solo para pruebas sin hardware: marca el proximo nodo sin evaluar como HIT
    // directamente, salteando los chequeos de tiempo/nivel que usa SubmitInput.
    private void ForzarHitDeProximoNodo()
    {
        if (activeChart == null) return;
        for (int i = 0; i < activeChart.nodes.Count; i++)
        {
            PipelineNode node = activeChart.nodes[i];
            if (!node.isEvaluated)
            {
                node.isEvaluated = true;
                OnNodeHit(node, node.targetLevel);
                return;
            }
        }
    }

    private void OnNodeHit(PipelineNode node, int levelReached)
    {
        currentCombo++;
        currentScore += 100 * currentCombo;
        Debug.Log($"<color=green>[HIT!]</color> Control: {node.targetControl} | Nivel Objetivo: {node.targetLevel} | Alcanzado: {levelReached} | Combo: {currentCombo}");
        bridge?.SendPipelineHit(node.targetControl.ToString(), currentCombo);
        BuscarResaltable(node.targetControl)?.SetResaltado(false);
        ActualizarScoreComboUI();
    }

    private void OnNodeMissed(PipelineNode node)
    {
        currentCombo = 0;
        Debug.Log($"<color=red>[MISS!]</color> Tiempo agotado para {node.targetControl} (Nivel pedido: {node.targetLevel})");
        BuscarResaltable(node.targetControl)?.SetResaltado(false);
        ActualizarScoreComboUI();
    }
}
