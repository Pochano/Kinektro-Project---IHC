using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Reconocedor de poses en tiempo real: cada cierto intervalo calcula los angulos
// actuales de la persona y los compara contra TODAS las poses guardadas por
// PoseRecorder, por vecino mas cercano (la plantilla con menor diferencia promedio
// de angulos gana). Si ni la mejor candidata entra dentro de la tolerancia, se
// considera que no hay ninguna pose reconocida.
public class PoseIdentifier : MonoBehaviour
{
    [Header("Fuente de datos")]
    public KinectSourceProvider sourceProvider;
    private IJointDataSource dataSource;

    [Header("Deteccion")]
    [Tooltip("Tolerancia en DESVIOS ESTANDAR (no en grados). Se calcula automaticamente cuanto varia cada angulo entre las variantes grabadas de esa pose (PoseMapperUI > AGREGAR COMO VARIANTE) y se compara en esa unidad. Valor tipico: 2.0 a 2.5. Mas bajo = mas estricto.")]
    public float toleranciaDesviaciones = 2.5f;
    [Tooltip("Cada cuanto se intenta identificar la pose. No hace falta que sea cada frame.")]
    public float intervaloSegundos = 0.3f;

    [Header("Eventos")]
    public UnityEvent<string> onPoseReconocida;
    public UnityEvent onNingunaPoseReconocida;

    private PoseLibraryData libreria;
    private float temporizador;
    private string ultimaPoseReconocida = null;

    // Para que otros scripts (ej. CrossPC_KinectBridge, al mostrar un prompt de pose
    // pedido por el pipeline) puedan consultar "que pose esta reconociendo ahora mismo
    // el jugador" sin tener que suscribirse a los UnityEvents.
    public string PoseActualReconocida => ultimaPoseReconocida;

    void Start()
    {
        if (sourceProvider != null) dataSource = sourceProvider.GetActiveSource();
        if (dataSource == null) Debug.LogError("[PoseIdentifier] No se pudo obtener una fuente de datos valida desde el SourceProvider.");

        libreria = PoseLibrary.Cargar();
        Debug.Log($"[PoseIdentifier] {libreria.poses.Count} poses cargadas desde la biblioteca.");
    }

    void Update()
    {
        temporizador += Time.deltaTime;
        if (temporizador < intervaloSegundos) return;
        temporizador = 0f;

        Identificar();
    }

    private void Identificar()
    {
        if (dataSource == null || libreria == null || libreria.poses.Count == 0) return;

        var angulosActuales = JointAngleCalculator.CalcularAngulos(dataSource);
        if (angulosActuales.Count == 0) return;
        angulosActuales = CalibracionJugador.Aplicar(angulosActuales);

        PoseMatching.EncontrarMasCercanaNormalizada(angulosActuales, libreria, out string mejorNombre, out float menorZScore);

        bool reconocida = mejorNombre != null && menorZScore <= toleranciaDesviaciones;
        string resultado = reconocida ? mejorNombre : null;

        if (resultado != ultimaPoseReconocida)
        {
            ultimaPoseReconocida = resultado;
            if (reconocida)
            {
                Debug.Log($"[PoseIdentifier] Pose reconocida: {resultado} ({menorZScore:F2} desvios estandar)");
                onPoseReconocida.Invoke(resultado);
            }
            else
            {
                Debug.Log($"[PoseIdentifier] Ninguna pose reconocida (mejor candidata: {mejorNombre}, {menorZScore:F2} desvios estandar)");
                onNingunaPoseReconocida.Invoke();
            }
        }
    }

    // Por si se grabaron poses nuevas con PoseRecorder durante la misma sesion de Play.
    public void RecargarLibreria()
    {
        libreria = PoseLibrary.Cargar();
        Debug.Log($"[PoseIdentifier] Biblioteca recargada: {libreria.poses.Count} poses.");
    }
}
