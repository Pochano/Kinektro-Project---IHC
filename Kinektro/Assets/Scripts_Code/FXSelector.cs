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

        // Notificación al Tutorial (pasa el número de canal)
        if (TutorialManager.Instance != null && isActive)
        {
            TutorialManager.Instance.OnFXButtonToggle(channelNumber);
        }
    }

    private void UpdateVisualColor()
    {
        if (fxButtonRenderer != null)
        {
            fxButtonRenderer.material.color = isActive ? colorActive : colorInactive;
        }
    }
    /// <summary>Efecto actualmente armado (0 = ninguno). Usado por el lector de altura de mano
    /// para saber que parametro de intensidad progresiva debe alimentar en este canal.</summary>
    public int ArmedEffect => armedEffect;

    /// <summary>True mientras la mano de este canal esta levantada (efecto activo de verdad).</summary>
    public bool IsActive => isActive;

    /// <summary>
    /// Llamado por el boton fisico presionado (buttonSlot entre 1 y 5).
    /// Des-presiona visualmente los demas botones del mismo canal (mutua exclusion).
    /// </summary>
    // === TEMPORAL: helper de prueba para QA manual, quitar despues de verificar ===
    [ContextMenu("TEST: Arm Reverb (slot 1)")]
    private void DebugArmReverb() => SelectEffect(1);

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

        // Notificación al Tutorial
        if (TutorialManager.Instance != null && armedEffect > 0)
        {
            if (TutorialManager.Instance.currentStep == TutorialStep.KinectFXButtons)
            {
                TutorialManager.Instance.OnFXButtonToggle(channelNumber);
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
            TutorialManager.Instance.OnFXButtonToggle(channelNumber);
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