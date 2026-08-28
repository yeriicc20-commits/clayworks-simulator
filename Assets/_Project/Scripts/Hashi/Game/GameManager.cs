using System;
using System.Collections;
using UnityEngine;

namespace Hashi
{
    // El que ata todo: creditos, turnos, victoria y derrota.
    //
    // Aqui NO se decide si el premio ha caido. Eso lo dice la bandeja mirando la
    // fisica, y este script se limita a enterarse. Es a proposito: en el momento
    // en que el que lleva la puntuacion puede declarar una victoria por su
    // cuenta, aparece la tentacion de "ayudar" al jugador cada X intentos, y eso
    // es exactamente lo que hace que una maquina se sienta tramposa.
    public class GameManager : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] MachineController maquina;
        [SerializeField] CreditsManager creditos;
        [SerializeField] DropZone bandeja;
        [SerializeField] PrizeSpawner generador;
        [SerializeField] BarRig barras;

        [Header("Dificultad")]
        [SerializeField] DifficultySettings[] dificultades;
        [SerializeField] int dificultadInicial = 1;

        [Header("Despues de ganar")]
        [Tooltip("Lo que dura la fiesta antes de volver a estar listo.")]
        [Range(0.5f, 10f)] [SerializeField] float esperaTrasGanar = 3.5f;

        [Tooltip("Pone otra caja al terminar la celebracion. Apagalo si "
                 + "prefieres que la maquina se quede vacia.")]
        [SerializeField] bool reponerPremio = true;

        [Header("Depuracion")]
        [Tooltip("Dibuja los gizmos de todos los cacharros y ensena datos de "
                 + "fisica. Se enciende y apaga con F1.")]
        [SerializeField] bool modoDepuracion;

        // ------------------------------------------------------------- eventos

        public event Action<string> AlMensaje;
        public event Action<int> AlCambiarPremios;
        public event Action AlGanar;
        public event Action AlPerder;
        public event Action<DifficultySettings> AlCambiarDificultad;
        public event Action<bool> AlCambiarDepuracion;

        // ------------------------------------------------------------- lectura

        public int Premios { get; private set; }
        public DifficultySettings Dificultad { get; private set; }
        public bool ModoDepuracion => modoDepuracion;
        public MachineController Maquina => maquina;
        public CreditsManager Creditos => creditos;
        public PrizeSpawner Generador => generador;
        public bool Celebrando { get; private set; }

        void OnEnable()
        {
            if (bandeja != null) bandeja.AlRecogerPremio += Ganar;
            if (maquina != null) maquina.AlTerminarTurno += TurnoTerminado;
            if (generador != null) generador.AlGenerar += PremioNuevo;
        }

        void OnDisable()
        {
            if (bandeja != null) bandeja.AlRecogerPremio -= Ganar;
            if (maquina != null) maquina.AlTerminarTurno -= TurnoTerminado;
            if (generador != null) generador.AlGenerar -= PremioNuevo;
        }

        void Start()
        {
            CambiarDificultad(dificultadInicial);
            Bloquear(true);
            AlCambiarPremios?.Invoke(Premios);
            Mensaje("READY");
        }

        void Update()
        {
            if (InputReader.Empezar()) Empezar();
            if (InputReader.AnadirCredito() && creditos != null) creditos.Anadir(1);
            if (InputReader.Reiniciar()) ReiniciarTodo();
            if (InputReader.Depuracion()) CambiarDepuracion(!modoDepuracion);
        }

        // ------------------------------------------------------------- partida

        // Mete el credito y arranca el turno. Es el unico camino para jugar.
        public void Empezar()
        {
            if (maquina == null || creditos == null) return;
            if (maquina.EnTurno || Celebrando) return;

            if (!creditos.Cobrar())
            {
                Mensaje("SIN CREDITOS");
                return;
            }

            maquina.EmpezarTurno();
            Bloquear(false);
            Mensaje("MOVE CLAW");
        }

        void TurnoTerminado()
        {
            // Si el premio hubiera caido, la bandeja ya habria avisado antes de
            // llegar aqui. Asi que llegar aqui es haber fallado.
            if (Celebrando) return;

            Bloquear(true);
            AlPerder?.Invoke();

            // La caja se queda EXACTAMENTE como haya quedado. Es media gracia
            // del juego: se avanza entre intentos, y el credito siguiente parte
            // de lo conseguido con el anterior.
            Mensaje(creditos != null && creditos.HayParaJugar
                ? "TRY AGAIN"
                : "SIN CREDITOS");
        }

        void Ganar(PrizeController premio)
        {
            if (Celebrando) return;

            Premios++;
            AlCambiarPremios?.Invoke(Premios);

            StartCoroutine(Celebrar());
        }

        IEnumerator Celebrar()
        {
            Celebrando = true;
            Bloquear(true);

            AlGanar?.Invoke();
            Mensaje("YOU WIN!");

            yield return new WaitForSeconds(esperaTrasGanar);

            // Se espera tambien a que la garra termine su vuelta, o la caja
            // nueva aparece con la garra todavia bajando encima.
            while (maquina != null && maquina.EnTurno) yield return null;

            if (reponerPremio && generador != null)
            {
                generador.Quitar();
                generador.GenerarAleatorio();
            }

            Celebrando = false;
            Mensaje("READY");
        }

        // Reinicia la partida entera: creditos, premios y caja nueva.
        public void ReiniciarTodo()
        {
            StopAllCoroutines();
            Celebrando = false;

            if (maquina != null) maquina.Abortar();
            if (bandeja != null) bandeja.Olvidar();
            if (creditos != null) creditos.Reiniciar();

            Premios = 0;
            AlCambiarPremios?.Invoke(Premios);

            if (generador != null)
            {
                generador.Quitar();
                generador.Generar(-1);
            }

            Bloquear(true);
            Mensaje("READY");
        }

        void PremioNuevo(PrizeController p)
        {
            // La dificultad tiene que caer tambien sobre la caja recien puesta,
            // o la primera partida se juega con los valores de fabrica.
            if (p != null) p.AplicarDificultad(Dificultad);
        }

        void Bloquear(bool si)
        {
            if (maquina != null) maquina.ControlesBloqueados = si;
        }

        void Mensaje(string texto)
        {
            AlMensaje?.Invoke(texto);
        }

        // ----------------------------------------------------------- dificultad

        public void CambiarDificultad(int indice)
        {
            if (dificultades == null || dificultades.Length == 0) return;

            indice = Mathf.Clamp(indice, 0, dificultades.Length - 1);
            AplicarDificultad(dificultades[indice]);
        }

        public void AplicarDificultad(DifficultySettings d)
        {
            if (d == null) return;

            Dificultad = d;

            if (maquina != null) maquina.AplicarDificultad(d);
            if (barras != null) barras.AplicarSeparacion(d.barDistance);

            if (generador != null && generador.Actual != null)
            {
                generador.Actual.AplicarDificultad(d);
            }

            AlCambiarDificultad?.Invoke(d);
        }

        public void CambiarDepuracion(bool si)
        {
            modoDepuracion = si;
            AlCambiarDepuracion?.Invoke(si);
        }
    }
}
