using System.Collections.Generic;
using UnityEngine;

// Logica de comparacion de poses, compartida entre PoseIdentifier (reconocimiento en
// segundo plano) y PoseMapperUI (la pantalla visual).
//
// IMPORTANTE: se usa la diferencia MAXIMA entre los angulos comparados, no el promedio.
// Con el promedio, un solo angulo bien distinto (p.ej. el hombro, al pasar de "brazos
// abajo" a "brazos arriba") quedaba diluido entre los demas angulos que coinciden sin
// que signifique nada - por ejemplo las piernas, que no cambian para nada entre dos
// poses que solo difieren en los brazos (y en el mock ni siquiera se mueven). Eso hacia
// que el promedio bajara de la tolerancia y la pose se reconociera igual aunque el
// usuario tuviera los brazos en una posicion totalmente distinta a la grabada.
// Con el maximo, alcanza con que UN angulo este muy lejos del grabado para descartar
// esa pose - tiene que coincidir bien en TODOS los angulos comparados, no "en promedio".
//
// ACTUALIZACION: ademas de la comparacion original (un solo ejemplar, tolerancia fija
// en grados), mas abajo se agrega EncontrarMasCercanaNormalizada, que compara contra el
// PROMEDIO de todas las variantes grabadas de una pose (ver PoseMapperUI, boton
// "AGREGAR COMO VARIANTE") usando el desvio estandar observado de cada angulo como
// unidad de medida en vez de una tolerancia fija en grados - asi un angulo que varia
// mucho entre personas distintas (por contextura, proporciones, etc.) se vuelve
// automaticamente mas permisivo, sin tener que ajustar la tolerancia a mano por
// articulacion.
public static class PoseMatching
{
    // Solo compara los angulos presentes en AMBOS lados - si un joint no se pudo leer
    // en este frame puntual, ese angulo se ignora en vez de penalizar la comparacion.
    public static float DiferenciaMaxima(Dictionary<string, float> actuales, Dictionary<string, float> plantilla)
    {
        float maximo = 0f;
        int cuenta = 0;
        foreach (var kv in plantilla)
        {
            if (actuales.TryGetValue(kv.Key, out float valorActual))
            {
                float diff = Mathf.Abs(valorActual - kv.Value);
                if (diff > maximo) maximo = diff;
                cuenta++;
            }
        }
        return cuenta == 0 ? float.MaxValue : maximo;
    }

    // Recorre toda la biblioteca y devuelve la plantilla con menor diferencia maxima,
    // junto con esa diferencia. nombre == null si la biblioteca esta vacia.
    public static void EncontrarMasCercana(Dictionary<string, float> actuales, PoseLibraryData libreria, out string nombre, out float diferencia)
    {
        nombre = null;
        diferencia = float.MaxValue;
        if (libreria == null) return;

        foreach (var plantilla in libreria.poses)
        {
            float d = DiferenciaMaxima(actuales, plantilla.ADiccionario());
            if (d < diferencia)
            {
                diferencia = d;
                nombre = plantilla.nombrePose;
            }
        }
    }

    // ------------------------------------------------------------------
    // Version normalizada: en vez de una tolerancia fija en grados igual para
    // los 8 angulos, calcula cuanto varia CADA angulo en particular entre las
    // variantes grabadas de una misma pose y mide la distancia en desvios
    // estandar (z-score) en vez de grados crudos. Un angulo que mostro poca
    // variacion entre las muestras exige mas parecido; uno que vario mucho
    // (por ejemplo por diferencias de contextura entre personas distintas) se
    // vuelve mas permisivo automaticamente, sin tocar ningun numero a mano.
    // ------------------------------------------------------------------

    // Piso minimo de desvio estandar por angulo, en grados. Evita dos
    // problemas: dividir por cero cuando una pose todavia tiene una sola
    // muestra (desvio = 0), y que un angulo que por pura casualidad salio
    // IDENTICO en las pocas muestras grabadas se vuelva imposible de
    // matchear despues. Con este piso, una pose de una sola muestra se
    // comporta igual que antes (equivalente a ~20 grados de tolerancia con
    // toleranciaDesviaciones = 2.5 en PoseIdentifier/PoseMapperUI).
    public const float DesviacionMinimaGrados = 8f;

    public class PerfilPose
    {
        public string nombre;
        public Dictionary<string, float> promedios = new Dictionary<string, float>();
        public Dictionary<string, float> desviaciones = new Dictionary<string, float>();
    }

    // Agrupa todas las plantillas de la libreria por nombre de pose y calcula,
    // para cada angulo, el promedio y el desvio estandar observado entre todas
    // las variantes guardadas con ese nombre.
    public static List<PerfilPose> ConstruirPerfiles(PoseLibraryData libreria)
    {
        var perfiles = new List<PerfilPose>();
        if (libreria == null) return perfiles;

        var porNombre = new Dictionary<string, List<Dictionary<string, float>>>();
        foreach (var plantilla in libreria.poses)
        {
            if (!porNombre.TryGetValue(plantilla.nombrePose, out var lista))
            {
                lista = new List<Dictionary<string, float>>();
                porNombre[plantilla.nombrePose] = lista;
            }
            lista.Add(plantilla.ADiccionario());
        }

        foreach (var kv in porNombre)
        {
            var perfil = new PerfilPose { nombre = kv.Key };
            var muestras = kv.Value;

            var nombresAngulos = new HashSet<string>();
            foreach (var m in muestras)
                foreach (var angulo in m.Keys)
                    nombresAngulos.Add(angulo);

            foreach (var nombreAngulo in nombresAngulos)
            {
                var valores = new List<float>();
                foreach (var m in muestras)
                    if (m.TryGetValue(nombreAngulo, out float v)) valores.Add(v);

                if (valores.Count == 0) continue;

                float promedio = 0f;
                foreach (var v in valores) promedio += v;
                promedio /= valores.Count;

                float varianza = 0f;
                foreach (var v in valores) varianza += (v - promedio) * (v - promedio);
                varianza /= valores.Count;
                float desvio = Mathf.Sqrt(varianza);

                perfil.promedios[nombreAngulo] = promedio;
                perfil.desviaciones[nombreAngulo] = Mathf.Max(desvio, DesviacionMinimaGrados);
            }

            perfiles.Add(perfil);
        }

        return perfiles;
    }

    // Distancia normalizada contra UN perfil: el maximo, entre todos los
    // angulos comparables, de "a cuantos desvios estandar esta la lectura
    // actual del promedio de esa pose". Sigue siendo un maximo (el angulo
    // mas lejos manda, misma idea que DiferenciaMaxima) pero cada angulo
    // mide con su propia vara.
    public static float DistanciaNormalizadaMaxima(Dictionary<string, float> actuales, PerfilPose perfil)
    {
        float maximo = 0f;
        int cuenta = 0;
        foreach (var kv in perfil.promedios)
        {
            if (actuales.TryGetValue(kv.Key, out float valorActual))
            {
                float desvio = perfil.desviaciones[kv.Key];
                float z = Mathf.Abs(valorActual - kv.Value) / desvio;
                if (z > maximo) maximo = z;
                cuenta++;
            }
        }
        return cuenta == 0 ? float.MaxValue : maximo;
    }

    // Igual que EncontrarMasCercana, pero agrupando variantes por nombre y
    // midiendo en desvios estandar en vez de grados crudos. zScoreMax es el
    // resultado a comparar contra una tolerancia en desvios estandar (por
    // ejemplo 2.0 a 2.5), no contra grados.
    public static void EncontrarMasCercanaNormalizada(Dictionary<string, float> actuales, PoseLibraryData libreria, out string nombre, out float zScoreMax)
    {
        nombre = null;
        zScoreMax = float.MaxValue;

        var perfiles = ConstruirPerfiles(libreria);
        foreach (var perfil in perfiles)
        {
            float z = DistanciaNormalizadaMaxima(actuales, perfil);
            if (z < zScoreMax)
            {
                zScoreMax = z;
                nombre = perfil.nombre;
            }
        }
    }

    // Igual que DistanciaNormalizadaMaxima, pero ademas devuelve el NOMBRE del angulo
    // que produjo la peor distancia (el "culpable" de que esa pose no matchee). Pensado
    // para debuguear a mano: muestra exactamente que articulacion esta fallando.
    public static float DistanciaNormalizadaMaximaDetallada(Dictionary<string, float> actuales, PerfilPose perfil, out string peorAngulo)
    {
        float maximo = 0f;
        peorAngulo = null;
        int cuenta = 0;
        foreach (var kv in perfil.promedios)
        {
            if (actuales.TryGetValue(kv.Key, out float valorActual))
            {
                float desvio = perfil.desviaciones[kv.Key];
                float z = Mathf.Abs(valorActual - kv.Value) / desvio;
                if (z > maximo) { maximo = z; peorAngulo = kv.Key; }
                cuenta++;
            }
        }
        if (cuenta == 0) { peorAngulo = null; return float.MaxValue; }
        return maximo;
    }
}
