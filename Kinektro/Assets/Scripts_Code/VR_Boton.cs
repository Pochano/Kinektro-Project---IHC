using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class VR_Boton : MonoBehaviour
{
    public enum ButtonType { Toggle, Momentary }
    public enum MovementAxis { Y_Axis, X_Axis, Z_Axis }

    public ButtonType type;

    [Header("Configuración de Movimiento")]
    public Transform buttonMesh;
    public MovementAxis pushAxis = MovementAxis.Y_Axis; // Elige el eje desde el Inspector
    public float pushDistance = 0.005f;

    private Vector3 unpressedPos;
    private Vector3 pressedPos;
    private bool isToggled = false;

    void Start()
    {
        if (buttonMesh == null) buttonMesh = transform;

        unpressedPos = buttonMesh.localPosition;
        CalculatePressedPosition();
    }

    void CalculatePressedPosition()
    {
        Vector3 direction = Vector3.zero;

        // Selecciona la dirección según la rotación interna del modelo
        switch (pushAxis)
        {
            case MovementAxis.Y_Axis: direction = new Vector3(0, -pushDistance, 0); break;
            case MovementAxis.X_Axis: direction = new Vector3(-pushDistance, 0, 0); break;
            case MovementAxis.Z_Axis: direction = new Vector3(0, 0, -pushDistance); break;
        }

        pressedPos = unpressedPos + direction;
    }

    public void OnPress()
    {
        CalculatePressedPosition(); // Actualiza si cambias valores en ejecución

        if (type == ButtonType.Momentary)
        {
            buttonMesh.localPosition = pressedPos;
        }
        else if (type == ButtonType.Toggle)
        {
            isToggled = !isToggled;
            buttonMesh.localPosition = isToggled ? pressedPos : unpressedPos;
        }
    }

    public void OnRelease()
    {
        if (type == ButtonType.Momentary)
        {
            buttonMesh.localPosition = unpressedPos;
        }
    }

    public void ForceUnpress()
    {
        isToggled = false;
        buttonMesh.localPosition = unpressedPos;
    }

    public bool IsToggled => isToggled;
}