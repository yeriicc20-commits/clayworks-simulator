using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// El menu del ordenador: TIENDA, PAGOS, PERSONAL y PRECIOS. Se construye por
// codigo sobre el canvas que ya existe, para no depender de montarlo a mano.
public class ComputerUI : MonoBehaviour
{
    public static ComputerUI Instance;

    public enum Section { Tienda, Expansion, Pagos, Personal, Precios }

    public enum PagosTab { Facturas, Banco }

    [Header("Aspecto")]
    public float navWidth = 190f;
    public float navButtonHeight = 54f;
    [Tooltip("Cuanto sube o baja el precio con los botones + y -.")]
    public float priceStep = 0.25f;
    [Tooltip("Alto de las pestanas de dentro de Pagos.")]
    public float pagosTabHeight = 40f;

    private ShopUI shopUI;
    private RectTransform root;
    private RectTransform content;

    private readonly Dictionary<Section, RectTransform> panels = new Dictionary<Section, RectTransform>();
    private readonly Dictionary<Section, Button> navButtons = new Dictionary<Section, Button>();

    private Section current = Section.Tienda;
    private bool built = false;

    // Referencias que hay que refrescar al abrir
    private TextMeshProUGUI billSummary;
    private RectTransform priceList;
    private RectTransform billList;
    private RectTransform expansionGrid;
    private TextMeshProUGUI expansionInfo;

    // Pagos tiene dos pestanas propias: las facturas y el banco.
    private RectTransform facturasPanel;
    private RectTransform bancoPanel;
    private RectTransform loanList;
    private TextMeshProUGUI bankSummary;

    private readonly Dictionary<PagosTab, Button> pagosTabs = new Dictionary<PagosTab, Button>();
    private PagosTab currentPagosTab = PagosTab.Facturas;

    void Awake()
    {
        Instance = this;
    }

    public static ComputerUI EnsureExists()
    {
        if (Instance != null) return Instance;

        ComputerUI existing = FindAnyObjectByType<ComputerUI>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        return new GameObject("ComputerUI").AddComponent<ComputerUI>();
    }

    public void Build(ShopUI shop)
    {
        if (built || shop == null || shop.shopPanel == null) return;

        shopUI = shop;

        RectTransform shopPanel = shop.shopPanel.GetComponent<RectTransform>();
        root = shopPanel.parent as RectTransform;

        if (root == null) return;

        BuildNav();
        BuildContentArea(shopPanel);

        built = true;

        Show(Section.Tienda);
    }

    void BuildNav()
    {
        Image bar = UIFactory.Box("NavBar", root, new Color(0.16f, 0.18f, 0.22f));

        bar.rectTransform.anchorMin = new Vector2(0f, 0f);
        bar.rectTransform.anchorMax = new Vector2(0f, 1f);
        bar.rectTransform.pivot = new Vector2(0f, 0.5f);
        bar.rectTransform.sizeDelta = new Vector2(navWidth, 0f);
        bar.rectTransform.anchoredPosition = Vector2.zero;

        UIFactory.Column(bar.rectTransform, 8f, new RectOffset(12, 12, 16, 12));

        TextMeshProUGUI title = UIFactory.Text("Titulo", bar.transform, "GESTION", 20, new Color(0.6f, 0.65f, 0.75f), TextAlignmentOptions.Center);
        UIFactory.Height(title.rectTransform, 34f);

        AddNavButton(bar.transform, Section.Tienda, "TIENDA");
        AddNavButton(bar.transform, Section.Expansion, "EXPANSION");
        AddNavButton(bar.transform, Section.Pagos, "PAGOS");
        AddNavButton(bar.transform, Section.Personal, "PERSONAL");
        AddNavButton(bar.transform, Section.Precios, "PRECIOS");
    }

    void AddNavButton(Transform parent, Section section, string label)
    {
        Button button = UIFactory.Button(section.ToString(), parent, label, 20,
            new Color(0.24f, 0.27f, 0.33f), Color.white, () => Show(section));

        UIFactory.Height(button.GetComponent<RectTransform>(), navButtonHeight);

        navButtons[section] = button;
    }

    void BuildContentArea(RectTransform shopPanel)
    {
        // La tienda ya existe montada en la escena: se reaprovecha tal cual y
        // solo se le hace hueco a la derecha de la barra.
        UIFactory.Stretch(shopPanel, navWidth, 0f, 0f, 0f);
        panels[Section.Tienda] = shopPanel;

        content = UIFactory.Rect("Secciones", root);
        UIFactory.Stretch(content, navWidth, 0f, 0f, 0f);

        panels[Section.Expansion] = BuildExpansion();
        panels[Section.Pagos] = BuildPagos();
        panels[Section.Personal] = BuildPersonal();
        panels[Section.Precios] = BuildPrecios();
    }

    RectTransform SectionPanel(string name, string heading, out RectTransform body)
    {
        Image panel = UIFactory.Box(name, content, UIFactory.Panel);
        UIFactory.Stretch(panel.rectTransform, 0f, 0f, 0f, 0f);

        TextMeshProUGUI title = UIFactory.Text("Heading", panel.transform, heading, 34, UIFactory.Ink, TextAlignmentOptions.Left);
        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(-60f, 50f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -22f);

        body = UIFactory.Rect("Body", panel.transform);
        UIFactory.Stretch(body, 30f, 80f, 30f, 30f);

        return panel.rectTransform;
    }

    RectTransform BuildExpansion()
    {
        RectTransform body;
        RectTransform panel = SectionPanel("ExpansionPanel", "Expansion", out body);

        expansionInfo = UIFactory.Text("Info", body, "", 18, UIFactory.Muted, TextAlignmentOptions.TopLeft);
        expansionInfo.rectTransform.anchorMin = new Vector2(0f, 1f);
        expansionInfo.rectTransform.anchorMax = new Vector2(1f, 1f);
        expansionInfo.rectTransform.pivot = new Vector2(0.5f, 1f);
        expansionInfo.rectTransform.sizeDelta = new Vector2(0f, 44f);
        expansionInfo.rectTransform.anchoredPosition = Vector2.zero;

        expansionGrid = UIFactory.Rect("Casillas", body);
        UIFactory.Stretch(expansionGrid, 0f, 52f, 0f, 0f);

        GridLayoutGroup grid = expansionGrid.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(128f, 78f);
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 4;
        grid.childAlignment = TextAnchor.UpperCenter;

        return panel;
    }

    public void RefreshExpansion()
    {
        if (expansionGrid == null) return;

        foreach (Transform child in expansionGrid) Destroy(child.gameObject);

        ExpansionManager expansion = ExpansionManager.EnsureExists();

        if (expansionInfo != null)
        {
            int next = expansion.currentLevel + 1;

            expansionInfo.text = expansion.currentLevel >= expansion.levels
                ? "<color=#4CAF50>Local al maximo: las " + expansion.levels + " ampliaciones compradas.</color>"
                : "Ampliaciones compradas: <b>" + expansion.currentLevel + " / " + expansion.levels + "</b>" +
                  "   ·   Siguiente: <b>" + GameManager.Format(expansion.PriceFor(next)) + "</b>" +
                  "\n<size=15>" + SizeLine(next) + "</size>";
        }

        for (int level = 1; level <= expansion.levels; level++)
        {
            BuildExpansionCard(expansion, level);
        }
    }

    // Cada ampliacion engancha un recuadro nuevo al local: saber cual toca y
    // cuanto mide antes de pagar ayuda a decidir.
    string SizeLine(int nextLevel)
    {
        LocalLayout layout = LocalLayout.EnsureExists();

        if (layout == null || !layout.HasGeometry) return "Se compran en orden, una detras de otra.";

        return layout.DescribeLevel(nextLevel);
    }

    void BuildExpansionCard(ExpansionManager expansion, int level)
    {
        bool bought = expansion.IsBought(level);
        bool available = expansion.IsAvailable(level);

        Color background = bought
            ? new Color(0.82f, 0.93f, 0.83f)
            : available ? UIFactory.Card : new Color(0.90f, 0.90f, 0.92f);

        Image card = UIFactory.Box("Exp" + level, expansionGrid, background);

        TextMeshProUGUI title = UIFactory.Text("Titulo", card.transform, "Expansion " + level, 16,
            bought || available ? UIFactory.Ink : UIFactory.Muted, TextAlignmentOptions.Center);

        title.rectTransform.anchorMin = new Vector2(0f, 1f);
        title.rectTransform.anchorMax = new Vector2(1f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.sizeDelta = new Vector2(-8f, 24f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -6f);

        if (bought)
        {
            TextMeshProUGUI done = UIFactory.Text("Estado", card.transform, "<b>COMPRADA</b>", 15,
                new Color(0.18f, 0.55f, 0.28f), TextAlignmentOptions.Center);

            UIFactory.Stretch(done.rectTransform, 4f, 30f, 4f, 6f);
            return;
        }

        TextMeshProUGUI price = UIFactory.Text("Precio", card.transform,
            GameManager.Format(expansion.PriceFor(level)), 15,
            available ? UIFactory.Ink : UIFactory.Muted, TextAlignmentOptions.Center);

        price.rectTransform.anchorMin = new Vector2(0f, 1f);
        price.rectTransform.anchorMax = new Vector2(1f, 1f);
        price.rectTransform.pivot = new Vector2(0.5f, 1f);
        price.rectTransform.sizeDelta = new Vector2(-8f, 20f);
        price.rectTransform.anchoredPosition = new Vector2(0f, -28f);

        // Solo la siguiente en la fila se puede comprar; el resto queda a la vista
        // para que sepas lo que viene, pero bloqueado.
        if (!available)
        {
            TextMeshProUGUI locked = UIFactory.Text("Bloqueada", card.transform, "<size=13>Bloqueada</size>", 13,
                UIFactory.Muted, TextAlignmentOptions.Center);

            locked.rectTransform.anchorMin = new Vector2(0f, 0f);
            locked.rectTransform.anchorMax = new Vector2(1f, 0f);
            locked.rectTransform.pivot = new Vector2(0.5f, 0f);
            locked.rectTransform.sizeDelta = new Vector2(-8f, 24f);
            locked.rectTransform.anchoredPosition = new Vector2(0f, 6f);
            return;
        }

        Button buy = UIFactory.Button("Comprar", card.transform, "COMPRAR", 14,
            new Color(0.16f, 0.45f, 0.85f), Color.white, null);

        RectTransform rect = buy.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(-16f, 26f);
        rect.anchoredPosition = new Vector2(0f, 8f);

        buy.onClick.AddListener(() =>
        {
            if (expansion.Buy(level))
            {
                NotificationManager.Nota("Expansion " + level + " comprada");
            }
            else
            {
                NotificationManager.Instance.ShowMessage("No tienes dinero suficiente");
            }

            RefreshExpansion();
        });
    }

    RectTransform BuildPagos()
    {
        RectTransform body;
        RectTransform panel = SectionPanel("PagosPanel", "Pagos", out body);

        // Arriba del todo, las dos pestanas de la seccion.
        RectTransform tabs = UIFactory.Rect("Pestanas", body);
        tabs.anchorMin = new Vector2(0f, 1f);
        tabs.anchorMax = new Vector2(1f, 1f);
        tabs.pivot = new Vector2(0.5f, 1f);
        tabs.sizeDelta = new Vector2(0f, pagosTabHeight);
        tabs.anchoredPosition = Vector2.zero;

        HorizontalLayoutGroup row = tabs.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 8f;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = true;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childAlignment = TextAnchor.MiddleLeft;

        AddPagosTab(tabs, PagosTab.Facturas, "FACTURAS");
        AddPagosTab(tabs, PagosTab.Banco, "BANCO");

        float top = pagosTabHeight + 12f;

        facturasPanel = UIFactory.Rect("Facturas", body);
        UIFactory.Stretch(facturasPanel, 0f, top, 0f, 0f);

        billSummary = UIFactory.Text("Resumen", facturasPanel, "", 19, UIFactory.Ink, TextAlignmentOptions.TopLeft);
        billSummary.rectTransform.anchorMin = new Vector2(0f, 1f);
        billSummary.rectTransform.anchorMax = new Vector2(1f, 1f);
        billSummary.rectTransform.pivot = new Vector2(0.5f, 1f);
        billSummary.rectTransform.sizeDelta = new Vector2(0f, 72f);
        billSummary.rectTransform.anchoredPosition = Vector2.zero;

        Button payAll = UIFactory.Button("PagarTodo", facturasPanel, "PAGAR TODO", 19,
            new Color(0.18f, 0.65f, 0.32f), Color.white, PayEverything);

        RectTransform payRect = payAll.GetComponent<RectTransform>();
        payRect.anchorMin = new Vector2(1f, 1f);
        payRect.anchorMax = new Vector2(1f, 1f);
        payRect.pivot = new Vector2(1f, 1f);
        payRect.sizeDelta = new Vector2(170f, 44f);
        payRect.anchoredPosition = new Vector2(0f, -4f);

        billList = UIFactory.Rect("Cargos", facturasPanel);
        UIFactory.Stretch(billList, 0f, 84f, 0f, 0f);
        UIFactory.Column(billList, 8f, new RectOffset(0, 0, 0, 0));

        bancoPanel = UIFactory.Rect("Banco", body);
        UIFactory.Stretch(bancoPanel, 0f, top, 0f, 0f);

        bankSummary = UIFactory.Text("Resumen", bancoPanel, "", 19, UIFactory.Ink, TextAlignmentOptions.TopLeft);
        bankSummary.rectTransform.anchorMin = new Vector2(0f, 1f);
        bankSummary.rectTransform.anchorMax = new Vector2(1f, 1f);
        bankSummary.rectTransform.pivot = new Vector2(0.5f, 1f);
        bankSummary.rectTransform.sizeDelta = new Vector2(0f, 54f);
        bankSummary.rectTransform.anchoredPosition = Vector2.zero;

        loanList = UIFactory.Rect("Prestamos", bancoPanel);
        UIFactory.Stretch(loanList, 0f, 62f, 0f, 0f);
        UIFactory.Column(loanList, 8f, new RectOffset(0, 0, 0, 0));

        // Se entra siempre por facturas; el banco queda escondido detras.
        ShowPagosTab(PagosTab.Facturas);

        return panel;
    }

    void AddPagosTab(RectTransform parent, PagosTab tab, string label)
    {
        Button button = UIFactory.Button(tab.ToString(), parent, label, 17,
            new Color(0.24f, 0.27f, 0.33f), Color.white, () => ShowPagosTab(tab));

        LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
        element.preferredWidth = 150f;

        pagosTabs[tab] = button;
    }

    void ShowPagosTab(PagosTab tab)
    {
        currentPagosTab = tab;

        if (facturasPanel != null) facturasPanel.gameObject.SetActive(tab == PagosTab.Facturas);
        if (bancoPanel != null) bancoPanel.gameObject.SetActive(tab == PagosTab.Banco);

        foreach (KeyValuePair<PagosTab, Button> entry in pagosTabs)
        {
            Image image = entry.Value != null ? entry.Value.targetGraphic as Image : null;
            if (image == null) continue;

            image.color = entry.Key == tab
                ? UIFactory.Accent
                : new Color(0.24f, 0.27f, 0.33f);
        }

        RefreshPagos();
    }

    void RefreshBank()
    {
        if (loanList == null) return;

        foreach (Transform child in loanList) Destroy(child.gameObject);

        BankManager bank = BankManager.EnsureExists();

        if (bankSummary != null)
        {
            bankSummary.text =
                "<b>Prestamos</b>   <size=15><color=#888888>el dinero entra hoy y se devuelve en cuotas diarias</color></size>\n" +
                "Debes ahora mismo: <color=#E53935>" + GameManager.Format(bank.DebtTotal) + "</color>";
        }

        foreach (BankManager.Loan loan in bank.Loans) BuildLoanCard(bank, loan);
    }

    void BuildLoanCard(BankManager bank, BankManager.Loan loan)
    {
        bool available = bank.IsAvailable(loan);
        bool active = loan.Active;
        bool repaid = loan.Repaid;

        Color background = repaid
            ? new Color(0.82f, 0.93f, 0.83f)
            : active ? new Color(1f, 0.95f, 0.82f)
            : available ? UIFactory.Card : new Color(0.90f, 0.90f, 0.92f);

        Image card = UIFactory.Box("Prestamo", loanList, background);
        UIFactory.Height(card.rectTransform, 74f);

        string state;

        if (repaid) state = "<color=#2E7D32>Devuelto</color>";
        else if (active) state = "Quedan <b>" + (loan.days - loan.installmentsPaid) + "</b> cuotas · " +
                                 GameManager.Format(loan.Remaining) + " por devolver";
        else if (available) state = "Devuelves " + GameManager.Format(loan.toRepay) + " en " + loan.days +
                                    " cuotas de " + GameManager.Format(loan.Installment);
        else state = "<color=#999999>Devuelve antes el prestamo anterior</color>";

        TextMeshProUGUI info = UIFactory.Text("Info", card.transform,
            "<b>" + GameManager.Format(loan.amount) + "</b>   <size=15><color=#888888>interes " +
            GameManager.Format(loan.Interest) + "</color></size>\n<size=15>" + state + "</size>",
            19, available || active || repaid ? UIFactory.Ink : UIFactory.Muted, TextAlignmentOptions.Left);

        info.rectTransform.anchorMin = Vector2.zero;
        info.rectTransform.anchorMax = Vector2.one;
        info.rectTransform.offsetMin = new Vector2(14f, 0f);
        info.rectTransform.offsetMax = new Vector2(-150f, 0f);

        if (!available) return;

        Button take = UIFactory.Button("Pedir", card.transform, "PEDIR", 17,
            UIFactory.Accent, Color.white, null);

        RectTransform rect = take.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(126f, 42f);
        rect.anchoredPosition = new Vector2(-12f, 0f);

        take.onClick.AddListener(() =>
        {
            if (bank.Take(loan))
            {
                NotificationManager.Instance.ShowMessage("Prestamo de " + GameManager.Format(loan.amount) + " concedido");
            }

            RefreshPagos();
        });
    }

    void RefreshBills()
    {
        if (billList == null) return;

        foreach (Transform child in billList) Destroy(child.gameObject);

        EconomyManager economy = EconomyManager.EnsureExists();

        if (economy.Pending.Count == 0)
        {
            TextMeshProUGUI clear = UIFactory.Text("AlDia", billList,
                "<color=#4CAF50>Todo pagado. No debes nada.</color>", 20, UIFactory.Muted, TextAlignmentOptions.TopLeft);

            UIFactory.Height(clear.rectTransform, 40f);
            return;
        }

        // Uno por dia, del mas antiguo al mas reciente.
        foreach (EconomyManager.Bill bill in economy.Pending)
        {
            BuildBillCard(economy, bill);
        }
    }

    void BuildBillCard(EconomyManager economy, EconomyManager.Bill bill)
    {
        Image card = UIFactory.Box("Cargo", billList, UIFactory.Card);
        UIFactory.Height(card.rectTransform, 62f);

        TextMeshProUGUI info = UIFactory.Text("Info", card.transform,
            "<b>" + bill.concept + " · Dia " + bill.day + "</b>\n" +
            "<size=15><color=#888888>" + GameManager.Format(bill.amount) + "</color></size>",
            18, UIFactory.Ink, TextAlignmentOptions.Left);

        info.rectTransform.anchorMin = Vector2.zero;
        info.rectTransform.anchorMax = Vector2.one;
        info.rectTransform.offsetMin = new Vector2(14f, 0f);
        info.rectTransform.offsetMax = new Vector2(-130f, 0f);

        Button pay = UIFactory.Button("Pagar", card.transform, "PAGAR", 17,
            new Color(0.18f, 0.65f, 0.32f), Color.white, null);

        RectTransform rect = pay.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(108f, 40f);
        rect.anchoredPosition = new Vector2(-12f, 0f);

        pay.onClick.AddListener(() =>
        {
            if (economy.Pay(bill))
            {
                NotificationManager.Instance.ShowMessage("Pagado " + GameManager.Format(bill.amount));
            }
            else
            {
                NotificationManager.Instance.ShowMessage("No tienes dinero suficiente");
            }

            RefreshPagos();
        });
    }

    void PayEverything()
    {
        EconomyManager economy = EconomyManager.EnsureExists();

        if (economy.Pending.Count == 0)
        {
            NotificationManager.Instance.ShowMessage("No hay nada pendiente");
        }
        else
        {
            int paid = economy.PayAll();

            NotificationManager.Instance.ShowMessage(paid == 0
                ? "No tienes dinero suficiente"
                : "Pagados " + paid + " cargos");
        }

        RefreshPagos();
    }

    RectTransform BuildPersonal()
    {
        RectTransform body;
        RectTransform panel = SectionPanel("PersonalPanel", "Personal", out body);

        TextMeshProUGUI info = UIFactory.Text("Info", body,
            "Aqui podras contratar empleados para que rellenen las maquinas,\n" +
            "atiendan el mostrador y recojan los premios por ti.\n\n" +
            "<color=#888888>Todavia no disponible.</color>",
            22, UIFactory.Muted, TextAlignmentOptions.TopLeft);

        UIFactory.Stretch(info.rectTransform, 0f, 0f, 0f, 0f);

        return panel;
    }

    RectTransform BuildPrecios()
    {
        RectTransform body;
        RectTransform panel = SectionPanel("PreciosPanel", "Precios", out body);

        TextMeshProUGUI help = UIFactory.Text("Ayuda", body,
            "Precio por partida de cada maquina. Tambien puedes cambiarlo\n" +
            "acercandote a la maquina y pulsando P.",
            18, UIFactory.Muted, TextAlignmentOptions.TopLeft);

        help.rectTransform.anchorMin = new Vector2(0f, 1f);
        help.rectTransform.anchorMax = new Vector2(1f, 1f);
        help.rectTransform.pivot = new Vector2(0.5f, 1f);
        help.rectTransform.sizeDelta = new Vector2(0f, 50f);
        help.rectTransform.anchoredPosition = Vector2.zero;

        priceList = UIFactory.Rect("Lista", body);
        UIFactory.Stretch(priceList, 0f, 58f, 0f, 0f);
        UIFactory.Column(priceList, 10f, new RectOffset(0, 0, 0, 0));

        return panel;
    }

    public void Show(Section section)
    {
        current = section;

        foreach (KeyValuePair<Section, RectTransform> entry in panels)
        {
            if (entry.Value != null) entry.Value.gameObject.SetActive(entry.Key == section);
        }

        foreach (KeyValuePair<Section, Button> entry in navButtons)
        {
            Image image = entry.Value != null ? entry.Value.targetGraphic as Image : null;
            if (image == null) continue;

            image.color = entry.Key == section
                ? UIFactory.Accent
                : new Color(0.24f, 0.27f, 0.33f);
        }

        if (section == Section.Tienda && shopUI != null) shopUI.RefreshCurrentTab();
        if (section == Section.Expansion) RefreshExpansion();
        if (section == Section.Pagos) RefreshPagos();
        if (section == Section.Precios) RefreshPrecios();
    }

    // Al encender el monitor siempre se entra por la tienda.
    public void OpenAtHome()
    {
        Show(Section.Tienda);
    }

    void RefreshPagos()
    {
        EconomyManager economy = EconomyManager.EnsureExists();

        if (billSummary != null)
        {
            billSummary.text =
                "<b>Factura de la luz</b>   <size=15><color=#888888>base " + economy.baseElectricity +
                "€ + " + economy.MachineCount + " maquinas + " + economy.DeviceCount + " aparatos</color></size>\n" +
                "Ahora mismo son <b>" + GameManager.Format(economy.CurrentDailyBill) + "</b> por dia\n" +
                "Pendiente: <color=#E53935>" + GameManager.Format(economy.PendingTotal) + "</color>" +
                "   ·   " + economy.DaysOverdue + " dia(s) sin pagar";
        }

        RefreshBills();
        RefreshBank();
    }


    public void RefreshPrecios()
    {
        if (priceList == null) return;

        foreach (Transform child in priceList) Destroy(child.gameObject);

        if (MachinePricing.All.Count == 0)
        {
            TextMeshProUGUI empty = UIFactory.Text("Vacio", priceList,
                "No tienes ninguna maquina colocada todavia.", 20, UIFactory.Muted, TextAlignmentOptions.TopLeft);

            UIFactory.Height(empty.rectTransform, 40f);
            return;
        }

        foreach (MachinePricing pricing in MachinePricing.All)
        {
            if (pricing != null) BuildPriceRow(pricing);
        }
    }

    void BuildPriceRow(MachinePricing pricing)
    {
        Image row = UIFactory.Box("Fila", priceList, UIFactory.Card);
        UIFactory.Height(row.rectTransform, 78f);

        TextMeshProUGUI info = UIFactory.Text("Info", row.transform, "", 18, UIFactory.Ink, TextAlignmentOptions.Left);
        info.rectTransform.anchorMin = new Vector2(0f, 0f);
        info.rectTransform.anchorMax = new Vector2(1f, 1f);
        info.rectTransform.offsetMin = new Vector2(14f, 0f);
        info.rectTransform.offsetMax = new Vector2(-170f, 0f);

        RefreshRowText(info, pricing);

        Button minus = UIFactory.Button("Menos", row.transform, "-", 26, new Color(0.85f, 0.87f, 0.9f), UIFactory.Ink, null);
        Anchor(minus.GetComponent<RectTransform>(), -132f, 46f);

        Button plus = UIFactory.Button("Mas", row.transform, "+", 26, new Color(0.85f, 0.87f, 0.9f), UIFactory.Ink, null);
        Anchor(plus.GetComponent<RectTransform>(), -22f, 46f);

        TextMeshProUGUI value = UIFactory.Text("Valor", row.transform, GameManager.Format(pricing.price), 24, UIFactory.Ink, TextAlignmentOptions.Center);
        Anchor(value.rectTransform, -77f, 60f);

        minus.onClick.AddListener(() =>
        {
            pricing.SetPrice(pricing.price - priceStep);
            value.text = GameManager.Format(pricing.price);
            RefreshRowText(info, pricing);
        });

        plus.onClick.AddListener(() =>
        {
            pricing.SetPrice(pricing.price + priceStep);
            value.text = GameManager.Format(pricing.price);
            RefreshRowText(info, pricing);
        });
    }

    void RefreshRowText(TextMeshProUGUI info, MachinePricing pricing)
    {
        info.text =
            "<b>" + pricing.MachineName + "</b>   " + pricing.PriceOpinion + "\n" +
            "<size=15><color=#888888>Recomendado " + GameManager.Format(pricing.recommendedPrice) +
            "   ·   Competencia " + GameManager.Format(pricing.competitionPrice) + "</color></size>";
    }

    static void Anchor(RectTransform rect, float xFromRight, float width)
    {
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 44f);
        rect.anchoredPosition = new Vector2(xFromRight, 0f);
    }
}
