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

        bool gotHand = source.TryGetJointPosition("HandRight", out Vector3 handPos);
        bool gotLow = source.TryGetJointPosition("SpineBase", out Vector3 lowPos);
        bool gotHigh = source.TryGetJointPosition("Head", out Vector3 headPos);

        float normalizedHeight = 0f;

        if (gotHand && gotLow && gotHigh)
        {
            // Altura relativa al propio cuerpo: SpineBase = "mano abajo", Head + margen = "mano arriba".
            float lowY = lowPos.y;
            float highY = headPos.y + headMargin;
            normalizedHeight = Mathf.Clamp01(Mathf.InverseLerp(lowY, highY, handPos.y));

            djManager.SetKinectParameter("HandHeight", normalizedHeight);
        }

        string fistState = source.GetHandState("HandRight");
        float targetFistValue = (fistState == "Closed") ? 1f : 0f;

        currentFistValue = Mathf.MoveTowards(
            currentFistValue,
            targetFistValue,
            Time.deltaTime / Mathf.Max(fistSmoothTime, 0.001f)
        );

        djManager.SetKinectParameter("FistClosed", currentFistValue);

        frameCounter++;
        if (logDebugInfo && frameCounter >= logEveryNFrames)
        {
            frameCounter = 0;
            Debug.Log(
                "KinectGestureFX | HandY: " + (gotHand ? handPos.y.ToString("F2") : "N/A") +
                " | Altura 0-1: " + normalizedHeight.ToString("F2") +
                " | Puno: " + fistState +
                " | FistValue: " + currentFistValue.ToString("F2")
            );
        }
    }
}
