using UnityEngine;

public class PlushDropZone : MonoBehaviour
{
    public int moneyReward = 20;
    public bool rewardOnlyForPlayer = true;
    public ClawController clawController;

    void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        PlushItem plush = other.GetComponentInParent<PlushItem>();
        if (plush == null || plush.isGrabbed || !plush.hasBeenGrabbed) return;

        Collect(plush);
    }

    // Punto unico de entrega. Lo llama tanto el trigger como la propia garra
    // cuando suelta el premio, para no depender de donde acabe cayendo.
    public void Collect(PlushItem plush)
    {
        if (plush == null || plush.collected) return;

        plush.collected = true;

        Transform carrySpot = clawController != null ? clawController.activeCarrySpot : null;

        if (carrySpot == null || !rewardOnlyForPlayer)
        {
            if (GameManager.Instance != null) GameManager.Instance.AddMoney(moneyReward);

            LevelManager levels = LevelManager.EnsureExists();
            if (levels != null) levels.Add(levels.xpPrizeSold);
        }

        if (carrySpot != null)
        {
            GiveToCarrier(plush, carrySpot);
        }
        else
        {
            Destroy(plush.gameObject);
        }
    }

    void GiveToCarrier(PlushItem plush, Transform carrySpot)
    {
        Rigidbody rb = plush.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        foreach (Collider col in plush.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
        }

        plush.transform.SetParent(carrySpot);
        plush.transform.localPosition = Vector3.zero;
        plush.transform.localRotation = Quaternion.identity;
    }
}
