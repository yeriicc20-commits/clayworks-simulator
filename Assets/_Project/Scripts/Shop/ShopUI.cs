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

    // Que pestana se esta viendo. Con dos bastaba un bool; con tres ya no.
    enum Pestana { Maquinas, Juguetes, Luces }

    private Pestana pestana = Pestana.Maquinas;
    private Button tabLucesButton;

    void Start()
    {
        if (tabMaquinasButton != null) tabMaquinasButton.onClick.AddListener(ShowMaquinas);
        if (tabJuguetesButton != null) tabJuguetesButton.onClick.AddListener(ShowJuguetes);

        CrearPestanaLuces();
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

    // La pestana de luces se clona de la de juguetes en vez de ponerla en la
    // escena.
    //
    // Clonada sale con el mismo tipo de letra, el mismo tamano y el mismo
    // fondo que las otras dos, y sigue igual el dia que se retoque el aspecto
    // de la tienda. Puesta a mano en la escena habria que acordarse de tocar
    // las tres cada vez.
    void CrearPestanaLuces()
    {
        if (tabJuguetesButton == null || tabMaquinasButton == null) return;
        if (tabLucesButton != null) return;

        RectTransform arriba = tabMaquinasButton.GetComponent<RectTransform>();
        RectTransform modelo = tabJuguetesButton.GetComponent<RectTransform>();

        tabLucesButton = Instantiate(tabJuguetesButton, modelo.parent);
        tabLucesButton.name = "Tab_Luces";

        RectTransform r = tabLucesButton.GetComponent<RectTransform>();

        // Debajo de juguetes, a la misma distancia que juguetes de maquinas:
        // asi las tres quedan repartidas igual sin numeros a mano.
        r.anchoredPosition = modelo.anchoredPosition
                             + (modelo.anchoredPosition - arriba.anchoredPosition);

        var texto = tabLucesButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (texto != null) texto.text = "LUCES";

        tabLucesButton.onClick.RemoveAllListeners();
        tabLucesButton.onClick.AddListener(ShowLuces);
    }

    public void RefreshCurrentTab()
    {
        if (pestana == Pestana.Luces) ShowLuces();
        else if (pestana == Pestana.Juguetes) ShowJuguetes();
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
        pestana = Pestana.Maquinas;

        maquinasPanel.SetActive(true);
        juguetesPanel.SetActive(false);
        ShopManager.Instance.GenerateShopUI();
    }

    public void ShowJuguetes()
    {
        pestana = Pestana.Juguetes;

        AbrirPanelDeLista();
        PopulateToyList();
    }

    public void ShowLuces()
    {
        pestana = Pestana.Luces;

        AbrirPanelDeLista();
        PopulateDecoList();
    }

    // Luces y juguetes comparten panel y contenedor: la lista se vacia y se
    // vuelve a llenar en cada cambio, asi que no hacen falta dos.
    void AbrirPanelDeLista()
    {
        maquinasPanel.SetActive(false);
        juguetesPanel.SetActive(true);

        if (machineListContainer != null) machineListContainer.gameObject.SetActive(false);
        if (noMachinesText != null) noMachinesText.SetActive(false);
        if (toySelectionPanel != null) toySelectionPanel.SetActive(true);
    }

    void PopulateDecoList()
    {
        foreach (Transform child in toyListContainer)
        {
            Destroy(child.gameObject);
        }

        DecoShopItem[] deco = ShopManager.Instance.decoItems;

        FilaLista.PrepararLista(toyListContainer);

        if (deco == null || deco.Length == 0)
        {
            NotificationManager.Nota("La pestana de luces esta vacia: pasa"
                                     + " ClayWorks > Construir luces.");
            return;
        }

        for (int i = 0; i < deco.Length; i++)
        {
            int index = i;

            FilaLista.Crear(toyListContainer, i, deco[index].itemName,
                            deco[index].icon, null, deco[index].price,
                            "Anadir", new Color(0.15f, 0.62f, 0.35f),
                            () => ShopManager.Instance.AddDecoToCart(index));
        }
    }

    void PopulateToyList()
    {
        foreach (Transform child in toyListContainer)
        {
            Destroy(child.gameObject);
        }

        ToyShopItem[] toys = ShopManager.Instance.toyItems;

        FilaLista.PrepararLista(toyListContainer);

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
