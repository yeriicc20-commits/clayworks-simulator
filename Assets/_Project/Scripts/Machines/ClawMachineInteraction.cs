using UnityEngine;

public class ClawMachineInteraction : MonoBehaviour
{
    public ClawController clawController;

    [Tooltip("Precio de partida si la maquina no tiene MachinePricing.")]
    public float cost = 5f;

    // Las teclas ya no son campos: las elige el jugador en el menu de
    // ajustes. Se dejan como propiedades con el mismo nombre para que todo
    // lo que las usa siga leyendose igual, carteles de ayuda incluidos.
    static KeyCode priceKey { get { return AjustesControles.Tecla(AjustesControles.Accion.Precios); } }
    static KeyCode useKey { get { return AjustesControles.Tecla(AjustesControles.Accion.Usar); } }

    private bool playerInRange = false;
    private MachinePricing pricing;

    // El precio manda desde MachinePricing; cost queda como respaldo.
    public float Price
    {
        get { return Pricing != null ? Pricing.price : cost; }
    }

    public MachinePricing Pricing
    {
        get
        {
            if (pricing == null) pricing = GetComponentInParent<MachinePricing>();
            if (pricing == null && clawController != null) pricing = MachinePricing.For(clawController);

            return pricing;
        }
    }

    void Update()
    {
        // Con la pantalla de precio abierta no se interactua con nada mas.
        if (PricePanel.IsOpen) return;

        if (!playerInRange) return;
        if (clawController.isControllable || clawController.IsBusy) return;

        InteractionUI.Prompt(
            AjustesControles.NombreTecla(useKey) + ": jugar ("
            + GameManager.Format(Price) + ")   ·   "
            + AjustesControles.NombreTecla(priceKey) + ": cambiar precio");

        if (AjustesControles.Pulsando(AjustesControles.Accion.Usar)) TryPay();
        else if (Input.GetKeyDown(priceKey)) OpenPricePanel();
    }

    void OpenPricePanel()
    {
        MachinePricing target = Pricing;

        if (target == null)
        {
            NotificationManager.Instance.ShowMessage("Esta maquina no tiene precio configurable");
            return;
        }

        InteractionUI.Hide();

        PricePanel.EnsureExists().Open(target);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;

            InteractionUI.Hide();
        }
    }

    void TryPay()
    {
        bool paid = GameManager.Instance.SpendMoney(Price);

        if (paid)
        {
            // La maquina se encarga: suena la moneda, espera un segundo y
            // entonces deja jugar. Aqui solo se le avisa de que esta pagada.
            clawController.MeterMoneda();
            InteractionUI.Hide();
        }
        else
        {
            NotificationManager.Instance.ShowMessage("No tienes dinero suficiente");
        }
    }
}
