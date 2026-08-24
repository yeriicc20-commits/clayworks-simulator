using UnityEngine;

// Base de las cajas que se pueden coger en brazos. Lo comun (detectar al
// jugador, el cartel y la recogida) vive aqui; cada caja decide que hace
// cuando la usas.
public abstract class CarriableBox : MonoBehaviour
{
    [Tooltip("A que distancia se puede coger, en metros.")]
    public float interactRange = 1.8f;

    private Transform player;
    private Camera playerCamera;
    private string shownPrompt = "";
    private bool promptVisible = false;

    protected virtual void Start()
    {
        FirstPersonController controller = FindAnyObjectByType<FirstPersonController>();
        if (controller != null) player = controller.transform;

        playerCamera = Camera.main;
    }

    // Hay que estar mirandola de verdad, no solo cerca. Se comprueba con un rayo
    // desde el centro de la pantalla contra sus propios colliders.
    bool PlayerIsLookingAtMe()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null) return false;

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, interactRange + 1f, ~0, QueryTriggerInteraction.Ignore)) return false;

        return hit.transform == transform || hit.transform.IsChildOf(transform);
    }

    void Update()
    {
        // Por distancia y no por trigger: al soltar la caja encima del jugador
        // el OnTriggerEnter no vuelve a dispararse y quedaba imposible recogerla.
        if (player == null) return;

        if (BoxCarrier.Instance != null && BoxCarrier.Instance.IsCarryingThis(this))
        {
            HidePrompt();
            return;
        }

        if (Vector3.Distance(transform.position, player.position) > interactRange)
        {
            HidePrompt();
            return;
        }

        if (!PlayerIsLookingAtMe())
        {
            HidePrompt();
            return;
        }

        bool busy = PlayerCarry.Busy;
        ShowPrompt(busy ? PlayerCarry.BusyMessage : "Pulsa E para coger la caja");

        if (!busy && Input.GetKeyDown(KeyCode.E)) Pickup();
    }

    void Pickup()
    {
        HidePrompt();

        BoxCarrier carrier = BoxCarrier.EnsureExists();

        if (carrier == null) return;

        carrier.Carry(this);
    }

    // Texto que se ve mientras la llevas encima.
    public abstract string CarryHint { get; }

    // Entrada propia de la caja mientras la llevas. Devuelve true si consume el
    // clic, para que no se interprete como "dejar la caja".
    public virtual bool HandleCarryInput(BoxCarrier carrier)
    {
        return false;
    }

    // Que hace la tecla de uso con la caja en brazos.
    public virtual void OnUseKey(BoxCarrier carrier) { }

    void ShowPrompt(string message)
    {
        promptVisible = true;

        if (shownPrompt == message) return;

        shownPrompt = message;
        if (InteractionUI.Instance != null) InteractionUI.Instance.ShowPrompt(message);
    }

    void HidePrompt()
    {
        if (!promptVisible) return;

        promptVisible = false;
        shownPrompt = "";
        if (InteractionUI.Instance != null) InteractionUI.Instance.HidePrompt();
    }

    void OnDisable()
    {
        HidePrompt();
    }
}
