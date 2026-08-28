using UnityEngine;

namespace Hashi
{
    // Movimiento de la garra: los dos carriles y la bajada.
    //
    // Sobre la jerarquia, que sorprende al abrirla: el cuerpo de la garra NO
    // cuelga de los carriles. Los carriles son adorno y siguen a la garra; el
    // cuerpo es un Rigidbody cinematico suelto que se mueve con MovePosition.
    //
    // Se hizo asi porque de las pinzas cuelgan Rigidbody de verdad con bisagras.
    // Un cuerpo fisico dentro de una cadena de transforms que alguien mueve a
    // mano cada fotograma no se entera de que se ha movido: PhysX lo teletransporta
    // y las bisagras dan tirones. Moviendo el cuerpo con MovePosition, en cambio,
    // el motor SI conoce la velocidad y se la pasa a las pinzas, que es de donde
    // sale que la garra empuje la caja al desplazarse.
    //
    // El balanceo se hace girando la garra alrededor del punto del que cuelga,
    // no inclinandola sobre si misma. Asi, ademas de ladearse, se desplaza a los
    // lados como un peso colgado de un cable, y el balanceo crece cuando baja
    // porque el cable es mas largo. Sale gratis y es lo que hace la de verdad.
    public class ClawController : MonoBehaviour
    {
        [Header("Piezas (adorno, siguen a la garra)")]
        [Tooltip("El puente que recorre el eje X.")]
        [SerializeField] Transform railX;

        [Tooltip("El carro que recorre el eje Z, hijo del puente.")]
        [SerializeField] Transform railZ;

        [Tooltip("El eje vertical del que cuelga la garra. Se estira al bajar.")]
        [SerializeField] Transform verticalAxis;

        [Header("Piezas (fisica)")]
        [Tooltip("Rigidbody CINEMATICO del cuerpo de la garra.")]
        [SerializeField] Rigidbody clawBody;

        [Header("Limites horizontales (metros, locales a ClawSystem)")]
        [SerializeField] float limitXMin = -0.22f;
        [SerializeField] float limitXMax = 0.22f;
        [SerializeField] float limitZMin = -0.22f;
        [SerializeField] float limitZMax = 0.22f;

        [Header("Alturas (metros, locales a ClawSystem)")]
        [Tooltip("Altura del carril del que cuelga la garra. No se mueve.")]
        [SerializeField] float alturaCarril = 0.75f;

        [Tooltip("Altura de reposo del cuerpo de la garra.")]
        [SerializeField] float alturaReposo = 0.66f;

        [Tooltip("Hasta donde baja. CUIDADO al tocarla: los dedos abiertos "
                 + "tienen que bajar por FUERA de la caja, no por encima. Si se "
                 + "baja de mas, el dedo abierto queda sobre la tapa y la garra, "
                 + "que es cinematica y gana siempre, aplasta la caja contra las "
                 + "barras en vez de empujarla. La cuenta esta en "
                 + "HashiWatashiBuilder, en GARRA_MINIMA.")]
        [SerializeField] float alturaMinima = 0.52f;

        [Header("Velocidades")]
        [Tooltip("Velocidad horizontal a fondo, en metros por segundo.")]
        [Range(0.05f, 2f)] [SerializeField] float moveSpeed = 0.22f;

        [Tooltip("Lo que tarda en coger la velocidad. Baja = arranque blando.")]
        [Range(0.2f, 20f)] [SerializeField] float aceleracion = 2.2f;

        [Tooltip("Lo que tarda en pararse. Si es muy alta, se clava en seco y "
                 + "el balanceo sale exagerado.")]
        [Range(0.2f, 20f)] [SerializeField] float deceleracion = 3.5f;

        [Range(0.05f, 2f)] [SerializeField] float dropSpeed = 0.30f;
        [Range(0.05f, 2f)] [SerializeField] float riseSpeed = 0.35f;

        [Header("Punto de partida")]
        [SerializeField] Vector2 posicionInicial = new Vector2(0f, -0.18f);

        [Header("Balanceo")]
        [Tooltip("Cuanto tira el cable de volver a la vertical. Mas alto = "
                 + "oscilacion mas rapida y mas corta.")]
        [Range(1f, 200f)] [SerializeField] float rigidez = 55f;

        [Tooltip("Cuanto se va apagando la oscilacion. Muy bajo y no para nunca.")]
        [Range(0.5f, 30f)] [SerializeField] float amortiguacion = 5.5f;

        [Tooltip("Grados de ladeo por cada metro/segundo de velocidad.")]
        [Range(0f, 90f)] [SerializeField] float gradosPorVelocidad = 22f;

        [Tooltip("Tope de ladeo, para que no de la vuelta entera si algo se "
                 + "descontrola.")]
        [Range(0f, 45f)] [SerializeField] float ladeoMaximo = 14f;

        [Header("Depuracion")]
        [SerializeField] bool mostrarGizmos = true;

        // ----------------------------------------------------------- interno

        Vector2 entrada;                 // lo que pide el jugador, -1..1
        Vector2 velocidadHorizontal;     // m/s, en el plano
        Vector2 posicionHorizontal;      // x, z locales
        float alturaObjetivo;
        float velocidadVertical;         // la que toque ahora mismo, m/s
        float alturaActual;

        float ladeoX, ladeoZ;            // grados
        float velLadeoX, velLadeoZ;
        Vector2 velocidadAnterior;
        bool volviendoACasa;

        // ------------------------------------------------------------ lectura

        public bool HorizontalBloqueado { get; set; }
        public float AlturaActual => alturaActual;
        public float AlturaReposo => alturaReposo;
        public float AlturaMinima => alturaMinima;
        public Rigidbody Cuerpo => clawBody;
        public Vector2 VelocidadHorizontal => velocidadHorizontal;
        public float RapidezHorizontal => velocidadHorizontal.magnitude;

        public bool AlturaEnDestino =>
            Mathf.Abs(alturaActual - alturaObjetivo) < 0.0015f;

        public bool EnCasa =>
            AlturaEnDestino
            && Vector2.Distance(posicionHorizontal, posicionInicial) < 0.004f;

        // Si ha llegado al final de su recorrido. Lo usa la maquina para pasar
        // de fase sola cuando el jugador no pulsa a tiempo, igual que una
        // recreativa de verdad: el carro no se queda ahi esperando.
        public bool EnLimiteX => posicionHorizontal.x >= limitXMax - 0.004f;
        public bool EnLimiteZ => posicionHorizontal.y >= limitZMax - 0.004f;

        // ----------------------------------------------------------- arranque

        void Awake()
        {
            posicionHorizontal = new Vector2(
                Mathf.Clamp(posicionInicial.x, limitXMin, limitXMax),
                Mathf.Clamp(posicionInicial.y, limitZMin, limitZMax));

            alturaActual = alturaReposo;
            alturaObjetivo = alturaReposo;
            velocidadVertical = riseSpeed;

            if (clawBody != null)
            {
                clawBody.isKinematic = true;
                clawBody.interpolation = RigidbodyInterpolation.Interpolate;
                clawBody.useGravity = false;
            }

            Colocar(true);
        }

        // ------------------------------------------------------------- ordenes

        // Lo que pide el jugador. X = izquierda/derecha, Y = fondo.
        public void Conducir(Vector2 v)
        {
            entrada = HorizontalBloqueado ? Vector2.zero : Vector2.ClampMagnitude(v, 1f);
            if (entrada.sqrMagnitude > 0.0001f) volviendoACasa = false;
        }

        public void Bajar()
        {
            alturaObjetivo = alturaMinima;
            velocidadVertical = dropSpeed;
        }

        public void Subir()
        {
            alturaObjetivo = alturaReposo;
            velocidadVertical = riseSpeed;
        }

        // Vuelve sola a la esquina de salida. El jugador no controla nada
        // mientras tanto: es la parte de la jugada en la que solo se mira.
        public void VolverACasa()
        {
            volviendoACasa = true;
            HorizontalBloqueado = true;
            Subir();
        }

        public void SoltarVuelta()
        {
            volviendoACasa = false;
        }

        // Cambia velocidades desde el preajuste de dificultad.
        public void AplicarDificultad(DifficultySettings d)
        {
            if (d == null) return;

            moveSpeed = d.clawMoveSpeed;
            dropSpeed = d.dropSpeed;
            riseSpeed = d.riseSpeed;
        }

        // -------------------------------------------------------------- fisica

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            MoverHorizontal(dt);
            MoverVertical(dt);
            Balancear(dt);
            Colocar(false);
        }

        void MoverHorizontal(float dt)
        {
            Vector2 objetivo;

            if (volviendoACasa)
            {
                // De vuelta no se acelera con la palanca: se va derecho al
                // punto de salida y se frena al llegar, como la de verdad.
                Vector2 delta = posicionInicial - posicionHorizontal;
                objetivo = delta.magnitude > 0.004f
                    ? delta.normalized * moveSpeed
                    : Vector2.zero;

                // Sin esto se pasa de largo y vuelve, en un vaiven que no acaba.
                float maxSinPasarse = delta.magnitude / Mathf.Max(dt, 0.0001f);
                objetivo = Vector2.ClampMagnitude(objetivo, maxSinPasarse);
            }
            else
            {
                objetivo = entrada * moveSpeed;
            }

            // Acelerar y frenar con ritmos distintos, eje a eje: soltar la
            // palanca tiene que frenar mas rapido de lo que arranca, o la garra
            // parece que patina sobre hielo.
            velocidadHorizontal.x = Acercar(velocidadHorizontal.x, objetivo.x, dt);
            velocidadHorizontal.y = Acercar(velocidadHorizontal.y, objetivo.y, dt);

            posicionHorizontal += velocidadHorizontal * dt;

            // Al chocar con el limite la velocidad se corta, no rebota.
            float x = Mathf.Clamp(posicionHorizontal.x, limitXMin, limitXMax);
            float z = Mathf.Clamp(posicionHorizontal.y, limitZMin, limitZMax);

            if (!Mathf.Approximately(x, posicionHorizontal.x)) velocidadHorizontal.x = 0f;
            if (!Mathf.Approximately(z, posicionHorizontal.y)) velocidadHorizontal.y = 0f;

            posicionHorizontal = new Vector2(x, z);
        }

        float Acercar(float actual, float objetivo, float dt)
        {
            bool acelerando = Mathf.Abs(objetivo) > Mathf.Abs(actual)
                              && Mathf.Sign(objetivo) == Mathf.Sign(actual == 0f ? objetivo : actual);

            float ritmo = (acelerando ? aceleracion : deceleracion) * moveSpeed;
            return Mathf.MoveTowards(actual, objetivo, ritmo * dt);
        }

        void MoverVertical(float dt)
        {
            alturaActual = Mathf.MoveTowards(alturaActual, alturaObjetivo,
                                             velocidadVertical * dt);
        }

        // Muelle amortiguado sobre el ladeo del cable. El objetivo del muelle no
        // es cero, es la inclinacion que corresponde a la velocidad actual: al
        // arrancar se queda atras, al frenar se va hacia delante, y al pararse
        // oscila hasta que se apaga sola.
        void Balancear(float dt)
        {
            Vector2 aceleracionReal = (velocidadHorizontal - velocidadAnterior) / Mathf.Max(dt, 0.0001f);
            velocidadAnterior = velocidadHorizontal;

            // Se mezcla velocidad (la garra colgada se queda atras mientras se
            // mueve) con aceleracion (el tiron de arrancar y frenar).
            float objetivoZ = Mathf.Clamp(
                -(velocidadHorizontal.x * gradosPorVelocidad + aceleracionReal.x * 0.25f),
                -ladeoMaximo, ladeoMaximo);

            float objetivoX = Mathf.Clamp(
                velocidadHorizontal.y * gradosPorVelocidad + aceleracionReal.y * 0.25f,
                -ladeoMaximo, ladeoMaximo);

            velLadeoZ += (-rigidez * (ladeoZ - objetivoZ) - amortiguacion * velLadeoZ) * dt;
            velLadeoX += (-rigidez * (ladeoX - objetivoX) - amortiguacion * velLadeoX) * dt;

            ladeoZ += velLadeoZ * dt;
            ladeoX += velLadeoX * dt;
        }

        // Compone la postura final y la escribe. Un solo sitio que escribe
        // posiciones: si el dia de manana la garra aparece donde no toca, es
        // aqui y en ningun otro lado.
        void Colocar(bool instantaneo)
        {
            Vector3 pivote = new Vector3(posicionHorizontal.x, alturaCarril,
                                         posicionHorizontal.y);

            float largoCable = Mathf.Max(0.01f, alturaCarril - alturaActual);
            Quaternion ladeo = Quaternion.Euler(ladeoX, 0f, ladeoZ);

            // La garra cuelga del pivote: girar el cable la desplaza tambien.
            Vector3 posLocal = pivote + ladeo * (Vector3.down * largoCable);

            if (clawBody != null)
            {
                Vector3 mundo = transform.TransformPoint(posLocal);
                Quaternion rot = transform.rotation * ladeo;

                if (instantaneo)
                {
                    clawBody.position = mundo;
                    clawBody.rotation = rot;
                    clawBody.transform.SetPositionAndRotation(mundo, rot);
                }
                else
                {
                    clawBody.MovePosition(mundo);
                    clawBody.MoveRotation(rot);
                }
            }

            // El adorno. No lleva fisica, asi que se escribe y ya.
            if (railX != null)
            {
                railX.localPosition = new Vector3(posicionHorizontal.x, alturaCarril, 0f);
            }

            if (railZ != null)
            {
                railZ.localPosition = new Vector3(0f, 0f, posicionHorizontal.y);
            }

            if (verticalAxis != null)
            {
                verticalAxis.localPosition = Vector3.zero;
                verticalAxis.localRotation = ladeo;

                // El eje se estira hasta donde este la garra. La malla es un
                // cilindro de Unity, que mide 2 de alto: la escala es la mitad.
                Transform malla = verticalAxis.childCount > 0
                    ? verticalAxis.GetChild(0) : null;

                if (malla != null)
                {
                    malla.localPosition = new Vector3(0f, -largoCable * 0.5f, 0f);
                    malla.localScale = new Vector3(malla.localScale.x,
                                                   largoCable * 0.5f,
                                                   malla.localScale.z);
                }
            }
        }

        // -------------------------------------------------------------- gizmos

        void OnDrawGizmos()
        {
            if (!mostrarGizmos) return;

            Gizmos.matrix = transform.localToWorldMatrix;

            // El rectangulo por el que puede pasear la garra.
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.8f);
            Vector3 a = new Vector3(limitXMin, alturaCarril, limitZMin);
            Vector3 b = new Vector3(limitXMax, alturaCarril, limitZMin);
            Vector3 c = new Vector3(limitXMax, alturaCarril, limitZMax);
            Vector3 d = new Vector3(limitXMin, alturaCarril, limitZMax);
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);

            // El recorrido vertical, de reposo a lo mas hondo.
            float x = Application.isPlaying ? posicionHorizontal.x : posicionInicial.x;
            float z = Application.isPlaying ? posicionHorizontal.y : posicionInicial.y;

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawLine(new Vector3(x, alturaCarril, z), new Vector3(x, alturaMinima, z));
            Gizmos.DrawWireCube(new Vector3(x, alturaReposo, z), new Vector3(0.03f, 0.002f, 0.03f));
            Gizmos.DrawWireCube(new Vector3(x, alturaMinima, z), new Vector3(0.05f, 0.002f, 0.05f));

            // El punto de salida.
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(new Vector3(posicionInicial.x, alturaReposo,
                                              posicionInicial.y), 0.02f);
        }
    }
}
