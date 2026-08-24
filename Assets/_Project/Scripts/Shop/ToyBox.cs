using UnityEngine;

// Caja de peluches: se lleva en brazos y se vacia apuntando a una maquina.
public class ToyBox : CarriableBox
{
    [HideInInspector] public GameObject toyPrefab;
    [HideInInspector] public int toyCount = 10;

    [Header("Meter juguetes")]
    public float insertDistance = 5f;
    public float insertInterval = 0.4f;

    private float insertTimer = 0f;

    public override string CarryHint
    {
        get
        {
            return "Apunta a una maquina y manten clic para meter peluches (" + toyCount + ") - Clic fuera: dejar la caja";
        }
    }

    // Clic apuntando a una maquina llena; clic a cualquier otro sitio deja la
    // caja, igual que con las demas.
    public override bool HandleCarryInput(BoxCarrier carrier)
    {
        if (!Input.GetMouseButton(0))
        {
            insertTimer = 0f;
            return false;
        }

        ClawController machine = AimedMachine();
        if (machine == null) return false;

        insertTimer -= Time.deltaTime;

        if (insertTimer <= 0f)
        {
            InsertOne(machine, carrier);
            insertTimer = insertInterval;
        }

        return true;
    }

    ClawController AimedMachine()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, insertDistance, ~0, QueryTriggerInteraction.Ignore)) return null;

        return hit.collider.GetComponentInParent<ClawController>();
    }

    void InsertOne(ClawController machine, BoxCarrier carrier)
    {
        machine.SpawnToyInside(toyPrefab);
        toyCount--;

        if (toyCount <= 0)
        {
            NotificationManager.Instance.ShowMessage("Caja vacia");
            carrier.ConsumeCarriedBox();
        }
        else
        {
            NotificationManager.Instance.ShowMessage("Peluches restantes: " + toyCount);
        }
    }
}
