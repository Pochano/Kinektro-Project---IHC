using UnityEngine;

/// <summary>
/// Permite activar/desactivar el modulo de efectos de un canal (Ch1 o Ch2)
/// con un simple click de mouse sobre el boton morado del canal, sin
/// depender del controlador VR ni del Kinect/mock. Requiere un Collider
/// en este mismo GameObject (ya presente: BoxCollider) para que
/// OnMouseDown se dispare.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FXButtonClick : MonoBehaviour
{
    [Tooltip("FXSelector del canal que este boton debe activar/desactivar.")]
    public FXSelector fxSelector;

    private void OnMouseDown()
    {
        if (fxSelector == null)
        {
            Debug.LogWarning($"[FXButtonClick] {name}: fxSelector no asignado.");
            return;
        }

        fxSelector.ToggleFXChannel();
    }
}
