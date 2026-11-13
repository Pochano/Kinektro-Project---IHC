using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Dibuja un esqueleto 2D (puntos + huesos) dentro de un RectTransform, proyectando
// X e Y de los joints 3D (se ignora la profundidad Z a proposito - es una vista de
// frente, como los visores clasicos de Kinect). Se auto-ajusta la escala cada frame
// segun el rango real de los joints detectados en ese instante, en vez de asumir un
// rango de coordenadas fijo - esto es importante porque el mock y el Kinect real NO
// usan la misma convencion de coordenadas (el mock usa alturas de mundo tipo 0.7-4.9,
// el Kinect real reporta valores relativos mucho mas chicos), asi que cualquier
// escala fija que funcione con uno se rompe con el otro.
public class SkeletonOverlay2D : MonoBehaviour
{
    [Header("Apariencia")]
    public float tamanoJoint = 14f;
    public float grosorHueso = 5f;
    public Color colorJoint = new Color(0.25f, 0.95f, 1f);
    public Color colorHueso = new Color(1f, 0.85f, 0.2f);
    [Range(0.6f, 0.95f)]
    public float margenRelativo = 0.85f; // cuanto del panel se usa (deja aire alrededor)

    private RectTransform contenedor;
    private IJointDataSource dataSource;

    private readonly List<(string, string)> huesos = new List<(string, string)>
    {
        ("SpineBase", "SpineMid"), ("SpineMid", "SpineShoulder"), ("SpineShoulder", "Neck"), ("Neck", "Head"),
        ("SpineShoulder", "ShoulderLeft"), ("ShoulderLeft", "ElbowLeft"), ("ElbowLeft", "WristLeft"), ("WristLeft", "HandLeft"),
        ("SpineShoulder", "ShoulderRight"), ("ShoulderRight", "ElbowRight"), ("ElbowRight", "WristRight"), ("WristRight", "HandRight"),
        ("SpineBase", "HipLeft"), ("HipLeft", "KneeLeft"), ("KneeLeft", "AnkleLeft"),
        ("SpineBase", "HipRight"), ("HipRight", "KneeRight"), ("KneeRight", "AnkleRight"),
    };

    private readonly Dictionary<string, RectTransform> puntosJoint = new Dictionary<string, RectTransform>();
    private readonly Dictionary<(string, string), RectTransform> lineasHueso = new Dictionary<(string, string), RectTransform>();

    public void Inicializar(RectTransform contenedorUI)
    {
        contenedor = contenedorUI;

        foreach (var par in huesos)
        {
            if (!lineasHueso.ContainsKey(par))
                lineasHueso[par] = CrearLinea(par.Item1 + "_" + par.Item2);

            if (!puntosJoint.ContainsKey(par.Item1)) puntosJoint[par.Item1] = CrearPunto(par.Item1);
            if (!puntosJoint.ContainsKey(par.Item2)) puntosJoint[par.Item2] = CrearPunto(par.Item2);
        }
    }

    public void SetDataSource(IJointDataSource fuente)
    {
        dataSource = fuente;
    }

    private RectTransform CrearPunto(string nombre)
    {
        var go = new GameObject("Joint_" + nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(contenedor, false);
        var img = go.GetComponent<Image>();
        img.color = colorJoint;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(tamanoJoint, tamanoJoint);
        go.SetActive(false);
        return rt;
    }

    private RectTransform CrearLinea(string nombre)
    {
        var go = new GameObject("Hueso_" + nombre, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(contenedor, false);
        var img = go.GetComponent<Image>();
        img.color = colorHueso;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        go.SetActive(false);
        return rt;
    }

    void Update()
    {
        if (dataSource == null || contenedor == null) return;

        // Junta las posiciones de todos los joints que usamos en los huesos.
        var posiciones = new Dictionary<string, Vector3>();
        var nombresUsados = new HashSet<string>();
        foreach (var par in huesos) { nombresUsados.Add(par.Item1); nombresUsados.Add(par.Item2); }

        foreach (var nombre in nombresUsados)
        {
            if (dataSource.TryGetJointPosition(nombre, out Vector3 pos))
                posiciones[nombre] = pos;
        }

        if (posiciones.Count == 0)
        {
            foreach (var p in puntosJoint.Values) p.gameObject.SetActive(false);
            foreach (var l in lineasHueso.Values) l.gameObject.SetActive(false);
            return;
        }

        // Auto-ajuste: encuentra el rango real de X e Y de los joints visibles AHORA,
        // y lo mapea para que llene el panel (dejando un margen). Minimo de rango para
        // no dividir por (casi) cero cuando la persona esta muy quieta o lejos.
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        foreach (var pos in posiciones.Values)
        {
            minX = Mathf.Min(minX, pos.x); maxX = Mathf.Max(maxX, pos.x);
            minY = Mathf.Min(minY, pos.y); maxY = Mathf.Max(maxY, pos.y);
        }
        float rangoX = Mathf.Max(maxX - minX, 0.05f);
        float rangoY = Mathf.Max(maxY - minY, 0.05f);
        float rango = Mathf.Max(rangoX, rangoY); // misma escala en X e Y para no deformar al muneco
        float centroX = (minX + maxX) * 0.5f;
        float centroY = (minY + maxY) * 0.5f;

        float panelW = contenedor.rect.width;
        float panelH = contenedor.rect.height;
        float escala = Mathf.Min(panelW, panelH) * margenRelativo / rango;

        Vector2 Proyectar(Vector3 p) => new Vector2((p.x - centroX) * escala, (p.y - centroY) * escala);

        foreach (var kv in puntosJoint)
        {
            bool visible = posiciones.ContainsKey(kv.Key);
            kv.Value.gameObject.SetActive(visible);
            if (visible) kv.Value.anchoredPosition = Proyectar(posiciones[kv.Key]);
        }

        foreach (var kv in lineasHueso)
        {
            var (a, b) = kv.Key;
            bool visible = posiciones.ContainsKey(a) && posiciones.ContainsKey(b);
            kv.Value.gameObject.SetActive(visible);
            if (!visible) continue;

            Vector2 pa = Proyectar(posiciones[a]);
            Vector2 pb = Proyectar(posiciones[b]);
            Vector2 diff = pb - pa;

            kv.Value.anchoredPosition = pa;
            kv.Value.sizeDelta = new Vector2(diff.magnitude, grosorHueso);
            float angulo = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
            kv.Value.localRotation = Quaternion.Euler(0, 0, angulo);
        }
    }
}
