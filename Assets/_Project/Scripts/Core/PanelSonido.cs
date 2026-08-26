using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Mezclador de sonido, con una barra por canal de 0 a 100.
//
// Se abre con F1 desde cualquier sitio y se mueve con el juego corriendo, que
// es lo unico que sirve para afinar volumenes: oyendo el cambio en el momento.
public class PanelSonido : MonoBehaviour
{
    public static PanelSonido Instance;
    public static bool IsOpen { get { return Instance != null && Instance.abierto; } }

    // La tecla la elige el jugador en el menu de ajustes.
    static KeyCode tecla { get { return AjustesControles.Tecla(AjustesControles.Accion.Sonido); } }
    public float paso = 0.05f;

    RectTransform panel;
    bool abierto = false;
    bool construido = false;

    readonly Image[] barras = new Image[4];
    readonly TextMeshProUGUI[] cifras = new TextMeshProUGUI[4];

    FirstPersonController playerController;

    void Awake()
    {
        Instance = this;
    }

    public static PanelSonido EnsureExists()
    {
        if (Instance != null) return Instance;

        PanelSonido existente = FindAnyObjectByType<PanelSonido>();
        if (existente != null)
        {
            Instance = existente;
            return Instance;
        }

        return new GameObject("PanelSonido").AddComponent<PanelSonido>();
    }

    void Update()
    {
        if (AjustesControles.Pulsando(AjustesControles.Accion.Sonido))
        {
            if (abierto) Cerrar();
            else Abrir();
        }

        if (abierto && Input.GetKeyDown(KeyCode.Escape)) Cerrar();
    }

    public void Abrir()
    {
        Construir();

        abierto = true;
        panel.gameObject.SetActive(true);

        // Se suelta el raton y se para al jugador, igual que hace el panel de
        // precios: si no, mover el raton por los botones gira la camara.
        playerController = FindAnyObjectByType<FirstPersonController>();
        if (playerController != null) playerController.enabled = false;

        CursorMode.Free(this);
        Refrescar();
    }

    public void Cerrar()
    {
        abierto = false;

        if (panel != null) panel.gameObject.SetActive(false);
        if (playerController != null) playerController.enabled = true;

        CursorMode.Release(this);
    }

    // ------------------------------------------------------------- construir

    void Construir()
    {
        if (construido) return;
        construido = true;

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        CanvasScaler escala = gameObject.GetComponent<CanvasScaler>();
        if (escala == null) escala = gameObject.AddComponent<CanvasScaler>();

        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        Image fondo = UIFactory.Box("Fondo", transform, new Color(0f, 0f, 0f, 0.55f));
        UIFactory.Stretch(fondo.rectTransform, 0f, 0f, 0f, 0f);

        Image caja = UIFactory.Box("Panel", fondo.transform, UIFactory.Panel);
        caja.sprite = UIFactory.RoundedSprite(18);
        caja.type = Image.Type.Sliced;

        panel = caja.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(620f, 460f);

        UIFactory.Column(panel, 14f, new RectOffset(30, 30, 26, 26));

        var titulo = UIFactory.Text("Titulo", panel, "Sonido", 34, UIFactory.Ink,
                                    TextAlignmentOptions.Left);
        UIFactory.Height(titulo.rectTransform, 44f);

        var ayuda = UIFactory.Text("Ayuda", panel,
                                   AjustesControles.NombreTecla(tecla)
                                   + " o Escape para cerrar", 18, UIFactory.Muted,
                                   TextAlignmentOptions.Left);
        UIFactory.Height(ayuda.rectTransform, 26f);

        for (int i = 0; i < 4; i++) Fila((AjustesSonido.Canal)i);

        // El panel nace apagado: se enciende al abrirlo.
        panel.gameObject.SetActive(false);

        // Y se entera si el volumen cambia por otro lado.
        AjustesSonido.Cambiado += Refrescar;
    }

    void Fila(AjustesSonido.Canal canal)
    {
        int i = (int)canal;

        RectTransform fila = UIFactory.Rect("Fila_" + canal, panel);
        UIFactory.Height(fila, 62f);

        var nombre = UIFactory.Text("Nombre", fila, AjustesSonido.Nombre(canal), 22,
                                    UIFactory.Ink, TextAlignmentOptions.Left);
        nombre.rectTransform.anchorMin = new Vector2(0f, 0f);
        nombre.rectTransform.anchorMax = new Vector2(0.28f, 1f);
        UIFactory.Stretch(nombre.rectTransform, 0f, 0f, 0f, 0f);

        // La barra: un canal vacio y encima el relleno. Es el "medidor".
        Image canalBarra = UIFactory.Box("Canal", fila, new Color(0.86f, 0.87f, 0.9f));
        canalBarra.sprite = UIFactory.RoundedSprite(9);
        canalBarra.type = Image.Type.Sliced;

        RectTransform cr = canalBarra.rectTransform;
        cr.anchorMin = new Vector2(0.28f, 0.5f);
        cr.anchorMax = new Vector2(0.70f, 0.5f);
        cr.pivot = new Vector2(0.5f, 0.5f);
        cr.offsetMin = new Vector2(0f, -11f);
        cr.offsetMax = new Vector2(0f, 11f);

        Image relleno = UIFactory.Box("Relleno", canalBarra.transform, UIFactory.Accent);
        relleno.sprite = UIFactory.RoundedSprite(9);
        relleno.type = Image.Type.Sliced;

        RectTransform rr = relleno.rectTransform;
        rr.anchorMin = new Vector2(0f, 0f);
        rr.anchorMax = new Vector2(1f, 1f);
        rr.offsetMin = Vector2.zero;
        rr.offsetMax = Vector2.zero;

        barras[i] = relleno;

        var cifra = UIFactory.Text("Cifra", fila, "0", 22, UIFactory.Ink,
                                   TextAlignmentOptions.Center);
        cifra.rectTransform.anchorMin = new Vector2(0.70f, 0f);
        cifra.rectTransform.anchorMax = new Vector2(0.80f, 1f);
        UIFactory.Stretch(cifra.rectTransform, 0f, 0f, 0f, 0f);
        cifras[i] = cifra;

        Boton(fila, "-", 0.81f, 0.895f, canal, -paso);
        Boton(fila, "+", 0.905f, 0.99f, canal, +paso);
    }

    void Boton(RectTransform fila, string etiqueta, float x0, float x1,
               AjustesSonido.Canal canal, float delta)
    {
        Button b = UIFactory.Button("Btn" + etiqueta + canal, fila, etiqueta, 24,
                                    UIFactory.Card, UIFactory.Ink,
                                    () => AjustesSonido.Sumar(canal, delta));

        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(x0, 0.15f);
        r.anchorMax = new Vector2(x1, 0.85f);
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
    }

    void Refrescar()
    {
        for (int i = 0; i < 4; i++)
        {
            if (barras[i] == null) continue;

            float v = AjustesSonido.Get((AjustesSonido.Canal)i);

            // El relleno se recorta por la derecha en proporcion al valor.
            RectTransform r = barras[i].rectTransform;
            r.anchorMax = new Vector2(Mathf.Max(0.001f, v), 1f);
            r.offsetMax = Vector2.zero;

            cifras[i].text = Mathf.RoundToInt(v * 100f).ToString();
        }
    }

    void OnDestroy()
    {
        AjustesSonido.Cambiado -= Refrescar;
    }
}
