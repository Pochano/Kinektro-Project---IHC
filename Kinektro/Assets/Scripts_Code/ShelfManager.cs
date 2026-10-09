using UnityEngine;
using UnityEngine.InputSystem;

public class ShelfManager : MonoBehaviour
{
    [Header("Referencias de la Estantería")]
    [Tooltip("Asigna aquí en orden las 6 bases (Base_1, Base_2... Base_6) de la jerarquía")]
    public Transform[] baseSockets = new Transform[6];

    [Header("Prefab & Transform de Referencia")]
    public GameObject vinylPrefab;
    public Transform vinylReferenceTransform;

    [Header("Colección de Metadatos de Vinilos (Auto-poblado en Runtime)")]
    public VinylMetadata[] vinylMetadatas;

    private ShelfSocketTrigger[] socketTriggers = new ShelfSocketTrigger[6];

    private void Awake()
    {
        // Inicializar Triggers en las bases
        for (int i = 0; i < baseSockets.Length; i++)
        {
            if (baseSockets[i] != null)
            {
                ShelfSocketTrigger trigger = baseSockets[i].GetComponent<ShelfSocketTrigger>();
                if (trigger == null)
                {
                    trigger = baseSockets[i].gameObject.AddComponent<ShelfSocketTrigger>();
                }
                trigger.socketIndex = i;
                trigger.shelfManager = this;
                socketTriggers[i] = trigger;
            }
        }
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) AutoSpawnNextVinyl(0);
        if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) AutoSpawnNextVinyl(1);
        if (Keyboard.current.digit4Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) AutoSpawnNextVinyl(2);
        if (Keyboard.current.digit5Key.wasPressedThisFrame || Keyboard.current.numpad4Key.wasPressedThisFrame) AutoSpawnNextVinyl(3);
        if (Keyboard.current.digit6Key.wasPressedThisFrame || Keyboard.current.numpad5Key.wasPressedThisFrame) AutoSpawnNextVinyl(4);
        if (Keyboard.current.digit7Key.wasPressedThisFrame || Keyboard.current.numpad6Key.wasPressedThisFrame) AutoSpawnNextVinyl(5);
    }

    public void SpawnVinylInSocket(int metadataIndex)
    {
        AutoSpawnNextVinyl(metadataIndex);
    }

    private void AutoSpawnNextVinyl(int metadataIndex)
    {
        int targetSocketIndex = -1;

        // 1. Buscar socket vacío
        for (int i = 0; i < socketTriggers.Length; i++)
        {
            if (socketTriggers[i] != null && socketTriggers[i].currentVinylInSocket == null)
            {
                targetSocketIndex = i;
                break;
            }
        }

        // 2. Si está llena la estantería, reciclar el disco más viejo
        if (targetSocketIndex == -1)
        {
            float oldestTime = float.MaxValue;

            for (int i = 0; i < socketTriggers.Length; i++)
            {
                if (socketTriggers[i] != null && socketTriggers[i].currentVinylInSocket != null)
                {
                    if (socketTriggers[i].lastPlacedTime < oldestTime)
                    {
                        oldestTime = socketTriggers[i].lastPlacedTime;
                        targetSocketIndex = i;
                    }
                }
            }

            if (targetSocketIndex != -1 && socketTriggers[targetSocketIndex].currentVinylInSocket != null)
            {
                Debug.Log($"[ShelfManager] Estantería llena. Eliminando el disco más antiguo del Socket {targetSocketIndex + 1}.");
                Destroy(socketTriggers[targetSocketIndex].currentVinylInSocket);
                socketTriggers[targetSocketIndex].currentVinylInSocket = null;
            }
        }

        if (targetSocketIndex == -1) return;

        Transform targetSocket = baseSockets[targetSocketIndex];
        if (targetSocket == null || vinylPrefab == null) return;

        // 3. Instanciar vinilo
        GameObject newVinyl = Instantiate(vinylPrefab);
        newVinyl.transform.SetParent(null);

        // 4. Configurar VinylData
        VinylData vinylData = newVinyl.GetComponent<VinylData>();
        if (vinylData != null)
        {
            vinylData.trackID = metadataIndex + 1;

            if (vinylMetadatas != null && metadataIndex >= 0 && metadataIndex < vinylMetadatas.Length)
            {
                vinylData.metadata = vinylMetadatas[metadataIndex];
            }

            vinylData.ApplyCoverArt();
        }

        // 5. Acoplar al socket
        if (socketTriggers[targetSocketIndex] != null)
        {
            newVinyl.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
            socketTriggers[targetSocketIndex].SnapVinylToSocket(newVinyl, vinylData);
        }
    }
}