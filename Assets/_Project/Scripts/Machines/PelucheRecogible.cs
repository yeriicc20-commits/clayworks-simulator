using UnityEngine;

// Deja recoger del suelo un peluche que ya ha salido de la maquina.
//
// Se lo pone PlushDropZone al peluche premiado. No lo llevan todos los peluches
// a proposito: los que estan dentro de la maquina son del dueno, no del
// jugador, y con esto puesto se podrian sacar a traves del cristal.
//
// Hay que estar AGACHADO y mirandolo. Lo de mirarlo es lo que impide cogerlo a
// traves de una pared o del cristal de la maquina: el rayo sale del centro de la
// pantalla y tiene que llegar hasta el peluche sin tropezar antes con nada.
public class PelucheRecogible : MonoBehaviour
{
    [Tooltip("A que distancia se puede coger, en metros. Corto: hay que "
             + "agacharse al lado, no señalarlo desde la otra punta.")]
    public float alcance = 1.6f;

    PlushItem peluche;
    Transform jugador;
    Camera ojo;
    bool avisoPuesto = false;

    void Start()
    {
        peluche = GetComponent<PlushItem>();

        FirstPersonController fpc = FindAnyObjectByType<FirstPersonController>();
        if (fpc != null) jugador = fpc.transform;

        ojo = Camera.main;
    }

    void Update()
    {
        if (jugador == null || peluche == null) { Ocultar(); return; }

        // Ya lo lleva puesto: aqui no hay nada que ofrecer.
        if (PelucheEnMano.Instancia != null && PelucheEnMano.Instancia.Sostiene(peluche))
        {
            Ocultar();
            return;
        }

        if (Vector3.Distance(transform.position, jugador.position) > alcance)
        {
            Ocultar();
            return;
        }

        if (!Mirandolo()) { Ocultar(); return; }

        if (!FirstPersonController.IsCrouching)
        {
            Avisar("Agachate para coger el peluche");
            return;
        }

        if (PlayerCarry.Busy)
        {
            Avisar(PlayerCarry.BusyMessage);
            return;
        }

        Avisar("Pulsa E para coger el peluche");

        if (Input.GetKeyDown(KeyCode.E))
        {
            Ocultar();
            PelucheEnMano.Coger(peluche);
        }
    }

    bool Mirandolo()
    {
        if (ojo == null) ojo = Camera.main;
        if (ojo == null) return false;

        Ray rayo = ojo.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        RaycastHit golpe;
        if (!Physics.Raycast(rayo, out golpe, alcance + 1f, ~0, QueryTriggerInteraction.Ignore))
            return false;

        return golpe.transform == transform || golpe.transform.IsChildOf(transform);
    }

    void Avisar(string texto)
    {
        avisoPuesto = true;
        InteractionUI.Prompt(texto);
    }

    void Ocultar()
    {
        if (!avisoPuesto) return;

        avisoPuesto = false;
        InteractionUI.Hide();
    }

    void OnDisable()
    {
        Ocultar();
    }
}
