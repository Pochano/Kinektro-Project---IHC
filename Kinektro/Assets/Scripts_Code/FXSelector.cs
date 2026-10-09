using UnityEngine;

/// <summary>
/// Controla los 5 botones de seleccion de efecto (FX) de un canal/plato de DJ.
/// Los botones solo "arman" (seleccionan) un efecto de forma mutuamente excluyente;
/// el efecto armado se activa/desactiva de verdad cuando se levanta/baja la mano
/// correspondiente (ver GestureDetector -> ActivateSelected() / Deactivate()).
/// </summary>
public class FXSelector : MonoBehaviour
{
    [Header("Referencias Principales")]
    public DJ_Manager djManager;

    [Tooltip("1 para el canal/plato izquierdo (Ch1), 2 para el derecho (Ch2)")]
    public int channelNumber = 1;

    [Header("Botones de Efecto (orden: Reverb, Delay, Distortion, LowPass, Flanger)")]
    public VR_Boton[] fxButtons = new VR_Boton[5];

    // 0 = None, 1 = Reverb, 2 = Delay, 3 = Distortion, 4 = LowPass, 5 = Flanger
    private int armedEffect = 0;
    private bool isActive = false;

    /// <summary>
    /// Llamado por el boton fisico presionado (buttonSlot entre 1 y 5).
    /// Des-presiona visualmente los demas botones del mismo canal (mutua exclusion).
    /// </summary>
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

        // Si la mano de este canal ya esta levantada, el cambio de efecto se refleja al instante.
        if (isActive)
        {
            ApplyArmedEffect();
        }

        Debug.Log($"[FXSelector Ch{channelNumber}] Efecto armado: {armedEffect}");
    }

    /// <summary>
    /// Activa en FMOD el efecto actualmente armado. Pensado para engancharse a
    /// GestureDetector.onLeftHandRaised / onRightHandRaised.
    /// </summary>
    public void ActivateSelected()
    {
        isActive = true;
        Debug.Log($"[FXSelector Ch{channelNumber}] ActivateSelected() llamado. armedEffect={armedEffect}, djManager nulo? {djManager == null}");
        ApplyArmedEffect();
    }

    /// <summary>
    /// Desactiva el efecto (vuelve a None) sin perder cual quedo armado.
    /// Pensado para GestureDetector.onLeftHandLowered / onRightHandLowered.
    /// </summary>
    public void Deactivate()
    {
        isActive = false;

        if (djManager != null)
        {
            djManager.SetFXSelect(channelNumber, 0);
        }
    }

    private void ApplyArmedEffect()
    {
        if (djManager == null || armedEffect <= 0) return;
        djManager.SetFXSelect(channelNumber, armedEffect);
    }
}
