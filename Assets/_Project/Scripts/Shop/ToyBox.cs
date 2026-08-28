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

        Component machine = AimedMachine();
        if (machine == null) return false;

        insertTimer -= Time.deltaTime;

        if (insertTimer <= 0f)
        {
            InsertOne(machine, carrier);
            insertTimer = insertInterval;
        }

        return true;
    }

    // Vale cualquiera de las dos maquinas del local. Devuelve el componente que
    // haya encontrado y quien lo use ya decide como meterle el juguete: la de
    // garra los echa dentro a monton, la de puente admite uno solo.
    Component AimedMachine()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, insertDistance, ~0, QueryTriggerInteraction.Ignore)) return null;

        ClawController garra = hit.collider.GetComponentInParent<ClawController>();
        if (garra != null) return garra;

        // La de puente no se busca hacia arriba, sino desde la RAIZ hacia abajo.
        //
        // Su PrizeSpawner vive en PrizeArea, que es hermano del cristal y del
        // faldon, no antepasado suyo: mirando hacia arriba desde lo que toca el
        // rayo no aparece nunca, y el clic se quedaba sin hacer nada. En la de
        // garra funciona de casualidad, porque su controlador si esta en la raiz.
        Transform raiz = hit.collider.transform.root;

        return raiz != null
            ? raiz.GetComponentInChildren<Hashi.PrizeSpawner>(true)
            : null;
    }

    void InsertOne(Component machine, BoxCarrier carrier)
    {
        // La de puente solo admite un premio a la vez, y hay que enterarse ANTES
        // de descontarlo de la caja: si no, cada clic sobre una maquina llena se
        // comeria un juguete sin meter nada.
        if (machine is Hashi.PrizeSpawner barras)
        {
            if (!barras.MeterPremio(toyPrefab, out string motivo))
            {
                NotificationManager.Nota(motivo);
                return;
            }
        }
        else if (machine is ClawController garra)
        {
            // Cada maquina admite lo suyo y nada mas. Un peluche no se sostiene
            // sobre dos barras y una caja de figura no hay quien la levante con
            // tres dedos: metidos donde no toca, el juguete no es que sea
            // dificil de sacar, es que la maquina deja de funcionar y parece
            // rota.
            //
            // La de puente ya se defendia sola dentro de MeterPremio; esta no,
            // y se tragaba lo que le echaran.
            if (toyPrefab == null || toyPrefab.GetComponent<PlushItem>() == null)
            {
                NotificationManager.Nota("Esto no va en la maquina de garra");
                return;
            }

            garra.SpawnToyInside(toyPrefab);
        }
        else
        {
            return;
        }

        toyCount--;

        if (toyCount <= 0)
        {
            NotificationManager.Nota("Caja vacia");
            carrier.ConsumeCarriedBox();
        }
        else
        {
            NotificationManager.Nota("Quedan " + toyCount + " en la caja");
        }
    }
}
