using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class TurntableDeck : MonoBehaviour
{
    [Header("Referencias Principales")]
    public DJ_Manager djManager;
    public DJ_DeckController deckController;
    public Transform turntablePlatterTransform;
    public BPMDisplayUI bpmDisplayUI;

    [Header("Configuración de Rotación del Plato")]
    public float rotationSpeed = 200f;
    public Vector3 rotationAxis = new Vector3(0, 0, 1);

    [Header("Ajustes de Magnetismo (Snap)")]
    public float maxSnapDistance = 0.35f;

    [Header("Ajustes de Postura y Posición del Disco")]
    [Tooltip("Rotación para acostar el vinilo plano.")]
    public Vector3 flatRotationEuler = new Vector3(0f, 90f, 0f);

    [Tooltip("Altura REAL en el eje Y vertical sobre el plato")]
    public float heightOffset = 0.05f;

    [Header("Estado Actual del Plato")]
    public VinylData currentVinyl;

    private bool isOccupied = false;
    private float currentFaderValue = 0.5f; // Neutral (1.0x pitch)

    private void Update()
    {
        if (turntablePlatterTransform != null && isOccupied)
        {
            turntablePlatterTransform.Rotate(rotationAxis * (rotationSpeed * Time.deltaTime));
        }

        if (currentVinyl != null && turntablePlatterTransform != null)
        {
            currentVinyl.transform.position = turntablePlatterTransform.position + (Vector3.up * heightOffset);
            Quaternion fixedRotation = Quaternion.Euler(flatRotationEuler);
            currentVinyl.transform.rotation = turntablePlatterTransform.rotation * fixedRotation;
        }
    }

    /// <summary>
    /// Evento invocado desde el VR_Fader de pitch/tempo
    /// </summary>
    public void OnTempoFaderChanged(float faderValue)
    {
        currentFaderValue = faderValue;

        if (deckController != null && djManager != null)
        {
            djManager.SetPitch(deckController.channelNumber, currentFaderValue);
        }

        UpdateBPMUI();
    }

    private void UpdateBPMUI()
    {
        if (currentVinyl != null && currentVinyl.metadata != null)
        {
            float speedMultiplier = Mathf.Lerp(0.84f, 1.16f, currentFaderValue);
            float realTimeBPM = currentVinyl.metadata.baseBPM * speedMultiplier;
            float pitchPercentage = (currentFaderValue - 0.5f) * 2f;

            if (bpmDisplayUI != null)
            {
                bpmDisplayUI.UpdateBPMDisplay(realTimeBPM, pitchPercentage);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (isOccupied) return;

        VinylData vinyl = other.GetComponentInParent<VinylData>();
        if (vinyl == null) return;

        XRGrabInteractable grab = vinyl.GetComponent<XRGrabInteractable>();

        float distanceToDeck = Vector3.Distance(vinyl.transform.position, transform.position);
        bool isReleased = (grab != null && !grab.isSelected);

        if (isReleased || distanceToDeck <= (maxSnapDistance * 0.5f))
        {
            PlaceVinylOnDeck(vinyl);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        VinylData vinyl = other.GetComponentInParent<VinylData>();

        if (vinyl != null && vinyl == currentVinyl)
        {
            XRGrabInteractable grab = vinyl.GetComponent<XRGrabInteractable>();

            if (grab != null && grab.isSelected)
            {
                RemoveVinylFromDeck();
            }
        }
    }

    private void PlaceVinylOnDeck(VinylData vinyl)
    {
        isOccupied = true;
        currentVinyl = vinyl;
        currentVinyl.SetState(VinylState.OnTurntable);

        Rigidbody rb = vinyl.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        if (bpmDisplayUI != null && vinyl.metadata != null)
        {
            bpmDisplayUI.DisplayVinylMetadata(vinyl.metadata);
        }

        UpdateBPMUI();

        int trackID = vinyl.trackID;
        Debug.Log($"<color=cyan>[TURNTABLE] CARGANDO CANCIÓN:</color> TrackID: {trackID} ('{vinyl.gameObject.name}')");

        if (deckController != null)
        {
            deckController.LoadVinylTrack(trackID);
            deckController.OnPlayButtonPressed();
        }

        if (djManager != null && deckController != null)
        {
            djManager.SetPitch(deckController.channelNumber, currentFaderValue);
            djManager.UpdateMasterBeatbar();
        }

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnVinylPlacedOnTurntable();

    }

    private void RemoveVinylFromDeck()
    {
        if (currentVinyl != null)
        {
            Debug.Log($"<color=yellow>[TURNTABLE] DISCO RETIRADO:</color> ID {currentVinyl.trackID}.");

            Rigidbody rb = currentVinyl.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }

            currentVinyl.SetState(VinylState.Grabbed);
            currentVinyl = null;
        }

        isOccupied = false;

        if (bpmDisplayUI != null)
        {
            bpmDisplayUI.ClearDisplay();
        }

        if (deckController != null)
        {
            deckController.UnloadVinylTrack();
            deckController.OnCueButtonPressed();
        }

        if (djManager != null)
        {
            djManager.UpdateMasterBeatbar();
        }
    }
}