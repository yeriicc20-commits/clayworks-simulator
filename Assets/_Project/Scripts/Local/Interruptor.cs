using UnityEngine;

// El interruptor de la luz: enciende y apaga todas las bombillas del local.
//
// Todas a la vez y no la de al lado. Con un interruptor por bombilla habria que
// ir dando a cada uno para cerrar el local, y ademas no hay forma de saber cual
// manda cual sin probarlos todos.
public class Interruptor : MonoBehaviour
{
    [Tooltip("Desde cuan lejos se puede pulsar.")]
    public float alcance = 2.2f;

    [Tooltip("Cuanto hay que estar mirandolo. 1 = justo al centro.")]
    [Range(0.5f, 1f)] public float precisionMirada = 0.9f;

    [Tooltip("La tecla, que gira sobre su borde de abajo al pulsarla.")]
    public Transform tecla;

    [Tooltip("Cuanto bascula la tecla, en grados.")]
    public float recorrido = 11f;

    [Tooltip("Lo que tarda en bascular.")]
    public float suavizado = 16f;

    Transform jugador;
    Camera ojo;

    Quaternion teclaReposo;
    float lado = -1f;

    void Start()
    {
        if (tecla != null) teclaReposo = tecla.localRotation;

        // Arranca coherente con lo que haya: si las luces estan encendidas, la
        // tecla tiene que estar pulsada hacia arriba.
        lado = Bombilla.AlgunaEncendida() ? 1f : -1f;
    }

    void Update()
    {
        Mover();

        if (!AlAlcance()) return;

        bool encendidas = Bombilla.AlgunaEncendida();

        InteractionUI.Prompt(
            AjustesControles.NombreTecla(
                AjustesControles.Tecla(AjustesControles.Accion.Usar))
            + (encendidas ? ": apagar la luz" : ": encender la luz"));

        if (!AjustesControles.Pulsando(AjustesControles.Accion.Usar)) return;

        Bombilla.EncenderTodas(!encendidas);

        lado = encendidas ? -1f : 1f;
    }

    // Cerca y mirandolo. Solo por distancia, dos interruptores en paredes
    // opuestas de un pasillo estrecho responderian los dos a la vez.
    bool AlAlcance()
    {
        if (jugador == null)
        {
            FirstPersonController fpc = FindAnyObjectByType<FirstPersonController>();
            if (fpc == null) return false;

            jugador = fpc.transform;
            ojo = fpc.cameraTransform != null
                ? fpc.cameraTransform.GetComponent<Camera>()
                : Camera.main;
        }

        if (CursorMode.FreeCursor) return false;

        Vector3 hacia = transform.position - jugador.position;
        if (hacia.sqrMagnitude > alcance * alcance) return false;

        Transform camara = ojo != null ? ojo.transform : jugador;

        return Vector3.Dot(camara.forward, hacia.normalized) >= precisionMirada;
    }

    void Mover()
    {
        if (tecla == null) return;

        Quaternion objetivo = teclaReposo * Quaternion.Euler(lado * recorrido, 0f, 0f);

        tecla.localRotation = Quaternion.Slerp(tecla.localRotation, objetivo,
                                               1f - Mathf.Exp(-suavizado * Time.deltaTime));
    }
}
