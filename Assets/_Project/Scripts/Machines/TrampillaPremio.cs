using UnityEngine;

// La puertecilla por la que se saca el premio.
//
// Se abre sola cuando hay un peluche esperando dentro y se cierra cuando ya no
// queda ninguno. No hay que avisarla desde ningun sitio: mira lo que hay en el
// cajon y decide. Con eventos habria que acordarse de llamarla desde la garra,
// desde el trigger del cajon y desde donde recoge el jugador, y el dia que se
// anadiese un cuarto camino la puerta se quedaria abierta para siempre.
//
// Gira sobre su canto de arriba, como una gatera: es lo que hace que el peluche
// se pueda sacar por debajo agachandose.
public class TrampillaPremio : MonoBehaviour
{
    [Tooltip("La hoja que gira. El marco y el tirador se quedan quietos.")]
    public Transform hoja;

    [Tooltip("Donde mira para saber si hay premio, en el espacio de la maquina.")]
    public Vector3 zonaLocal;

    public float radioZona = 0.35f;

    [Tooltip("Capa de los peluches.")]
    public LayerMask capaPeluche;

    [Header("Movimiento")]
    public float anguloAbierta = 74f;

    [Tooltip("Hacia que lado se abre. Lo pone el builder segun donde este el "
             + "frente de la maquina.")]
    public float sentido = 1f;

    public float velocidad = 150f;

    [Tooltip("Lo que tarda en cerrarse despues de quedarse vacia. Un poco, para "
             + "que no se cierre en las narices del jugador.")]
    public float esperaCierre = 1.2f;

    Vector3 posCerrada;
    Quaternion rotCerrada;
    Vector3 bisagraLocal;
    Vector3 ejeLocal;

    float angulo = 0f;
    float objetivo = 0f;
    float vacioDesde = -1f;
    float proximaMirada = 0f;

    readonly Collider[] dentro = new Collider[4];

    public bool Abierta { get { return angulo > 1f; } }

    void Awake()
    {
        if (hoja == null)
        {
            enabled = false;
            return;
        }

        posCerrada = hoja.localPosition;
        rotCerrada = hoja.localRotation;

        // La bisagra es el canto de ARRIBA de la hoja, y el eje su lado ancho.
        // Los dos se miden de la propia hoja en vez de escribirlos: si manana
        // cambia el tamano de la puerta en Blender, esto sigue valiendo.
        Bounds caja = EnvolventeLocal(hoja, hoja.parent);

        bisagraLocal = new Vector3(caja.center.x, caja.max.y, caja.center.z);

        // El eje ancho es el mas largo de los dos horizontales.
        ejeLocal = caja.size.x >= caja.size.z ? Vector3.right : Vector3.forward;
    }

    void Update()
    {
        // Mirar si hay premio no hace falta cada fotograma: es una consulta a
        // PhysX por maquina y en la sala hay varias.
        if (Time.time >= proximaMirada)
        {
            proximaMirada = Time.time + 0.25f;
            Decidir();
        }

        Animar();
    }

    void Decidir()
    {
        Vector3 zona = transform.TransformPoint(zonaLocal);

        int cuantos = Physics.OverlapSphereNonAlloc(zona, radioZona, dentro,
                                                    capaPeluche,
                                                    QueryTriggerInteraction.Ignore);

        bool hay = false;

        for (int i = 0; i < cuantos; i++)
        {
            if (dentro[i] == null) continue;

            // Solo cuenta un peluche que ya se pueda coger. Los que siguen
            // dentro de la maquina estan en la misma capa, y sin esto la puerta
            // se abriria sola con la maquina llena.
            if (dentro[i].GetComponentInParent<PelucheRecogible>() == null) continue;

            hay = true;
            break;
        }

        if (hay)
        {
            vacioDesde = -1f;
            objetivo = anguloAbierta;
            return;
        }

        // Un poco de margen antes de cerrar: si se cierra en el mismo instante
        // en que el jugador coge el peluche, la puerta le pasa por delante de la
        // cara y parece que le ha pillado la mano.
        if (vacioDesde < 0f) vacioDesde = Time.time;

        if (Time.time - vacioDesde >= esperaCierre) objetivo = 0f;
    }

    void Animar()
    {
        if (Mathf.Approximately(angulo, objetivo)) return;

        angulo = Mathf.MoveTowards(angulo, objetivo, velocidad * Time.deltaTime);

        // Se gira alrededor de la bisagra, no del pivote de la hoja. Girando
        // sobre el pivote, la puerta se abriria atravesando su propio marco.
        Quaternion giro = Quaternion.AngleAxis(angulo * Mathf.Sign(sentido), ejeLocal);

        hoja.localRotation = giro * rotCerrada;
        hoja.localPosition = bisagraLocal + giro * (posCerrada - bisagraLocal);
    }

    static Bounds EnvolventeLocal(Transform que, Transform respectoA)
    {
        Bounds b = new Bounds(que.localPosition, Vector3.zero);
        bool primero = true;

        foreach (Renderer r in que.GetComponentsInChildren<Renderer>())
        {
            foreach (Vector3 esquina in Esquinas(r.bounds))
            {
                Vector3 p = respectoA.InverseTransformPoint(esquina);

                if (primero) { b = new Bounds(p, Vector3.zero); primero = false; }
                else b.Encapsulate(p);
            }
        }

        return b;
    }

    static Vector3[] Esquinas(Bounds b)
    {
        Vector3 n = b.min;
        Vector3 x = b.max;

        return new[]
        {
            new Vector3(n.x, n.y, n.z), new Vector3(x.x, n.y, n.z),
            new Vector3(n.x, x.y, n.z), new Vector3(x.x, x.y, n.z),
            new Vector3(n.x, n.y, x.z), new Vector3(x.x, n.y, x.z),
            new Vector3(n.x, x.y, x.z), new Vector3(x.x, x.y, x.z),
        };
    }
}
