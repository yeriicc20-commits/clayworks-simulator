using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Una fila de lista, de las que se leen en vertical.
//
// La tienda y el carrito usan esta misma, y por eso vive aparte. Con dos
// versiones parecidas, la del carrito y la de la tienda se van separando cada
// vez que se toca una y acaban pareciendo dos programas distintos.
//
//   [foto]  nombre                     x3      120 $   [boton]
//
// Las columnas de la derecha van ancladas a la derecha y el nombre se come lo
// que sobre: asi los precios quedan alineados entre si aunque los nombres midan
// cosas distintas, que es lo que hace que una lista se pueda leer de un vistazo.
public static class FilaLista
{
    public static readonly Color Tinta = new Color(0.13f, 0.14f, 0.17f);
    public static readonly Color Suave = new Color(0.45f, 0.47f, 0.52f);

    static readonly Color PAR = new Color(1f, 1f, 1f, 0.55f);
    static readonly Color IMPAR = new Color(0f, 0f, 0f, 0.035f);

    public const float ALTO = 58f;
    public const float FOTO = 46f;

    const int MARGEN = 10;
    const float HUECO = 6f;

    // Deja el contenedor listo para leerse como una lista.
    //
    // Los contenedores de la escena llevan GridLayoutGroup con celdas de
    // 200x240. Una fila de lista necesita el ancho entero: metida en una celda
    // de 200 se queda con la foto y el nombre fuera del recorte y solo asoma el
    // trozo del precio, que es exactamente como se veia.
    //
    // Lo que NO se puede hacer es cambiar la rejilla por un VerticalLayoutGroup.
    // Un GameObject admite un solo LayoutGroup, y desactivar el que hay no vale
    // de nada: AddComponent devuelve null igualmente. Lo hice y dejo la tienda
    // en blanco, porque la NullReferenceException se llevaba por delante el
    // resto de GenerateShopUI antes de crear ni una fila.
    //
    // No hace falta: una rejilla de una sola columna a todo lo ancho ya ES una
    // lista. Se configura la que esta puesta y no se le toca nada a la escena.
    public static void PrepararLista(Transform contenedor)
    {
        RectTransform rt = contenedor as RectTransform;
        if (rt == null) return;

        float ancho = rt.rect.width - MARGEN * 2f;

        // Si el layout todavia no ha corrido el rect mide 0, y unas celdas de
        // nada se ven igual de mal que no tener lista.
        if (ancho < 1f) ancho = 620f;

        GridLayoutGroup rejilla = contenedor.GetComponent<GridLayoutGroup>();

        if (rejilla != null)
        {
            rejilla.enabled = true;
            rejilla.padding = new RectOffset(MARGEN, MARGEN, MARGEN, MARGEN);
            rejilla.cellSize = new Vector2(ancho, ALTO);
            rejilla.spacing = new Vector2(0f, HUECO);
            rejilla.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            rejilla.constraintCount = 1;
            rejilla.startAxis = GridLayoutGroup.Axis.Horizontal;
            rejilla.startCorner = GridLayoutGroup.Corner.UpperLeft;
            rejilla.childAlignment = TextAnchor.UpperCenter;
            return;
        }

        // Sin rejilla de por medio, un vertical normal.
        VerticalLayoutGroup lista = contenedor.GetComponent<VerticalLayoutGroup>();

        if (lista == null)
        {
            // Con cualquier otro LayoutGroup puesto, tampoco se puede anadir.
            // Antes de estrellarse, mejor dejarlo como esta y decirlo.
            LayoutGroup otro = contenedor.GetComponent<LayoutGroup>();

            if (otro != null)
            {
                Debug.LogWarning("[Lista] " + contenedor.name + " lleva un "
                                 + otro.GetType().Name + ", asi que no puedo poner"
                                 + " la lista en vertical.");
                return;
            }

            lista = contenedor.gameObject.AddComponent<VerticalLayoutGroup>();
        }

        lista.enabled = true;
        lista.spacing = HUECO;
        lista.padding = new RectOffset(MARGEN, MARGEN, MARGEN, MARGEN);
        lista.childAlignment = TextAnchor.UpperCenter;
        lista.childControlWidth = true;
        lista.childControlHeight = true;
        lista.childForceExpandWidth = true;
        lista.childForceExpandHeight = false;
    }

    // Devuelve la fila para que quien la pida pueda colgarle mas cosas.
    public static RectTransform Crear(Transform padre, int indice, string nombre,
                                      Sprite icono, string cantidad, int precio,
                                      string etiquetaBoton, Color colorBoton,
                                      Action alPulsar)
    {
        RectTransform raiz = UIFactory.Rect("Fila_" + nombre, padre);
        UIFactory.Height(raiz, ALTO);

        // Las filas alternas con un fondo apenas visible. Es lo unico que deja
        // seguir un renglon de izquierda a derecha sin cambiarse de linea.
        Image fondo = UIFactory.Box("Fondo", raiz, indice % 2 == 0 ? PAR : IMPAR);
        UIFactory.Stretch(fondo.rectTransform, 0f, 0f, 0f, 0f);
        fondo.sprite = UIFactory.RoundedSprite(8);
        fondo.type = Image.Type.Sliced;
        fondo.raycastTarget = false;

        // --- la foto -------------------------------------------------------
        Image imagen = UIFactory.Box("Foto", raiz, Color.white);
        Izquierda(imagen.rectTransform, 10f, FOTO);
        imagen.preserveAspect = true;
        imagen.raycastTarget = false;

        Sprite foto = IconoTienda.Para(nombre, icono);

        if (foto != null)
        {
            imagen.sprite = foto;
        }
        else
        {
            // Sin foto, un hueco marcado. Un cuadro blanco parece una imagen que
            // no ha terminado de cargar y da la sensacion de que algo va mal.
            imagen.color = new Color(0f, 0f, 0f, 0.06f);
            imagen.sprite = UIFactory.RoundedSprite(6);
            imagen.type = Image.Type.Sliced;
        }

        // --- columnas de la derecha, de fuera hacia dentro -------------------
        float d = 10f;
        float reservado = 10f;

        if (!string.IsNullOrEmpty(etiquetaBoton))
        {
            Button b = UIFactory.Button("Accion", raiz, etiquetaBoton, 17,
                                        colorBoton, Color.white, alPulsar);

            RectTransform rb = b.GetComponent<RectTransform>();
            Derecha(rb, d, 104f, 38f);

            Image cara = b.GetComponent<Image>();

            if (cara != null)
            {
                cara.sprite = UIFactory.RoundedSprite(9);
                cara.type = Image.Type.Sliced;
            }

            ColorBlock cb = b.colors;
            cb.highlightedColor = new Color(1.12f, 1.12f, 1.12f);
            cb.pressedColor = new Color(0.86f, 0.86f, 0.86f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;

            d += 104f + 10f;
            reservado += 104f + 10f;
        }

        var texto = UIFactory.Text("Precio", raiz, GameManager.Format(precio), 19,
                                   Tinta, TextAlignmentOptions.Right);
        texto.fontStyle = FontStyles.Bold;
        Derecha(texto.rectTransform, d, 104f, 0f);

        d += 104f + 10f;
        reservado += 104f + 10f;

        if (!string.IsNullOrEmpty(cantidad))
        {
            var cant = UIFactory.Text("Cantidad", raiz, cantidad, 18, Suave,
                                      TextAlignmentOptions.Center);
            Derecha(cant.rectTransform, d, 56f, 0f);

            reservado += 56f + 10f;
        }

        // --- el nombre, con lo que quede -------------------------------------
        var titulo = UIFactory.Text("Nombre", raiz, nombre, 19, Tinta,
                                    TextAlignmentOptions.Left);

        RectTransform n = titulo.rectTransform;
        n.anchorMin = new Vector2(0f, 0f);
        n.anchorMax = new Vector2(1f, 1f);
        n.offsetMin = new Vector2(10f + FOTO + 12f, 0f);
        n.offsetMax = new Vector2(-reservado, 0f);

        // Cortado con puntos suspensivos, no partido en dos lineas: partiendolo
        // la fila se descuadra y la lista deja de leerse en vertical.
        titulo.enableWordWrapping = false;
        titulo.overflowMode = TextOverflowModes.Ellipsis;

        return raiz;
    }

    static void Izquierda(RectTransform r, float desde, float lado)
    {
        r.anchorMin = new Vector2(0f, 0.5f);
        r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot = new Vector2(0f, 0.5f);
        r.sizeDelta = new Vector2(lado, lado);
        r.anchoredPosition = new Vector2(desde, 0f);
    }

    static void Derecha(RectTransform r, float desde, float ancho, float alto)
    {
        r.anchorMin = new Vector2(1f, 0.5f);
        r.anchorMax = new Vector2(1f, 0.5f);
        r.pivot = new Vector2(1f, 0.5f);
        r.sizeDelta = new Vector2(ancho, alto > 0f ? alto : ALTO);
        r.anchoredPosition = new Vector2(-desde, 0f);
    }
}
