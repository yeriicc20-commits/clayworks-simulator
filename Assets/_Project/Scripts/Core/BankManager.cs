using UnityEngine;
using System.Collections.Generic;

// El banco. Presta dinero por adelantado y se lo cobra en cuotas diarias, que
// caen en la misma lista de facturas que la luz. Solo se puede pedir el primer
// prestamo; los gordos se desbloquean cuando has devuelto el anterior.
public class BankManager : MonoBehaviour
{
    public static BankManager Instance;

    public class Loan
    {
        public int index;
        public float amount;      // lo que te dan
        public float toRepay;     // lo que devuelves en total
        public int days;          // en cuantas cuotas

        public bool taken;
        public int installmentsIssued;
        public int installmentsPaid;

        public bool Active { get { return taken && installmentsPaid < days; } }
        public bool Repaid { get { return taken && installmentsPaid >= days; } }

        public float Installment { get { return days <= 0 ? toRepay : toRepay / days; } }
        public float Remaining { get { return Installment * (days - installmentsPaid); } }
        public float Interest { get { return toRepay - amount; } }
    }

    public readonly List<Loan> Loans = new List<Loan>();

    void Awake()
    {
        Instance = this;

        if (Loans.Count == 0) BuildOffers();
    }

    public static BankManager EnsureExists()
    {
        if (Instance != null) return Instance;

        BankManager existing = FindAnyObjectByType<BankManager>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        return new GameObject("BankManager").AddComponent<BankManager>();
    }

    void BuildOffers()
    {
        Loans.Add(new Loan { index = 0, amount = 1000f, toRepay = 1200f, days = 10 });
        Loans.Add(new Loan { index = 1, amount = 3000f, toRepay = 3750f, days = 15 });
        Loans.Add(new Loan { index = 2, amount = 5000f, toRepay = 6500f, days = 20 });
    }

    // El primero esta abierto desde el principio. Cada uno de los siguientes
    // pide tener devuelto el anterior: nada de encadenar deudas.
    public bool IsAvailable(Loan loan)
    {
        if (loan == null || loan.taken) return false;
        if (loan.index == 0) return true;

        Loan previous = Loans[loan.index - 1];

        return previous.Repaid;
    }

    public bool Take(Loan loan)
    {
        if (!IsAvailable(loan)) return false;
        if (GameManager.Instance == null) return false;

        loan.taken = true;

        GameManager.Instance.AddMoney(loan.amount);

        return true;
    }

    // Al cerrar el dia cada prestamo vivo deja su cuota en las facturas.
    public void ChargeDay(int day)
    {
        EconomyManager economy = EconomyManager.EnsureExists();

        if (economy == null) return;

        foreach (Loan loan in Loans)
        {
            if (!loan.taken) continue;
            if (loan.installmentsIssued >= loan.days) continue;

            loan.installmentsIssued++;

            Loan captured = loan;

            EconomyManager.Bill bill = new EconomyManager.Bill();
            bill.day = day;
            bill.concept = "Cuota prestamo " + GameManager.Format(loan.amount);
            bill.amount = loan.Installment;
            bill.onPaid = () => captured.installmentsPaid++;

            economy.Pending.Add(bill);
        }
    }

    public float DebtTotal
    {
        get
        {
            float total = 0f;

            foreach (Loan loan in Loans)
            {
                if (loan.Active) total += loan.Remaining;
            }

            return total;
        }
    }
}
