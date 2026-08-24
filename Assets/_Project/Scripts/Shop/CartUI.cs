using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class CartUI : MonoBehaviour
{
    public static CartUI Instance;

    public GameObject cartPanel;
    public Button cartButton;
    public Button checkoutButton;
    public Transform cartListContainer;
    public GameObject itemCardPrefab;
    public TextMeshProUGUI totalText;
    public TextMeshProUGUI miniTotalText;

    private bool isOpen = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (cartButton != null) cartButton.onClick.AddListener(ToggleCart);
        if (checkoutButton != null) checkoutButton.onClick.AddListener(() => ShoppingCart.Instance.Checkout());

        cartPanel.SetActive(false);
        Refresh(ShoppingCart.Instance.GetLines());
    }

    void ToggleCart()
    {
        isOpen = !isOpen;
        cartPanel.SetActive(isOpen);

        if (isOpen)
        {
            Refresh(ShoppingCart.Instance.GetLines());
        }
    }

    public void Refresh(List<CartLine> lines)
    {
        foreach (Transform child in cartListContainer)
        {
            Destroy(child.gameObject);
        }

        int total = 0;

        foreach (CartLine line in lines)
        {
            total += line.unitPrice * line.quantity;

            GameObject card = Instantiate(itemCardPrefab, cartListContainer);
            ItemCardUI cardUI = card.GetComponent<ItemCardUI>();

            string label = line.itemName + " x" + line.quantity;

            cardUI.Setup(label, line.unitPrice * line.quantity, line.icon, () => ShoppingCart.Instance.RemoveOne(line.itemName));
        }

        if (totalText != null)
        {
            totalText.text = "Total: " + GameManager.Format(total);
        }

        if (miniTotalText != null)
        {
            miniTotalText.text = GameManager.Format(total);
        }
    }
}
