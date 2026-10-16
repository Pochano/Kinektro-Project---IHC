using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
public class ShelfSocketTrigger : MonoBehaviour
{
    [Header("Identificador de Socket")]
    public int socketIndex = 0;

    [Header("Referencias")]
    public ShelfManager shelfManager;

    [Header("Estado del Socket")]
    public GameObject currentVinylInSocket = null;

    [HideInInspector] public float lastPlacedTime = 0f;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        if (shelfManager == null) shelfManager = FindObjectOfType<ShelfManager>();
    }

    private void OnTriggerStay(Collider other)
    {
        if (currentVinylInSocket != null) return;

        VinylData vinylData = other.GetComponentInParent<VinylData>();
        if (vinylData == null) return;

        XRGrabInteractable grabInteractable = vinylData.GetComponent<XRGrabInteractable>();

        // BLOQUEO DE IMÁN: Ignora el imán de la repisa si el vinilo está en la mano del jugador
        if (grabInteractable != null && grabInteractable.isSelected) return;

        SnapVinylToSocket(vinylData.gameObject, vinylData);
    }

    private void OnTriggerExit(Collider other)
    {
        VinylData vinylData = other.GetComponentInParent<VinylData>();
        if (vinylData != null && vinylData.gameObject == currentVinylInSocket)
        {
            currentVinylInSocket = null;
            lastPlacedTime = 0f;
        }
    }

    public void SnapVinylToSocket(GameObject vinylObj, VinylData vinylData)
    {
        currentVinylInSocket = vinylObj;
        lastPlacedTime = Time.time;

        if (vinylData != null)
        {
            vinylData.SetState(VinylState.OnShelf);
        }

        Rigidbody rb = vinylObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Vector3 targetPos = transform.position;
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            targetPos = renderer.bounds.center;
        }

        vinylObj.transform.position = targetPos;

        Quaternion baseRotation = (shelfManager != null && shelfManager.vinylReferenceTransform != null)
            ? shelfManager.vinylReferenceTransform.rotation
            : transform.rotation;

        vinylObj.transform.rotation = Quaternion.Euler(180f, baseRotation.eulerAngles.y, baseRotation.eulerAngles.z);

        if (shelfManager != null && shelfManager.vinylReferenceTransform != null)
        {
            vinylObj.transform.localScale = shelfManager.vinylReferenceTransform.lossyScale;
        }

        Debug.Log($"[ShelfSocket] Disco '{vinylObj.name}' acoplado al Socket {socketIndex + 1} en t={lastPlacedTime:F2}s.");
    }
}