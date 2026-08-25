using UnityEngine;

public class ClawMachineInteraction : MonoBehaviour
{
    public ClawController clawController;

    [Tooltip("Precio de partida si la maquina no tiene MachinePricing.")]
    public float cost = 5f;

    [Header("Ajustar precio")]
    public KeyCode priceKey = KeyCode.P;

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
            "E: jugar (" + GameManager.Format(Price) + ")   ·   " + priceKey + ": cambiar precio");

        if (Input.GetKeyDown(KeyCode.E)) TryPay();
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
            ClawAudio audio3d = clawController.GetComponent<ClawAudio>();
            if (audio3d != null) audio3d.Moneda();

            clawController.isControllable = true;
            InteractionUI.Hide();
        }
        else
        {
            NotificationManager.Instance.ShowMessage("No tienes dinero suficiente");
        }
    }
}
