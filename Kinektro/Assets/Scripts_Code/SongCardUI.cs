using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(XRSimpleInteractable))]
public class SongCardUI : MonoBehaviour
{
    [Header("Referencias UI")]
    public Image coverImage;
    public TextMeshProUGUI songTitleText;
    public TextMeshProUGUI artistNameText;
    public TextMeshProUGUI bpmText;
    public Button cardButton; // Opcional si mantienes clic de mouse

    private VinylMetadata currentMetadata;
    private MusicLibraryManager libraryManager;

    private XRSimpleInteractable interactable;
    private BoxCollider boxCollider;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        boxCollider = GetComponent<BoxCollider>();
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnXRSelectEntered);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnXRSelectEntered);
        }
    }

    public void SetupCard(VinylMetadata metadata, MusicLibraryManager manager)
    {
        currentMetadata = metadata;
        libraryManager = manager;

        if (metadata == null) return;

        if (songTitleText != null) songTitleText.text = metadata.songTitle;
        if (artistNameText != null) artistNameText.text = metadata.artistName;
        if (bpmText != null) bpmText.text = $"{metadata.baseBPM:F0} BPM";

        if (coverImage != null && metadata.coverArt != null)
        {
            Sprite coverSprite = Sprite.Create(
                metadata.coverArt,
                new Rect(0, 0, metadata.coverArt.width, metadata.coverArt.height),
                new Vector2(0.5f, 0.5f)
            );
            coverImage.sprite = coverSprite;
        }

        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(OnCardClicked);
        }

        // Auto-ajustar el tamaño del Collider al tamaño del RectTransform de la tarjeta
        AdjustColliderSize();
    }

    private void AdjustColliderSize()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null && boxCollider != null)
        {
            boxCollider.size = new Vector3(rectTransform.rect.width, rectTransform.rect.height, 5f);
            boxCollider.center = new Vector3(0, 0, 0);
        }
    }

    private void OnXRSelectEntered(SelectEnterEventArgs args)
    {
        OnCardClicked();
    }

    public void OnCardClicked()
    {
        if (libraryManager != null && currentMetadata != null)
        {
            libraryManager.SpawnVinylFromCard(currentMetadata);
        }
    }

    // Método listo para desactivar/activar el collider durante el Scroll (Paso futuro)
    public void SetColliderActive(bool active)
    {
        if (boxCollider != null)
        {
            boxCollider.enabled = active;
        }
    }
}