using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

// Pantalla completa de mapeo de gestos para el Jugador B (tipo "Just Dance"): muestra
// el esqueleto en vivo, deja escribir un nombre y una cantidad de repeticiones, y con
// un boton graba esa pose promediando las repeticiones (igual que PoseRecorder, pero
// con una interfaz visual en vez de cajitas de texto encima del juego). Tambien
// muestra en vivo que pose esta reconociendo el sistema ahora mismo, para debuguear.
// Todo el Canvas se arma por codigo en Awake() - solo hace falta un GameObject vacio
// con este script y el Source Provider asignado, no hay que armar nada a mano.
public class PoseMapperUI : MonoBehaviour
{
    [Header("Fuente de datos")]
    public KinectSourceProvider sourceProvider;
    private IJointDataSource dataSource;

    [Header("Grabado")]
    [Tooltip("Cuenta regresiva antes de capturar cada repeticion, en segundos.")]
    public float cuentaRegresivaSegundos = 3f;
    [Tooltip("Cuanto tiempo se promedian los angulos DENTRO de una repeticion, para filtrar temblor de un solo frame.")]
    public float ventanaCapturaSegundos = 0.5f;

    [Header("Reconocimiento en vivo (para debuguear)")]
    [Tooltip("Tolerancia en DESVIOS ESTANDAR (no en grados). Ver PoseIdentifier.toleranciaDesviaciones para la explicacion completa. Valor tipico: 2.0 a 2.5.")]
    public float toleranciaDesviaciones = 2.5f;
    public float intervaloIdentificacionSegundos = 0.3f;

    [Header("Calibracion por jugador")]
    [Tooltip("Nombre de la pose guardada a usar como referencia 'neutral' al calibrar a cada jugador nuevo.")]
    public string nombrePoseCalibNeutral = "neutro";
    [Tooltip("Nombre de la pose guardada a usar como referencia 'extrema' (bien distinta de la neutral) al calibrar a cada jugador nuevo.")]
    public string nombrePoseCalibExtrema = "BrazosArriba";

    // --- UI (armada en Awake) ---
    private InputField campoNombre;
    private InputField campoRepeticiones;
    private Text textoEstado;
    private Text textoEstadoGrande;
    private Text textoPoseActual;
    private Text textoDebugDetallado;
    private Text textoListaPoses;
    private Button botonGrabar;
    private Button botonEliminar;
    private Button botonAgregarVariante;
    private Button botonCalibrar;
    private Dropdown comboPoses;
    private SkeletonOverlay2D esqueleto;

    private GameObject canvasMapeoGO;

    private PoseLibraryData libreria;
    private bool grabando = false;
    private bool actualizandoCombo = false;
    private float temporizadorIdentificacion;
    private string ultimaPoseMostrada = null;

    void Awake()
    {
        ConstruirUI();
    }

    void Start()
    {
        if (sourceProvider != null) dataSource = sourceProvider.GetActiveSource();
        if (dataSource == null) Debug.LogError("[PoseMapperUI] No se pudo obtener una fuente de datos valida desde el SourceProvider.");

        esqueleto.SetDataSource(dataSource);
        RecargarLibreria();
    }

    void Update()
    {
        // Tecla M: muestra/oculta esta pantalla de mapeo/calibracion a mano, por si
        // necesitas volver a calibrar a otra persona sin reiniciar el juego. Se puede
        // usar aunque este oculta (no depende del resto del metodo).
        if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame && canvasMapeoGO != null)
        {
            canvasMapeoGO.SetActive(!canvasMapeoGO.activeSelf);
        }

        if (grabando || dataSource == null) return;

        temporizadorIdentificacion += Time.deltaTime;
        if (temporizadorIdentificacion < intervaloIdentificacionSegundos) return;
        temporizadorIdentificacion = 0f;

        var angulosActuales = JointAngleCalculator.CalcularAngulos(dataSource);
        if (angulosActuales.Count == 0) return;
        angulosActuales = CalibracionJugador.Aplicar(angulosActuales);

        // Calculo detallado contra TODAS las poses (no solo la ganadora), para poder ver
        // en pantalla cual angulo en particular sacó a cada pose fuera de tolerancia.
        var perfiles = PoseMatching.ConstruirPerfiles(libreria);
        string mejorNombre = null;
        float menorZScore = float.MaxValue;
        var sbDebug = new StringBuilder();
        foreach (var perfil in perfiles)
        {
            float z = PoseMatching.DistanciaNormalizadaMaximaDetallada(angulosActuales, perfil, out string peorAngulo);
            sbDebug.AppendLine($"{perfil.nombre}: {z:F2} desv.est.  (peor angulo: {peorAngulo})");
            if (z < menorZScore)
            {
                menorZScore = z;
                mejorNombre = perfil.nombre;
            }
        }
        if (textoDebugDetallado != null) textoDebugDetallado.text = sbDebug.ToString();

        // Mismo detalle pero a la Console - asi no haces falta sacar captura de pantalla
        // mientras estas parado haciendo la pose con las manos ocupadas. Despues de probar,
        // pausa el Play y buscá en la Console las lineas que empiezan con [PoseMapperUI][DEBUG].
        Debug.Log($"[PoseMapperUI][DEBUG] t={Time.time:F1}\n{sbDebug}");

        bool reconocida = mejorNombre != null && menorZScore <= toleranciaDesviaciones;
        string resultado = reconocida ? mejorNombre : null;

        if (resultado != ultimaPoseMostrada)
        {
            ultimaPoseMostrada = resultado;
            textoPoseActual.text = reconocida
                ? $"Pose detectada: {mejorNombre}  ({menorZScore:F2} desv.est.)"
                : (mejorNombre != null ? $"Pose detectada: ninguna  (mas cercana: {mejorNombre}, {menorZScore:F2} desv.est.)" : "Pose detectada: ninguna");
        }
    }

    private void RecargarLibreria()
    {
        libreria = PoseLibrary.Cargar();
        ActualizarListaPoses();
    }

    // Actualiza el texto chico (detallado, en el panel de controles) Y el texto
    // gigante (pensado para leerse desde lejos mientras uno mismo graba la pose,
    // sin estar parado frente al monitor). El color del texto grande cambia segun
    // el momento (amarillo = cuenta regresiva, rojo = capturando/error, verde = listo).
    private void SetEstado(string mensaje, Color colorGrande)
    {
        textoEstado.text = mensaje;
        textoEstadoGrande.text = mensaje;
        textoEstadoGrande.color = colorGrande;
    }

    private void ActualizarListaPoses()
    {
        if (libreria.poses.Count == 0)
        {
            textoListaPoses.text = "Poses guardadas: (ninguna todavia)";
        }
        else
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Poses guardadas ({libreria.poses.Count}):");
            foreach (var p in libreria.poses)
                sb.AppendLine($" - {p.nombrePose}  ({p.angulos.Count} angulos, promedio de {p.repeticionesUsadas} repeticiones)");
            textoListaPoses.text = sb.ToString();
        }
        ActualizarComboPoses();
    }

    // Llena el combobox con "-- nueva pose --" + el nombre de cada pose ya guardada.
    // Se llama cada vez que se recarga la biblioteca (al iniciar, y despues de grabar o eliminar),
    // asi el combo siempre refleja lo que hay en poses.json.
    private void ActualizarComboPoses()
    {
        if (comboPoses == null) return;
        actualizandoCombo = true;
        comboPoses.ClearOptions();
        var opciones = new List<string> { "-- nueva pose --" };
        foreach (var p in libreria.poses) opciones.Add(p.nombrePose);
        comboPoses.AddOptions(opciones);
        comboPoses.value = 0;
        comboPoses.RefreshShownValue();
        actualizandoCombo = false;
    }

    // Al elegir una pose existente del combobox, autocompleta el campo de nombre para
    // poder sobreescribirla (apretando GRABAR POSE de nuevo, con el mismo nombre) o
    // borrarla (apretando ELIMINAR POSE), sin tener que tipear el nombre a mano.
    private void OnComboPosesChanged(int indice)
    {
        if (actualizandoCombo) return;
        if (indice <= 0) return; // "-- nueva pose --": se deja que el usuario escriba un nombre nuevo
        int i = indice - 1;
        if (i >= 0 && i < libreria.poses.Count)
            campoNombre.text = libreria.poses[i].nombrePose;
    }

    private void OnClickGrabar()
    {
        string nombre = campoNombre.text.Trim();
        if (string.IsNullOrEmpty(nombre) || !int.TryParse(campoRepeticiones.text, out int reps) || reps <= 0)
        {
            SetEstado("Nombre invalido o cantidad de repeticiones invalida.", new Color(1f, 0.3f, 0.3f));
            return;
        }
        if (dataSource == null)
        {
            SetEstado("No hay fuente de datos (Source Provider) asignada.", new Color(1f, 0.3f, 0.3f));
            return;
        }
        StartCoroutine(GrabarPoseRoutine(nombre, reps, agregarComoVariante: false));
    }

    // Igual que GRABAR POSE, pero no borra las muestras anteriores con el mismo nombre:
    // las suma como una variante mas. Usa esto cuando quieras que la misma pose la
    // reconozca mas de una persona (cada una grabando su propia version de "Brazos
    // arriba", por ejemplo) en vez de pisar la unica muestra que ya existia.
    private void OnClickAgregarVariante()
    {
        string nombre = campoNombre.text.Trim();
        if (string.IsNullOrEmpty(nombre) || !int.TryParse(campoRepeticiones.text, out int reps) || reps <= 0)
        {
            SetEstado("Nombre invalido o cantidad de repeticiones invalida.", new Color(1f, 0.3f, 0.3f));
            return;
        }
        if (dataSource == null)
        {
            SetEstado("No hay fuente de datos (Source Provider) asignada.", new Color(1f, 0.3f, 0.3f));
            return;
        }
        StartCoroutine(GrabarPoseRoutine(nombre, reps, agregarComoVariante: true));
    }

    // Borra la pose cuyo nombre esta escrito en el mismo campo de texto que se usa
    // para grabar. Asi no hace falta otra UI aparte: escribis el nombre y elegis
    // GRABAR (para crear/sobreescribir) o ELIMINAR (para borrarla).
    private void OnClickEliminar()
    {
        string nombre = campoNombre.text.Trim();
        if (string.IsNullOrEmpty(nombre))
        {
            SetEstado("Escribi el nombre de la pose que queres eliminar.", new Color(1f, 0.3f, 0.3f));
            return;
        }
        if (grabando)
        {
            SetEstado("Espera a que termine de grabar antes de eliminar.", new Color(1f, 0.3f, 0.3f));
            return;
        }

        bool borrada = PoseLibrary.EliminarPose(nombre);
        if (borrada)
        {
            SetEstado($"Pose '{nombre}' ELIMINADA.", new Color(1f, 0.6f, 0.2f));
            RecargarLibreria();
        }
        else
        {
            SetEstado($"No existe ninguna pose llamada '{nombre}'.", new Color(1f, 0.3f, 0.3f));
        }
    }

    private IEnumerator GrabarPoseRoutine(string nombrePose, int repeticiones, bool agregarComoVariante)
    {
        grabando = true;
        botonGrabar.interactable = false;
        botonAgregarVariante.interactable = false;

        var acumulado = new Dictionary<string, float>();
        var conteoPorAngulo = new Dictionary<string, int>();

        for (int rep = 1; rep <= repeticiones; rep++)
        {
            float restante = cuentaRegresivaSegundos;
            while (restante > 0f)
            {
                SetEstado($"Repeticion {rep}/{repeticiones} - mantene la pose\n{Mathf.CeilToInt(restante)}", new Color(1f, 0.82f, 0.15f));
                restante -= Time.deltaTime;
                yield return null;
            }

            SetEstado($"Repeticion {rep}/{repeticiones}\nCAPTURANDO - NO TE MUEVAS", new Color(1f, 0.25f, 0.2f));
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

            SetEstado($"Repeticion {rep}/{repeticiones} capturada", new Color(0.4f, 1f, 0.5f));
            yield return new WaitForSeconds(0.3f);
        }

        var promedioFinal = new Dictionary<string, float>();
        foreach (var kv in acumulado)
            promedioFinal[kv.Key] = kv.Value / conteoPorAngulo[kv.Key];

        if (promedioFinal.Count == 0)
        {
            SetEstado("No se pudo capturar ningun angulo. No se guardo nada.", new Color(1f, 0.3f, 0.3f));
        }
        else
        {
            var template = PoseTemplate.DesdeDiccionario(nombrePose, promedioFinal, repeticiones);
            if (agregarComoVariante)
            {
                PoseLibrary.AgregarMuestra(template);
                SetEstado($"¡Variante NUEVA de '{nombrePose}' agregada! (las anteriores se mantienen)", new Color(0.3f, 1f, 0.45f));
            }
            else
            {
                PoseLibrary.GuardarPose(template);
                SetEstado($"¡Pose '{nombrePose}' GUARDADA!", new Color(0.3f, 1f, 0.45f));
            }
            RecargarLibreria();
        }

        botonGrabar.interactable = true;
        botonAgregarVariante.interactable = true;
        grabando = false;
    }

    // Calibra a la persona que esta parada frente al sensor AHORA MISMO: le pide
    // mantener la pose neutral y despues la extrema (configurables arriba, por defecto
    // "neutro" y "BrazosArriba") y con esos dos puntos ajusta sus lecturas en vivo para
    // que se parezcan mas a las plantillas ya grabadas - sin que haga falta que ESTA
    // persona en particular haya grabado sus propias variantes antes de jugar.
    private void OnClickCalibrar()
    {
        if (grabando)
        {
            SetEstado("Espera a que termine la operacion actual antes de calibrar.", new Color(1f, 0.3f, 0.3f));
            return;
        }
        StartCoroutine(CalibrarJugadorRoutine());
    }

    private IEnumerator CalibrarJugadorRoutine()
    {
        if (dataSource == null)
        {
            SetEstado("No hay fuente de datos (Source Provider) asignada.", new Color(1f, 0.3f, 0.3f));
            yield break;
        }

        var perfiles = PoseMatching.ConstruirPerfiles(libreria);
        PoseMatching.PerfilPose perfilNeutral = null;
        PoseMatching.PerfilPose perfilExtremo = null;
        foreach (var p in perfiles)
        {
            if (p.nombre == nombrePoseCalibNeutral) perfilNeutral = p;
            if (p.nombre == nombrePoseCalibExtrema) perfilExtremo = p;
        }

        if (perfilNeutral == null || perfilExtremo == null)
        {
            SetEstado($"Para calibrar necesito que existan las poses '{nombrePoseCalibNeutral}' y '{nombrePoseCalibExtrema}' ya grabadas.", new Color(1f, 0.3f, 0.3f));
            yield break;
        }

        grabando = true;
        botonGrabar.interactable = false;
        botonAgregarVariante.interactable = false;
        botonCalibrar.interactable = false;

        var personaNeutral = new Dictionary<string, float>();
        var personaExtremo = new Dictionary<string, float>();

        yield return StartCoroutine(CapturarAngulosPromedio(personaNeutral, nombrePoseCalibNeutral));
        yield return new WaitForSeconds(0.6f);
        yield return StartCoroutine(CapturarAngulosPromedio(personaExtremo, nombrePoseCalibExtrema));

        CalibracionJugador.Calibrar(personaNeutral, personaExtremo, perfilNeutral.promedios, perfilExtremo.promedios);
        SetEstado("¡Calibracion lista! Pasando a la pantalla de juego...", new Color(0.3f, 1f, 0.45f));

        botonGrabar.interactable = true;
        botonAgregarVariante.interactable = true;
        botonCalibrar.interactable = true;
        grabando = false;

        // Terminada la calibracion, esta pantalla de mapeo/debug ya cumplio su proposito -
        // se oculta para dejar ver limpio el prompt de poses del pipeline (el Canvas que
        // arma CrossPC_KinectBridge). Se puede volver a traer con la tecla M.
        yield return new WaitForSeconds(1.5f);
        if (canvasMapeoGO != null) canvasMapeoGO.SetActive(false);
    }

    // Cuenta regresiva + ventana de captura, igual que GrabarPoseRoutine pero para UNA
    // sola muestra (sin repeticiones) y sin guardar nada en la biblioteca - solo llena
    // "destino" con el promedio de angulos capturado, para usarlo como referencia de
    // calibracion.
    private IEnumerator CapturarAngulosPromedio(Dictionary<string, float> destino, string etiquetaPose)
    {
        destino.Clear();

        float restante = cuentaRegresivaSegundos;
        while (restante > 0f)
        {
            SetEstado($"Calibracion: haz la pose '{etiquetaPose}'\n{Mathf.CeilToInt(restante)}", new Color(1f, 0.82f, 0.15f));
            restante -= Time.deltaTime;
            yield return null;
        }

        SetEstado($"Calibracion: CAPTURANDO '{etiquetaPose}' - NO TE MUEVAS", new Color(1f, 0.25f, 0.2f));
        var suma = new Dictionary<string, float>();
        var cuenta = new Dictionary<string, int>();
        float tiempoVentana = 0f;

        while (tiempoVentana < ventanaCapturaSegundos)
        {
            var angulos = JointAngleCalculator.CalcularAngulos(dataSource);
            foreach (var kv in angulos)
            {
                if (!suma.ContainsKey(kv.Key)) { suma[kv.Key] = 0f; cuenta[kv.Key] = 0; }
                suma[kv.Key] += kv.Value;
                cuenta[kv.Key]++;
            }
            tiempoVentana += Time.deltaTime;
            yield return null;
        }

        foreach (var kv in suma)
            destino[kv.Key] = kv.Value / cuenta[kv.Key];
    }

    // ---------------------------------------------------------------
    // Construccion de la UI por codigo
    // ---------------------------------------------------------------

    private void ConstruirUI()
    {
        if (FindObjectOfType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        var canvasGO = new GameObject("PoseMapperCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        canvasMapeoGO = canvasGO;
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform canvasRT = canvasGO.GetComponent<RectTransform>();

        // Fondo general
        CrearPanel(canvasRT, "Fondo", new Color(0.06f, 0.07f, 0.09f, 1f), Ancla(0, 0, 1, 1, 0, 0));

        CrearTexto(canvasRT, "Titulo", "MAPEO DE GESTOS - JUGADOR B", 26, FontStyle.Bold, TextAnchor.MiddleLeft,
            AnclaTopLeft(40, -18, 1200, 36), new Color(0.6f, 0.65f, 0.75f));

        // --- Banner gigante de estado: pensado para leerse desde lejos mientras uno
        // mismo esta grabando la pose y no puede acercarse al monitor a leer letra chica.
        textoEstadoGrande = CrearTexto(canvasRT, "EstadoGrande", "Listo para grabar", 68, FontStyle.Bold, TextAnchor.MiddleCenter,
            AnclaTopLeft(40, -64, 1840, 210), new Color(0.85f, 0.85f, 0.9f));

        // --- Panel del esqueleto (izquierda) ---
        var panelEsqueleto = CrearPanel(canvasRT, "PanelEsqueleto", new Color(0.1f, 0.11f, 0.14f, 1f), AnclaTopLeft(40, -300, 760, 600));
        esqueleto = panelEsqueleto.gameObject.AddComponent<SkeletonOverlay2D>();
        // El overlay dibuja sus joints/huesos centrados en el panel (pivot 0.5,0.5 del propio panel).
        esqueleto.Inicializar(panelEsqueleto);

        textoPoseActual = CrearTexto(canvasRT, "PoseActual", "Pose detectada: ninguna", 24, FontStyle.Bold, TextAnchor.MiddleLeft,
            AnclaTopLeft(40, -910, 760, 40), new Color(0.4f, 1f, 0.6f));

        // Debug temporal: muestra la distancia (en desvios estandar) contra TODAS las
        // poses a la vez, y cual angulo en particular es el que mas se aleja en cada una -
        // para diagnosticar a mano por que una pose no matchea aunque "deberia".
        textoDebugDetallado = CrearTexto(canvasRT, "DebugDetallado", "", 15, FontStyle.Normal, TextAnchor.UpperLeft,
            AnclaTopLeft(40, -960, 1840, 110), new Color(1f, 0.85f, 0.3f));

        // --- Panel de controles (derecha) ---
        var panelControles = CrearPanel(canvasRT, "PanelControles", new Color(0.1f, 0.11f, 0.14f, 1f), AnclaTopLeft(840, -300, 1040, 440));

        CrearTexto(panelControles, "LabelNombre", "Nombre de la pose:", 20, FontStyle.Normal, TextAnchor.MiddleLeft,
            AnclaTopLeft(24, -24, 300, 32), Color.white);
        campoNombre = CrearInputField(panelControles, "CampoNombre", "ej: BrazosArriba", AnclaTopLeft(330, -24, 650, 36));

        // Combobox con las poses ya guardadas: elegir una autocompleta el nombre de arriba,
        // para poder sobreescribirla (GRABAR POSE) o borrarla (ELIMINAR POSE) sin tipear.
        CrearTexto(panelControles, "LabelCombo", "Pose existente:", 20, FontStyle.Normal, TextAnchor.MiddleLeft,
            AnclaTopLeft(24, -72, 300, 32), Color.white);
        comboPoses = CrearDropdown(panelControles, "ComboPoses", AnclaTopLeft(330, -72, 650, 36));
        comboPoses.onValueChanged.AddListener(OnComboPosesChanged);

        CrearTexto(panelControles, "LabelReps", "Repeticiones:", 20, FontStyle.Normal, TextAnchor.MiddleLeft,
            AnclaTopLeft(24, -120, 300, 32), Color.white);
        campoRepeticiones = CrearInputField(panelControles, "CampoReps", "5", AnclaTopLeft(330, -120, 150, 36));
        campoRepeticiones.text = "5";
        campoRepeticiones.contentType = InputField.ContentType.IntegerNumber;

        botonGrabar = CrearBoton(panelControles, "BotonGrabar", "GRABAR POSE", AnclaTopLeft(24, -172, 620, 52), new Color(0.18f, 0.55f, 0.95f));
        botonGrabar.onClick.AddListener(OnClickGrabar);

        botonEliminar = CrearBoton(panelControles, "BotonEliminar", "ELIMINAR POSE", AnclaTopLeft(660, -172, 356, 52), new Color(0.75f, 0.25f, 0.2f));
        botonEliminar.onClick.AddListener(OnClickEliminar);

        textoEstado = CrearTexto(panelControles, "Estado", "Listo para grabar.", 18, FontStyle.Italic, TextAnchor.UpperLeft,
            AnclaTopLeft(24, -240, 992, 80), new Color(0.85f, 0.85f, 0.9f));

        textoListaPoses = CrearTexto(panelControles, "ListaPoses", "Poses guardadas: (ninguna todavia)", 17, FontStyle.Normal, TextAnchor.UpperLeft,
            AnclaTopLeft(24, -328, 992, 100), new Color(0.75f, 0.8f, 0.9f));

        // Boton extra: agrega esta grabacion como UNA MUESTRA MAS de la misma pose, sin
        // borrar las que ya existian con ese nombre (a diferencia de GRABAR POSE, que
        // reemplaza). Grabar la misma pose varias veces con personas de distinto tamano
        // y contextura mejora el reconocimiento para todos, sin tocar el algoritmo.
        botonAgregarVariante = CrearBoton(panelControles, "BotonAgregarVariante", "AGREGAR COMO VARIANTE (no reemplaza)", AnclaTopLeft(24, -442, 992, 52), new Color(0.2f, 0.65f, 0.55f));
        botonAgregarVariante.onClick.AddListener(OnClickAgregarVariante);

        CrearTexto(panelControles, "NotaVariante", "Tip: graba la misma pose con distintas personas usando este boton para que el reconocimiento funcione para todos, no solo para quien grabo primero.",
            14, FontStyle.Italic, TextAnchor.UpperLeft, AnclaTopLeft(24, -500, 992, 60), new Color(0.6f, 0.75f, 0.7f));

        // Boton de calibracion rapida por jugador: antes de que una persona nueva juegue,
        // le hace mantener la pose neutral y despues la extrema, y ajusta sus lecturas en
        // vivo para que se parezcan mas a las plantillas ya grabadas (ver CalibracionJugador.cs).
        botonCalibrar = CrearBoton(panelControles, "BotonCalibrar", "CALIBRAR JUGADOR NUEVO", AnclaTopLeft(24, -580, 992, 52), new Color(0.6f, 0.35f, 0.85f));
        botonCalibrar.onClick.AddListener(OnClickCalibrar);

        CrearTexto(panelControles, "NotaCalibrar", "Antes de que juegue una persona nueva: apreta esto, mantene la pose neutral cuando se te indique, y despues la pose de brazos arriba. Ajusta sus lecturas para que calcen mejor con las plantillas ya grabadas por otra persona.",
            14, FontStyle.Italic, TextAnchor.UpperLeft, AnclaTopLeft(24, -638, 992, 60), new Color(0.75f, 0.65f, 0.85f));

        // --- Nota inferior ---
        CrearTexto(canvasRT, "Nota", "F1 = mano derecha arriba | F2 = puno derecho | F3 = mano izquierda arriba | F4 = puno izquierdo (solo con Use Mock activado)",
            15, FontStyle.Italic, TextAnchor.MiddleLeft, AnclaTopLeft(840, -760, 1040, 40), new Color(0.5f, 0.55f, 0.6f));
    }

    private RectTransform CrearPanel(RectTransform padre, string nombre, Color color, (Vector2 min, Vector2 max, Vector2 pos, Vector2 size) layout)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(padre, false);
        go.GetComponent<Image>().color = color;
        AplicarLayout(go.GetComponent<RectTransform>(), layout);
        return go.GetComponent<RectTransform>();
    }

    private Text CrearTexto(RectTransform padre, string nombre, string contenido, int tamano, FontStyle estilo, TextAnchor alineacion,
        (Vector2 min, Vector2 max, Vector2 pos, Vector2 size) layout, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(padre, false);
        var txt = go.GetComponent<Text>();
        txt.text = contenido;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = tamano;
        txt.fontStyle = estilo;
        txt.alignment = alineacion;
        txt.color = color;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        AplicarLayout(go.GetComponent<RectTransform>(), layout);
        return txt;
    }

    private InputField CrearInputField(RectTransform padre, string nombre, string placeholder, (Vector2 min, Vector2 max, Vector2 pos, Vector2 size) layout)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(padre, false);
        go.GetComponent<Image>().color = new Color(0.18f, 0.19f, 0.23f);
        AplicarLayout(go.GetComponent<RectTransform>(), layout);

        var textoGO = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textoGO.transform.SetParent(go.transform, false);
        var txt = textoGO.GetComponent<Text>();
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize = 20;
        txt.color = Color.white;
        txt.supportRichText = false;
        var txtRT = textoGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(10, 4); txtRT.offsetMax = new Vector2(-10, -4);

        var placeholderGO = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        placeholderGO.transform.SetParent(go.transform, false);
        var ph = placeholderGO.GetComponent<Text>();
        ph.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        ph.fontSize = 20;
        ph.fontStyle = FontStyle.Italic;
        ph.color = new Color(1, 1, 1, 0.4f);
        ph.text = placeholder;
        var phRT = placeholderGO.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = new Vector2(10, 4); phRT.offsetMax = new Vector2(-10, -4);

        var field = go.GetComponent<InputField>();
        field.textComponent = txt;
        field.placeholder = ph;
        return field;
    }

    private Button CrearBoton(RectTransform padre, string nombre, string texto, (Vector2 min, Vector2 max, Vector2 pos, Vector2 size) layout, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);
        go.GetComponent<Image>().color = color;
        AplicarLayout(go.GetComponent<RectTransform>(), layout);

        CrearTexto(go.GetComponent<RectTransform>(), "Text", texto, 22, FontStyle.Bold, TextAnchor.MiddleCenter,
            (Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), Color.white);

        return go.GetComponent<Button>();
    }

    // Combobox (Dropdown nativo de Unity UI) armado a mano por codigo: necesita su propio
    // "Template" (la lista que se despliega) con un Viewport+Content+Item, siguiendo la
    // misma estructura que usa Unity cuando se crea un Dropdown desde el editor.
    private Dropdown CrearDropdown(RectTransform padre, string nombre, (Vector2 min, Vector2 max, Vector2 pos, Vector2 size) layout)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(Image), typeof(Dropdown));
        go.transform.SetParent(padre, false);
        var bg = go.GetComponent<Image>();
        bg.color = new Color(0.18f, 0.19f, 0.23f, 1f);
        AplicarLayout(go.GetComponent<RectTransform>(), layout);
        var dropdown = go.GetComponent<Dropdown>();
        dropdown.targetGraphic = bg;

        // Texto visible cuando el combobox esta cerrado (la opcion elegida).
        var labelGO = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGO.transform.SetParent(go.transform, false);
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(10f, 4f);
        labelRT.offsetMax = new Vector2(-10f, -4f);
        var labelText = labelGO.GetComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 18;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleLeft;
        dropdown.captionText = labelText;

        // Template: la lista desplegable. Arranca desactivada; Unity la activa sola al abrir el combo.
        var templateGO = new GameObject("Template", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        templateGO.transform.SetParent(go.transform, false);
        var templateRT = templateGO.GetComponent<RectTransform>();
        templateRT.anchorMin = new Vector2(0f, 0f);
        templateRT.anchorMax = new Vector2(1f, 0f);
        templateRT.pivot = new Vector2(0.5f, 1f);
        templateRT.anchoredPosition = new Vector2(0f, 2f);
        templateRT.sizeDelta = new Vector2(0f, 220f);
        templateGO.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.14f, 1f);

        var viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
        viewportGO.transform.SetParent(templateGO.transform, false);
        var viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin = Vector2.zero;
        viewportRT.anchorMax = Vector2.one;
        viewportRT.sizeDelta = Vector2.zero;
        viewportRT.pivot = new Vector2(0f, 1f);
        viewportGO.GetComponent<Image>().color = Color.white;
        viewportGO.GetComponent<Mask>().showMaskGraphic = false;

        var contentGO = new GameObject("Content", typeof(RectTransform));
        contentGO.transform.SetParent(viewportGO.transform, false);
        var contentRT = contentGO.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0f, 1f);
        contentRT.anchorMax = new Vector2(1f, 1f);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = new Vector2(0f, 30f);

        var itemGO = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
        itemGO.transform.SetParent(contentGO.transform, false);
        var itemRT = itemGO.GetComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0f, 0.5f);
        itemRT.anchorMax = new Vector2(1f, 0.5f);
        itemRT.sizeDelta = new Vector2(0f, 30f);

        var itemBgGO = new GameObject("Item Background", typeof(RectTransform), typeof(Image));
        itemBgGO.transform.SetParent(itemGO.transform, false);
        var itemBgRT = itemBgGO.GetComponent<RectTransform>();
        itemBgRT.anchorMin = Vector2.zero;
        itemBgRT.anchorMax = Vector2.one;
        itemBgRT.sizeDelta = Vector2.zero;
        itemBgGO.GetComponent<Image>().color = new Color(0.25f, 0.5f, 0.9f, 0.4f);

        var itemCheckGO = new GameObject("Item Checkmark", typeof(RectTransform), typeof(Image));
        itemCheckGO.transform.SetParent(itemGO.transform, false);
        var itemCheckRT = itemCheckGO.GetComponent<RectTransform>();
        itemCheckRT.anchorMin = new Vector2(0f, 0.5f);
        itemCheckRT.anchorMax = new Vector2(0f, 0.5f);
        itemCheckRT.sizeDelta = new Vector2(16f, 16f);
        itemCheckRT.anchoredPosition = new Vector2(12f, 0f);
        itemCheckGO.GetComponent<Image>().color = new Color(0.3f, 1f, 0.5f);

        var itemLabelGO = new GameObject("Item Label", typeof(RectTransform), typeof(Text));
        itemLabelGO.transform.SetParent(itemGO.transform, false);
        var itemLabelRT = itemLabelGO.GetComponent<RectTransform>();
        itemLabelRT.anchorMin = Vector2.zero;
        itemLabelRT.anchorMax = Vector2.one;
        itemLabelRT.offsetMin = new Vector2(28f, 1f);
        itemLabelRT.offsetMax = new Vector2(-10f, -1f);
        var itemLabelText = itemLabelGO.GetComponent<Text>();
        itemLabelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        itemLabelText.fontSize = 17;
        itemLabelText.color = Color.white;
        itemLabelText.alignment = TextAnchor.MiddleLeft;

        var toggle = itemGO.GetComponent<Toggle>();
        toggle.targetGraphic = itemBgGO.GetComponent<Image>();
        toggle.graphic = itemCheckGO.GetComponent<Image>();
        toggle.isOn = true;

        var scrollRect = templateGO.GetComponent<ScrollRect>();
        scrollRect.content = contentRT;
        scrollRect.viewport = viewportRT;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        dropdown.template = templateRT;
        dropdown.itemText = itemLabelText;
        dropdown.itemImage = itemCheckGO.GetComponent<Image>();

        templateGO.SetActive(false);

        return dropdown;
    }

    // Layout "ancla desde la esquina superior izquierda" con offsets en pixeles de referencia (1920x1080).
    private (Vector2, Vector2, Vector2, Vector2) AnclaTopLeft(float x, float y, float w, float h)
    {
        return (new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, y), new Vector2(w, h));
    }

    // Layout "estira para llenar todo el padre" (usado para el fondo general).
    private (Vector2, Vector2, Vector2, Vector2) Ancla(float minX, float minY, float maxX, float maxY, float x, float y)
    {
        return (new Vector2(minX, minY), new Vector2(maxX, maxY), new Vector2(x, y), Vector2.zero);
    }

    private void AplicarLayout(RectTransform rt, (Vector2 min, Vector2 max, Vector2 pos, Vector2 size) layout)
    {
        rt.anchorMin = layout.min;
        rt.anchorMax = layout.max;
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = layout.pos;
        if (layout.min == layout.max || (layout.min != Vector2.zero || layout.max != Vector2.one))
        {
            if (layout.size != Vector2.zero) rt.sizeDelta = layout.size;
        }
        if (layout.min == Vector2.zero && layout.max == Vector2.one)
        {
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
