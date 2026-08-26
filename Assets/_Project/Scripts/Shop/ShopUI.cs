using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    public GameObject shopPanel;

    [Header("Pestanas")]
    public Button tabMaquinasButton;
    public Button tabJuguetesButton;
    public GameObject maquinasPanel;
    public GameObject juguetesPanel;

    [Header("Juguetes")]
    public Transform machineListContainer;
    public GameObject noMachinesText;
    public GameObject toySelectionPanel;
    public Transform toyListContainer;
    public Button backToMachinesButton;

    public bool IsOpen { get; private set; }

    private bool showingJuguetes = false;

    void Start()
    {
        if (tabMaquinasButton != null) tabMaquinasButton.onClick.AddListener(ShowMaquinas);
        if (tabJuguetesButton != null) tabJuguetesButton.onClick.AddListener(ShowJuguetes);
        if (backToMachinesButton != null) backToMachinesButton.gameObject.SetActive(false);
    }

    // El monitor esta siempre encendido, aunque no lo estes usando.
    public void ShowOnMonitor()
    {
        if (shopPanel == null) return;

        shopPanel.SetActive(true);

        ComputerUI.EnsureExists().Build(this);

        RefreshCurrentTab();
    }

    public void RefreshCurrentTab()
    {
        if (showingJuguetes) ShowJuguetes();
        else ShowMaquinas();
    }

    // Abrir y cerrar no encienden ni apagan la pantalla: solo cambian si puedes
    // tocarla. Asi al salir sigues viendo la pestana que dejaste.
    public void Open()
    {
        IsOpen = true;

        // Al encender el ordenador siempre se entra por la tienda.
        if (ComputerUI.Instance != null) ComputerUI.Instance.OpenAtHome();
        else if (shopPanel != null) shopPanel.SetActive(true);

        RefreshCurrentTab();
    }

    public void Close()
    {
        IsOpen = false;
    }

    public void OnBuyButtonPressed(int itemIndex)
    {
        ShopManager.Instance.AddItemToCart(itemIndex);
    }

    public void ShowMaquinas()
    {
        showingJuguetes = false;

        maquinasPanel.SetActive(true);
        juguetesPanel.SetActive(false);
        ShopManager.Instance.GenerateShopUI();
    }

    public void ShowJuguetes()
    {
        showingJuguetes = true;

        maquinasPanel.SetActive(false);
        juguetesPanel.SetActive(true);

        if (machineListContainer != null) machineListContainer.gameObject.SetActive(false);
        if (noMachinesText != null) noMachinesText.SetActive(false);
        if (toySelectionPanel != null) toySelectionPanel.SetActive(true);

        PopulateToyList();
    }

    void PopulateToyList()
    {
        foreach (Transform child in toyListContainer)
        {
            Destroy(child.gameObject);
        }

        ToyShopItem[] toys = ShopManager.Instance.toyItems;

        // Misma lista que las maquinas: las dos pestanas se leen igual.
        for (int i = 0; i < toys.Length; i++)
        {
            int index = i;

            FilaLista.Crear(toyListContainer, i, toys[index].itemName,
                            toys[index].icon, null, toys[index].price,
                            "Anadir", new Color(0.15f, 0.62f, 0.35f),
                            () => ShopManager.Instance.AddToyToCart(index));
        }
    }
}
