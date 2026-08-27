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


    [Tooltip("La tecla, que gira sobre su borde de abajo al pulsarla.")]
    public Transform tecla;

    [Tooltip("Cuanto bascula la tecla, en grados.")]
    public float recorrido = 11f;

    [Tooltip("Lo que tarda en bascular.")]
    public float suavizado = 16f;

    Transform camara;

    Quaternion teclaReposo;
    float lado = -1f;

    static bool contado = false;

    void Start()
    {
        if (tecla != null) teclaReposo = tecla.localRotation;

        // Arranca coherente con lo que haya: si las luces estan encendidas, la
        // tecla tiene que estar pulsada hacia arriba.
        lado = Bombilla.AlgunaEncendida() ? 1f : -1f;

        // Una linea y una sola vez en toda la partida. Sin ella, "no esta
        // puesto", "no lo estoy mirando" y "no funciona" son el mismo sintoma.
        if (contado) return;
        contado = true;

        Debug.Log("[Interruptor] Colocado. Bombillas en el local: "
                  + Bombilla.Todas.Count + ". Apuntale y pulsa "
                  + AjustesControles.NombreTecla(
                      AjustesControles.Tecla(AjustesControles.Accion.Usar))
                  + ".", this);
    }

    void Update()
    {
        Mover();

        if (!Apuntado.A(transform, ref camara, alcance)) return;

        bool encendidas = Bombilla.AlgunaEncendida();

        InteractionUI.Prompt(
            AjustesControles.NombreTecla(
                AjustesControles.Tecla(AjustesControles.Accion.Usar))
            + (encendidas ? ": apagar la luz" : ": encender la luz"));

        if (!AjustesControles.Pulsando(AjustesControles.Accion.Usar)) return;

        Bombilla.EncenderTodas(!encendidas);

        lado = encendidas ? -1f : 1f;
    }

    void Mover()
    {
        if (tecla == null) return;

        Quaternion objetivo = teclaReposo * Quaternion.Euler(lado * recorrido, 0f, 0f);

        tecla.localRotation = Quaternion.Slerp(tecla.localRotation, objetivo,
                                               1f - Mathf.Exp(-suavizado * Time.deltaTime));
    }
}
