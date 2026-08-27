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

    // Se instala solo en el interruptor que ya esta puesto en la escena.
    //
    // El de la escena no es el prefab: es el FBX arrastrado a mano, o sea la
    // malla pelada. No lleva este componente ni collider, asi que no daba
    // cartel ni encendia nada, y por fuera se ve exactamente igual que uno
    // que si funciona.
    //
    // Se busca por el nombre. No es fino, pero es lo unico que tienen en
    // comun una malla suelta y un prefab, y la alternativa era pedir que se
    // borre y se vuelva a colocar el bueno.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AlArrancar()
    {
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t == null) continue;
            if (!t.name.StartsWith("Interruptor")) continue;

            // Ni si ya lo lleva, ni si es una pieza de uno que si lo lleva.
            if (t.GetComponentInParent<Interruptor>() != null) continue;

            Instalar(t.gameObject);
        }
    }

    static void Instalar(GameObject go)
    {
        Interruptor script = go.AddComponent<Interruptor>();

        foreach (Transform hijo in go.GetComponentsInChildren<Transform>(true))
        {
            if (hijo.name != "Tecla") continue;

            script.tecla = hijo;
            break;
        }

        // Sin collider no hay a que apuntar: el rayo le pasa por encima y no
        // saldria el cartel por mucho que lo mires.
        if (go.GetComponentInChildren<Collider>(true) != null) return;

        Bounds caja = new Bounds();
        bool primero = true;

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (primero)
            {
                caja = r.bounds;
                primero = false;
                continue;
            }

            caja.Encapsulate(r.bounds);
        }

        if (primero) return;

        BoxCollider col = go.AddComponent<BoxCollider>();

        col.center = go.transform.InverseTransformPoint(caja.center);
        col.size = caja.size;
    }

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

        Debug.Log("[Interruptor] Listo en '" + name + "'. Luces en el local: "
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
            + " para interactuar");

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
