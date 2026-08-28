using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hashi
{
    // La interfaz: creditos, premios, tiempo, estado y los mensajes gordos.
    //
    // Solo lee y ensena. No decide nada, no cobra creditos y no declara
    // victorias: para eso pide las cosas al GameManager. Una interfaz que
    // ademas lleva reglas de juego es la que acaba dando una partida gratis
    // porque alguien pulso el boton dos veces muy rapido.
    public class UIManager : MonoBehaviour
    {
        [Header("Referencias de juego")]
        [SerializeField] GameManager juego;
        [SerializeField] CreditsManager creditos;
        [SerializeField] MachineController maquina;
        [SerializeField] CameraController camaras;
        [SerializeField] AudioManager sonido;

        [Header("Textos")]
        [SerializeField] TMP_Text textoCreditos;
        [SerializeField] TMP_Text textoPremios;
        [SerializeField] TMP_Text textoTiempo;
        [SerializeField] TMP_Text textoEstado;

        [Tooltip("El cartel grande del centro: READY, DROP!, YOU WIN!...")]
        [SerializeField] TMP_Text textoMensaje;

        [Tooltip("Datos de fisica. Solo se ve con el modo depuracion (F1).")]
        [SerializeField] TMP_Text textoDepuracion;

        [Header("Botones")]
        [SerializeField] Button botonStart;
        [SerializeField] Button botonReset;
        [SerializeField] Button botonCamara;
        [SerializeField] Button botonSonido;
        [SerializeField] Button botonCredito;

        [Header("Mensajes")]
        [Tooltip("Lo que se queda un mensaje en pantalla. 0 = para siempre.")]
        [Range(0f, 10f)] [SerializeField] float duracionMensaje = 2.5f;

        [SerializeField] Color colorNormal = Color.white;
        [SerializeField] Color colorVictoria = new Color(1f, 0.85f, 0.2f);
        [SerializeField] Color colorDerrota = new Color(1f, 0.45f, 0.45f);

        float mensajeHasta = -1f;

        void OnEnable()
        {
            if (creditos != null) creditos.AlCambiar += PintarCreditos;

            if (juego != null)
            {
                juego.AlMensaje += Mensaje;
                juego.AlCambiarPremios += PintarPremios;
                juego.AlGanar += Victoria;
                juego.AlPerder += Derrota;
                juego.AlCambiarDepuracion += CambiarDepuracion;
            }

            if (maquina != null) maquina.AlCambiarEstado += PintarEstado;

            // Comparacion con null a la vieja usanza y no con "?.". El operador
            // se salta la comprobacion que hace Unity para los objetos ya
            // destruidos, asi que con un componente borrado daria true y
            // petaria justo donde parece que no puede petar.
            Conectar(botonStart, () => { Click(); if (juego != null) juego.Empezar(); });
            Conectar(botonReset, () => { Click(); if (juego != null) juego.ReiniciarTodo(); });
            Conectar(botonCamara, () => { Click(); if (camaras != null) camaras.Siguiente(); });
            Conectar(botonSonido, () => { Click(); if (sonido != null) sonido.AlternarSilencio(); });
            Conectar(botonCredito, () => { Click(); if (creditos != null) creditos.Anadir(1); });
        }

        void OnDisable()
        {
            if (creditos != null) creditos.AlCambiar -= PintarCreditos;

            if (juego != null)
            {
                juego.AlMensaje -= Mensaje;
                juego.AlCambiarPremios -= PintarPremios;
                juego.AlGanar -= Victoria;
                juego.AlPerder -= Derrota;
                juego.AlCambiarDepuracion -= CambiarDepuracion;
            }

            if (maquina != null) maquina.AlCambiarEstado -= PintarEstado;
        }

        void Start()
        {
            if (creditos != null) PintarCreditos(creditos.Creditos);
            if (juego != null) PintarPremios(juego.Premios);
            if (maquina != null) PintarEstado(maquina.EstadoActual);

            CambiarDepuracion(juego != null && juego.ModoDepuracion);
        }

        void Update()
        {
            PintarTiempo();
            PintarDepuracion();

            if (mensajeHasta > 0f && Time.time > mensajeHasta)
            {
                mensajeHasta = -1f;
                if (textoMensaje != null) textoMensaje.text = "";
            }

            // El boton de empezar solo se puede pulsar cuando de verdad se
            // puede empezar. Un boton que se deja pulsar y no hace nada parece
            // un fallo del juego.
            if (botonStart != null)
            {
                botonStart.interactable = creditos != null && creditos.HayParaJugar
                                          && maquina != null && !maquina.EnTurno
                                          && juego != null && !juego.Celebrando;
            }
        }

        // ------------------------------------------------------------- pintado

        void PintarCreditos(int n)
        {
            if (textoCreditos != null) textoCreditos.text = "CREDITOS  " + n;
        }

        void PintarPremios(int n)
        {
            if (textoPremios != null) textoPremios.text = "PREMIOS  " + n;
        }

        void PintarTiempo()
        {
            if (textoTiempo == null || maquina == null) return;

            if (!maquina.EnTurno || maquina.Ocupado)
            {
                textoTiempo.text = "TIEMPO  --";
                return;
            }

            textoTiempo.text = "TIEMPO  " + Mathf.CeilToInt(maquina.TiempoRestante);
        }

        void PintarEstado(MachineController.Estado e)
        {
            if (textoEstado != null) textoEstado.text = "ESTADO  " + Nombre(e);

            // Los avisos que van pegados a un estado concreto de la maquina.
            switch (e)
            {
                case MachineController.Estado.AvanzaX: Mensaje("PULSA PARA PARAR"); break;
                case MachineController.Estado.AvanzaZ: Mensaje("Y AHORA EL FONDO"); break;
                case MachineController.Estado.Bajando: Mensaje("DROP!"); break;
                case MachineController.Estado.Cerrando: Mensaje("GOOD LUCK"); break;
            }
        }

        static string Nombre(MachineController.Estado e)
        {
            switch (e)
            {
                case MachineController.Estado.Reposo: return "EN ESPERA";
                case MachineController.Estado.AvanzaX: return "AVANZA DERECHA";
                case MachineController.Estado.AvanzaZ: return "AVANZA AL FONDO";
                case MachineController.Estado.Bajando: return "BAJANDO";
                case MachineController.Estado.Cerrando: return "CERRANDO";
                case MachineController.Estado.Subiendo: return "SUBIENDO";
                case MachineController.Estado.Volviendo: return "VOLVIENDO";
            }

            return "?";
        }

        public void Mensaje(string texto)
        {
            Mensaje(texto, colorNormal);
        }

        void Mensaje(string texto, Color color)
        {
            if (textoMensaje == null) return;

            textoMensaje.text = texto;
            textoMensaje.color = color;
            mensajeHasta = duracionMensaje > 0f ? Time.time + duracionMensaje : -1f;
        }

        void Victoria() { Mensaje("YOU WIN!", colorVictoria); }
        void Derrota() { Mensaje("TRY AGAIN", colorDerrota); }

        // --------------------------------------------------------- depuracion

        void CambiarDepuracion(bool si)
        {
            if (textoDepuracion != null) textoDepuracion.gameObject.SetActive(si);
        }

        void PintarDepuracion()
        {
            if (textoDepuracion == null || !textoDepuracion.gameObject.activeSelf) return;
            if (juego == null) return;

            PrizeController p = juego.Generador != null ? juego.Generador.Actual : null;

            string dificultad = juego.Dificultad != null ? juego.Dificultad.nombre : "-";

            if (p == null)
            {
                textoDepuracion.text = "DIFICULTAD  " + dificultad + "\nsin premio";
                return;
            }

            // Lo que de verdad hace falta para entender una jugada: cuanto ha
            // girado la caja y donde le queda el centro de masas.
            textoDepuracion.text =
                "DIFICULTAD  " + dificultad
                + "\nFPS  " + Mathf.RoundToInt(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime))
                + "\ninclinacion  " + p.Inclinacion.ToString("0.0") + " grados"
                + "\nmasa  " + p.Masa.ToString("0.00") + " kg"
                + "\ncentro de masas Y  " + p.CentroDeMasasMundo.y.ToString("0.000")
                + "\nquieta  " + (p.EstaQuieta ? "si" : "no")
                + "\nestado caja  " + p.EstadoActual;
        }

        void Click()
        {
            if (sonido != null) sonido.Boton();
        }

        static void Conectar(Button b, UnityEngine.Events.UnityAction accion)
        {
            if (b == null) return;

            // Se limpia antes de anadir: al recargar el dominio en el editor se
            // acumulaban dos y tres llamadas por boton, y un START pulsado una
            // vez cobraba tres creditos.
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(accion);
        }
    }
}
