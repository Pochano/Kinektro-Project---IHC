using System.Collections;
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

        // Espera a que el Grid Layout Group le asigne las dimensiones reales (200x225)
        StartCoroutine(AdjustColliderSizeDelayed());
    }

    private IEnumerator AdjustColliderSizeDelayed()
    {
        // Fuerza el cálculo inmediato del Layout
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());

        // Espera al final del frame para leer las dimensiones reales acomodadas por el Grid
        yield return new WaitForEndOfFrame();

        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null && boxCollider != null)
        {
            float width = rectTransform.rect.width;
            float height = rectTransform.rect.height;

            // Fallback por si sigue dando un valor muy pequeño debido al Grid
            if (width < 10f) width = 200f;
            if (height < 10f) height = 225f;

            boxCollider.size = new Vector3(width, height, 10f);

            Vector2 pivot = rectTransform.pivot;
            float centerX = (0.5f - pivot.x) * width;
            float centerY = (0.5f - pivot.y) * height;

            boxCollider.center = new Vector3(centerX, centerY, 0f);
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

    public void SetColliderActive(bool active)
    {
        if (boxCollider != null)
        {
            boxCollider.enabled = active;
        }
    }
}