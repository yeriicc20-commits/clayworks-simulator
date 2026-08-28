using UnityEngine;

namespace Hashi
{
    // El sonido de la maquina.
    //
    // Los clips se dejan sin asignar a proposito: el que falte simplemente no
    // suena y el juego sigue. Nada de comprobar clips en cada disparo ni de
    // llenar la consola de avisos por un sonido que aun no existe.
    //
    // Se engancha a los eventos de los demas scripts en vez de que ellos llamen
    // aqui. Asi el sonido se puede quitar entero de la escena y no se rompe
    // nada: al reves, la maquina acabaria llena de "if (audio != null)".
    public class AudioManager : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] MachineController maquina;
        [SerializeField] ClawController garra;
        [SerializeField] ClawFingerController pinzas;
        [SerializeField] PrizeSpawner generador;
        [SerializeField] DropZone bandeja;
        [SerializeField] GameManager juego;

        [Header("Fuentes")]
        [Tooltip("Para los golpes sueltos.")]
        [SerializeField] AudioSource fuente;

        [Tooltip("Para el zumbido del motor mientras se mueve. Tiene que estar "
                 + "en bucle.")]
        [SerializeField] AudioSource motor;

        [Header("Clips")]
        [SerializeField] AudioClip clipMovimiento;
        [SerializeField] AudioClip clipBajar;
        [SerializeField] AudioClip clipCerrarPinzas;
        [SerializeField] AudioClip clipPinzaGolpeaPremio;
        [SerializeField] AudioClip clipPremioGolpeaBarra;
        [SerializeField] AudioClip clipPremioCae;
        [SerializeField] AudioClip clipVictoria;
        [SerializeField] AudioClip clipBoton;

        [Header("Mezcla")]
        [Range(0f, 1f)] [SerializeField] float volumenGeneral = 0.8f;

        [Tooltip("Volumen del motor a velocidad maxima.")]
        [Range(0f, 1f)] [SerializeField] float volumenMotor = 0.35f;

        [Tooltip("Impulso a partir del cual un golpe suena a todo volumen. Sin "
                 + "esto, todos los golpes suenan igual de fuertes y la caja "
                 + "parece de hierro.")]
        [Range(0.01f, 5f)] [SerializeField] float impulsoParaVolumenMaximo = 0.6f;

        bool silenciado;

        public bool Silenciado => silenciado;

        void Awake()
        {
            if (fuente == null) fuente = GetComponent<AudioSource>();

            if (motor != null)
            {
                motor.clip = clipMovimiento;
                motor.loop = true;
                motor.volume = 0f;
                if (clipMovimiento != null) motor.Play();
            }
        }

        void OnEnable()
        {
            if (maquina != null)
            {
                maquina.AlSoltar += Bajar;
                maquina.AlCerrarPinzas += CerrarPinzas;
            }

            if (pinzas != null) pinzas.AlTocarPremio += PinzaGolpea;
            if (generador != null) generador.AlGenerar += Enganchar;
            if (bandeja != null) bandeja.AlRecogerPremio += PremioCae;
            if (juego != null) juego.AlGanar += Victoria;

            // La caja puede existir ya antes de que este script despierte.
            if (generador != null && generador.Actual != null) Enganchar(generador.Actual);
        }

        void OnDisable()
        {
            if (maquina != null)
            {
                maquina.AlSoltar -= Bajar;
                maquina.AlCerrarPinzas -= CerrarPinzas;
            }

            if (pinzas != null) pinzas.AlTocarPremio -= PinzaGolpea;
            if (generador != null) generador.AlGenerar -= Enganchar;
            if (bandeja != null) bandeja.AlRecogerPremio -= PremioCae;
            if (juego != null) juego.AlGanar -= Victoria;
        }

        void Update()
        {
            if (motor == null) return;

            // El motor sube y baja con la velocidad de la garra en vez de estar
            // o encendido o apagado. Es lo que hace que se note la inercia sin
            // mirar la pantalla.
            float objetivo = 0f;

            if (!silenciado && garra != null)
            {
                float v = Mathf.Clamp01(garra.RapidezHorizontal / 0.35f);
                bool bajandoOsubiendo = maquina != null
                    && (maquina.EstadoActual == MachineController.Estado.Bajando
                        || maquina.EstadoActual == MachineController.Estado.Subiendo);

                objetivo = Mathf.Max(v, bajandoOsubiendo ? 0.7f : 0f) * volumenMotor;
            }

            motor.volume = Mathf.MoveTowards(motor.volume, objetivo, Time.deltaTime * 2.5f);
        }

        // -------------------------------------------------------------- avisos

        void Enganchar(PrizeController p)
        {
            if (p == null) return;

            p.AlGolpearBarra += PremioGolpeaBarra;
            p.AlGolpearGarra += PinzaGolpea;
        }

        void Bajar() { Sonar(clipBajar, 1f); }
        void CerrarPinzas() { Sonar(clipCerrarPinzas, 1f); }
        void Victoria() { Sonar(clipVictoria, 1f); }
        void PremioCae(PrizeController p) { Sonar(clipPremioCae, 1f); }

        void PinzaGolpea(float impulso)
        {
            Sonar(clipPinzaGolpeaPremio, Volumen(impulso));
        }

        void PremioGolpeaBarra(float impulso)
        {
            Sonar(clipPremioGolpeaBarra, Volumen(impulso));
        }

        public void Boton() { Sonar(clipBoton, 1f); }

        public void Silenciar(bool si)
        {
            silenciado = si;
            if (silenciado && motor != null) motor.volume = 0f;
        }

        public void AlternarSilencio() { Silenciar(!silenciado); }

        float Volumen(float impulso)
        {
            return Mathf.Clamp01(impulso / impulsoParaVolumenMaximo);
        }

        void Sonar(AudioClip clip, float volumen)
        {
            if (silenciado || clip == null || fuente == null) return;
            if (volumen <= 0.01f) return;

            // Un pelo de variacion de tono en cada golpe. Repetir el mismo clip
            // clavado suena a maquina de tragaperras rota en tres golpes.
            fuente.pitch = Random.Range(0.95f, 1.05f);
            fuente.PlayOneShot(clip, volumen * volumenGeneral);
        }
    }
}
