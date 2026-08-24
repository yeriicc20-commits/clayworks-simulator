using UnityEngine;
using System.Collections.Generic;

// Los gastos fijos del local. De momento solo la luz, que sube con lo que tengas
// enchufado, pero el sitio para anadir alquiler o mantenimiento es este.
public class EconomyManager : MonoBehaviour
{
    public static EconomyManager Instance;

    [Header("Factura de la luz")]
    [Tooltip("Lo que pagas aunque el local este vacio.")]
    public int baseElectricity = 15;
    [Tooltip("Lo que suma cada maquina de garra encendida.")]
    public int perMachine = 8;
    [Tooltip("Lo que suma cualquier otro trasto colocado.")]
    public int perDevice = 2;

    // Cada dia genera su propio cargo. Se acumulan hasta que los pagas uno a
    // uno, que es lo que deja ver cuantos dias llevas debiendo.
    public class Bill
    {
        public int day;
        public string concept;
        public float amount;

        // Aviso opcional para quien haya emitido el cargo. Lo usa el banco para
        // ir tachando cuotas del prestamo segun las pagas.
        public System.Action onPaid;
    }

    public readonly List<Bill> Pending = new List<Bill>();

    public float PendingTotal
    {
        get
        {
            float total = 0f;

            foreach (Bill bill in Pending) total += bill.amount;

            return total;
        }
    }

    public int DaysOverdue { get { return Pending.Count; } }

    void Awake()
    {
        Instance = this;
    }

    public static EconomyManager EnsureExists()
    {
        if (Instance != null) return Instance;

        EconomyManager existing = FindAnyObjectByType<EconomyManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        return new GameObject("EconomyManager").AddComponent<EconomyManager>();
    }

    public int MachineCount
    {
        get { return ClawController.AllMachines.Count; }
    }

    // Todo lo colocado que no sea una maquina: ordenador, mostrador, etc.
    public int DeviceCount
    {
        get
        {
            int total = 0;

            foreach (PlaceableObject placed in PlaceableObject.AllPlaced)
            {
                if (placed == null) continue;
                if (placed.GetComponentInParent<ClawController>() != null) continue;

                total++;
            }

            return total;
        }
    }

    public int CurrentDailyBill
    {
        get { return baseElectricity + MachineCount * perMachine + DeviceCount * perDevice; }
    }

    // Se llama al cerrar el dia: deja un cargo nuevo en la lista.
    public void ChargeDay(int day)
    {
        Bill bill = new Bill();
        bill.day = day;
        bill.concept = "Luz";
        bill.amount = CurrentDailyBill;

        Pending.Add(bill);
    }

    public bool Pay(Bill bill)
    {
        if (bill == null || !Pending.Contains(bill)) return false;
        if (GameManager.Instance == null) return false;

        if (!GameManager.Instance.SpendMoney(bill.amount)) return false;

        Pending.Remove(bill);

        if (bill.onPaid != null) bill.onPaid();

        return true;
    }

    // De la mas antigua a la mas nueva, y para en cuanto no llegue el dinero.
    public int PayAll()
    {
        int paid = 0;

        while (Pending.Count > 0)
        {
            if (!Pay(Pending[0])) break;

            paid++;
        }

        return paid;
    }
}
