using UnityEngine;

// Lee los gestos del Kinect (mano derecha) y los manda como parametros FMOD
// hacia la cancion que DJ_Manager tiene sonando en este momento.
// No crea su propio EventInstance: reutiliza la que ya controla DJ_Manager,
// para no reproducir la musica dos veces a la vez.
public class KinectGestureFX : MonoBehaviour
{
    [Header("Fuente de datos (arrastra el mismo SourceProvider de siempre)")]
    public KinectSourceProvider sourceProvider;

    [Header("Referencia al DJ_Manager de la escena")]
    public DJ_Manager djManager;

    [Header("Margen extra sobre la cabeza para 'mano totalmente arriba' (metros)")]
    public float headMargin = 0.3f;

    [Header("Que tan rapido se suaviza el cambio del puno (segundos)")]
    public float fistSmoothTime = 0.2f;

    [Header("Debug")]
    public bool logDebugInfo = true;
    public int logEveryNFrames = 15;

    private float currentFistValue = 0f;
    private int frameCounter = 0;

    void Update()
    {
        if (sourceProvider == null || djManager == null) return;

        IJointDataSource source = sourceProvider.GetActiveSource();
        if (source == null) return;

        // Obtener las articulaciones del Kinect real
        bool gotHand = source.TryGetJointPosition("HandRight", out Vector3 handPos);
        bool gotLow = source.TryGetJointPosition("SpineBase", out Vector3 lowPos);
        bool gotHigh = source.TryGetJointPosition("Head", out Vector3 headPos);

        if (gotHand)
        {
            float normalizedHeight = 0.5f;

            // Si el Kinect envía la estructura completa del cuerpo
            if (gotLow && gotHigh)
            {
                float lowY = lowPos.y;
                float highY = headPos.y + headMargin;
                normalizedHeight = Mathf.Clamp01(Mathf.InverseLerp(lowY, highY, handPos.y));
            }
            else
            {
                // Fallback en caso de que solo reciba la posición Y absoluta de la mano
                normalizedHeight = Mathf.Clamp01(Mathf.InverseLerp(0.5f, 2.0f, handPos.y));
            }

            // 1. Modificar el parámetro de sonido en FMOD
            djManager.SetKinectParameter("HandHeight", normalizedHeight);

            // 2. Si el tutorial está en el paso de mover la mano (Paso 11 / KinectHandMove),
            // avanza en cuanto detecta que la mano está arriba o en movimiento real.
            if (TutorialManager.Instance != null && TutorialManager.Instance.currentStep == TutorialStep.KinectHandMove)
            {
                TutorialManager.Instance.OnKinectHandMoved();
            }
        }

        // Procesar puño cerrado
        string fistState = source.GetHandState("HandRight");
        float targetFistValue = (fistState == "Closed") ? 1f : 0f;

        currentFistValue = Mathf.MoveTowards(
            currentFistValue,
            targetFistValue,
            Time.deltaTime / Mathf.Max(fistSmoothTime, 0.001f)
        );

        djManager.SetKinectParameter("FistClosed", currentFistValue);
    }
}