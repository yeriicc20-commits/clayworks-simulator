using UnityEngine;

// Script de pruebas del movimiento. Escribia en consola cada frame mientras
// mantuvieras WASD, y eso llego a dejar el log en 125 MB y a tapar los mensajes
// que hacian falta para depurar. Se deja el componente para no romper la escena,
// pero sin escribir nada. Ponle avisos: true si necesitas volver a mirarlo.
public class TestInput : MonoBehaviour
{
    public bool avisos = false;

    void Update()
    {
        if (!avisos) return;

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (h != 0f || v != 0f) Debug.Log("Horizontal: " + h + " | Vertical: " + v);
    }
}
