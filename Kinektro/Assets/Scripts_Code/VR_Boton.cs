using System.Collections;
using UnityEngine;

public class VR_Boton : MonoBehaviour, IResaltable
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
    public Color offColor = new Color(0.12f, 0.12f, 0.12f, 1f);
    public Color inColor = Color.yellow;
    public Color activeColor = Color.green;

    [Header("Resaltado del Pipeline (Jugador A)")]
    public Color avisoColor = new Color(1f, 0.08f, 0.08f, 1f);
    public Color resaltadoColor = new Color(0f, 1f, 0.2f, 1f);

    [Header("Pipeline Target")]
    public DJControlTarget pipelineTarget = DJControlTarget.None;

    private Vector3 unpressedPos;
    private Vector3 pressedPos;
    private bool isToggled = false;
    private Coroutine flashRoutine;

    private void Awake()
    {
        if (buttonMesh == null) buttonMesh = transform;
        unpressedPos = buttonMesh.localPosition;

        if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();

        // Fuerza colores consistentes de reposo y aviso
        offColor = new Color(0.12f, 0.12f, 0.12f, 1f);
        avisoColor = new Color(1f, 0.08f, 0.08f, 1f);
        resaltadoColor = new Color(0f, 1f, 0.2f, 1f);

        // Si se dejó en None en el Inspector, deduce el control por el nombre del botón y su canal
        if (pipelineTarget == DJControlTarget.None)
        {
            string n = gameObject.name.ToLower();
            string parentName = transform.parent != null ? transform.parent.name.ToLower() : "";
            string chTag = (n.Contains("ch2") || n.Contains("2") || parentName.Contains("ch2")) ? "2" : "1";

            string keyword = "";
            if (n.Contains("play")) keyword = "play";
            else if (n.Contains("cue")) keyword = "cue";
            else if (n.Contains("loop")) keyword = "loop";
            else if (n.Contains("reset") || n.Contains("reinicio") || n.Contains("tempo")) keyword = "reset";

            if (!string.IsNullOrEmpty(keyword))
            {
                foreach (DJControlTarget val in System.Enum.GetValues(typeof(DJControlTarget)))
                {
                    string enumName = val.ToString().ToLower();
                    if (enumName.Contains(keyword) && enumName.Contains(chTag))
                    {
                        pipelineTarget = val;
                        break;
                    }
                }
            }
        }

        SetButtonColor(offColor);
    }

    private void Start()
    {
        ForceUnpress();
    }

    private void CalculatePressedPosition()
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

        if (gameObject.name.Contains("StartPipeline"))
        {
            if (DJPipelineEvaluator.Instance != null)
            {
                DJPipelineEvaluator.Instance.StartCountdown();
            }
        }

        // Notifica una sola vez al evaluador del pipeline
        if (DJPipelineEvaluator.Instance != null && pipelineTarget != DJControlTarget.None)
        {
            DJPipelineEvaluator.Instance.SubmitInput(pipelineTarget, 1f);
        }

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
            Material[] mats = targetRenderer.materials;
            bool colorApplied = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].name.Contains("Color_Sync") || mats[i].name.Contains("Sync"))
                {
                    mats[i].color = newColor;
                    if (mats[i].HasProperty("_EmissionColor"))
                    {
                        mats[i].SetColor("_EmissionColor", newColor);
                    }
                    colorApplied = true;
                    break;
                }
            }

            if (!colorApplied && mats.Length > 0)
            {
                mats[0].color = newColor;
                if (mats[0].HasProperty("_EmissionColor"))
                {
                    mats[0].SetColor("_EmissionColor", newColor);
                }
            }

            targetRenderer.materials = mats;
        }
    }

    public void SetResaltado(bool activo)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        SetButtonColor(activo ? avisoColor : offColor);
    }

    public void SetProximidadColor(float cercania)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        SetButtonColor(avisoColor);
    }

    public void FlashSuccess()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashSuccessRoutine());
    }

    private IEnumerator FlashSuccessRoutine()
    {
        SetButtonColor(resaltadoColor);
        yield return new WaitForSeconds(0.25f);
        SetButtonColor(offColor);
        flashRoutine = null;
    }

    public bool IsToggled => isToggled;
}