using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Herramienta de grabacion de poses. Interfaz en pantalla (OnGUI) con dos campos -
// nombre de la pose y cantidad de repeticiones - y un boton para grabar. Por cada
// repeticion: cuenta regresiva para que la persona se acomode, despues se capturan
// los angulos durante una ventana corta (para promediar y filtrar el temblor de un
// solo frame), y al final se promedian TODAS las repeticiones entre si para obtener
// un valor mas estable por angulo. El resultado se guarda con PoseLibrary.
public class PoseRecorder : MonoBehaviour
{
    [Header("Fuente de datos")]
    public KinectSourceProvider sourceProvider;
    private IJointDataSource dataSource;

    [Header("Tiempos")]
    [Tooltip("Cuenta regresiva antes de capturar cada repeticion, en segundos.")]
    public float cuentaRegresivaSegundos = 3f;
    [Tooltip("Cuanto tiempo se promedian los angulos DENTRO de una repeticion, para filtrar temblor de un solo frame.")]
    public float ventanaCapturaSegundos = 0.5f;

    private string nombrePoseInput = "";
    private string repeticionesInput = "5";
    private string estado = "Listo para grabar.";
    private bool grabando = false;

    void Start()
    {
        if (sourceProvider != null) dataSource = sourceProvider.GetActiveSource();
        if (dataSource == null) Debug.LogError("[PoseRecorder] No se pudo obtener una fuente de datos valida desde el SourceProvider.");
    }

    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 380, 160), "Grabador de poses");

        GUI.Label(new Rect(20, 35, 150, 20), "Nombre de la pose:");
        nombrePoseInput = GUI.TextField(new Rect(180, 35, 190, 22), nombrePoseInput);

        GUI.Label(new Rect(20, 62, 150, 20), "Repeticiones:");
        repeticionesInput = GUI.TextField(new Rect(180, 62, 190, 22), repeticionesInput);

        GUI.enabled = !grabando && dataSource != null;
        if (GUI.Button(new Rect(20, 92, 350, 32), "Grabar pose"))
        {
            if (!string.IsNullOrWhiteSpace(nombrePoseInput) && int.TryParse(repeticionesInput, out int reps) && reps > 0)
            {
                StartCoroutine(GrabarPoseRoutine(nombrePoseInput.Trim(), reps));
            }
            else
            {
                estado = "Nombre invalido o cantidad de repeticiones invalida.";
            }
        }
        GUI.enabled = true;

        GUI.Label(new Rect(20, 128, 350, 40), estado);
    }

    private IEnumerator GrabarPoseRoutine(string nombrePose, int repeticiones)
    {
        grabando = true;
        var acumulado = new Dictionary<string, float>();
        var conteoPorAngulo = new Dictionary<string, int>();

        for (int rep = 1; rep <= repeticiones; rep++)
        {
            // Cuenta regresiva: le da tiempo a la persona de acomodarse en la pose.
            float restante = cuentaRegresivaSegundos;
            while (restante > 0f)
            {
                estado = $"Repeticion {rep}/{repeticiones} - mantene la pose. Capturando en {restante:F1}s...";
                restante -= Time.deltaTime;
                yield return null;
            }

            // Captura promediada durante la ventana (filtra temblor de un solo frame).
            estado = $"Repeticion {rep}/{repeticiones} - CAPTURANDO, no te muevas...";
            var sumaVentana = new Dictionary<string, float>();
            var cuentaVentana = new Dictionary<string, int>();
            float tiempoVentana = 0f;

            while (tiempoVentana < ventanaCapturaSegundos)
            {
                var angulos = JointAngleCalculator.CalcularAngulos(dataSource);
                foreach (var kv in angulos)
                {
                    if (!sumaVentana.ContainsKey(kv.Key)) { sumaVentana[kv.Key] = 0f; cuentaVentana[kv.Key] = 0; }
                    sumaVentana[kv.Key] += kv.Value;
                    cuentaVentana[kv.Key]++;
                }
                tiempoVentana += Time.deltaTime;
                yield return null;
            }

            foreach (var kv in sumaVentana)
            {
                float promedioVentana = kv.Value / cuentaVentana[kv.Key];
                if (!acumulado.ContainsKey(kv.Key)) { acumulado[kv.Key] = 0f; conteoPorAngulo[kv.Key] = 0; }
                acumulado[kv.Key] += promedioVentana;
                conteoPorAngulo[kv.Key]++;
            }

            estado = $"Repeticion {rep}/{repeticiones} capturada.";
            yield return new WaitForSeconds(0.3f);
        }

        var promedioFinal = new Dictionary<string, float>();
        foreach (var kv in acumulado)
            promedioFinal[kv.Key] = kv.Value / conteoPorAngulo[kv.Key];

        if (promedioFinal.Count == 0)
        {
            estado = "No se pudo capturar ningun angulo (joints no detectados). No se guardo nada.";
            Debug.LogWarning($"[PoseRecorder] {estado}");
            grabando = false;
            yield break;
        }

        var template = PoseTemplate.DesdeDiccionario(nombrePose, promedioFinal, repeticiones);
        PoseLibrary.GuardarPose(template);

        estado = $"Pose '{nombrePose}' guardada: {promedioFinal.Count} angulos, promedio de {repeticiones} repeticiones.";
        Debug.Log($"[PoseRecorder] {estado}");
        grabando = false;
    }
}
