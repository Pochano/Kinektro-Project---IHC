using UnityEngine;
using UnityEngine.UI;

public class MusicLibraryManager : MonoBehaviour
{
    [Header("Colección de Canciones")]
    public VinylMetadata[] allVinylMetadatas;

    [Header("Referencias de UI")]
    public Transform scrollContentContainer; // Content del ScrollRect
    public GameObject songCardPrefab;        // Prefab con SongCardUI

    [Header("Referencia a la Estantería 3D")]
    [Tooltip("Asigna el GameObject que tiene el script ShelfManager")]
    public ShelfManager shelfManager;

    private void Start()
    {
        // Si no está asignado manualmente, busca el ShelfManager en la escena
        if (shelfManager == null)
        {
            shelfManager = FindObjectOfType<ShelfManager>();
        }

        PopulateLibrary();
    }

    public void PopulateLibrary()
    {
        if (scrollContentContainer == null || songCardPrefab == null) return;

        // Limpiar elementos existentes
        foreach (Transform child in scrollContentContainer)
        {
            Destroy(child.gameObject);
        }

        if (allVinylMetadatas == null || allVinylMetadatas.Length == 0) return;

        // Generar tarjetas en el carrusel
        foreach (var metadata in allVinylMetadatas)
        {
            if (metadata == null) continue;

            GameObject cardObj = Instantiate(songCardPrefab, scrollContentContainer);
            SongCardUI cardUI = cardObj.GetComponent<SongCardUI>();
            if (cardUI != null)
            {
                cardUI.SetupCard(metadata, this);
            }
        }
    }

    /// <summary>
    /// Se ejecuta al pulsar una tarjeta de la UI para instanciar el disco en la estantería.
    /// </summary>
    public void SpawnVinylFromCard(VinylMetadata metadata)
    {
        if (metadata == null) return;

        if (shelfManager != null)
        {
            // Busca la posición del metadato dentro del arreglo para determinar su índice de socket (0 a 5)
            int targetSocketIndex = -1;
            if (allVinylMetadatas != null)
            {
                for (int i = 0; i < allVinylMetadatas.Length; i++)
                {
                    if (allVinylMetadatas[i] == metadata)
                    {
                        targetSocketIndex = i;
                        break;
                    }
                }
            }

            // Si no coincide con el orden del arreglo, se usa un índice por defecto
            if (targetSocketIndex < 0) targetSocketIndex = 0;

            // Delegar la instanciación al ShelfManager en las bases/sockets dinámicos
            shelfManager.SpawnVinylInSocket(targetSocketIndex);
            Debug.Log($"[MusicLibrary] Solicitado spawn de '{metadata.songTitle}' en la base/socket {targetSocketIndex + 1}.");
        }
        else
        {
            Debug.LogError("[MusicLibrary] No se encontró la referencia a 'ShelfManager' en la escena.");
        }
    }
}