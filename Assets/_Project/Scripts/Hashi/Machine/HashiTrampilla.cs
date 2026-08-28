using UnityEngine;

namespace Hashi
{
    // La puertecilla por la que se saca el premio.
    //
    // Hace lo mismo que TrampillaPremio en la maquina de garra, y con el mismo
    // truco de giro, pero mirando cajas en vez de peluches: aquella busca
    // PlushItem, que mi premio no es, asi que no se podia reutilizar tal cual.
    //
    // Se abre sola cuando hay una caja esperando dentro y se cierra cuando ya no
    // queda ninguna. Nadie tiene que avisarla: mira lo que hay en la bandeja y
    // decide. Con eventos habria que acordarse de llamarla desde el detector,
    // desde el generador y desde donde recoge el jugador, y el dia que se
    // anadiese un cuarto camino la puerta se quedaria abierta para siempre.
    //
    // Gira sobre su canto de ARRIBA, como una gatera: asi el premio se saca por
    // debajo y la hoja no le da al jugador en la mano al cerrarse.
    public class HashiTrampilla : MonoBehaviour
    {
        [Tooltip("La hoja que gira. El marco se queda quieto.")]
        [SerializeField] Transform hoja;

        [Tooltip("Donde mira para saber si hay premio, en el espacio de la puerta.")]
        [SerializeField] Vector3 zonaLocal = new Vector3(0f, -0.05f, 0.20f);

        [SerializeField] float radioZona = 0.30f;

        [Tooltip("Capa de los premios.")]
        [SerializeField] LayerMask capaPremio;

        [Header("Movimiento")]
        [SerializeField] float anguloAbierta = 72f;

        [Tooltip("Hacia que lado se abre. Lo pone el constructor.")]
        [SerializeField] float sentido = 1f;

        [SerializeField] float velocidad = 150f;

        [Tooltip("Lo que tarda en cerrarse al quedarse vacia. Un poco, para que "
                 + "no se cierre en las narices del jugador.")]
        [SerializeField] float esperaCierre = 1.5f;

        Vector3 posCerrada;
        Quaternion rotCerrada;
        Vector3 bisagraLocal;
        Vector3 ejeLocal;

        float angulo;
        float objetivo;
        float vacioDesde = -1f;
        float proximaMirada;

        readonly Collider[] dentro = new Collider[4];

        public bool Abierta => angulo > 1f;

        void Awake()
        {
            if (hoja == null)
            {
                enabled = false;
                return;
            }

            posCerrada = hoja.localPosition;
            rotCerrada = hoja.localRotation;

            // La bisagra es el canto de ARRIBA de la hoja, y el eje su lado
            // ancho. Los dos se miden de la propia hoja en vez de escribirlos:
            // si manana cambia el tamano de la boca, esto sigue valiendo.
            Bounds caja = EnvolventeLocal(hoja, hoja.parent);

            bisagraLocal = new Vector3(caja.center.x, caja.max.y, caja.center.z);
            ejeLocal = caja.size.x >= caja.size.z ? Vector3.right : Vector3.forward;
        }

        void Update()
        {
            // Preguntarle a la fisica no hace falta cada fotograma: es una
            // consulta por maquina y en la sala puede haber varias.
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
                                                        capaPremio,
                                                        QueryTriggerInteraction.Ignore);

            bool hay = false;

            for (int i = 0; i < cuantos; i++)
            {
                if (dentro[i] == null) continue;
                if (dentro[i].GetComponentInParent<PrizeController>() == null) continue;

                hay = true;
                break;
            }

            if (hay)
            {
                vacioDesde = -1f;
                objetivo = anguloAbierta;
                return;
            }

            if (vacioDesde < 0f) vacioDesde = Time.time;

            if (Time.time - vacioDesde >= esperaCierre) objetivo = 0f;
        }

        void Animar()
        {
            if (Mathf.Approximately(angulo, objetivo)) return;

            angulo = Mathf.MoveTowards(angulo, objetivo, velocidad * Time.deltaTime);

            // Se gira alrededor de la BISAGRA, no del pivote de la hoja. Sobre
            // su propio pivote, la puerta se abriria atravesando su marco.
            Quaternion giro = Quaternion.AngleAxis(angulo * Mathf.Sign(sentido), ejeLocal);

            hoja.localRotation = giro * rotCerrada;
            hoja.localPosition = bisagraLocal + giro * (posCerrada - bisagraLocal);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(transform.TransformPoint(zonaLocal), radioZona);
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
}
