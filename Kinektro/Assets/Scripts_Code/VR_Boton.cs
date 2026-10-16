using UnityEngine;

public class VR_Boton : MonoBehaviour
{
    public enum ButtonType { Toggle, Momentary }
    public enum MovementAxis { Y_Axis, X_Axis, Z_Axis }

    public ButtonType type;

    [Header("Configuración de Movimiento")]
    public Transform buttonMesh;
    public MovementAxis pushAxis = MovementAxis.Y_Axis;
    public float pushDistance = 0.005f;

    [Header("Configuración de Colores")]
    public Renderer targetRenderer;
    public Color offColor = Color.white;
    public Color inColor = Color.yellow;   // Punto A marcado
    public Color activeColor = Color.green; // Loop Activo (Punto B)

    private Vector3 unpressedPos;
    private Vector3 pressedPos;
    private bool isToggled = false;
    private Material buttonMaterial;

    void Awake()
    {
        if (buttonMesh == null) buttonMesh = transform;
        unpressedPos = buttonMesh.localPosition;
        CalculatePressedPosition();

        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer != null)
        {
            buttonMaterial = targetRenderer.material; // Instancia local del material
        }
    }

    void Start()
    {
        ForceUnpress();
    }

    void CalculatePressedPosition()
    {
        Vector3 direction = Vector3.zero;

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
        CalculatePressedPosition();

        if (type == ButtonType.Momentary)
        {
            buttonMesh.localPosition = pressedPos;
        }
        else if (type == ButtonType.Toggle)
        {
            isToggled = !isToggled;
            buttonMesh.localPosition = isToggled ? pressedPos : unpressedPos;
        }

        // --- CONEXIÓN CON EL TUTORIAL BÁSICO ---
        if (BasicTutorialManager.Instance != null)
        {
            if (gameObject.name.Contains("Play")) BasicTutorialManager.Instance.OnPlayPressed();
            else if (gameObject.name.Contains("Cue")) BasicTutorialManager.Instance.OnCuePressed();
            else if (gameObject.name.Contains("Loop")) BasicTutorialManager.Instance.OnLoopPressed();
            else if (gameObject.name.Contains("Reinicio")) BasicTutorialManager.Instance.OnResetTempoPressed();
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
        if (buttonMesh != null)
        {
            buttonMesh.localPosition = unpressedPos;
        }
        SetButtonColor(offColor);
    }

    public void SetButtonColor(Color newColor)
    {
        if (targetRenderer != null)
        {
            // Obtenemos los materiales instanciados
            Material[] mats = targetRenderer.materials;

            bool colorApplied = false;

            // 1. Busca específicamente el material del aro por nombre ("Color_Sync")
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].name.Contains("Color_Sync") || mats[i].name.Contains("Sync"))
                {
                    mats[i].color = newColor;

                    // Si usa Shader URP/Lit con emisión, actualiza la propiedad de brillo
                    if (mats[i].HasProperty("_EmissionColor"))
                    {
                        mats[i].SetColor("_EmissionColor", newColor);
                    }
                    colorApplied = true;
                    break;
                }
            }

            // 2. Si no lo encuentra por nombre, modifica el Element 0 por defecto
            if (!colorApplied && mats.Length > 0)
            {
                mats[0].color = newColor;
                if (mats[0].HasProperty("_EmissionColor"))
                {
                    mats[0].SetColor("_EmissionColor", newColor);
                }
            }

            // Reasignamos los materiales actualizados al Mesh Renderer
            targetRenderer.materials = mats;
        }
    }

    public bool IsToggled => isToggled;
}