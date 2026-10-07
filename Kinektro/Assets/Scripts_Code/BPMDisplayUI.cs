using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class BPMDisplayUI : MonoBehaviour
{
    [Header("Text UI References")]
    public TextMeshProUGUI trackTitleText;
    public TextMeshProUGUI artistText;
    public TextMeshProUGUI bpmText;
    public Image coverImage;

    [Header("Pitch Slider Visual")]
    public Slider pitchVisualSlider; // Opcional: muestra la barra de pitch (-50% a +50%)

    /// <summary>
    /// Actualiza la pantalla cuando se coloca un nuevo disco en la reproductora
    /// </summary>
    public void DisplayVinylMetadata(VinylMetadata metadata)
    {
        if (metadata == null) return;

        if (trackTitleText != null) trackTitleText.text = metadata.songTitle;
        if (artistText != null) artistText.text = metadata.artistName;
        if (coverImage != null && metadata.coverArt != null)
        {
            Sprite coverSprite = Sprite.Create(
                metadata.coverArt,
                new Rect(0, 0, metadata.coverArt.width, metadata.coverArt.height),
                new Vector2(0.5f, 0.5f)
            );
            coverImage.sprite = coverSprite;
        }
    }

    /// <summary>
    /// Actualiza en tiempo real el valor de BPM en la pantalla
    /// </summary>
    public void UpdateBPMDisplay(float currentBPM, float pitchPercent)
    {
        if (bpmText != null)
        {
            bpmText.text = $"{currentBPM:F1} BPM";
        }

        if (pitchVisualSlider != null)
        {
            pitchVisualSlider.value = pitchPercent;
        }
    }
    public void ClearDisplay()
{
    if (trackTitleText != null) trackTitleText.text = "Canción";
    if (artistText != null) artistText.text = "Autor";
    if (bpmText != null) bpmText.text = "BPM";
    if (coverImage != null)
    {
        coverImage.sprite = null;
        coverImage.enabled = true; // Oculta el recuadro blanco de la imagen
    }
}
}