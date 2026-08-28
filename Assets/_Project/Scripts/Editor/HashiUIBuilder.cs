using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Monta el Canvas de la maquina.
//
// Aparte del constructor de la maquina porque son dos trabajos que no se
// parecen en nada: uno coloca cosas en metros dentro de un mueble y el otro
// coloca cajas de texto en pixeles sobre la pantalla. Juntos daban un fichero
// de mil lineas donde no se encontraba nada.
public static class HashiUIBuilder
{
    const string RUTA_FUENTE =
        "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    // Colores de la maquina: azul marino de fondo, amarillo de marquesina y
    // rosa de los adornos. Los mismos que el mueble, para que la interfaz
    // parezca parte de la maquina y no una capa pegada encima.
    static readonly Color FONDO = new Color(0.09f, 0.11f, 0.28f, 0.85f);
    static readonly Color BORDE = new Color(1f, 0.85f, 0.20f, 1f);
    static readonly Color TEXTO = new Color(1f, 0.97f, 0.90f, 1f);
    static readonly Color BOTON = new Color(0.20f, 0.30f, 0.65f, 0.95f);
    static readonly Color BOTON_TEXTO = Color.white;

    public class Refs
    {
        public Canvas canvas;
        public Hashi.UIManager gestor;
        public TMP_Text creditos, premios, tiempo, estado, mensaje, depuracion;
        public Button start, reset, camara, sonido, credito;
    }

    public static Refs Construir(Transform padre)
    {
        var r = new Refs();

        TMP_FontAsset fuente = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RUTA_FUENTE);

        if (fuente == null)
        {
            Debug.LogWarning("[Hashi] No encuentro la fuente de TextMeshPro en "
                             + RUTA_FUENTE + ". Los textos saldran sin fuente "
                             + "asignada; se arregla poniendo una a mano.");
        }

        // ------------------------------------------------------------ canvas
        GameObject canvasGo = new GameObject("UI",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(padre, false);
        canvasGo.layer = LayerMask.NameToLayer("UI");

        r.canvas = canvasGo.GetComponent<Canvas>();
        r.canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler escala = canvasGo.GetComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);

        // A media distancia entre ancho y alto: escalando solo por ancho, en
        // pantallas panoramicas los textos se salen por abajo.
        escala.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        escala.matchWidthOrHeight = 0.5f;

        AsegurarEventSystem(padre);

        // ------------------------------------------------ marcador izquierda
        RectTransform izq = Panel("Marcador", canvasGo.transform,
                                  new Vector2(0f, 1f), new Vector2(0f, 1f),
                                  new Vector2(24f, -24f), new Vector2(360f, 150f));

        r.creditos = Texto("Creditos", izq, fuente, "CREDITOS  0", 44f,
                           TextAlignmentOptions.Left,
                           new Vector2(20f, -18f), new Vector2(320f, 56f));

        r.premios = Texto("Premios", izq, fuente, "PREMIOS  0", 44f,
                          TextAlignmentOptions.Left,
                          new Vector2(20f, -80f), new Vector2(320f, 56f));

        // ---------------------------------------------------- marcador derecha
        RectTransform der = Panel("Estado", canvasGo.transform,
                                  new Vector2(1f, 1f), new Vector2(1f, 1f),
                                  new Vector2(-24f, -24f), new Vector2(400f, 150f));

        r.tiempo = Texto("Tiempo", der, fuente, "TIEMPO  --", 44f,
                         TextAlignmentOptions.Right,
                         new Vector2(-20f, -18f), new Vector2(360f, 56f),
                         new Vector2(1f, 1f));

        r.estado = Texto("EstadoMaquina", der, fuente, "ESTADO  EN ESPERA", 40f,
                         TextAlignmentOptions.Right,
                         new Vector2(-20f, -80f), new Vector2(360f, 56f),
                         new Vector2(1f, 1f));

        // ------------------------------------------------------ cartel central
        r.mensaje = Texto("Mensaje", canvasGo.transform, fuente, "READY", 130f,
                          TextAlignmentOptions.Center,
                          new Vector2(0f, 210f), new Vector2(1400f, 200f),
                          new Vector2(0.5f, 0.5f));

        r.mensaje.fontStyle = FontStyles.Bold;
        r.mensaje.color = TEXTO;

        // Contorno y brillo: encima de una maquina blanca y llena de luces, un
        // texto plano se pierde y no se lee.
        r.mensaje.outlineWidth = 0.25f;
        r.mensaje.outlineColor = new Color32(20, 10, 60, 255);

        // ------------------------------------------------------------ botones
        RectTransform fila = Panel("Botones", canvasGo.transform,
                                   new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                   new Vector2(0f, 28f), new Vector2(1120f, 96f));

        fila.GetComponent<Image>().color = new Color(0.09f, 0.11f, 0.28f, 0.55f);

        HorizontalLayoutGroup fl = fila.gameObject.AddComponent<HorizontalLayoutGroup>();
        fl.spacing = 16f;
        fl.padding = new RectOffset(16, 16, 12, 12);
        fl.childAlignment = TextAnchor.MiddleCenter;
        fl.childForceExpandWidth = true;
        fl.childForceExpandHeight = true;

        r.start = Boton("Start", fila, fuente, "START");
        r.reset = Boton("Reset", fila, fuente, "RESET");
        r.camara = Boton("Camara", fila, fuente, "CAMERA");
        r.sonido = Boton("Sonido", fila, fuente, "SOUND");
        r.credito = Boton("Credito", fila, fuente, "+1 CREDITO");

        // ------------------------------------------------------------ ayudas
        TMP_Text ayuda = Texto("Ayuda", canvasGo.transform, fuente,
            "ENTER  meter credito      ESPACIO  parar el carro (dos veces: derecha y fondo)\n"
            + "1 2 3  camara      C  credito extra      R  reiniciar      F1  depuracion",
            26f, TextAlignmentOptions.Right,
            new Vector2(-24f, 150f), new Vector2(900f, 90f), new Vector2(1f, 0f));

        ayuda.color = new Color(1f, 1f, 1f, 0.55f);

        // ------------------------------------------------------- depuracion
        r.depuracion = Texto("Depuracion", canvasGo.transform, fuente, "", 28f,
                             TextAlignmentOptions.TopLeft,
                             new Vector2(24f, 150f), new Vector2(560f, 260f),
                             new Vector2(0f, 0f));

        r.depuracion.color = new Color(0.55f, 1f, 0.65f, 0.95f);
        r.depuracion.gameObject.SetActive(false);

        // -------------------------------------------------------- el gestor
        r.gestor = canvasGo.AddComponent<Hashi.UIManager>();

        return r;
    }

    // Sin EventSystem los botones se ven pero no se pueden pulsar, y no hay
    // ningun aviso que lo diga.
    static void AsegurarEventSystem(Transform padre)
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
        go.transform.SetParent(padre, false);

        // El modulo del Input System nuevo, no el clasico: el proyecto tiene el
        // paquete puesto, y el modulo viejo se queja o directamente no lee nada
        // segun como este configurado el proyecto.
        InputSystemUIInputModule modulo = go.AddComponent<InputSystemUIInputModule>();
        modulo.AssignDefaultActions();
    }

    // --------------------------------------------------------------- piezas

    static RectTransform Panel(string nombre, Transform padre, Vector2 anclaMin,
                               Vector2 anclaMax, Vector2 posicion, Vector2 tamano)
    {
        GameObject go = new GameObject(nombre, typeof(Image));
        go.transform.SetParent(padre, false);
        go.layer = LayerMask.NameToLayer("UI");

        Image img = go.GetComponent<Image>();
        img.color = FONDO;
        img.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anclaMin;
        rt.anchorMax = anclaMax;
        rt.pivot = anclaMin;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;

        // Un filo amarillo abajo, como la banda de las maquinas de verdad.
        GameObject filo = new GameObject("Filo", typeof(Image));
        filo.transform.SetParent(go.transform, false);
        filo.layer = go.layer;

        Image fimg = filo.GetComponent<Image>();
        fimg.color = BORDE;
        fimg.raycastTarget = false;

        RectTransform frt = filo.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0f, 0f);
        frt.anchorMax = new Vector2(1f, 0f);
        frt.pivot = new Vector2(0.5f, 0f);
        frt.anchoredPosition = Vector2.zero;
        frt.sizeDelta = new Vector2(0f, 5f);

        return rt;
    }

    static TMP_Text Texto(string nombre, Transform padre, TMP_FontAsset fuente,
                          string contenido, float tamanoLetra,
                          TextAlignmentOptions alineacion, Vector2 posicion,
                          Vector2 tamano, Vector2? ancla = null)
    {
        GameObject go = new GameObject(nombre, typeof(TextMeshProUGUI));
        go.transform.SetParent(padre, false);
        go.layer = LayerMask.NameToLayer("UI");

        TextMeshProUGUI t = go.GetComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;

        t.text = contenido;
        t.fontSize = tamanoLetra;
        t.alignment = alineacion;
        t.color = TEXTO;
        t.raycastTarget = false;
        t.enableWordWrapping = false;

        Vector2 a = ancla ?? new Vector2(0f, 1f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = a;
        rt.anchorMax = a;
        rt.pivot = a;
        rt.anchoredPosition = posicion;
        rt.sizeDelta = tamano;

        return t;
    }

    static Button Boton(string nombre, Transform padre, TMP_FontAsset fuente,
                        string etiqueta)
    {
        GameObject go = new GameObject(nombre, typeof(Image), typeof(Button));
        go.transform.SetParent(padre, false);
        go.layer = LayerMask.NameToLayer("UI");

        Image img = go.GetComponent<Image>();
        img.color = BOTON;

        Button b = go.GetComponent<Button>();

        // Colores del boton a mano: con los de fabrica, un boton apagado se ve
        // casi igual que uno encendido y parece que no responde.
        ColorBlock cb = b.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 1f) * 1.15f;
        cb.pressedColor = new Color(0.75f, 0.8f, 1f);
        cb.disabledColor = new Color(0.4f, 0.42f, 0.5f, 0.6f);
        b.colors = cb;

        GameObject txt = new GameObject("Texto", typeof(TextMeshProUGUI));
        txt.transform.SetParent(go.transform, false);
        txt.layer = go.layer;

        TextMeshProUGUI t = txt.GetComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;

        t.text = etiqueta;
        t.fontSize = 34f;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = BOTON_TEXTO;
        t.raycastTarget = false;

        RectTransform trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;

        return b;
    }
}
