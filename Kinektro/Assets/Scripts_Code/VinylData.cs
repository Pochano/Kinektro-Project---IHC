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
    public Image frontCoverImage;
    public Image backCoverImage;

    [Header("Configuración de Autodestrucción")]
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
        if (currentState != VinylState.OnShelf && currentState != VinylState.OnTurntable)
        {
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
                unplacedTimer = 0f;
            }
        }
        else
        {
            unplacedTimer = 0f;
        }
    }

    public void SetState(VinylState newState)
    {
        currentState = newState;
        unplacedTimer = 0f;
    }

    public void ApplyCoverArt()
    {
        if (metadata == null || metadata.coverArt == null) return;

        Texture2D texture = metadata.coverArt;
        Sprite coverSprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );

        if (frontCoverImage != null)
        {
            frontCoverImage.sprite = coverSprite;
            frontCoverImage.color = Color.white;
        }

        if (backCoverImage != null)
        {
            backCoverImage.sprite = coverSprite;
            backCoverImage.color = Color.white;
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        SetState(VinylState.Grabbed);

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = false;
        }

        // TELETRANSPORTE DIRECTO A LA MANO
        if (args.interactorObject != null)
        {
            Transform targetAttach = args.interactorObject.GetAttachTransform(grabInteractable);

            if (targetAttach == null && args.interactorObject.transform != null)
            {
                targetAttach = args.interactorObject.transform;
            }

            if (targetAttach != null)
            {
                transform.position = targetAttach.position;
                transform.rotation = targetAttach.rotation;
            }
        }

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnVinylGrabbed();
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        unplacedTimer = 0f;

        if (rb != null && currentState == VinylState.Grabbed)
        {
            rb.useGravity = true;
        }
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