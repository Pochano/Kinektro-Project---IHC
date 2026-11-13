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
}
