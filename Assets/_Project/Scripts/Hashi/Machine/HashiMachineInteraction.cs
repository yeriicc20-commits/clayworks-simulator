using System.Collections;
using UnityEngine;

namespace Hashi
{
    // El puente entre la maquina de puente y el local: pagar y jugar.
    //
    // Hace lo mismo y con las mismas piezas que ClawMachineInteraction, que es
    // la de la maquina de garra: el cartel de "E: jugar", el dinero del local,
    // el panel de precios con la P. Copiar ese comportamiento no es pereza, es
    // lo contrario: dos maquinas del mismo local que se cobran distinto o piden
    // teclas distintas se sienten como dos juegos pegados con cinta.
    //
    // Lo que NO comparte es el sistema de creditos de la escena de pruebas. Ahi
    // hay fichas; aqui hay euros del negocio, y son cosas distintas.
    public class HashiMachineInteraction : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] MachineController maquina;

        [Tooltip("Precio por partida si la maquina no lleva MachinePricing.")]
        [SerializeField] float cost = 5f;

        [Tooltip("Lo que espera desde que suena la moneda hasta que se puede "
                 + "jugar. Sin esa pausa, la moneda y el arranque se pisan.")]
        [Range(0f, 3f)] [SerializeField] float esperaMoneda = 0.8f;

        bool jugadorCerca;
        bool arrancando;
        MachinePricing precios;

        // Las teclas las elige el jugador en el menu de ajustes, igual que en la
        // otra maquina.
        static KeyCode TeclaUsar =>
            AjustesControles.Tecla(AjustesControles.Accion.Usar);

        static KeyCode TeclaPrecios =>
            AjustesControles.Tecla(AjustesControles.Accion.Precios);

        public float Precio => Precios != null ? Precios.price : cost;

        public MachinePricing Precios
        {
            get
            {
                if (precios == null) precios = GetComponentInParent<MachinePricing>();
                return precios;
            }
        }

        void Awake()
        {
            if (maquina == null) maquina = GetComponentInParent<MachineController>();
        }

        void OnEnable()
        {
            if (maquina == null) return;

            maquina.PonerMando(MachineController.Mando.Tienda);
            maquina.ControlesBloqueados = true;
            maquina.AlTerminarTurno += TurnoTerminado;
        }

        void OnDisable()
        {
            if (maquina != null) maquina.AlTerminarTurno -= TurnoTerminado;
        }

        void Update()
        {
            // Con el panel de precios delante no se interactua con nada mas.
            if (PricePanel.IsOpen) return;
            if (!jugadorCerca || maquina == null) return;
            if (maquina.EnTurno || arrancando) return;

            // GameManager es el del local, no el de la maquina de puente. Los
            // dos se llaman igual y estan en sitios distintos, asi que aqui hay
            // que decir cual con nombre y apellidos.
            InteractionUI.Prompt(
                AjustesControles.NombreTecla(TeclaUsar) + ": jugar ("
                + global::GameManager.Format(Precio) + ")   ·   "
                + AjustesControles.NombreTecla(TeclaPrecios) + ": cambiar precio");

            if (AjustesControles.Pulsando(AjustesControles.Accion.Usar)) Pagar();
            else if (AjustesControles.Pulsando(AjustesControles.Accion.Precios)) AbrirPrecios();
        }

        void Pagar()
        {
            if (global::GameManager.Instance == null) return;

            if (!global::GameManager.Instance.SpendMoney(Precio))
            {
                if (NotificationManager.Instance != null)
                {
                    NotificationManager.Instance.ShowMessage("No tienes dinero suficiente");
                }

                return;
            }

            InteractionUI.Hide();
            StartCoroutine(Arrancar());
        }

        IEnumerator Arrancar()
        {
            arrancando = true;

            yield return new WaitForSeconds(esperaMoneda);

            arrancando = false;

            maquina.EmpezarTurno();
            maquina.ControlesBloqueados = false;
        }

        void TurnoTerminado()
        {
            // Se vuelve a cerrar el mando: sin esto, terminado el turno la garra
            // se seguiria moviendo gratis con las teclas.
            maquina.ControlesBloqueados = true;
        }

        void AbrirPrecios()
        {
            if (Precios == null)
            {
                if (NotificationManager.Instance != null)
                {
                    NotificationManager.Instance.ShowMessage(
                        "Esta maquina no tiene precio configurable");
                }

                return;
            }

            InteractionUI.Hide();
            PricePanel.EnsureExists().Open(Precios);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) jugadorCerca = true;
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            jugadorCerca = false;
            InteractionUI.Hide();
        }
    }
}
