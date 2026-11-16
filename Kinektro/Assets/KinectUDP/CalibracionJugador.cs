using System.Collections.Generic;
using UnityEngine;

// Calibracion rapida por jugador: antes de que alguien nuevo use el Kinect, le hacemos
// mantener dos poses de referencia (por defecto "neutro" y "BrazosArriba", las mismas
// que ya tenes grabadas) y con eso corregimos sus lecturas en vivo para que se parezcan
// mas a las plantillas que ya existen en la biblioteca - sin necesitar que cada persona
// nueva grabe sus propias variantes de las 4 poses antes de poder jugar.
//
// Por que esto suma algo que los angulos solos no dan: los angulos ya son invariantes a
// tamano/posicion/orientacion (ver JointAngleCalculator), pero NO corrigen que dos
// personas tengan distinto rango de movimiento (cuanto pueden abrir el hombro, por
// ejemplo) ni distinta postura de "reposo" (como tienen el brazo cuando estan parados
// normales). Eso es una diferencia de comportamiento/flexibilidad individual, no de
// escala geometrica - por eso hace falta esta calibracion aparte.
//
// Metodo (por cada angulo, de forma independiente): un reescalado afin anclado en dos
// puntos conocidos -
//
//   ajustado = templateNeutral + (actual - personaNeutral) * escala
//   escala   = (templateExtremo - templateNeutral) / (personaExtremo - personaNeutral)
//
// Si para algun angulo la diferencia entre el neutral y el extremo DE LA PERSONA es
// muy chica (ej. las piernas, que no cambian entre "neutro" y "BrazosArriba"), no hay
// forma confiable de estimar una escala para ese angulo - ahi se usa escala = 1 (solo
// se corrige el desplazamiento/offset, no el rango).
public static class CalibracionJugador
{
    private const float DeltaMinimoParaEscalar = 10f;

    public static bool Calibrada { get; private set; } = false;

    private static readonly Dictionary<string, float> escala = new Dictionary<string, float>();
    private static readonly Dictionary<string, float> offset = new Dictionary<string, float>();

    // personaNeutral/personaExtremo: lo que se le midio a ESTA persona en cada pose de
    // referencia. templateNeutral/templateExtremo: el perfil (promedio) ya guardado en
    // la biblioteca para esas mismas poses (ver PoseMatching.ConstruirPerfiles).
    public static void Calibrar(
        Dictionary<string, float> personaNeutral,
        Dictionary<string, float> personaExtremo,
        Dictionary<string, float> templateNeutral,
        Dictionary<string, float> templateExtremo)
    {
        escala.Clear();
        offset.Clear();

        foreach (var kv in personaNeutral)
        {
            string angulo = kv.Key;
            float persN = kv.Value;
            if (!templateNeutral.TryGetValue(angulo, out float tplN)) continue;

            float esc = 1f;
            if (personaExtremo.TryGetValue(angulo, out float persE) &&
                templateExtremo.TryGetValue(angulo, out float tplE))
            {
                float deltaPersona = persE - persN;
                float deltaTemplate = tplE - tplN;
                if (Mathf.Abs(deltaPersona) >= DeltaMinimoParaEscalar)
                {
                    esc = deltaTemplate / deltaPersona;
                }
            }

            escala[angulo] = esc;
            offset[angulo] = tplN - persN * esc;
        }

        Calibrada = offset.Count > 0;
        Debug.Log($"[CalibracionJugador] Calibracion lista con {offset.Count} angulos ajustados.");
    }

    public static void Limpiar()
    {
        escala.Clear();
        offset.Clear();
        Calibrada = false;
    }

    // Devuelve un diccionario NUEVO con los angulos corregidos. Si todavia no se calibro
    // a nadie, devuelve los valores originales sin tocar (se comporta como siempre).
    public static Dictionary<string, float> Aplicar(Dictionary<string, float> actuales)
    {
        if (!Calibrada) return actuales;

        var resultado = new Dictionary<string, float>();
        foreach (var kv in actuales)
        {
            string angulo = kv.Key;
            if (escala.TryGetValue(angulo, out float esc) && offset.TryGetValue(angulo, out float off))
            {
                float ajustado = kv.Value * esc + off;
                resultado[angulo] = Mathf.Clamp(ajustado, 0f, 180f);
            }
            else
            {
                resultado[angulo] = kv.Value;
            }
        }
        return resultado;
    }
}
