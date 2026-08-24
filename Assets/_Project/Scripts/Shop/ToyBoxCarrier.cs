using UnityEngine;

public class ToyBoxCarrier : MonoBehaviour
{
    public static ToyBoxCarrier Instance;

    public Camera playerCamera;
    public float insertDistance = 5f;
    public float insertInterval = 0.4f;

    private GameObject currentToyPrefab;
    private int remainingCount;
    private bool isCarrying = false;
    private float insertTimer = 0f;

    public bool IsCarrying { get { return isCarrying; } }

    void Awake()
    {
        Instance = this;
    }

    public void StartCarrying(GameObject toyPrefab, int count)
    {
        currentToyPrefab = toyPrefab;
        remainingCount = count;
        isCarrying = true;
        insertTimer = 0f;

        NotificationManager.Instance.ShowMessage("Llevas una caja con " + count + " peluches. Mira a una maquina y manten click izquierdo");
    }

    void Update()
    {
        if (!isCarrying) return;

        if (Input.GetMouseButton(0))
        {
            insertTimer -= Time.deltaTime;

            if (insertTimer <= 0f)
            {
                TryInsertOne();
                insertTimer = insertInterval;
            }
        }
        else
        {
            insertTimer = 0f;
        }
    }

    void TryInsertOne()
    {
        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit, insertDistance)) return;

        ClawController machine = hit.collider.GetComponentInParent<ClawController>();
        if (machine == null) return;

        machine.SpawnToyInside(currentToyPrefab);
        remainingCount--;

        if (remainingCount <= 0)
        {
            NotificationManager.Instance.ShowMessage("Caja vacia");
            StopCarrying();
        }
        else
        {
            NotificationManager.Instance.ShowMessage("Peluches restantes: " + remainingCount);
        }
    }

    void StopCarrying()
    {
        isCarrying = false;
        currentToyPrefab = null;
        remainingCount = 0;
    }
}
