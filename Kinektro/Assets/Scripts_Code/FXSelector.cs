using UnityEngine;

public class FXSelector : MonoBehaviour
{
    [Header("Referencias Principales")]
    public DJ_Manager djManager;

    [Tooltip("1 para el canal/plato izquierdo (Ch1), 2 para el derecho (Ch2)")]
    public int channelNumber = 1;

    [Header("Visuales del Botón Morado FX")]
    public MeshRenderer fxButtonRenderer;
    public Color colorActive = new Color(0.6f, 0f, 1f, 1f);      // Morado Encendido
    public Color colorInactive = new Color(0.25f, 0f, 0.4f, 1f); // Morado Apagado

    [Header("Botones de Efecto (orden: Reverb, Delay, Distortion, LowPass, Flanger)")]
    public VR_Boton[] fxButtons = new VR_Boton[5];

    private int armedEffect = 0;
    private bool isActive = false;

    private void Start()
    {
        UpdateVisualColor();
    }

    public void ToggleFXChannel()
    {
        isActive = !isActive;

        if (isActive)
        {
            ApplyArmedEffect();
            Debug.Log($"[FXSelector Ch{channelNumber}] Módulo FX ACTIVADO.");
        }
        else
        {
            if (djManager != null)
            {
                djManager.SetFXSelect(channelNumber, 0);
            }
            Debug.Log($"[FXSelector Ch{channelNumber}] Módulo FX DESACTIVADO.");
        }

        UpdateVisualColor();

        // Notificación al Tutorial
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.OnFXButtonToggle();
        }
    }

    private void UpdateVisualColor()
    {
        if (fxButtonRenderer != null)
        {
            fxButtonRenderer.material.color = isActive ? colorActive : colorInactive;
        }
    }

    public void SelectEffect(int buttonSlot)
    {
        if (fxButtons != null)
        {
            for (int i = 0; i < fxButtons.Length; i++)
            {
                if (i != (buttonSlot - 1) && fxButtons[i] != null)
                {
                    fxButtons[i].ForceUnpress();
                }
            }
        }

        armedEffect = buttonSlot;

        // --- SOLUCIÓN PARA EL TUTORIAL ---
        if (TutorialManager.Instance != null && armedEffect > 0)
        {
            // Si el tutorial está atascado en activar el botón morado (Paso 11),
            // lo avanzamos primero a la selección de efecto y luego completamos la selección.
            if (TutorialManager.Instance.currentStep == TutorialStep.FXButton)
            {
                TutorialManager.Instance.OnFXButtonToggle();
            }

            TutorialManager.Instance.OnFXSelected();
        }

        if (isActive)
        {
            ApplyArmedEffect();
        }

        Debug.Log($"[FXSelector Ch{channelNumber}] Efecto armado: {armedEffect}");
    }

    public void ActivateSelected()
    {
        isActive = true;
        ApplyArmedEffect();
        UpdateVisualColor();

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.OnFXButtonToggle();
        }
    }

    public void Deactivate()
    {
        isActive = false;

        if (djManager != null)
        {
            djManager.SetFXSelect(channelNumber, 0);
        }
        UpdateVisualColor();
    }

    private void ApplyArmedEffect()
    {
        if (djManager == null || armedEffect <= 0) return;
        djManager.SetFXSelect(channelNumber, armedEffect);
    }
}