using UnityEngine;

namespace Hashi
{
    // Las tres camaras de la maquina, con las teclas 1, 2 y 3.
    //
    // Son tres SITIOS, no tres camaras encendidas a la vez. Tener tres Camera
    // en la escena y ir apagandolas cuesta el triple en la GPU y ademas corta el
    // cambio en seco; con una sola que se desplaza, el cambio de plano se ve
    // moverse y queda mucho mejor por menos.
    public class CameraController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] Camera camara;

        [Tooltip("En orden: frontal (1), en angulo (2), cenital (3).")]
        [SerializeField] Transform[] posiciones = new Transform[3];

        [Tooltip("A donde mira. Normalmente, el centro de las barras.")]
        [SerializeField] Transform puntoDeMira;

        [Header("Movimiento")]
        [Tooltip("Lo que tarda en llegar al sitio nuevo. 0 = corte seco.")]
        [Range(0f, 1f)] [SerializeField] float suavizado = 0.18f;

        [Header("Seguimiento de la garra")]
        [Tooltip("La camara se corre un poco hacia donde esta la garra. Ayuda a "
                 + "ver donde va a caer sin marear.")]
        [SerializeField] bool seguirGarra = true;

        [SerializeField] Transform garra;

        [Tooltip("Cuanto acompana. 1 seria pegarse a la garra y marea; 0,15 es "
                 + "suficiente para que se note que la camara esta viva.")]
        [Range(0f, 1f)] [SerializeField] float intensidadSeguimiento = 0.15f;

        [Tooltip("Tope del desplazamiento, en metros.")]
        [Range(0f, 1f)] [SerializeField] float desplazamientoMaximo = 0.12f;

        int indice;
        Vector3 velocidadPos;
        Vector3 centroReferencia;

        public int Indice => indice;

        void Awake()
        {
            if (camara == null) camara = GetComponent<Camera>();
            if (camara == null) camara = Camera.main;

            centroReferencia = puntoDeMira != null ? puntoDeMira.position : Vector3.zero;

            Cambiar(0, true);
        }

        void Update()
        {
            int tecla = InputReader.Camara();
            if (tecla > 0) Cambiar(tecla - 1);
        }

        // En LateUpdate: la garra se mueve en FixedUpdate y las posiciones se
        // interpolan durante Update. Moviendo la camara antes, se ve la garra un
        // fotograma por detras y da un temblor muy raro.
        void LateUpdate()
        {
            if (camara == null) return;

            Transform destino = PosicionValida(indice);
            if (destino == null) return;

            Vector3 objetivo = destino.position;

            if (seguirGarra && garra != null)
            {
                Vector3 desvio = garra.position - centroReferencia;
                desvio.y = 0f;                                    // solo a los lados
                desvio *= intensidadSeguimiento;
                desvio = Vector3.ClampMagnitude(desvio, desplazamientoMaximo);
                objetivo += desvio;
            }

            if (suavizado <= 0.001f)
            {
                camara.transform.position = objetivo;
            }
            else
            {
                camara.transform.position = Vector3.SmoothDamp(
                    camara.transform.position, objetivo, ref velocidadPos, suavizado);
            }

            // La orientacion se saca del punto de mira, no del sitio: asi mover
            // un sitio de camara no obliga a reorientarlo tambien a mano.
            Quaternion rot = puntoDeMira != null
                ? Quaternion.LookRotation(puntoDeMira.position - camara.transform.position)
                : destino.rotation;

            camara.transform.rotation = suavizado <= 0.001f
                ? rot
                : Quaternion.Slerp(camara.transform.rotation, rot,
                                   1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, suavizado)));
        }

        public void Cambiar(int nuevo, bool instantaneo = false)
        {
            if (posiciones == null || posiciones.Length == 0) return;

            indice = Mathf.Clamp(nuevo, 0, posiciones.Length - 1);

            if (!instantaneo || camara == null) return;

            Transform t = PosicionValida(indice);
            if (t == null) return;

            camara.transform.position = t.position;
            camara.transform.rotation = puntoDeMira != null
                ? Quaternion.LookRotation(puntoDeMira.position - t.position)
                : t.rotation;
        }

        // Pasa a la siguiente. Lo usa el boton CAMERA de la interfaz.
        public void Siguiente()
        {
            if (posiciones == null || posiciones.Length == 0) return;
            Cambiar((indice + 1) % posiciones.Length);
        }

        Transform PosicionValida(int i)
        {
            if (posiciones == null || i < 0 || i >= posiciones.Length) return null;
            return posiciones[i];
        }
    }
}
