using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class MusicLibraryManager : MonoBehaviour
{
    [Header("Configuración de UI & Prefabs")]
    public GameObject songCardPrefab;      // Prefab de SongCardUI
    public Transform contentGrid;          // Content_Grid (GridLayoutGroup)

    [Header("Conexión con la Estantería")]
    public ShelfManager shelfManager;     // Referencia al ShelfManager de la escena

    [Header("Referencias UI - Filtros y Búsqueda")]
    public Button pistasButton;
    public Button basesButton;
    public Button efectosButton;
    public TMP_InputField searchInputField;

    [Header("Referencias UI - Paginación")]
    public Button prevButton;
    public Button nextButton;
    public TextMeshProUGUI pageText;

    [Header("Ajustes de Paginación")]
    public int itemsPerPage = 6;

    // Listas internas de datos
    private List<VinylMetadata> allVinyls = new List<VinylMetadata>();
    private List<VinylMetadata> filteredVinyls = new List<VinylMetadata>();

    private int currentPage = 0;
    private string currentCategory = "Pistas";

    private void Start()
    {
        if (shelfManager == null)
        {
            shelfManager = FindObjectOfType<ShelfManager>();
        }

        // 1. Cargar metadatos de vinilos y poblar automáticamente Managers
        LoadVinylData();

        // 2. Asignar Listeners a Filtros
        if (pistasButton != null) pistasButton.onClick.AddListener(() => SelectCategory("Pistas"));
        if (basesButton != null) basesButton.onClick.AddListener(() => SelectCategory("Bases"));
        if (efectosButton != null) efectosButton.onClick.AddListener(() => SelectCategory("Efectos"));

        // 3. Listener a Búsqueda
        if (searchInputField != null) searchInputField.onValueChanged.AddListener(OnSearchChanged);

        // 4. Listeners a Paginación
        if (prevButton != null) prevButton.onClick.AddListener(PreviousPage);
        if (nextButton != null) nextButton.onClick.AddListener(NextPage);

        // 5. Renderizar categoría por defecto
        ApplyFiltersAndRender();
    }

    private void LoadVinylData()
    {
        allVinyls.Clear();

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets("t:VinylMetadata", new[] { "Assets/Data/Vinyls" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            VinylMetadata metadata = AssetDatabase.LoadAssetAtPath<VinylMetadata>(path);
            if (metadata != null)
            {
                allVinyls.Add(metadata);
            }
        }
#else
        VinylMetadata[] loaded = Resources.LoadAll<VinylMetadata>("Vinyls");
        allVinyls.AddRange(loaded);
#endif

        VinylMetadata[] metadataArray = allVinyls.ToArray();

        // Auto-poblar ShelfManager
        if (shelfManager != null)
        {
            shelfManager.vinylMetadatas = metadataArray;
            Debug.Log($"[MusicLibraryManager] ShelfManager auto-poblado con {metadataArray.Length} metadatos.");
        }

        // Auto-poblar DJ_Manager con los eventos FMOD
        DJ_Manager djManager = FindObjectOfType<DJ_Manager>();
        if (djManager != null)
        {
            djManager.AutoPopulateSongEventsFromMetadata(metadataArray);
        }
    }

    public void SelectCategory(string category)
    {
        currentCategory = category;
        currentPage = 0;
        ApplyFiltersAndRender();
    }

    private void OnSearchChanged(string query)
    {
        currentPage = 0;
        ApplyFiltersAndRender();
    }

    private void ApplyFiltersAndRender()
    {
        string searchQuery = searchInputField != null ? searchInputField.text.ToLower() : "";

        filteredVinyls = allVinyls.FindAll(vinyl =>
        {
            if (vinyl == null) return false;

            // Filtro por campo 'category'
            bool matchesCategory = string.Equals(vinyl.category, currentCategory, System.StringComparison.OrdinalIgnoreCase);

            // Si la categoría no está marcada explícitamente, soporte fallback por nombre de archivo/canción
            if (!matchesCategory && string.IsNullOrEmpty(vinyl.category))
            {
                if (currentCategory == "Bases") matchesCategory = vinyl.songTitle.ToLower().Contains("base");
                else if (currentCategory == "Efectos") matchesCategory = vinyl.songTitle.ToLower().Contains("fx");
                else matchesCategory = true;
            }

            bool matchesSearch = string.IsNullOrEmpty(searchQuery) ||
                                vinyl.songTitle.ToLower().Contains(searchQuery) ||
                                vinyl.artistName.ToLower().Contains(searchQuery);

            return matchesCategory && matchesSearch;
        });

        RenderPage();
    }

    private void RenderPage()
    {
        foreach (Transform child in contentGrid)
        {
            Destroy(child.gameObject);
        }

        int totalItems = filteredVinyls.Count;
        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)totalItems / itemsPerPage));

        currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1);

        int startIndex = currentPage * itemsPerPage;
        int endIndex = Mathf.Min(startIndex + itemsPerPage, totalItems);

        for (int i = startIndex; i < endIndex; i++)
        {
            VinylMetadata metadata = filteredVinyls[i];
            GameObject cardObj = Instantiate(songCardPrefab, contentGrid);

            SongCardUI cardUI = cardObj.GetComponent<SongCardUI>();
            if (cardUI != null)
            {
                cardUI.SetupCard(metadata, this);
            }
        }

        if (pageText != null) pageText.text = $"Página {currentPage + 1} de {totalPages}";
        if (prevButton != null) prevButton.interactable = currentPage > 0;
        if (nextButton != null) nextButton.interactable = currentPage < totalPages - 1;
    }

    public void NextPage() { currentPage++; RenderPage(); }
    public void PreviousPage() { currentPage--; RenderPage(); }

    public void SpawnVinylFromCard(VinylMetadata metadata)
    {
        if (shelfManager == null)
        {
            Debug.LogError("[MusicLibraryManager] No se encontró la referencia a ShelfManager.");
            return;
        }

        int index = allVinyls.IndexOf(metadata);

        if (index != -1)
        {
            shelfManager.SpawnVinylInSocket(index);
            Debug.Log($"[MusicLibraryManager] Solicitado spawn en Socket para '{metadata.songTitle}' (Índice: {index}).");
        }

        if (TutorialManager.Instance != null) TutorialManager.Instance.OnTrackSelectedFromLibrary();

    }

}