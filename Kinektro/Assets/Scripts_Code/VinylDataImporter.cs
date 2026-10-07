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
        string mp3FolderPath = @"C:\Users\Pochano\Desktop\Kinektro\Kinektro\F_Audio\Assets";
        string outputPath = "Assets/Data/Vinyls";

        if (!Directory.Exists(mp3FolderPath))
        {
            Debug.LogError($"No se encontró la carpeta de MP3s en: {mp3FolderPath}");
            return;
        }

        if (!Directory.Exists(outputPath)) 
        {
            Directory.CreateDirectory(outputPath);
        }

        string[] filePaths = Directory.GetFiles(mp3FolderPath, "*.mp3");

        if (filePaths.Length == 0)
        {
            Debug.LogWarning("No se encontraron archivos .mp3 en: " + mp3FolderPath);
            return;
        }

        int count = 0;
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
                
                // Si el asset ya existe, lo eliminamos primero para forzar la regeneración limpia
                if (AssetDatabase.LoadAssetAtPath<VinylMetadata>(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                VinylMetadata newMetadata = ScriptableObject.CreateInstance<VinylMetadata>();
                newMetadata.songTitle = title;
                newMetadata.artistName = artist;
                newMetadata.baseBPM = bpm;
                
                // Asignación directa del evento sin la subcarpeta /Tracks/
                newMetadata.fmodAudioEvent = EventReference.Find($"event:/{fileName}");

                if (file.Tag.Pictures.Length > 0)
                {
                    var picData = file.Tag.Pictures[0].Data.Data;
                    Texture2D coverTex = new Texture2D(2, 2);
                    coverTex.LoadImage(picData);

                    string coverPath = $"{outputPath}/{fileName}_Cover.png";
                    
                    System.IO.File.WriteAllBytes(coverPath, coverTex.EncodeToPNG());
                    AssetDatabase.Refresh();

                    Texture2D importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(coverPath);
                    newMetadata.coverArt = importedTex;
                }

                AssetDatabase.CreateAsset(newMetadata, assetPath);
                count++;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error procesando {fileName}: {e.Message}");
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"¡Listo! Se regeneraron {count} assets. Revisa que la ruta ahora diga 'event:/<Nombre>'.");
    }
}
#endif