using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Pantalla para poner precio a una maquina. Libera el raton, congela la camara
// y deja escribir el importe con el teclado, decimales incluidos.
public class PricePanel : MonoBehaviour
{
    public static PricePanel Instance;

    public static bool IsOpen { get { return Instance != null && Instance.open; } }

    private bool open = false;
    private MachinePricing target;
    private string typed = "";

    private RectTransform panel;
    private TextMeshProUGUI header;
    private TextMeshProUGUI marketInfo;
    private TextMeshProUGUI entry;
    private TextMeshProUGUI opinion;

    private FirstPersonController playerController;

    void Awake()
    {
        Instance = this;
    }

    public static PricePanel EnsureExists()
    {
        if (Instance != null) return Instance;

        PricePanel existing = FindAnyObjectByType<PricePanel>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        return new GameObject("PricePanel").AddComponent<PricePanel>();
    }

    public void Open(MachinePricing pricing)
    {
        if (pricing == null) return;

        Build();

        target = pricing;
        typed = "";
        open = true;

        panel.gameObject.SetActive(true);

        playerController = FindAnyObjectByType<FirstPersonController>();
        if (playerController != null) playerController.enabled = false;

        CursorMode.Free(this);

        Refresh();
    }

    public void Close()
    {
        open = false;
        target = null;

        if (panel != null) panel.gameObject.SetActive(false);

        if (playerController != null) playerController.enabled = true;

        CursorMode.Release(this);
    }

    void OnDisable()
    {
        open = false;

        CursorMode.Release(this);
    }

    void Update()
    {
        if (!open || target == null) return;

        ReadTyping();

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Apply();
        else if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // Se lee el teclado a mano en vez de con un InputField: no hay que pelearse
    // con el foco y acepta el punto y la coma indistintamente.
    void ReadTyping()
    {
        bool changed = false;

        foreach (char c in Input.inputString)
        {
            if (c == '\b')
            {
                if (typed.Length > 0)
                {
                    typed = typed.Substring(0, typed.Length - 1);
                    changed = true;
                }
            }
            else if (char.IsDigit(c))
            {
                if (typed.Length < 6)
                {
                    typed += c;
                    changed = true;
                }
            }
            else if ((c == '.' || c == ',') && !typed.Contains("."))
            {
                typed += ".";
                changed = true;
            }
        }

        if (changed) Refresh();
    }

    float TypedValue
    {
        get
        {
            if (string.IsNullOrEmpty(typed)) return target.price;

            float value;
            if (float.TryParse(typed, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                return value;
            }

            return target.price;
        }
    }

    void Apply()
    {
        if (string.IsNullOrEmpty(typed))
        {
            Close();
            return;
        }

        target.SetPrice(TypedValue);

        if (ComputerUI.Instance != null) ComputerUI.Instance.RefreshPrecios();

        NotificationManager.Instance.ShowMessage("Precio: " + GameManager.Format(target.price));

        Close();
    }

    void Refresh()
    {
        if (target == null) return;

        header.text = target.MachineName;

        marketInfo.text =
            "Precio actual        <b>" + GameManager.Format(target.price) + "</b>\n" +
            "Recomendado          " + GameManager.Format(target.recommendedPrice) + "\n" +
            "Competencia          " + GameManager.Format(target.competitionPrice);

        entry.text = string.IsNullOrEmpty(typed)
            ? "<color=#999999>Escribe el precio…</color>"
            : typed + "€";

        float preview = string.IsNullOrEmpty(typed) ? target.price : TypedValue;
        float over = preview - target.competitionPrice;

        string veredicto;
        if (over <= -2f) veredicto = "<color=#4CAF50>Barato · vendras lleno</color>";
        else if (over >= 3f) veredicto = "<color=#E53935>Caro · perderas clientes</color>";
        else veredicto = "<color=#FFB300>En mercado</color>";

        int percent = Mathf.RoundToInt(Mathf.Clamp01(1f - Mathf.Max(0f, over) * target.lossPerExtraEuro) * 100f);

        opinion.text = veredicto + "\n<size=17><color=#888888>Jugarian " + percent + " de cada 100 clientes</color></size>";
    }

    void Build()
    {
        if (panel != null) return;

        Canvas canvas = FindScreenCanvas();
        if (canvas == null) return;

        Image backdrop = UIFactory.Box("PricePanel", canvas.transform, new Color(0f, 0f, 0f, 0.55f));
        UIFactory.Stretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);
        panel = backdrop.rectTransform;

        Image card = UIFactory.Box("Card", panel, UIFactory.Panel);
        card.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        card.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        card.rectTransform.sizeDelta = new Vector2(430f, 330f);
        card.rectTransform.anchoredPosition = Vector2.zero;

        header = UIFactory.Text("Header", card.transform, "", 26, UIFactory.Ink, TextAlignmentOptions.Center);
        Place(header.rectTransform, 0f, -22f, 390f, 40f);

        marketInfo = UIFactory.Text("Market", card.transform, "", 19, UIFactory.Ink, TextAlignmentOptions.TopLeft);
        Place(marketInfo.rectTransform, 0f, -80f, 390f, 90f);

        Image field = UIFactory.Box("Field", card.transform, Color.white);
        Place(field.rectTransform, 0f, -185f, 390f, 52f);

        entry = UIFactory.Text("Entry", field.transform, "", 26, UIFactory.Ink, TextAlignmentOptions.Center);
        UIFactory.Stretch(entry.rectTransform, 8f, 4f, 8f, 4f);

        opinion = UIFactory.Text("Opinion", card.transform, "", 19, UIFactory.Ink, TextAlignmentOptions.Center);
        Place(opinion.rectTransform, 0f, -245f, 390f, 50f);

        TextMeshProUGUI help = UIFactory.Text("Help", card.transform,
            "Enter para guardar   ·   Esc para salir", 16, UIFactory.Muted, TextAlignmentOptions.Center);
        Place(help.rectTransform, 0f, -300f, 390f, 24f);

        panel.gameObject.SetActive(false);
    }

    // El canvas de pantalla, no el del monitor: este panel se ve delante de ti.
    Canvas FindScreenCanvas()
    {
        Canvas[] all = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);

        foreach (Canvas canvas in all)
        {
            if (canvas.renderMode != RenderMode.WorldSpace) return canvas.rootCanvas;
        }

        return null;
    }

    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(x, y);
    }
}
