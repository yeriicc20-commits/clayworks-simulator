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

    void OnTriggerEnter(Collider other) { Mirar(other); }

    // Tambien mientras esta dentro, no solo al entrar.
    //
    // Un peluche que se queda parado en la boca no vuelve a disparar Enter
    // nunca, y se quedaba ahi sin ser de nadie: ni premio ni de la maquina.
    void OnTriggerStay(Collider other) { Mirar(other); }

    void Mirar(Collider other)
    {
        PlushItem plush = other.GetComponentInParent<PlushItem>();

        if (plush == null || plush.isGrabbed || plush.collected) return;

        // Ya NO se exige que la garra lo haya agarrado.
        //
        // Se exigia, y por eso un peluche que la garra tira al agujero de un
        // empujon se quedaba en el cajon sin poder cogerse y con la puerta
        // cerrada. En una maquina de verdad lo que cae por el agujero es tuyo,
        // lo hayas cogido con la garra o lo hayas empujado. Es media gracia del
        // juego, de hecho.
        Collect(plush);
    }

    // Punto unico de entrega. Lo llama tanto el trigger como la propia garra
    // cuando suelta el premio, para no depender de donde acabe cayendo.
    public void Collect(PlushItem plush)
    {
        if (plush == null || plush.collected) return;

        plush.collected = true;

        // El golpe del peluche contra la chapa del cajon.
        if (clawController != null)
        {
            ClawAudio audio3d = clawController.GetComponent<ClawAudio>();
            if (audio3d != null) audio3d.PelucheCae();
        }

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
            // Antes se destruia aqui mismo: cobrabas y el peluche
            // desaparecia. Ahora se queda en el cajon para que el jugador se
            // agache y lo coja. El dinero sigue saliendo igual, que es lo que
            // paga la maquina; esto solo anade poder llevarselo.
            DejarloParaRecoger(plush);
        }
    }

    // Lo prepara para que se pueda recoger del suelo.
    void DejarloParaRecoger(PlushItem plush)
    {
        // El componente se pone AQUI y no en el prefab a proposito. Los
        // peluches que estan dentro de la maquina son del dueno, y con esto
        // puesto de fabrica se podrian sacar a traves del cristal sin mas que
        // agacharse delante.
        if (plush.GetComponent<PelucheRecogible>() == null)
        {
            plush.gameObject.AddComponent<PelucheRecogible>();
        }

        Rigidbody rb = plush.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Que se calme deprisa dentro del cajon en vez de rodar hasta un
            // rincon donde ya no se llegue a cogerlo.
            rb.linearDamping = Mathf.Max(rb.linearDamping, 1.2f);
            rb.angularDamping = Mathf.Max(rb.angularDamping, 2.5f);
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
