using System.Collections.Generic;
using UnityEngine;

// Calcula angulos entre articulaciones a partir de las posiciones 3D que entrega
// IJointDataSource. Usamos angulos (no posiciones XYZ crudas) porque son invariantes
// a donde esta parada la persona respecto a la camara, a su altura y a su orientacion -
// lo que importa para reconocer una pose es "que tan doblado esta el codo", no
// "en que coordenada exacta del mundo esta la mano".
public static class JointAngleCalculator
{
    public struct AngleDef
    {
        public string nombre;
        public string jointA;
        public string vertice;
        public string jointB;

        public AngleDef(string nombre, string jointA, string vertice, string jointB)
        {
            this.nombre = nombre;
            this.jointA = jointA;
            this.vertice = vertice;
            this.jointB = jointB;
        }
    }

    // Set de angulos por defecto: brazos, hombros, piernas y caderas.
    // Para agregar/sacar un angulo de la comparacion, solo hay que editar esta lista -
    // el resto del sistema (grabado y reconocimiento) la usa automaticamente.
    public static readonly List<AngleDef> Definiciones = new List<AngleDef>
    {
        new AngleDef("CodoIzquierdo",   "ShoulderLeft",  "ElbowLeft", "WristLeft"),
        new AngleDef("CodoDerecho",     "ShoulderRight", "ElbowRight","WristRight"),
        new AngleDef("HombroIzquierdo", "SpineShoulder", "ShoulderLeft", "ElbowLeft"),
        new AngleDef("HombroDerecho",   "SpineShoulder", "ShoulderRight","ElbowRight"),
        new AngleDef("RodillaIzquierda","HipLeft",       "KneeLeft",  "AnkleLeft"),
        new AngleDef("RodillaDerecha",  "HipRight",      "KneeRight", "AnkleRight"),
        new AngleDef("CaderaIzquierda", "SpineBase",     "HipLeft",   "KneeLeft"),
        new AngleDef("CaderaDerecha",   "SpineBase",     "HipRight",  "KneeRight"),

        // Estos dos NO miden el doblez local del brazo (eso ya lo hacen Codo/Hombro) -
        // miden hacia donde apunta la muneca respecto de la CABEZA, tomando el pecho
        // como vertice. Sirven para distinguir "mano cerca de la cabeza" (angulo chico)
        // de "mano cerca del estomago/cintura" (angulo grande, cercano a 180), algo que
        // Codo+Hombro solos no pueden diferenciar si el doblez del codo resulta parecido
        // en ambos casos aunque el antebrazo apunte en direcciones opuestas.
        new AngleDef("AlturaManoIzquierda", "Head", "SpineShoulder", "WristLeft"),
        new AngleDef("AlturaManoDerecha",   "Head", "SpineShoulder", "WristRight"),
    };

    // Calcula todos los angulos definidos arriba usando la fuente de datos activa
    // (mock o Kinect real, es transparente). Si falta algun joint en este frame,
    // ese angulo puntual se omite en vez de romper todo el calculo.
    public static Dictionary<string, float> CalcularAngulos(IJointDataSource fuente)
    {
        var resultado = new Dictionary<string, float>();
        if (fuente == null) return resultado;

        foreach (var def in Definiciones)
        {
            if (fuente.TryGetJointPosition(def.jointA, out Vector3 a) &&
                fuente.TryGetJointPosition(def.vertice, out Vector3 v) &&
                fuente.TryGetJointPosition(def.jointB, out Vector3 b))
            {
                Vector3 dirA = a - v;
                Vector3 dirB = b - v;
                float angulo = Vector3.Angle(dirA, dirB); // 0 a 180 grados
                resultado[def.nombre] = angulo;
            }
        }
        return resultado;
    }
}
