#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using FMODUnity;

public class VinylDataImporter : EditorWindow
{
    [MenuItem("DJ Tools/Auto-Generate Vinyl Metadata Assets")]
    public static void GenerateVinylData()
    {
        // Ruta raíz de tus MP3s tageados
        string mp3RootFolderPath = @"C:\Users\Pochano\Desktop\Kinektro\MP3_Tags";
        string outputRootPath = "Assets/Data/Vinyls";

        string[] categories = new string[] { "Pistas", "Bases", "Efectos" };

        if (!Directory.Exists(mp3RootFolderPath))
        {
            Debug.LogError($"[VinylDataImporter] No se encontró la carpeta raíz de MP3s en: {mp3RootFolderPath}");
            return;
        }

        int totalCount = 0;

        foreach (string category in categories)
        {
            string categoryAudioPath = Path.Combine(mp3RootFolderPath, category);
            string outputPath = $"{outputRootPath}/{category}";

            if (!Directory.Exists(categoryAudioPath))
            {
                Directory.CreateDirectory(categoryAudioPath);
                continue;
            }

            if (!Directory.Exists(outputPath)) 
            {
                Directory.CreateDirectory(outputPath);
            }

            string[] filePaths = Directory.GetFiles(categoryAudioPath, "*.mp3");

            foreach (string path in filePaths)
            {
                string fileName = Path.GetFileNameWithoutExtension(path);

                try
                {
                    var file = TagLib.File.Create(path);

                    string title = string.IsNullOrEmpty(file.Tag.Title) ? fileName : file.Tag.Title;
                    string artist = string.IsNullOrEmpty(file.Tag.FirstPerformer) ? "Artista Desconocido" : file.Tag.FirstPerformer;
                    float bpm = file.Tag.BeatsPerMinute > 0 ? (float)file.Tag.BeatsPerMinute : 120f;

                    string assetPath = $"{outputPath}/{fileName}_Data.asset";
                    
                    if (AssetDatabase.LoadAssetAtPath<VinylMetadata>(assetPath) != null)
                    {
                        AssetDatabase.DeleteAsset(assetPath);
                    }

                    VinylMetadata newMetadata = ScriptableObject.CreateInstance<VinylMetadata>();
                    newMetadata.songTitle = title;
                    newMetadata.artistName = artist;
                    newMetadata.baseBPM = bpm;
                    newMetadata.category = category; // Asigna "Pistas", "Bases" o "Efectos"
                    
                    newMetadata.fmodAudioEvent = EventReference.Find($"event:/{fileName}");

                    if (file.Tag.Pictures.Length > 0)
                    {
                        var picData = file.Tag.Pictures[0].Data.Data;
                        Texture2D coverTex = new Texture2D(2, 2);
                        coverTex.LoadImage(picData);

                        string coverPath = $"{outputPath}/{fileName}_Cover.png";
                        File.WriteAllBytes(coverPath, coverTex.EncodeToPNG());
                        AssetDatabase.Refresh();

                        Texture2D importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(coverPath);
                        newMetadata.coverArt = importedTex;
                    }

                    AssetDatabase.CreateAsset(newMetadata, assetPath);
                    totalCount++;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error procesando {fileName} en {category}: {e.Message}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[VinylDataImporter] ¡Listo! Se generaron {totalCount} assets de metadatos organizados por subcarpetas.");
    }
}
#endif