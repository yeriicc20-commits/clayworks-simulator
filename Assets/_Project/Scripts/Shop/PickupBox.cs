using UnityEngine;

// Caja con una maquina o un mueble dentro: E lo saca para colocarlo.
public class PickupBox : CarriableBox
{
    [HideInInspector] public GameObject machinePrefab;

    public override string CarryHint
    {
        get { return "E: sacar el contenido - Clic izquierdo: dejar la caja"; }
    }

    public override void OnUseKey(BoxCarrier carrier)
    {
        carrier.DeployContents(machinePrefab);
    }
}
