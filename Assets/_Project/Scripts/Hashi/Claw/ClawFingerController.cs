using System;
using UnityEngine;

namespace Hashi
{
    // Las dos pinzas: bisagra de verdad, motor de verdad, colision de verdad.
    //
    // Nada de girar los dedos escribiendo su rotacion. Un transform que se mete
    // dentro de una caja no la empuja: PhysX ve dos cuerpos solapados y los
    // separa de golpe, asi que la caja sale disparada o no se entera. Con una
    // bisagra con motor, el dedo empuja con un par concreto, la caja le hace
    // fuerza en contra, y lo que pasa lo decide el motor de fisica. Es de donde
    // sale la mecanica entera del hashi-watashi: el par que la pinza mete en una
    // esquina es lo que hace girar la caja.
    //
    // El par va en newton-metro, y conviene saber leer el numero. El brazo mide
    // 11,5 cm mas el codo, y toca la caja a unos 10 cm de la bisagra, asi que
    // 0,45 N.m son unos 4,5 N en el punto de contacto. Una caja de 400 g pesa
    // 3,9 N, o sea que el empujon es del orden de su propio peso: la mueve, la
    // va girando poco a poco, y no la manda contra el cristal. Subirlo a 2 N.m
    // la haria volcar de un golpe y el juego dejaria de tener gracia.
    public class ClawFingerController : MonoBehaviour
    {
        [Header("Piezas")]
        [Tooltip("Cuerpo de la garra al que se enganchan las dos bisagras.")]
        [SerializeField] Rigidbody clawBody;

        [SerializeField] HingeJoint leftFinger;
        [SerializeField] HingeJoint rightFinger;

        [Header("Angulos (grados)")]
        [Tooltip("Pinzas cerradas. Negativo = puntas hacia dentro.")]
        [Range(-60f, 10f)] [SerializeField] float closedAngle = -20f;

        [Tooltip("Pinzas abiertas. Es la posicion de reposo.")]
        [Range(0f, 80f)] [SerializeField] float openAngle = 20f;

        [Header("Velocidades (grados por segundo)")]
        [Range(10f, 500f)] [SerializeField] float openSpeed = 90f;

        [Tooltip("Cerrar rapido da un golpe seco que puede volcar la caja de "
                 + "una; cerrar despacio la empuja poco a poco. Cambia la "
                 + "manera de jugar mas que ninguna otra cosa.")]
        [Range(10f, 500f)] [SerializeField] float closeSpeed = 120f;

        [Header("Fuerza")]
        [Tooltip("Par del motor de cada bisagra, en newton-metro. Es la fuerza "
                 + "con la que la pinza empuja la caja.")]
        [Range(0.05f, 10f)] [SerializeField] float gripForce = 0.45f;

        [Header("Deteccion de cierre")]
        [Tooltip("Margen para dar el angulo por alcanzado.")]
        [Range(0.5f, 10f)] [SerializeField] float margenAngulo = 2f;

        [Tooltip("Si las pinzas dejan de moverse este rato, se da el cierre por "
                 + "terminado aunque no lleguen al angulo: quiere decir que hay "
                 + "una caja en medio, que es justo lo que se busca.")]
        [Range(0.05f, 2f)] [SerializeField] float tiempoAtasco = 0.25f;

        [Range(0.5f, 30f)] [SerializeField] float velocidadMinimaMovimiento = 4f;

        // ------------------------------------------------------------ interno

        bool cerrando;
        float quietasDesde = -1f;

        float anguloIzqAnterior, anguloDerAnterior;
        float velIzq, velDer;

        // ------------------------------------------------------------ eventos

        // Salta cuando una pinza toca el premio, con el impulso del golpe.
        public event Action<float> AlTocarPremio;

        // ------------------------------------------------------------ lectura

        public float AnguloIzquierda => leftFinger != null ? leftFinger.angle : 0f;
        public float AnguloDerecha => rightFinger != null ? rightFinger.angle : 0f;
        public float Apertura => (AnguloIzquierda + AnguloDerecha) * 0.5f;
        public bool Cerrando => cerrando;
        public float FuerzaActual => gripForce;

        // Cerradas del todo, o paradas contra algo. Las dos cosas valen para dar
        // el paso por terminado: si estan paradas es que hay caja en medio.
        public bool CierreTerminado
        {
            get
            {
                if (!cerrando) return false;
                if (Apertura <= closedAngle + margenAngulo) return true;
                return quietasDesde >= 0f && Time.time - quietasDesde >= tiempoAtasco;
            }
        }

        public bool AperturaTerminada
        {
            get
            {
                if (cerrando) return false;
                if (Apertura >= openAngle - margenAngulo) return true;
                return quietasDesde >= 0f && Time.time - quietasDesde >= tiempoAtasco;
            }
        }

        // Estan apretando algo: han dejado de moverse sin llegar al tope.
        public bool Apretando =>
            cerrando && quietasDesde >= 0f
            && Time.time - quietasDesde >= tiempoAtasco
            && Apertura > closedAngle + margenAngulo;

        // ----------------------------------------------------------- arranque

        void Awake()
        {
            Configurar();
            Abrir();
        }

        void OnValidate()
        {
            // Cerrado siempre por debajo de abierto, o los limites de la bisagra
            // salen del reves y las pinzas se quedan clavadas sin decir nada.
            if (openAngle <= closedAngle) openAngle = closedAngle + 1f;
        }

        // Deja las dos bisagras listas. Se llama al arrancar y cada vez que
        // cambia la dificultad, porque el par es un ajuste de dificultad.
        public void Configurar()
        {
            // Los brazos abren de lado a lado, en paralelo a las barras: uno se
            // pone en un extremo de la caja y otro en el otro, que es como se ve
            // en las fotos de la maquina y como se juega de verdad.
            //
            // Puede sorprender, porque empujar a lo largo de las barras parece
            // el eje inutil. No lo es: apretando los dos extremos de una caja
            // que no esta centrada, un brazo toca antes que el otro y lo que
            // sale es un par de GIRO sobre la vertical. La caja se va poniendo
            // de lado hasta que su lado corto queda cruzando las barras, y
            // entonces ya no llega a las dos y cae. Es la tecnica de "girarla",
            // y por eso en todas las fotos la caja esta torcida.
            //
            // Los ejes van al reves el uno del otro a proposito: asi un mismo
            // signo de velocidad cierra los dos brazos y no hay que llevar la
            // cuenta de cual es cual en cada sitio.
            //
            // El signo importa y es facil equivocarse. Girando sobre +Z, un
            // punto que cuelga hacia abajo se va hacia +X; asi que el brazo
            // izquierdo, para ABRIR hacia la izquierda, necesita -Z.
            Preparar(leftFinger, new Vector3(0f, 0f, -1f));
            Preparar(rightFinger, new Vector3(0f, 0f, 1f));
        }

        void Preparar(HingeJoint j, Vector3 eje)
        {
            if (j == null) return;

            j.connectedBody = clawBody;
            j.axis = eje;
            j.useLimits = true;
            j.limits = new JointLimits
            {
                min = closedAngle,
                max = openAngle,
                bounciness = 0f,
                bounceMinVelocity = 0f,
                contactDistance = 0f,
            };

            j.useSpring = false;
            j.useMotor = true;
            j.enableCollision = false;   // la pinza no choca con su propio cuerpo

            // La bisagra no se rompe pase lo que pase. Si se rompiera, el dedo se
            // quedaria suelto dentro de la maquina y no hay como recuperarlo.
            j.breakForce = Mathf.Infinity;
            j.breakTorque = Mathf.Infinity;

            Rigidbody rb = j.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.solverIterations = 24;
                rb.solverVelocityIterations = 10;
                rb.maxDepenetrationVelocity = 1.5f;
            }
        }

        // ------------------------------------------------------------- ordenes

        public void Cerrar()
        {
            cerrando = true;
            quietasDesde = -1f;
            Motor(-closeSpeed);
        }

        public void Abrir()
        {
            cerrando = false;
            quietasDesde = -1f;
            Motor(openSpeed);
        }

        void Motor(float velocidad)
        {
            Aplicar(leftFinger, velocidad);
            Aplicar(rightFinger, velocidad);
        }

        void Aplicar(HingeJoint j, float velocidad)
        {
            if (j == null) return;

            j.useMotor = true;
            j.motor = new JointMotor
            {
                targetVelocity = velocidad,
                force = gripForce,
                freeSpin = false,
            };
        }

        // El par de esta partida, que lo decide el mando del cuadro trasero.
        //
        // Se escribe en caliente y sin reconfigurar la bisagra entera: cambiar
        // los limites a mitad de un cierre da un tiron. Solo el motor.
        public void PonerFuerza(float par)
        {
            gripForce = Mathf.Max(0.01f, par);
            Motor(cerrando ? -closeSpeed : openSpeed);
        }

        public void AplicarDificultad(DifficultySettings d)
        {
            if (d == null) return;

            gripForce = d.clawGripForce;
            closeSpeed = d.clawCloseSpeed;

            Configurar();
            Motor(cerrando ? -closeSpeed : openSpeed);
        }

        // ------------------------------------------------------------ vigilancia

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            float iz = AnguloIzquierda;
            float de = AnguloDerecha;

            velIzq = Mathf.Abs(iz - anguloIzqAnterior) / Mathf.Max(dt, 0.0001f);
            velDer = Mathf.Abs(de - anguloDerAnterior) / Mathf.Max(dt, 0.0001f);

            anguloIzqAnterior = iz;
            anguloDerAnterior = de;

            // Se miran las DOS a la vez: mientras una siga avanzando, el cierre
            // no ha terminado aunque la otra este apoyada en la caja.
            bool quietas = velIzq < velocidadMinimaMovimiento
                           && velDer < velocidadMinimaMovimiento;

            if (quietas)
            {
                if (quietasDesde < 0f) quietasDesde = Time.time;
            }
            else
            {
                quietasDesde = -1f;
            }
        }

        // Lo llaman los reles de contacto que llevan pegados los dos dedos.
        public void AvisarContacto(float impulso)
        {
            AlTocarPremio?.Invoke(impulso);
        }
    }
}
