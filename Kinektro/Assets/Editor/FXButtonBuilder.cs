using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using TMPro;

/// <summary>
/// Herramienta de editor: crea los 5 botones de seleccion de FX por canal
/// (izquierda = Ch1, derecha = Ch2), en los laterales de la consola, con
/// etiqueta de texto encima de cada uno, y los cablea a FXSelector.
/// Se puede volver a ejecutar: borra y reconstruye sus propios objetos.
/// </summary>
public static class FXButtonBuilder
{
    static readonly string[] fxNames = { "Reverb", "Delay", "Distortion", "LowPass", "Flanger" };

    // --- Constantes de posicionamiento, ajustables para iterar visualmente ---
    // Los botones van APOYADOS sobre la mesa (misma altura, sin apilarse en Y),
    // al costado de la consola, en una fila a lo largo de la profundidad de la mesa (eje Z).
    static float sideExtraCh1 = 1f;       // cuanto se empuja el bloque del canal 1 (izquierda) hacia el lateral, al costado de la consola
    static float sideExtraCh2 = 1f;       // lo mismo para el canal 2 (derecha); separado para poder ajustar cada lado de forma independiente
    static float verticalStart = -0.08f;  // offset vertical (Y) respecto al boton "Effect": deja los botones apoyados sobre la mesa, no flotando
    static float depthSpacing = 0.08f;    // separacion entre botones a lo largo de la profundidad de la mesa (Z)
    static float depthStart = -0.16f;     // offset de profundidad (Z) del primer boton respecto al boton "Effect"
    static float labelHeightOffset = 0.05f; // altura de la etiqueta sobre cada boton
    static float labelScale = 0.03f;
    static float fontSize = 36f;

    static Vector3 WorldCenter(GameObject go)
    {
        Renderer r = go.GetComponentInChildren<Renderer>();
        if (r != null) return r.bounds.center;
        return go.transform.position;
    }

    [MenuItem("DJ Tools/Build FX Buttons (Ch1 + Ch2)")]
    public static void BuildAll()
    {
        DJ_Manager djManager = Object.FindObjectOfType<DJ_Manager>();
        GameObject ch1Template = GameObject.Find("Ch1_Play");
        GameObject ch2Template = GameObject.Find("Ch2_Play");
        GameObject ch1Effect = GameObject.Find("Ch1_Effect");
        GameObject ch2Effect = GameObject.Find("Ch2_Effect");
        GameObject ch1Plato = GameObject.Find("Ch1_Plato");
        GameObject ch2Plato = GameObject.Find("Ch2_Plato");
        XRInteractionManager xrManager = Object.FindObjectOfType<XRInteractionManager>();

        if (djManager == null || ch1Template == null || ch2Template == null ||
            ch1Effect == null || ch2Effect == null || ch1Plato == null || ch2Plato == null)
        {
            Debug.LogError("[FXButtonBuilder] Faltan objetos de referencia en la escena (DJ_Manager / Ch1_Play / Ch2_Play / Ch1_Effect / Ch2_Effect / Ch1_Plato / Ch2_Plato).");
            return;
        }

        Vector3 sideAxis = (WorldCenter(ch1Plato) - WorldCenter(ch2Plato)).normalized;

        FXSelector sel1 = BuildChannel(djManager, 1, ch1Template, ch1Effect, sideAxis, sideExtraCh1, xrManager);
        FXSelector sel2 = BuildChannel(djManager, 2, ch2Template, ch2Effect, -sideAxis, sideExtraCh2, xrManager);

        WireGestureDetector(sel1, sel2);

        Debug.Log("[FXButtonBuilder] Listo: botones FX creados para Canal 1 y Canal 2, y gestos de manos conectados.");
    }

    static FXSelector BuildChannel(DJ_Manager djManager, int channel, GameObject interactableTemplate, GameObject visualTemplate, Vector3 outwardAxis, float sideExtra, XRInteractionManager xrManager)
    {
        Transform parentConsole = visualTemplate.transform.parent;

        string rootName = $"Ch{channel}_FX_Buttons";
        Transform existingRoot = parentConsole.Find(rootName);
        if (existingRoot != null) Object.DestroyImmediate(existingRoot.gameObject);

        GameObject root = new GameObject(rootName);
        root.transform.SetParent(parentConsole, false);
        root.transform.position = WorldCenter(visualTemplate);
        root.transform.rotation = visualTemplate.transform.rotation;

        GameObject selectorObj = new GameObject($"FXSelector_Ch{channel}");
        selectorObj.transform.SetParent(root.transform, false);
        FXSelector selector = selectorObj.AddComponent<FXSelector>();
        selector.djManager = djManager;
        selector.channelNumber = channel;
        selector.fxButtons = new VR_Boton[5];

        Vector3 basePos = WorldCenter(visualTemplate)
                           + outwardAxis * sideExtra
                           + Vector3.up * verticalStart;

        // Fila a lo largo de la profundidad de la mesa (Z del mundo), misma altura para los 5.
        Vector3 depthAxis = Vector3.forward;
        Vector3 rowStart = basePos + depthAxis * depthStart;

        for (int i = 0; i < 5; i++)
        {
            Vector3 pos = rowStart + depthAxis * (depthSpacing * i);
            string name = $"Ch{channel}_FX_{i + 1}_{fxNames[i]}";

            GameObject buttonGO = (GameObject)Object.Instantiate(interactableTemplate, root.transform);
            buttonGO.name = name;
            buttonGO.transform.position = pos;
            buttonGO.transform.rotation = interactableTemplate.transform.rotation;

            VR_Boton vrBoton = buttonGO.GetComponent<VR_Boton>();
            XRSimpleInteractable interactable = buttonGO.GetComponent<XRSimpleInteractable>();

            if (vrBoton == null || interactable == null)
            {
                Debug.LogError($"[FXButtonBuilder] La plantilla '{interactableTemplate.name}' no tiene VR_Boton/XRSimpleInteractable en su raiz.");
                continue;
            }

            vrBoton.type = VR_Boton.ButtonType.Toggle;

            if (xrManager != null) interactable.interactionManager = xrManager;

            int slot = i + 1;

            ClearPersistentCalls(interactable, "m_FirstSelectEntered");
            ClearPersistentCalls(interactable, "m_LastSelectExited");

            UnityEventTools.AddVoidPersistentListener(interactable.firstSelectEntered, vrBoton.OnPress);
            UnityEventTools.AddIntPersistentListener(interactable.firstSelectEntered, selector.SelectEffect, slot);
            UnityEventTools.AddVoidPersistentListener(interactable.lastSelectExited, vrBoton.OnRelease);

            selector.fxButtons[i] = vrBoton;

            CreateLabel(root.transform, buttonGO.transform, pos + Vector3.up * labelHeightOffset, outwardAxis, fxNames[i]);
        }

        return selector;
    }

    static void ClearPersistentCalls(Object component, string eventFieldPath)
    {
        SerializedObject so = new SerializedObject(component);
        SerializedProperty callsProp = so.FindProperty(eventFieldPath + ".m_PersistentCalls.m_Calls");
        if (callsProp != null)
        {
            callsProp.ClearArray();
            so.ApplyModifiedProperties();
        }
    }

    static void CreateLabel(Transform root, Transform buttonTransform, Vector3 worldPos, Vector3 outwardAxis, string text)
    {
        GameObject labelGO = new GameObject("Label_" + text);
        labelGO.transform.SetParent(root, true);
        labelGO.transform.position = worldPos;
        labelGO.transform.rotation = Quaternion.LookRotation(-outwardAxis, Vector3.up);
        // El padre (root) puede tener una escala acumulada muy chica (por la
        // cadena de padres de la consola). labelScale esta pensado como un
        // tamano absoluto en unidades del mundo, asi que se compensa
        // dividiendo por la escala del padre para que el tamano final en
        // el mundo sea siempre el mismo, sin importar donde cuelgue el root.
        Vector3 parentLossyScale = root.lossyScale;
        labelGO.transform.localScale = new Vector3(
            labelScale / Mathf.Max(parentLossyScale.x, 0.0001f),
            labelScale / Mathf.Max(parentLossyScale.y, 0.0001f),
            labelScale / Mathf.Max(parentLossyScale.z, 0.0001f));

        TextMeshPro tmp = labelGO.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.enableAutoSizing = false;
        RectTransform rt = tmp.rectTransform;
        rt.sizeDelta = new Vector2(220, 60);

        // La etiqueta siempre mira hacia la camara (billboard), asi no
        // depende de calcular bien el angulo exacto hacia el jugador.
        labelGO.AddComponent<FXLabelBillboard>();
    }

    static void WireGestureDetector(FXSelector sel1, FXSelector sel2)
    {
        GameObject kinectObj = GameObject.Find("KinectManager_UDP");
        if (kinectObj == null)
        {
            Debug.LogWarning("[FXButtonBuilder] No se encontro 'KinectManager_UDP'; no se conectaron los gestos de manos.");
            return;
        }

        GestureDetector gd = kinectObj.GetComponent<GestureDetector>();
        if (gd == null)
        {
            gd = kinectObj.AddComponent<GestureDetector>();
            KinectSourceProvider provider = kinectObj.GetComponent<KinectSourceProvider>();
            if (provider != null) gd.sourceProvider = provider;
            Debug.Log("[FXButtonBuilder] Se agrego GestureDetector a 'KinectManager_UDP' (no existia en la escena).");
        }

        if (gd.onRightHandRaised == null) gd.onRightHandRaised = new UnityEngine.Events.UnityEvent();
        if (gd.onRightHandLowered == null) gd.onRightHandLowered = new UnityEngine.Events.UnityEvent();
        if (gd.onLeftHandRaised == null) gd.onLeftHandRaised = new UnityEngine.Events.UnityEvent();
        if (gd.onLeftHandLowered == null) gd.onLeftHandLowered = new UnityEngine.Events.UnityEvent();

        // Se limpian los listeners existentes para que la herramienta sea
        // idempotente (si no, cada re-ejecucion deja listeners huerfanos
        // apuntando a FXSelector destruidos de corridas anteriores).
        ClearPersistentCalls(gd, "onLeftHandRaised");
        ClearPersistentCalls(gd, "onLeftHandLowered");
        ClearPersistentCalls(gd, "onRightHandRaised");
        ClearPersistentCalls(gd, "onRightHandLowered");

        AddIfMissing(gd.onLeftHandRaised, sel1.ActivateSelected);
        AddIfMissing(gd.onLeftHandLowered, sel1.Deactivate);
        AddIfMissing(gd.onRightHandRaised, sel2.ActivateSelected);
        AddIfMissing(gd.onRightHandLowered, sel2.Deactivate);

        EditorUtility.SetDirty(gd);
    }

    static void AddIfMissing(UnityEngine.Events.UnityEvent evt, UnityEngine.Events.UnityAction action)
    {
        if (evt == null)
        {
            Debug.LogWarning("[FXButtonBuilder] Evento nulo al conectar gesto; se omite esta conexion.");
            return;
        }
        int count = evt.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (evt.GetPersistentTarget(i) == (Object)action.Target && evt.GetPersistentMethodName(i) == action.Method.Name)
            {
                return; // ya estaba conectado
            }
        }
        UnityEventTools.AddVoidPersistentListener(evt, action);
    }
}
