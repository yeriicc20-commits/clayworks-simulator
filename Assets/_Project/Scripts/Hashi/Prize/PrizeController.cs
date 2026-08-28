using System;
using UnityEngine;

namespace Hashi
{
    // El premio: una caja apoyada sobre las dos barras.
    //
    // Aqui NO hay ni una linea que mueva la caja. Ni un teletransporte, ni una
    // animacion de caida, ni un "si la garra ha tocado, dala por ganada". La
    // caja tiene masa, rozamiento y un centro de masas, y cae solo cuando la
    // fisica dice que ha perdido el equilibrio. Todo lo demas seria mentira y se
    // nota jugando: el jugador aprende a leer una fisica de verdad en dos
    // partidas, y una falsa tambien.
    //
    // Lo unico que se toca a mano es la colocacion inicial (Reposicionar), y
    // solo cuando alguien la pide expresamente.
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class PrizeController : MonoBehaviour
    {
        public enum Estado
        {
            EnLasBarras,   // apoyado arriba, la partida sigue
            Cayendo,       // ha pasado por el hueco y todavia no ha llegado
            Recogido       // dentro de la bandeja y dado por bueno
        }

        [Header("Fisica de la caja")]
        [Tooltip("Masa en kilos.")]
        [SerializeField] float mass = 0.40f;

        [Tooltip("Tamano en metros. X va a lo largo de las barras, Y es lo que "
                 + "tiene que pasar por el hueco al volcar, y Z es lo que cruza "
                 + "las dos barras y la sujeta.")]
        [SerializeField] Vector3 size = new Vector3(0.17f, 0.115f, 0.22f);

        [Tooltip("Centro de masas respecto al centro de la caja, en metros. "
                 + "Bajarlo la vuelve mucho mas estable.")]
        [SerializeField] Vector3 centerOfMass = Vector3.zero;

        [Tooltip("Rozamiento contra las barras y contra las pinzas.")]
        [Range(0.02f, 1.2f)] [SerializeField] float friction = 0.42f;

        [Tooltip("Rebote. El carton no bota, dejalo casi a cero.")]
        [Range(0f, 1f)] [SerializeField] float bounciness = 0.02f;

        [Header("Amortiguacion")]
        [Range(0f, 2f)] [SerializeField] float linearDamping = 0.02f;
        [Range(0f, 2f)] [SerializeField] float angularDamping = 0.06f;

        [Header("Calidad de la simulacion")]
        [Tooltip("Iteraciones del solver solo para esta caja. Una caja en "
                 + "equilibrio sobre dos cilindros es de los casos que peor "
                 + "lleva PhysX con los valores por defecto: tiembla y se cae "
                 + "sola. Subirlo aqui cuesta poco porque es un solo cuerpo.")]
        [Range(6, 60)] [SerializeField] int solverIterations = 24;

        [Range(2, 40)] [SerializeField] int solverVelocityIterations = 10;

        [Tooltip("Tope a la velocidad con la que PhysX separa dos cuerpos que se "
                 + "han metido el uno en el otro. Sin tope, un pellizco de la "
                 + "pinza dispara la caja contra el techo.")]
        [Range(0.2f, 10f)] [SerializeField] float maxDepenetrationVelocity = 1.5f;

        [Header("Quietud")]
        [Tooltip("Por debajo de esta velocidad se considera parada.")]
        [SerializeField] float umbralQuietud = 0.02f;

        [Tooltip("Cuanto tiene que llevar parada para darla por asentada.")]
        [SerializeField] float tiempoQuietud = 0.35f;

        [Header("Depuracion")]
        [SerializeField] bool mostrarGizmos = true;

        // --------------------------------------------------------- referencias

        Rigidbody rb;
        BoxCollider caja;
        Transform malla;
        PhysicsMaterial material;
        float quietoDesde = -1f;

        // Los valores "de fabrica" de esta caja, antes de que la dificultad los
        // toque. Se guardan una vez y no se vuelven a escribir. Sin esto, pasar
        // de Facil a Dificil y volver no devolveria los numeros originales: cada
        // cambio multiplicaria sobre el anterior y a la tercera la caja pesaria
        // ocho kilos sin que nadie entendiera por que.
        float masaBase;
        float rozamientoBase;
        Vector3 comBase;
        bool baseTomada;

        // ------------------------------------------------------------- eventos

        // El golpe contra una barra, con el impulso, para poder sonar mas fuerte
        // o mas flojo segun el golpe.
        public event Action<float> AlGolpearBarra;

        // Una pinza ha tocado la caja.
        public event Action<float> AlGolpearGarra;

        public event Action<Estado> AlCambiarEstado;

        // ------------------------------------------------------------- lectura

        public Rigidbody Cuerpo => rb;
        public BoxCollider Caja => caja;
        public Vector3 Tamano => size;
        public float Masa => mass;
        public Estado EstadoActual { get; private set; } = Estado.EnLasBarras;

        // Cuanto se ha desviado de estar derecha, en grados. 0 = plana, 90 = de
        // canto. Es la medida de "cuanto llevo avanzado" de la jugada.
        public float Inclinacion => Vector3.Angle(transform.up, Vector3.up);

        // Centro de masas en coordenadas de mundo. Lo que decide si vuelca.
        public Vector3 CentroDeMasasMundo =>
            rb != null ? rb.worldCenterOfMass : transform.position;

        public Bounds Envolvente =>
            caja != null ? caja.bounds : new Bounds(transform.position, size);

        public bool EstaQuieta =>
            quietoDesde >= 0f && Time.time - quietoDesde >= tiempoQuietud;

        // ------------------------------------------------------------ arranque

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            caja = GetComponent<BoxCollider>();
            if (transform.childCount > 0) malla = transform.GetChild(0);

            GuardarBase();
            Aplicar();
        }

        void GuardarBase()
        {
            if (baseTomada) return;

            masaBase = mass;
            rozamientoBase = friction;
            comBase = centerOfMass;
            baseTomada = true;
        }

        void OnValidate()
        {
            // En el editor tambien, para poder cambiar el tamano y verlo.
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (caja == null) caja = GetComponent<BoxCollider>();
            if (malla == null && transform.childCount > 0) malla = transform.GetChild(0);

            size = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y),
                               Mathf.Max(0.01f, size.z));

            if (!Application.isPlaying) AplicarForma();
        }

        void OnDestroy()
        {
            // El material se crea por instancia; si no se borra, se van
            // acumulando uno por cada caja generada.
            if (material != null) Destroy(material);
        }

        // Vuelca todos los ajustes sobre los componentes. Se puede llamar en
        // caliente: la dificultad lo hace al cambiar de preajuste.
        public void Aplicar()
        {
            AplicarForma();

            if (rb == null) return;

            rb.mass = mass;
            rb.linearDamping = linearDamping;
            rb.angularDamping = angularDamping;
            rb.centerOfMass = centerOfMass;
            rb.solverIterations = solverIterations;
            rb.solverVelocityIterations = solverVelocityIterations;
            rb.maxDepenetrationVelocity = maxDepenetrationVelocity;

            // Continua y contra todo: la caja al caer por el hueco pasa muy
            // cerca de las dos barras, y con deteccion discreta se cuela por
            // dentro de una barra en un fotograma malo.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (caja == null) return;

            if (material == null)
            {
                material = new PhysicsMaterial("Premio (instancia)");
                material.hideFlags = HideFlags.DontSave;
            }

            material.dynamicFriction = friction;

            // Estatico algo por encima del dinamico, como en la vida: es lo que
            // hace que la caja se quede clavada hasta que la empujas de verdad,
            // en vez de ir deslizandose sola sobre las barras.
            material.staticFriction = friction * 1.2f;
            material.bounciness = bounciness;
            material.frictionCombine = PhysicsMaterialCombine.Average;
            material.bounceCombine = PhysicsMaterialCombine.Minimum;

            caja.sharedMaterial = material;
        }

        void AplicarForma()
        {
            if (caja != null)
            {
                caja.size = size;
                caja.center = Vector3.zero;
            }

            // La malla va dentro y escalada; el collider se queda sin escalar.
            if (malla != null)
            {
                malla.localPosition = Vector3.zero;
                malla.localRotation = Quaternion.identity;
                malla.localScale = size;
            }
        }

        // Copia una definicion de premio entera.
        public void Aplicar(PrizeDefinition def)
        {
            if (def == null) return;

            size = def.size;
            mass = def.mass;
            linearDamping = def.linearDamping;
            angularDamping = def.angularDamping;
            centerOfMass = def.centerOfMassOffset;
            friction = def.friction;
            bounciness = def.bounciness;

            // La caja nueva trae sus propios valores de fabrica.
            masaBase = mass;
            rozamientoBase = friction;
            comBase = centerOfMass;
            baseTomada = true;

            Aplicar();
            Pintar(def.color, def.ilustracion);
        }

        // La dificultad no sustituye a la caja, la corrige: multiplica su masa y
        // su rozamiento y le desplaza el centro de masas. Asi una caja ligera
        // sigue siendo la mas ligera en Extremo, solo que todas cuestan mas.
        // Sustituyendo los valores, las cinco cajas serian la misma y sobrarian
        // cuatro.
        public void AplicarDificultad(DifficultySettings d)
        {
            if (d == null) return;

            GuardarBase();

            mass = Mathf.Max(0.01f, masaBase * d.prizeMass);
            friction = Mathf.Clamp(rozamientoBase * d.prizeFriction, 0.02f, 1.2f);
            centerOfMass = comBase + d.centerOfMassOffset;

            Aplicar();
        }

        public void Pintar(Color color, Texture2D ilustracion = null)
        {
            if (malla == null) return;

            Renderer r = malla.GetComponent<Renderer>();
            if (r == null) return;

            // Material por instancia: si se tocara el compartido, todas las
            // cajas del proyecto cambiarian de color a la vez.
            Material m = Application.isPlaying ? r.material : r.sharedMaterial;
            if (m == null) return;

            m.SetColor("_BaseColor", color);
            m.color = color;

            if (ilustracion != null) m.SetTexture("_BaseMap", ilustracion);
        }

        // Unico sitio donde se mueve la caja a mano. Se usa al generarla y en el
        // boton de reiniciar; nunca durante una jugada.
        public void Reposicionar(Vector3 posicion, Quaternion rotacion)
        {
            if (rb == null) rb = GetComponent<Rigidbody>();

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            transform.SetPositionAndRotation(posicion, rotacion);

            // Sincroniza tambien el cuerpo, o al reanudar la fisica la caja
            // aparece un fotograma en el sitio viejo.
            rb.position = posicion;
            rb.rotation = rotacion;

            CambiarEstado(Estado.EnLasBarras);
            quietoDesde = -1f;
        }

        public void Despertar()
        {
            if (rb != null) rb.WakeUp();
        }

        public void CambiarEstado(Estado nuevo)
        {
            if (EstadoActual == nuevo) return;

            EstadoActual = nuevo;
            AlCambiarEstado?.Invoke(nuevo);
        }

        // ---------------------------------------------------------- vigilancia

        void FixedUpdate()
        {
            if (rb == null) return;

            bool quieta = rb.linearVelocity.sqrMagnitude < umbralQuietud * umbralQuietud
                          && rb.angularVelocity.sqrMagnitude < umbralQuietud * umbralQuietud;

            if (quieta)
            {
                if (quietoDesde < 0f) quietoDesde = Time.time;
            }
            else
            {
                quietoDesde = -1f;
            }
        }

        void OnCollisionEnter(Collision c)
        {
            float impulso = c.impulse.magnitude;

            if (impulso < 0.001f) return;

            int capa = c.gameObject.layer;

            if (capa == HashiLayers.Barras) AlGolpearBarra?.Invoke(impulso);
            else if (capa == HashiLayers.Garra) AlGolpearGarra?.Invoke(impulso);
        }

        // -------------------------------------------------------------- gizmos

        void OnDrawGizmosSelected()
        {
            if (!mostrarGizmos) return;

            // El bulto de la caja.
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, size);

            // El centro de masas, que es lo que de verdad hay que mirar cuando
            // una caja no vuelca como se espera.
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 1f);
            Gizmos.DrawSphere(centerOfMass, Mathf.Min(size.x, size.y, size.z) * 0.12f);

            // La vertical del centro de masas: mientras caiga entre las dos
            // barras, la caja aguanta; en cuanto pasa de una, vuelca.
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 com = transform.TransformPoint(centerOfMass);
            Gizmos.DrawLine(com, com + Vector3.down * 0.5f);
        }
    }
}
