using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Un angulo guardado dentro de una plantilla. JsonUtility (el serializador nativo de
// Unity, sin librerias externas) no sabe serializar Dictionary directamente, por eso
// usamos una lista de este struct en vez de un Dictionary<string,float> a secas.
[Serializable]
public struct AnguloGuardado
{
    public string nombre;
    public float valor;
}

// Una pose guardada: su nombre y el promedio de angulos capturado por PoseRecorder.
[Serializable]
public class PoseTemplate
{
    public string nombrePose;
    public List<AnguloGuardado> angulos = new List<AnguloGuardado>();
    public int repeticionesUsadas;

    public Dictionary<string, float> ADiccionario()
    {
        var dic = new Dictionary<string, float>();
        foreach (var a in angulos) dic[a.nombre] = a.valor;
        return dic;
    }

    public static PoseTemplate DesdeDiccionario(string nombrePose, Dictionary<string, float> angulos, int repeticiones)
    {
        var t = new PoseTemplate { nombrePose = nombrePose, repeticionesUsadas = repeticiones };
        foreach (var kv in angulos)
            t.angulos.Add(new AnguloGuardado { nombre = kv.Key, valor = kv.Value });
        return t;
    }
}

// Contenedor de todas las poses guardadas - es literalmente lo que se escribe/lee del
// archivo JSON en disco.
[Serializable]
public class PoseLibraryData
{
    public List<PoseTemplate> poses = new List<PoseTemplate>();
}

// Carga y guardado de la biblioteca de poses. Se guarda dentro de Assets/PoseData/
// (no en persistentDataPath) a proposito: asi queda versionada junto con el resto del
// proyecto y se puede compartir entre PC A y PC B haciendo commit + push + pull, igual
// que el resto del codigo de este puente.
public static class PoseLibrary
{
    private static string Ruta => Path.Combine(Application.dataPath, "PoseData", "poses.json");

    public static PoseLibraryData Cargar()
    {
        if (!File.Exists(Ruta))
            return new PoseLibraryData();

        string json = File.ReadAllText(Ruta);
        var datos = JsonUtility.FromJson<PoseLibraryData>(json);
        return datos ?? new PoseLibraryData();
    }

    public static void Guardar(PoseLibraryData datos)
    {
        string carpeta = Path.GetDirectoryName(Ruta);
        if (!Directory.Exists(carpeta)) Directory.CreateDirectory(carpeta);

        string json = JsonUtility.ToJson(datos, true);
        File.WriteAllText(Ruta, json);
        Debug.Log($"[PoseLibrary] Guardado en: {Ruta}");
    }

    // Agrega o reemplaza (por nombre) una pose y guarda de inmediato en disco.
    // Si ya existe una pose con ese mismo nombre, queda sobreescrita: no hace falta
    // borrarla antes, solo volver a grabar con el mismo nombre.
    public static void GuardarPose(PoseTemplate pose)
    {
        var datos = Cargar();
        datos.poses.RemoveAll(p => p.nombrePose == pose.nombrePose);
        datos.poses.Add(pose);
        Guardar(datos);
    }

    // Borra (por nombre) una pose guardada y persiste el cambio en disco.
    // Devuelve true si habia una pose con ese nombre y se borro, false si no existia.
    public static bool EliminarPose(string nombrePose)
    {
        var datos = Cargar();
        int removidas = datos.poses.RemoveAll(p => p.nombrePose == nombrePose);
        if (removidas > 0) Guardar(datos);
        return removidas > 0;
    }
}
