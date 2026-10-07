using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public enum VinylState
{
    OnShelf,
    Grabbed,
    OnTurntable
}

public class VinylData : MonoBehaviour
{
    [Header("Metadata Reference")]
    public VinylMetadata metadata;

    [Header("Referencias de UI (Sticker Canvas)")]
    [Tooltip("Asigna el objeto FrontCover (componente Image)")]
    public Image frontCoverImage;
    [Tooltip("Asigna el objeto BackCover (componente Image)")]
    public Image backCoverImage;

    [Header("Configuración de Autodestrucción")]
    [Tooltip("Tiempo en segundos antes de destruirse si el disco no está en una repisa o tornamesa")]
    public float floatingLifetime = 15f;

    public int trackID;
    public VinylState currentState = VinylState.OnShelf;

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private float unplacedTimer = 0f;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();

        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }
    }

    private void Update()
    {
        // Si el disco NO está en la repisa ni en la tornamesa
        if (currentState != VinylState.OnShelf && currentState != VinylState.OnTurntable)
        {
            // Solo contamos el tiempo si NO lo está sosteniendo el jugador
            if (grabInteractable != null && !grabInteractable.isSelected)
            {
                unplacedTimer += Time.deltaTime;

                if (unplacedTimer >= floatingLifetime)
                {
                    Debug.Log($"[VinylData] El disco '{gameObject.name}' estuvo flotando más de {floatingLifetime}s. Destruyendo...");
                    Destroy(gameObject);
                }
            }
            else
            {
                // Si el jugador lo agarra en la mano, reiniciamos el contador de autodestrucción
                unplacedTimer = 0f;
            }
        }
        else
        {
            // Si está bien colocado en repisa o tornamesa, reseteamos el contador
            unplacedTimer = 0f;
        }
    }

    public void SetState(VinylState newState)
    {
        currentState = newState;
        unplacedTimer = 0f; // Reseteamos el temporizador al cambiar de estado
    }

    /// <summary>
    /// Convierte la Texture2D del metadata en Sprite y la aplica a las carátulas del Canvas
    /// </summary>
    public void ApplyCoverArt()
    {
        if (metadata == null || metadata.coverArt == null) return;

        // 1. Convertir la Texture2D del ScriptableObject a Sprite
        Texture2D texture = metadata.coverArt;
        Sprite coverSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );

        // 2. Asignar el sprite a la cara frontal
        if (frontCoverImage != null)
        {
            frontCoverImage.sprite = coverSprite;
            frontCoverImage.color = Color.white;
        }

        // 3. Asignar el sprite a la cara trasera
        if (backCoverImage != null)
        {
            backCoverImage.sprite = coverSprite;
            backCoverImage.color = Color.white;
        }

        Debug.Log($"[VinylData] Sticker UI de '{metadata.songTitle}' aplicado con éxito en ambas caras.");
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        SetState(VinylState.Grabbed);

        if (rb != null)
        {
            rb.isKinematic = false;
        }
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Al soltarlo en el aire, se mantiene como Grabbed/Unplaced hasta entrar a un socket
        unplacedTimer = 0f;
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }
}