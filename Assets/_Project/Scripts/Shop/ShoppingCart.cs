using UnityEngine;
using System.Collections.Generic;

public class CartLine
{
    public string itemName;
    public int unitPrice;
    public Sprite icon;
    public int quantity;
    public System.Action<int> onPurchaseOne;
}

public class ShoppingCart : MonoBehaviour
{
    public static ShoppingCart Instance;

    private List<CartLine> lines = new List<CartLine>();

    void Awake()
    {
        Instance = this;
    }

    public void AddItem(string itemName, int unitPrice, Sprite icon, System.Action<int> onPurchaseOne)
    {
        foreach (CartLine line in lines)
        {
            if (line.itemName == itemName)
            {
                line.quantity++;
                CartUI.Instance.Refresh(lines);
                return;
            }
        }

        lines.Add(new CartLine { itemName = itemName, unitPrice = unitPrice, icon = icon, quantity = 1, onPurchaseOne = onPurchaseOne });
        CartUI.Instance.Refresh(lines);
    }

    public void RemoveOne(string itemName)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].itemName == itemName)
            {
                lines[i].quantity--;
                if (lines[i].quantity <= 0) lines.RemoveAt(i);
                break;
            }
        }

        CartUI.Instance.Refresh(lines);
    }

    public int GetTotalPrice()
    {
        int total = 0;
        foreach (CartLine line in lines) total += line.unitPrice * line.quantity;
        return total;
    }

    public List<CartLine> GetLines()
    {
        return lines;
    }

    public void Checkout()
    {
        if (lines.Count == 0)
        {
            NotificationManager.Instance.ShowMessage("El carrito esta vacio");
            return;
        }

        int total = GetTotalPrice();
        bool paid = GameManager.Instance.SpendMoney(total);

        if (!paid)
        {
            NotificationManager.Instance.ShowMessage("No tienes dinero suficiente");
            return;
        }

        int spawnIndex = 0;
        int failed = 0;

        foreach (CartLine line in lines)
        {
            for (int i = 0; i < line.quantity; i++)
            {
                // Si una entrega falla, que no se lleve por delante el resto del
                // pedido: ya has pagado por todo.
                try
                {
                    line.onPurchaseOne(spawnIndex);
                }
                catch (System.Exception error)
                {
                    failed++;
                    Debug.LogError("[ShoppingCart] No he podido entregar \"" + line.itemName + "\": " + error.Message);
                }

                spawnIndex++;
            }
        }

        if (failed > 0)
        {
            GameManager.Instance.AddMoney(total);
            NotificationManager.Instance.ShowMessage("Fallo en la entrega, dinero devuelto");
        }
        else
        {
            NotificationManager.Nota("Compra realizada");
        }

        lines.Clear();
        CartUI.Instance.Refresh(lines);
    }
}
