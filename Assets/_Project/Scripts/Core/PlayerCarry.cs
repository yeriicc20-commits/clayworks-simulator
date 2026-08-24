// El jugador solo puede llevar una caja encima: o una de juguetes, o una
// maquina pendiente de colocar. Aqui se centraliza la comprobacion para que
// todos los sitios que recogen cosas usen la misma regla.
public static class PlayerCarry
{
    public static bool IsCarryingToyBox
    {
        get { return ToyBoxCarrier.Instance != null && ToyBoxCarrier.Instance.IsCarrying; }
    }

    public static bool IsPlacingMachine
    {
        get { return PlacementManager.Instance != null && PlacementManager.Instance.IsPlacing; }
    }

    public static bool IsCarryingBox
    {
        get { return BoxCarrier.Instance != null && BoxCarrier.Instance.IsCarrying; }
    }

    public static bool HandsFull
    {
        get { return IsCarryingToyBox || IsPlacingMachine || IsCarryingBox; }
    }

    // Con el ordenador o la pantalla de precios abiertos no se toca nada mas.
    public static bool Busy
    {
        get { return HandsFull || MonitorTerminal.InUse || PricePanel.IsOpen; }
    }

    public static string BusyMessage
    {
        get
        {
            if (IsCarryingToyBox) return "Ya llevas una caja de juguetes";
            if (IsCarryingBox) return "Ya llevas una caja en brazos";
            if (IsPlacingMachine) return "Estas colocando algo";
            if (MonitorTerminal.InUse) return "Estas usando el ordenador";

            return "";
        }
    }
}
