using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// El menu de ajustes, el que sale con Escape.
//
// Se monta entero desde codigo y con su propio Canvas, igual que PanelSonido:
// asi no hay que tocar la escena, y no se puede romper por haberla guardado sin
// querer o por no haberla guardado.
//
// La forma es la de cualquier menu de ajustes que funcione: los apartados a la
// izquierda, el contenido a la derecha, y abajo del todo lo que se hace con lo
// que has cambiado. Se ve de un vistazo donde estas y que te queda por mirar,
// que es justo lo que no dan los menus de una sola columna larga.
public class MenuAjustes : MonoBehaviour
{
    public static MenuAjustes Instance;
    public static bool IsOpen { get { return Instance != null && Instance.abierto; } }

    // --------------------------------------------------------------- colores

    // Oscuro, y a proposito.
    //
    // Las demas pantallas del juego son claras porque son el monitor de la
    // tienda: un objeto que esta dentro del mundo. Esta no, esta es del juego.
    // Oscura sobre el local encendido se entiende sola como otra capa, y no
    // como una pantalla mas de la tienda.
    static readonly Color VELO = new Color(0.02f, 0.03f, 0.05f, 0.74f);
    static readonly Color FONDO = new Color(0.09f, 0.10f, 0.13f);
    static readonly Color RAIL = new Color(0.065f, 0.075f, 0.095f);
    static readonly Color FILA_PAR = new Color(0.13f, 0.15f, 0.19f);
    static readonly Color FILA_IMPAR = new Color(0.11f, 0.125f, 0.16f);
    static readonly Color TEXTO = new Color(0.91f, 0.92f, 0.95f);
    static readonly Color SUAVE = new Color(0.55f, 0.58f, 0.66f);
    static readonly Color ACENTO = new Color(0.20f, 0.52f, 0.92f);
    static readonly Color PELIGRO = new Color(0.82f, 0.32f, 0.30f);
    static readonly Color APAGADO = new Color(1f, 1f, 1f, 0.10f);
    static readonly Color RAYA = new Color(1f, 1f, 1f, 0.06f);

    const float ANCHO = 1040f;
    const float ALTO = 700f;
    const float RAIL_ANCHO = 232f;
    const float CABECERA = 78f;
    const float PIE = 76f;
    const float FILA_ALTO = 48f;
    const float TECLA_ANCHO = 152f;

    // ----------------------------------------------------------- estado vivo

    enum Seccion { Juego, Sonido, Controles }

    bool abierto = false;
    bool construido = false;

    Seccion seccion = Seccion.Juego;

    // La accion que esta esperando a que pulses una tecla, o -1.
    int escuchando = -1;

    // Si estabas ocupado en el fotograma ANTERIOR, no en este.
    //
    // Al salir del ordenador con Escape, ese mismo Escape lo ve tambien este
    // menu en el mismo fotograma. Y como no hay orden garantizado entre los
    // Update, si el ordenador va primero ya ha soltado el raton cuando miramos
    // -- asi que la pantalla parece libre y el menu se abre solo, justo encima
    // de lo que acabas de cerrar.
    //
    // Mirando como estabas el fotograma de antes, esa carrera desaparece: ese
    // Escape era para el ordenador, y este menu no lo toca.
    bool ocupadoAntes = true;

    RectTransform velo;
    RectTransform panel;
    RectTransform zonaContenido;
    RectTransform confirmacion;

    readonly List<Button> botonesSeccion = new List<Button>();
    readonly List<GameObject> contenido = new List<GameObject>();

    readonly List<Button> botonesTecla = new List<Button>();
    readonly List<AjustesControles.Accion> accionesTecla = new List<AjustesControles.Accion>();

    Button botonGuardar;
    TextMeshProUGUI avisoPie;

    // ------------------------------------------------ lo que habia al entrar

    // La foto de como estaba todo al abrir.
    //
    // Los cambios se aplican en el momento -- mover la barra del sonido sin oir
    // el efecto no sirve de nada -- asi que "descartar" no puede ser "no
    // aplicarlo todavia": tiene que ser volver a dejarlo como estaba. Y para
    // eso hay que haberlo apuntado antes de tocar nada.
    float sensibilidadAntes;
    bool invertirAntes;
    readonly float[] volumenAntes = new float[4];
    KeyCode[] teclasAntes;

    // -------------------------------------------------------------- arranque

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arrancar()
    {
        if (Instance != null) return;
        if (FindAnyObjectByType<MenuAjustes>() != null) return;

        // Se crea solo. Puesto a mano en la escena habria que acordarse de
        // ponerlo en cada escena nueva, y el dia que faltase no habria menu de
        // ajustes y nada diria por que.
        GameObject go = new GameObject("MenuAjustes");
        go.AddComponent<MenuAjustes>();

        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // --------------------------------------------------------------- entrada

    void Update()
    {
        if (escuchando >= 0)
        {
            EscucharTecla();
            return;
        }

        if (confirmacion != null && confirmacion.gameObject.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) CerrarConfirmacion();
            return;
        }

        if (!Input.GetKeyDown(KeyCode.Escape)) return;

        if (abierto) IntentarSalir();
        else if (PuedeAbrir()) Abrir();
    }

    void LateUpdate()
    {
        ocupadoAntes = Ocupado();
    }

    // Escape solo abre los ajustes cuando no estas en ninguna otra cosa.
    //
    // Escape ya significa "sal de aqui" en unos cuantos sitios: el ordenador,
    // la pantalla de precios, mirar un peluche, colocar una maquina. Abrir los
    // ajustes encima seria quitarles la salida a todos ellos.
    //
    // CursorMode.FreeCursor va el ultimo aposta: no nombra ninguna pantalla en
    // concreto, asi que cubre tambien las que se hagan mas adelante.
    bool PuedeAbrir()
    {
        return !ocupadoAntes && !Ocupado();
    }

    static bool Ocupado()
    {
        if (PlayerCarry.Busy) return true;
        if (PanelSonido.IsOpen) return true;
        if (FirstPersonController.LookLocked) return true;
        if (CursorMode.FreeCursor) return true;

        return false;
    }

    void EscucharTecla()
    {
        // Escape aqui es "dejalo como estaba", no "cierra el menu".
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            escuchando = -1;
            RefrescarTeclas();
            return;
        }

        KeyCode k = AjustesControles.Capturar();
        if (k == KeyCode.None) return;

        AjustesControles.Set((AjustesControles.Accion)escuchando, k);

        escuchando = -1;
        RefrescarTeclas();
    }

    // ---------------------------------------------------------- abrir y salir

    public void Abrir()
    {
        Construir();
        Apuntar();

        abierto = true;
        velo.gameObject.SetActive(true);

        CursorMode.Free(this);

        Mostrar(Seccion.Juego);
    }

    void Cerrar()
    {
        abierto = false;
        escuchando = -1;

        if (velo != null) velo.gameObject.SetActive(false);

        CursorMode.Release(this);
    }

    void IntentarSalir()
    {
        if (!HayCambios())
        {
            Cerrar();
            return;
        }

        confirmacion.gameObject.SetActive(true);
    }

    void Guardar()
    {
        // Los valores ya estan puestos, y ya se escribieron en PlayerPrefs segun
        // se tocaban. Esto es el volcado a disco, que es lo unico que falta para
        // que sobrevivan a cerrar el juego.
        PlayerPrefs.Save();

        CerrarConfirmacion();
        Cerrar();
    }

    void Descartar()
    {
        AjustesJuego.Sensibilidad = sensibilidadAntes;
        AjustesJuego.InvertirY = invertirAntes;

        for (int i = 0; i < volumenAntes.Length; i++)
        {
            AjustesSonido.Set((AjustesSonido.Canal)i, volumenAntes[i]);
        }

        for (int i = 0; i < teclasAntes.Length; i++)
        {
            AjustesControles.Set((AjustesControles.Accion)i, teclasAntes[i]);
        }

        PlayerPrefs.Save();

        CerrarConfirmacion();
        Cerrar();
    }

    void CerrarConfirmacion()
    {
        if (confirmacion != null) confirmacion.gameObject.SetActive(false);
    }

    void Apuntar()
    {
        sensibilidadAntes = AjustesJuego.Sensibilidad;
        invertirAntes = AjustesJuego.InvertirY;

        for (int i = 0; i < volumenAntes.Length; i++)
        {
            volumenAntes[i] = AjustesSonido.Get((AjustesSonido.Canal)i);
        }

        teclasAntes = new KeyCode[AjustesControles.Total];

        for (int i = 0; i < teclasAntes.Length; i++)
        {
            teclasAntes[i] = AjustesControles.Tecla((AjustesControles.Accion)i);
        }
    }

    bool HayCambios()
    {
        if (teclasAntes == null) return false;

        if (!Mathf.Approximately(sensibilidadAntes, AjustesJuego.Sensibilidad)) return true;
        if (invertirAntes != AjustesJuego.InvertirY) return true;

        for (int i = 0; i < volumenAntes.Length; i++)
        {
            float ahora = AjustesSonido.Get((AjustesSonido.Canal)i);
            if (!Mathf.Approximately(volumenAntes[i], ahora)) return true;
        }

        for (int i = 0; i < teclasAntes.Length; i++)
        {
            if (teclasAntes[i] != AjustesControles.Tecla((AjustesControles.Accion)i)) return true;
        }

        return false;
    }

    void Restaurar()
    {
        AjustesJuego.Restaurar();
        AjustesControles.Restaurar();

        AjustesSonido.Set(AjustesSonido.Canal.General, 1.00f);
        AjustesSonido.Set(AjustesSonido.Canal.Musica, 0.55f);
        AjustesSonido.Set(AjustesSonido.Canal.Motores, 0.35f);
        AjustesSonido.Set(AjustesSonido.Canal.Efectos, 0.90f);

        Refrescar();
    }

    // ======================================================== montar la caja

    void Construir()
    {
        if (construido) return;
        construido = true;

        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Por encima del mezclador de sonido, que va en 250.
        canvas.sortingOrder = 300;

        CanvasScaler escala = gameObject.GetComponent<CanvasScaler>();
        if (escala == null) escala = gameObject.AddComponent<CanvasScaler>();

        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920f, 1080f);
        escala.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
        {
            gameObject.AddComponent<GraphicRaycaster>();
        }

        Image fondoVelo = UIFactory.Box("Velo", transform, VELO);
        UIFactory.Stretch(fondoVelo.rectTransform, 0f, 0f, 0f, 0f);
        velo = fondoVelo.rectTransform;

        Image caja = UIFactory.Box("Panel", velo, FONDO);
        caja.sprite = UIFactory.RoundedSprite(20);
        caja.type = Image.Type.Sliced;

        panel = caja.rectTransform;
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = new Vector2(ANCHO, ALTO);

        Cabecera();
        Rail();

        zonaContenido = UIFactory.Rect("Contenido", panel);
        zonaContenido.anchorMin = new Vector2(0f, 0f);
        zonaContenido.anchorMax = new Vector2(1f, 1f);
        zonaContenido.offsetMin = new Vector2(RAIL_ANCHO + 26f, PIE);
        zonaContenido.offsetMax = new Vector2(-30f, -CABECERA);

        Pie();
        MontarConfirmacion();

        velo.gameObject.SetActive(false);
    }

    void Cabecera()
    {
        var t = UIFactory.Text("Titulo", panel, "AJUSTES", 28, TEXTO,
                               TextAlignmentOptions.Left);
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 7f;

        RectTransform r = t.rectTransform;
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(-60f, CABECERA);
        r.anchoredPosition = Vector2.zero;

        Raya(1f, CABECERA);
    }

    // Una linea fina de separacion, anclada arriba o abajo del panel.
    void Raya(float desdeArriba, float y)
    {
        Image linea = UIFactory.Box("Raya", panel, RAYA);

        RectTransform r = linea.rectTransform;
        r.anchorMin = new Vector2(0f, desdeArriba);
        r.anchorMax = new Vector2(1f, desdeArriba);
        r.pivot = new Vector2(0.5f, desdeArriba);
        r.sizeDelta = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(0f, desdeArriba > 0.5f ? -y : y);
    }

    void Rail()
    {
        Image fondo = UIFactory.Box("Rail", panel, RAIL);
        fondo.sprite = UIFactory.RoundedSprite(14);
        fondo.type = Image.Type.Sliced;

        RectTransform r = fondo.rectTransform;
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 0.5f);
        r.sizeDelta = new Vector2(RAIL_ANCHO, -(CABECERA + PIE));
        r.anchoredPosition = new Vector2(0f, (PIE - CABECERA) * 0.5f);

        VerticalLayoutGroup col = UIFactory.Column(r, 6f, new RectOffset(14, 14, 18, 14));
        col.childAlignment = TextAnchor.UpperCenter;
        col.childControlWidth = true;
        col.childControlHeight = true;
        col.childForceExpandWidth = true;
        col.childForceExpandHeight = false;

        BotonSeccion(r, "JUEGO", Seccion.Juego);
        BotonSeccion(r, "SONIDO", Seccion.Sonido);
        BotonSeccion(r, "CONTROLES", Seccion.Controles);
    }

    void BotonSeccion(RectTransform padre, string etiqueta, Seccion cual)
    {
        Button b = UIFactory.Button("Sec_" + etiqueta, padre, etiqueta, 18,
                                    RAIL, SUAVE, () => Mostrar(cual));

        UIFactory.Height(b.GetComponent<RectTransform>(), 46f);

        Image cara = b.GetComponent<Image>();
        cara.sprite = UIFactory.RoundedSprite(10);
        cara.type = Image.Type.Sliced;

        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;

        botonesSeccion.Add(b);
    }

    void Mostrar(Seccion s)
    {
        seccion = s;

        for (int i = 0; i < botonesSeccion.Count; i++)
        {
            bool activo = i == (int)s;

            Image cara = botonesSeccion[i].GetComponent<Image>();
            cara.color = activo ? ACENTO : RAIL;

            var t = botonesSeccion[i].GetComponentInChildren<TextMeshProUGUI>();
            t.color = activo ? Color.white : SUAVE;
            t.fontStyle = activo ? FontStyles.Bold : FontStyles.Normal;
        }

        Refrescar();
    }

    void Pie()
    {
        Raya(0f, PIE);

        avisoPie = UIFactory.Text("Aviso", panel, "", 17, SUAVE,
                                  TextAlignmentOptions.Left);

        RectTransform ar = avisoPie.rectTransform;
        ar.anchorMin = new Vector2(0f, 0f);
        ar.anchorMax = new Vector2(0f, 0f);
        ar.pivot = new Vector2(0f, 0.5f);
        ar.sizeDelta = new Vector2(440f, PIE);
        ar.anchoredPosition = new Vector2(30f, PIE * 0.5f);

        float x = 30f;

        botonGuardar = BotonPie("Guardar", ACENTO, ref x, Guardar);
        BotonPie("Descartar", APAGADO, ref x, Descartar);
        BotonPie("Restaurar todo", APAGADO, ref x, Restaurar);
    }

    Button BotonPie(string etiqueta, Color color, ref float x, Action alPulsar)
    {
        Button b = UIFactory.Button("Pie_" + etiqueta, panel, etiqueta, 18,
                                    color, TEXTO, alPulsar);

        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(1f, 0f);
        r.anchorMax = new Vector2(1f, 0f);
        r.pivot = new Vector2(1f, 0.5f);
        r.sizeDelta = new Vector2(152f, 40f);
        r.anchoredPosition = new Vector2(-x, PIE * 0.5f);

        Image cara = b.GetComponent<Image>();
        cara.sprite = UIFactory.RoundedSprite(10);
        cara.type = Image.Type.Sliced;

        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f);
        cb.pressedColor = new Color(0.85f, 0.85f, 0.85f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;

        x += 152f + 10f;
        return b;
    }

    void MontarConfirmacion()
    {
        Image tapa = UIFactory.Box("Confirmacion", panel,
                                   new Color(0.02f, 0.03f, 0.05f, 0.84f));
        UIFactory.Stretch(tapa.rectTransform, 0f, 0f, 0f, 0f);
        tapa.sprite = UIFactory.RoundedSprite(20);
        tapa.type = Image.Type.Sliced;

        confirmacion = tapa.rectTransform;

        Image caja = UIFactory.Box("Caja", confirmacion, FILA_PAR);
        caja.sprite = UIFactory.RoundedSprite(16);
        caja.type = Image.Type.Sliced;

        RectTransform c = caja.rectTransform;
        c.anchorMin = c.anchorMax = new Vector2(0.5f, 0.5f);
        c.pivot = new Vector2(0.5f, 0.5f);
        c.sizeDelta = new Vector2(580f, 230f);
        c.anchoredPosition = Vector2.zero;

        var t = UIFactory.Text("Titulo", c, "Tienes cambios sin guardar", 24,
                               TEXTO, TextAlignmentOptions.Center);
        t.fontStyle = FontStyles.Bold;
        Arriba(t.rectTransform, 36f, 40f);

        var s = UIFactory.Text("Sub", c,
                               "Si sales ahora se pierde lo que has tocado.", 17,
                               SUAVE, TextAlignmentOptions.Center);
        Arriba(s.rectTransform, 82f, 30f);

        BotonConfirmar(c, "Guardar y salir", ACENTO, -160f, 190f, Guardar);
        BotonConfirmar(c, "Descartar", PELIGRO, 20f, 150f, Descartar);
        BotonConfirmar(c, "Seguir aqui", APAGADO, 180f, 150f, CerrarConfirmacion);

        confirmacion.gameObject.SetActive(false);
    }

    static void Arriba(RectTransform r, float desde, float alto)
    {
        r.anchorMin = new Vector2(0f, 1f);
        r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.sizeDelta = new Vector2(-40f, alto);
        r.anchoredPosition = new Vector2(0f, -desde);
    }

    void BotonConfirmar(RectTransform padre, string etiqueta, Color color,
                        float x, float ancho, Action alPulsar)
    {
        Button b = UIFactory.Button("Conf_" + etiqueta, padre, etiqueta, 18,
                                    color, TEXTO, alPulsar);

        RectTransform r = b.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(ancho, 44f);
        r.anchoredPosition = new Vector2(x, -66f);

        Image cara = b.GetComponent<Image>();
        cara.sprite = UIFactory.RoundedSprite(10);
        cara.type = Image.Type.Sliced;
    }

    // ====================================================== pintar cada tabla

    void Refrescar()
    {
        if (zonaContenido == null) return;

        // Apagados antes de destruirlos: Destroy no se hace efectivo hasta el
        // final del fotograma, y si no se ven los viejos y los nuevos a la vez.
        foreach (GameObject g in contenido)
        {
            if (g == null) continue;

            g.SetActive(false);
            Destroy(g);
        }

        contenido.Clear();
        botonesTecla.Clear();
        accionesTecla.Clear();

        switch (seccion)
        {
            case Seccion.Juego: TablaJuego(); break;
            case Seccion.Sonido: TablaSonido(); break;
            default: TablaControles(); break;
        }

        // Los botones de tecla nacen con la etiqueta vacia y se rellenan aqui,
        // que es el mismo sitio donde se repintan al reasignar una.
        RefrescarTeclas();
    }

    void TablaJuego()
    {
        RectTransform col = Columna(zonaContenido);

        Apartado(col, "Camara");

        RectTransform f = Fila(col, "Sensibilidad del raton", 0, 350f);
        TextMeshProUGUI cifra = Cifra(f);
        cifra.text = AjustesJuego.Sensibilidad.ToString("0.0");

        Barra(f, AjustesJuego.SENSIBILIDAD_MIN, AjustesJuego.SENSIBILIDAD_MAX,
              AjustesJuego.Sensibilidad,
              v =>
              {
                  AjustesJuego.Sensibilidad = v;
                  cifra.text = v.ToString("0.0");
                  MarcarCambios();
              });

        RectTransform f2 = Fila(col, "Invertir el eje Y", 1, 110f);
        Interruptor(f2, AjustesJuego.InvertirY,
                    v => { AjustesJuego.InvertirY = v; MarcarCambios(); });

        Nota(col, "La sensibilidad se nota al momento: mueve la barra y prueba a "
                  + "girar sin cerrar esto.");
    }

    void TablaSonido()
    {
        RectTransform col = Columna(zonaContenido);

        Apartado(col, "Volumen");

        for (int i = 0; i < 4; i++)
        {
            AjustesSonido.Canal canal = (AjustesSonido.Canal)i;

            RectTransform f = Fila(col, AjustesSonido.Nombre(canal), i, 350f);
            TextMeshProUGUI cifra = Cifra(f);
            cifra.text = Mathf.RoundToInt(AjustesSonido.Get(canal) * 100f).ToString();

            Barra(f, 0f, 1f, AjustesSonido.Get(canal),
                  v =>
                  {
                      AjustesSonido.Set(canal, v);
                      cifra.text = Mathf.RoundToInt(v * 100f).ToString();
                      MarcarCambios();
                  });
        }

        Nota(col, "General multiplica a los demas: bajandolo baja todo.");
    }

    void TablaControles()
    {
        // Aqui si hace falta scroll: son diecinueve teclas con sus apartados, y
        // no caben en los 546 de alto que tiene la zona de contenido.
        GameObject go = new GameObject("Scroll", typeof(RectTransform));
        go.transform.SetParent(zonaContenido, false);
        contenido.Add(go);

        RectTransform sr = (RectTransform)go.transform;
        UIFactory.Stretch(sr, 0f, 0f, 0f, 0f);

        ScrollRect scroll = go.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;

        RectTransform vista = UIFactory.Rect("Vista", sr);
        UIFactory.Stretch(vista, 0f, 0f, 0f, 0f);
        vista.gameObject.AddComponent<RectMask2D>();

        RectTransform lista = UIFactory.Rect("Lista", vista);

        // Anclada arriba y con el pivote arriba: asi crece hacia abajo.
        lista.anchorMin = new Vector2(0f, 1f);
        lista.anchorMax = new Vector2(1f, 1f);
        lista.pivot = new Vector2(0.5f, 1f);
        lista.sizeDelta = new Vector2(0f, 0f);
        lista.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup col = UIFactory.Column(lista, 6f, new RectOffset(0, 12, 0, 12));
        col.childAlignment = TextAnchor.UpperCenter;
        col.childControlWidth = true;
        col.childControlHeight = true;
        col.childForceExpandWidth = true;
        col.childForceExpandHeight = false;

        // Aqui un ContentSizeFitter SI vale, y la diferencia importa.
        //
        // El contenido de un ScrollRect va anclado arriba con el pivote arriba,
        // asi que el fitter le fija el alto de verdad. Sobre un rect estirado de
        // (0,0) a (1,1) se lo sumaria al del padre y lo mandaria fuera de la
        // pantalla, que es lo que me paso en la lista de la tienda.
        ContentSizeFitter ajuste = lista.gameObject.AddComponent<ContentSizeFitter>();
        ajuste.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vista;
        scroll.content = lista;

        string grupo = "";

        for (int i = 0; i < AjustesControles.Total; i++)
        {
            AjustesControles.Accion a = (AjustesControles.Accion)i;
            string g = AjustesControles.Grupo(a);

            if (g != grupo)
            {
                Apartado(lista, g);
                grupo = g;
            }

            RectTransform f = Fila(lista, AjustesControles.Descripcion(a), i,
                                   TECLA_ANCHO + 30f);
            BotonTecla(f, a);
        }
    }

    // ------------------------------------------------------------- ladrillos

    RectTransform Columna(RectTransform padre)
    {
        RectTransform col = UIFactory.Rect("Columna", padre);
        UIFactory.Stretch(col, 0f, 0f, 0f, 0f);

        VerticalLayoutGroup v = UIFactory.Column(col, 6f, new RectOffset(0, 0, 0, 0));
        v.childAlignment = TextAnchor.UpperCenter;
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;

        contenido.Add(col.gameObject);
        return col;
    }

    void Apartado(RectTransform padre, string titulo)
    {
        var t = UIFactory.Text("Apartado", padre, titulo.ToUpper(), 14, SUAVE,
                               TextAlignmentOptions.BottomLeft);
        t.fontStyle = FontStyles.Bold;
        t.characterSpacing = 6f;

        UIFactory.Height(t.rectTransform, 38f);
    }

    void Nota(RectTransform padre, string texto)
    {
        var t = UIFactory.Text("Nota", padre, texto, 15, SUAVE,
                               TextAlignmentOptions.TopLeft);
        UIFactory.Height(t.rectTransform, 46f);
    }

    RectTransform Fila(RectTransform padre, string etiqueta, int indice,
                       float reservado)
    {
        Image fondo = UIFactory.Box("Fila", padre,
                                    indice % 2 == 0 ? FILA_PAR : FILA_IMPAR);
        fondo.sprite = UIFactory.RoundedSprite(10);
        fondo.type = Image.Type.Sliced;

        RectTransform r = fondo.rectTransform;
        UIFactory.Height(r, FILA_ALTO);

        var t = UIFactory.Text("Nombre", r, etiqueta, 18, TEXTO,
                               TextAlignmentOptions.Left);

        RectTransform tr = t.rectTransform;
        tr.anchorMin = new Vector2(0f, 0f);
        tr.anchorMax = new Vector2(1f, 1f);
        tr.offsetMin = new Vector2(16f, 0f);
        tr.offsetMax = new Vector2(-reservado, 0f);

        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;

        return r;
    }

    TextMeshProUGUI Cifra(RectTransform fila)
    {
        var t = UIFactory.Text("Cifra", fila, "", 18, SUAVE,
                               TextAlignmentOptions.Right);

        Derecha(t.rectTransform, 16f, 56f, FILA_ALTO);
        return t;
    }

    static void Derecha(RectTransform r, float desde, float ancho, float alto)
    {
        r.anchorMin = new Vector2(1f, 0.5f);
        r.anchorMax = new Vector2(1f, 0.5f);
        r.pivot = new Vector2(1f, 0.5f);
        r.sizeDelta = new Vector2(ancho, alto);
        r.anchoredPosition = new Vector2(-desde, 0f);
    }

    // Una barra de las de arrastrar, montada a mano.
    //
    // Es el Slider de Unity de toda la vida: hace falta el relleno y el mango
    // por separado, y decirle cual es cada uno. Montado asi se puede arrastrar,
    // pinchar en un punto, y mover con las flechas.
    Slider Barra(RectTransform fila, float min, float max, float valor,
                 UnityEngine.Events.UnityAction<float> alCambiar)
    {
        GameObject go = new GameObject("Barra", typeof(RectTransform));
        go.transform.SetParent(fila, false);

        RectTransform r = (RectTransform)go.transform;
        Derecha(r, 84f, 250f, 20f);

        Image canal = go.AddComponent<Image>();
        canal.sprite = UIFactory.RoundedSprite(5);
        canal.type = Image.Type.Sliced;
        canal.color = new Color(1f, 1f, 1f, 0.12f);

        Slider s = go.AddComponent<Slider>();
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = min;
        s.maxValue = max;
        s.transition = Selectable.Transition.None;

        RectTransform area = UIFactory.Rect("Area", r);
        UIFactory.Stretch(area, 0f, 0f, 0f, 0f);

        Image relleno = UIFactory.Box("Relleno", area, ACENTO);
        relleno.sprite = UIFactory.RoundedSprite(5);
        relleno.type = Image.Type.Sliced;
        UIFactory.Stretch(relleno.rectTransform, 0f, 0f, 0f, 0f);

        RectTransform areaMango = UIFactory.Rect("AreaMango", r);
        UIFactory.Stretch(areaMango, 10f, 0f, 10f, 0f);

        Image mango = UIFactory.Box("Mango", areaMango, Color.white);
        mango.sprite = UIFactory.RoundedSprite(10);
        mango.type = Image.Type.Sliced;
        mango.rectTransform.sizeDelta = new Vector2(20f, 20f);

        s.fillRect = relleno.rectTransform;
        s.handleRect = mango.rectTransform;
        s.targetGraphic = mango;

        // El valor antes que el aviso: puesto al reves, colocar la barra ya
        // contaria como que has tocado algo y saltaria lo de "sin guardar".
        s.value = valor;
        s.onValueChanged.AddListener(alCambiar);

        return s;
    }

    void Interruptor(RectTransform fila, bool valor, Action<bool> alCambiar)
    {
        // En una casilla hay que mirar si esta marcada; aqui pone SI o NO y se
        // acabo. En una lista de ajustes se leen todos de una pasada.
        bool[] estado = { valor };

        Button b = UIFactory.Button("Interruptor", fila, valor ? "SI" : "NO", 17,
                                    valor ? ACENTO : APAGADO, TEXTO, null);

        Derecha(b.GetComponent<RectTransform>(), 16f, 80f, 34f);

        Image cara = b.GetComponent<Image>();
        cara.sprite = UIFactory.RoundedSprite(17);
        cara.type = Image.Type.Sliced;

        var etiqueta = b.GetComponentInChildren<TextMeshProUGUI>();

        b.onClick.AddListener(() =>
        {
            estado[0] = !estado[0];

            etiqueta.text = estado[0] ? "SI" : "NO";
            cara.color = estado[0] ? ACENTO : APAGADO;

            alCambiar(estado[0]);
        });
    }

    void BotonTecla(RectTransform fila, AjustesControles.Accion a)
    {
        Button b = UIFactory.Button("Tecla", fila, "", 17, APAGADO, TEXTO,
                                    () => { escuchando = (int)a; RefrescarTeclas(); });

        Derecha(b.GetComponent<RectTransform>(), 16f, TECLA_ANCHO, 34f);

        Image cara = b.GetComponent<Image>();
        cara.sprite = UIFactory.RoundedSprite(8);
        cara.type = Image.Type.Sliced;

        ColorBlock cb = b.colors;
        cb.highlightedColor = new Color(1.4f, 1.4f, 1.4f);
        cb.fadeDuration = 0.08f;
        b.colors = cb;

        botonesTecla.Add(b);
        accionesTecla.Add(a);
    }

    // Solo repinta las teclas, sin rehacer la tabla.
    //
    // Rehaciendola entera, al asignar una tecla de las de abajo el scroll
    // saltaba al principio y habia que volver a bajar. Cambiar el texto de los
    // botones que ya estan puestos deja la lista donde la tenias.
    void RefrescarTeclas()
    {
        for (int i = 0; i < botonesTecla.Count; i++)
        {
            Button b = botonesTecla[i];
            if (b == null) continue;

            AjustesControles.Accion a = accionesTecla[i];
            bool esperando = escuchando == (int)a;

            var t = b.GetComponentInChildren<TextMeshProUGUI>();

            if (t != null)
            {
                t.text = esperando
                    ? "Pulsa una tecla"
                    : AjustesControles.NombreTecla(AjustesControles.Tecla(a));

                t.color = esperando ? Color.white : TEXTO;
            }

            Image cara = b.GetComponent<Image>();
            if (cara != null) cara.color = esperando ? ACENTO : APAGADO;
        }

        MarcarCambios();
    }

    void MarcarCambios()
    {
        bool hay = HayCambios();

        if (avisoPie != null)
        {
            avisoPie.text = hay
                ? "Tienes cambios sin guardar"
                : "Escape para salir";

            avisoPie.color = hay ? new Color(0.95f, 0.75f, 0.35f) : SUAVE;
        }

        if (botonGuardar != null)
        {
            Image cara = botonGuardar.GetComponent<Image>();
            if (cara != null) cara.color = hay ? ACENTO : APAGADO;
        }
    }
}
