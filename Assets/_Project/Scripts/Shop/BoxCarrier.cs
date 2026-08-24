using UnityEngine;

// Llevar una caja en brazos. Con la caja encima, E saca lo que hay dentro para
// colocarlo y otra E lo vuelve a guardar. La caja se puede soltar y recoger.
public class BoxCarrier : MonoBehaviour
{
    public static BoxCarrier Instance;

    [Header("Como se sujeta")]
    public Transform holdPoint;
    public Vector3 heldLocalPosition = new Vector3(0.32f, -0.3f, 0.6f);
    public Vector3 heldLocalEuler = new Vector3(-8f, 22f, 6f);
    public float heldScale = 0.35f;

    [Header("Controles")]
    public KeyCode useKey = KeyCode.E;
    [Tooltip("Boton del raton para dejar la caja en el suelo. 0 = izquierdo.")]
    public int dropMouseButton = 0;

    public bool IsCarrying { get { return carried != null; } }
    public bool IsDeploying { get; private set; }
    public bool IsPlacingBox { get; private set; }

    private CarriableBox carried;
    private Vector3 originalScale = Vector3.one;
    private int pickedUpFrame = -1;
    private string shownHint = "";
    private bool hintVisible = false;

    void Awake()
    {
        Instance = this;

        if (holdPoint == null) BuildHoldPoint();
    }

    // No hace falta ponerlo en la escena: si no existe, se monta solo colgando
    // de la camara del jugador.
    public static BoxCarrier EnsureExists()
    {
        if (Instance != null) return Instance;

        BoxCarrier existing = FindAnyObjectByType<BoxCarrier>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        GameObject holder = new GameObject("BoxCarrier");

        return holder.AddComponent<BoxCarrier>();
    }

    void BuildHoldPoint()
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            Debug.LogError("[BoxCarrier] No encuentro la camara del jugador (tag MainCamera).", this);
            return;
        }

        GameObject point = new GameObject("BoxHoldPoint");
        point.transform.SetParent(cam.transform, false);
        point.transform.localPosition = Vector3.zero;
        point.transform.localRotation = Quaternion.identity;

        holdPoint = point.transform;
    }

    public bool IsCarryingThis(CarriableBox box)
    {
        return carried != null && carried == box;
    }

    public bool Carry(CarriableBox box)
    {
        if (carried != null || box == null) return false;

        if (holdPoint == null)
        {
            Debug.LogError("[BoxCarrier] Falta holdPoint: sin el no hay donde sujetar la caja.", this);
            return false;
        }

        carried = box;
        originalScale = box.transform.localScale;

        // Sin esto, la misma pulsacion que la recoge la desplegaria acto seguido.
        pickedUpFrame = Time.frameCount;

        SetPhysicsEnabled(box, false);

        box.transform.SetParent(holdPoint, false);
        box.transform.localPosition = heldLocalPosition;
        box.transform.localRotation = Quaternion.Euler(heldLocalEuler);
        box.transform.localScale = originalScale * heldScale;

        NotificationManager.Instance.ShowMessage("Caja en brazos");

        return true;
    }

    void Update()
    {
        if (carried == null)
        {
            HideHint();
            return;
        }

        if (Time.frameCount == pickedUpFrame) return;

        if (IsDeploying)
        {
            ShowHint("Clic izquierdo para colocar - " + useKey + " para guardarlo en la caja");

            if (Input.GetKeyDown(useKey)) PlacementManager.Instance.CancelPlacement();
            return;
        }

        if (IsPlacingBox)
        {
            ShowHint("Clic izquierdo para dejar la caja aqui - " + useKey + " para volver a cogerla");

            if (Input.GetKeyDown(useKey)) PlacementManager.Instance.CancelPlacement();
            return;
        }

        ShowHint(carried.CarryHint);

        // La caja tiene prioridad: si consume el clic (meter peluches en una
        // maquina) no se interpreta como dejarla en el suelo.
        if (carried.HandleCarryInput(this)) return;

        if (Input.GetKeyDown(useKey)) carried.OnUseKey(this);
        else if (Input.GetMouseButtonDown(dropMouseButton)) Drop();
    }

    // Saca el contenido: la caja desaparece de las manos mientras colocas.
    public void DeployContents(GameObject contents)
    {
        if (carried == null) return;

        if (contents == null)
        {
            NotificationManager.Instance.ShowMessage("Esta caja esta vacia");
            return;
        }

        IsDeploying = true;
        carried.gameObject.SetActive(false);

        PlacementManager.Instance.StartPlacement(contents, OnContentPlaced, ReturnToArms);
    }

    // La caja se ha gastado del todo mientras la llevabas.
    public void ConsumeCarriedBox()
    {
        if (carried == null) return;

        Destroy(carried.gameObject);
        carried = null;

        HideHint();
    }

    // Cancelaste la colocacion: la caja vuelve a los brazos con su contenido.
    void ReturnToArms()
    {
        IsDeploying = false;

        if (carried != null) carried.gameObject.SetActive(true);
    }

    // Colocado de verdad: la caja ya no hace falta.
    void OnContentPlaced()
    {
        IsDeploying = false;

        if (carried != null) Destroy(carried.gameObject);

        carried = null;
    }

    // Dejar la caja se coloca igual que una maquina: fantasma, rejilla y clic.
    public void Drop()
    {
        if (carried == null) return;

        IsPlacingBox = true;

        // El fantasma se clona de la caja, asi que primero le devolvemos su
        // tamano real y la sacamos de las manos.
        carried.transform.SetParent(null, true);
        carried.transform.localScale = originalScale;

        PlacementManager.Instance.StartMoving(carried.gameObject, OnBoxPlaced, OnBoxPlacementCancelled);

        carried.gameObject.SetActive(false);
    }

    void OnBoxPlaced(Vector3 position, Quaternion rotation)
    {
        IsPlacingBox = false;

        if (carried == null) return;

        carried.gameObject.SetActive(true);
        carried.transform.SetPositionAndRotation(position, rotation);

        SetPhysicsEnabled(carried, true);

        carried = null;
    }

    void OnBoxPlacementCancelled()
    {
        IsPlacingBox = false;

        if (carried == null) return;

        carried.gameObject.SetActive(true);

        carried.transform.SetParent(holdPoint, false);
        carried.transform.localPosition = heldLocalPosition;
        carried.transform.localRotation = Quaternion.Euler(heldLocalEuler);
        carried.transform.localScale = originalScale * heldScale;
    }

    // El aviso se queda fijo mientras lleves la caja: no depende de acordarse.
    void ShowHint(string message)
    {
        hintVisible = true;

        if (shownHint == message) return;

        shownHint = message;
        if (InteractionUI.Instance != null) InteractionUI.Instance.ShowPrompt(message);
    }

    void HideHint()
    {
        if (!hintVisible) return;

        hintVisible = false;
        shownHint = "";
        if (InteractionUI.Instance != null) InteractionUI.Instance.HidePrompt();
    }

    void SetPhysicsEnabled(CarriableBox box, bool enabled)
    {
        Rigidbody rb = box.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Primero quitar el kinematic: tocar la velocidad de un rigidbody
            // kinematico es un error en Unity.
            rb.isKinematic = !enabled;
            rb.detectCollisions = enabled;

            if (enabled)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // Si al reaparecer roza algo, que se separe despacio en vez de
                // salir rebotada.
                rb.maxDepenetrationVelocity = 0.5f;
            }
        }

        foreach (Collider col in box.GetComponentsInChildren<Collider>())
        {
            col.enabled = enabled;
        }
    }
}
