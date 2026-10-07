using UnityEngine;
using FMODUnity;

public class GestureAudioController : MonoBehaviour
{
    [Header("Referencia al Manager de Canciones")]
    [Tooltip("Arrastra aquí el GameObject que tiene el script DJ_Manager")]
    public DJ_Manager djManager;

    [Header("Referencia a la Perilla de Volumen 3D")]
    [Tooltip("Arrastra la perilla 3D de volumen (VR_Knob_Joystick) aquí")]
    public VR_Knob_Joystick volumeKnob;

    [Header("Referencias de Red y Esqueleto")]
    public KinectSourceProvider sourceProvider;

    [Header("Configuración de Parámetros de FMOD")]
    [Tooltip("Nombre exacto del parámetro de volumen en FMOD Studio")]
    public string fmodVolumeParam = "Volume";

    [Tooltip("Nombre exacto del parámetro de Reverb en FMOD Studio")]
    public string fmodReverbParam = "ReverbWet";

    [Header("Rangos de Altura para Volumen")]
    [Tooltip("Altura mínima de la mano (m) -> Volumen 0.0 (Silencio)")]
    public float minY = 0.8f;

    [Tooltip("Altura máxima de la mano (m) -> Volumen 1.0 (Máximo)")]
    public float maxY = 1.8f;

    [Header("Ajustes de Reverb (Puño)")]
    public float reverbWetClosed = 0.0f;    // En dB (0 dB = Efecto máximo)
    public float reverbWetOpen = -10000.0f; // En dB (-10000 dB = Sin efecto)

    private float currentVolume = 0.5f;
    private float currentReverbWet = -10000.0f;

    void Update()
    {
        if (sourceProvider == null) return;

        IJointDataSource source = sourceProvider.GetActiveSource();
        if (source == null) return;

        // Intentamos obtener la mano derecha y la referencia de la cintura/cabeza
        if (source.TryGetJointPosition("HandRight", out Vector3 handRightPos))
        {
            float targetVolume = 0.5f;

            // Si tenemos la cadera (SpineBase) y la cabeza (Head), calculamos la altura relativa al cuerpo
            if (source.TryGetJointPosition("SpineBase", out Vector3 spineBasePos) &&
                source.TryGetJointPosition("Head", out Vector3 headPos))
            {
                // 0.0 = Mano a la altura de la cintura | 1.0 = Mano a la altura de la cabeza
                targetVolume = Mathf.InverseLerp(spineBasePos.y, headPos.y, handRightPos.y);
            }
            else
            {
                // Si solo tenemos hombro y mano, usamos minY y maxY
                targetVolume = Mathf.InverseLerp(minY, maxY, handRightPos.y);
            }

            currentVolume = Mathf.Clamp01(targetVolume);

            // 1. Enviar el nivel de volumen al reproductor de FMOD (DJ_Manager)
            if (djManager != null)
            {
                djManager.SetVolume(currentVolume);
            }

            // 2. Girar la perilla física 3D respetando sus ángulos
            if (volumeKnob != null)
            {
                volumeKnob.SetValue(currentVolume);
            }

            // 3. Actualizar parámetro global en FMOD
            if (!string.IsNullOrEmpty(fmodVolumeParam))
            {
                RuntimeManager.StudioSystem.setParameterByName(fmodVolumeParam, currentVolume);
            }

            // Control de Reverb según puño abierto/cerrado
            string rightHandState = source.GetHandState("HandRight");
            bool isFistClosed = (rightHandState == "Closed");

            currentReverbWet = isFistClosed ? reverbWetClosed : reverbWetOpen;

            if (!string.IsNullOrEmpty(fmodReverbParam))
            {
                RuntimeManager.StudioSystem.setParameterByName(fmodReverbParam, currentReverbWet);
            }

            Debug.Log($"GestureAudio (FMOD) | ManoY: {handRightPos.y:F2}m | Volumen: {currentVolume:F2} ({(currentVolume * 100):F0}%) | Puño: {rightHandState}");
        }
    }
}