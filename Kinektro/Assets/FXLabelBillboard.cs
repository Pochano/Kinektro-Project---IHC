using UnityEngine;

/// <summary>
/// Hace que un texto 3D (TextMeshPro world-space) siempre mire hacia la
/// camara principal, para que sea legible sin importar desde que lado
/// o angulo lo vea el jugador en VR. Funciona tanto en Play Mode como
/// en el Editor (para poder verificar visualmente sin correr el juego).
/// </summary>
[ExecuteAlways]
public class FXLabelBillboard : MonoBehaviour
{
    void LateUpdate()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Copiar la rotacion de la camara directamente es mas robusto que
        // calcular LookRotation a mano: evita el caso degenerado en que la
        // direccion hacia la camara queda paralela al vector "up" (eso
        // puede producir una rotacion invalida / NaN y romper el render).
        transform.rotation = cam.transform.rotation;
    }
}
