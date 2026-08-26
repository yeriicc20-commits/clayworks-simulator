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

        if (!ApuntandoAqui()) return;

        bool encendidas = Bombilla.AlgunaEncendida();

        InteractionUI.Prompt(
            AjustesControles.NombreTecla(
                AjustesControles.Tecla(AjustesControles.Accion.Usar))
            + (encendidas ? ": apagar la luz" : ": encender la luz"));

        if (!AjustesControles.Pulsando(AjustesControles.Accion.Usar)) return;

        Bombilla.EncenderTodas(!encendidas);

        lado = encendidas ? -1f : 1f;
    }

    // Un rayo desde el centro de la pantalla, y si toca esto, es que lo miras.
    //
    // Antes esto era distancia mas angulo, y estaba mal de dos maneras: la
    // direccion salia de los PIES del jugador y se comparaba con hacia donde
    // mira la CAMARA, metro y medio mas arriba, asi que con un interruptor a la
    // altura del pecho no se cumplia nunca. Y para arreglarlo hacia falta que
    // cameraTransform tuviera un componente Camera, cosa que nadie garantiza.
    //
    // El rayo no tiene ninguno de esos problemas y ademas es lo que el jugador
    // cree que esta haciendo: apuntar. De regalo, una pared por delante tapa el
    // interruptor, que con el angulo se pulsaba a traves de ella.
    bool ApuntandoAqui()
    {
        if (CursorMode.FreeCursor) return false;
        if (!BuscarCamara()) return false;

        RaycastHit toque;

        if (!Physics.Raycast(camara.position, camara.forward, out toque, alcance,
                             ~0, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        // Vale tambien si toca una pieza hija, como la tecla.
        return toque.collider.transform == transform
               || toque.collider.transform.IsChildOf(transform);
    }

    bool BuscarCamara()
    {
        if (camara != null) return true;

        FirstPersonController fpc = FindAnyObjectByType<FirstPersonController>();

        // Por este orden: la que el jugador tenga puesta, y si no la principal.
        // No se pide el componente Camera, solo el sitio desde el que se mira:
        // pedirlo era justo lo que dejaba esto sin funcionar.
        if (fpc != null && fpc.cameraTransform != null) camara = fpc.cameraTransform;
        else if (Camera.main != null) camara = Camera.main.transform;

        return camara != null;
    }

    void Mover()
    {
        if (tecla == null) return;

        Quaternion objetivo = teclaReposo * Quaternion.Euler(lado * recorrido, 0f, 0f);

        tecla.localRotation = Quaternion.Slerp(tecla.localRotation, objetivo,
                                               1f - Mathf.Exp(-suavizado * Time.deltaTime));
    }
}
