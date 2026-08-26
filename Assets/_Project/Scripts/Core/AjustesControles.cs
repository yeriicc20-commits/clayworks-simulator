using System;
using UnityEngine;

// Las teclas del juego, en un solo sitio y reasignables.
//
// Antes cada tecla vivia como campo publico en el componente que la usaba:
// useKey en BoxCarrier, teclaVender en PelucheEnMano, priceKey en cada maquina,
// y unas cuantas escritas a mano dentro del codigo. Con eso no hay pantalla de
// controles posible: no existe la lista de "que teclas hay", y aunque se
// cambiase una en el inspector, las maquinas que se instancian luego nacerian
// con la suya propia.
//
// Ahora la lista es esta enumeracion, y los componentes preguntan aqui en el
// momento de leer. Cambiar una tecla surte efecto al instante y en todo lo que
// exista, incluido lo que se cree despues.
public static class AjustesControles
{
    // El orden importa: Grupo() parte la lista por tramos, asi que las acciones
    // de un mismo apartado tienen que ir seguidas.
    public enum Accion
    {
        Adelante, Atras, Izquierda, Derecha, Correr, Agacharse,
        Usar, Lanzar, Vender, Soltar,
        GarraAdelante, GarraAtras, GarraIzquierda, GarraDerecha, BajarGarra,
        Precios, Sonido, AbrirLocal, CerrarLocal,
    }

    // Mismo orden que la enumeracion, y asi tiene que quedarse.
    static readonly KeyCode[] PorDefecto =
    {
        KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D,
        KeyCode.LeftShift, KeyCode.LeftControl,

        KeyCode.E, KeyCode.Q, KeyCode.V, KeyCode.G,

        KeyCode.I, KeyCode.K, KeyCode.J, KeyCode.L, KeyCode.Space,

        KeyCode.P, KeyCode.F1, KeyCode.O, KeyCode.Return,
    };

    public static int Total { get { return PorDefecto.Length; } }

    static readonly KeyCode[] teclas = new KeyCode[PorDefecto.Length];
    static bool cargado = false;

    public static event Action Cambiado;

    public static KeyCode Tecla(Accion a)
    {
        Cargar();
        return teclas[(int)a];
    }

    // Atajos, porque casi todas las lecturas son de uno de estos dos tipos.
    //
    // Y ademas son la puerta: con el menu de ajustes delante, el teclado es
    // suyo. Sin esto, reasignando la tecla de abrir el local, la O que pulsas
    // para asignarla abre el local de verdad por detras del menu.
    //
    // La puerta esta aqui y no en Tecla() a proposito: el menu necesita seguir
    // preguntando que tecla tiene cada accion para poder pintarlas.
    public static bool Pulsada(Accion a)
    {
        if (MenuAjustes.IsOpen) return false;

        return Input.GetKey(Tecla(a));
    }

    public static bool Pulsando(Accion a)
    {
        if (MenuAjustes.IsOpen) return false;

        return Input.GetKeyDown(Tecla(a));
    }

    public static KeyCode Defecto(Accion a) { return PorDefecto[(int)a]; }

    // Asignar una tecla nunca deja dos acciones con la misma.
    //
    // Si la tecla ya estaba cogida, las dos acciones se intercambian en vez de
    // quedarse las dos con ella. Dejar el choque y avisar obliga a ir a buscar
    // la otra para arreglarla; intercambiando, el teclado siempre queda entero.
    public static void Set(Accion a, KeyCode tecla)
    {
        Cargar();

        // Escape no se puede asignar: es la salida de todas las pantallas, y
        // quien la perdiera se quedaria sin manera de volver.
        if (tecla == KeyCode.None || tecla == KeyCode.Escape) return;
        if (teclas[(int)a] == tecla) return;

        KeyCode anterior = teclas[(int)a];

        for (int i = 0; i < teclas.Length; i++)
        {
            if (i == (int)a || teclas[i] != tecla) continue;

            teclas[i] = anterior;
            PlayerPrefs.SetString(Clave((Accion)i), anterior.ToString());
        }

        teclas[(int)a] = tecla;
        PlayerPrefs.SetString(Clave(a), tecla.ToString());

        if (Cambiado != null) Cambiado();
    }

    public static void Restaurar()
    {
        Cargar();

        for (int i = 0; i < teclas.Length; i++)
        {
            teclas[i] = PorDefecto[i];
            PlayerPrefs.SetString(Clave((Accion)i), teclas[i].ToString());
        }

        if (Cambiado != null) Cambiado();
    }

    // ---------------------------------------------------------------- textos

    public static string Descripcion(Accion a)
    {
        switch (a)
        {
            case Accion.Adelante: return "Andar hacia delante";
            case Accion.Atras: return "Andar hacia atras";
            case Accion.Izquierda: return "Andar a la izquierda";
            case Accion.Derecha: return "Andar a la derecha";
            case Accion.Correr: return "Correr";
            case Accion.Agacharse: return "Agacharse";

            case Accion.Usar: return "Coger y usar";
            case Accion.Lanzar: return "Lanzar lo que llevas";
            case Accion.Vender: return "Vender lo que llevas";
            case Accion.Soltar: return "Dejar lo que llevas";

            case Accion.GarraAdelante: return "Mover la garra al fondo";
            case Accion.GarraAtras: return "Mover la garra hacia ti";
            case Accion.GarraIzquierda: return "Mover la garra a la izquierda";
            case Accion.GarraDerecha: return "Mover la garra a la derecha";
            case Accion.BajarGarra: return "Bajar la garra";

            case Accion.Precios: return "Cambiar el precio de la maquina";
            case Accion.Sonido: return "Mezclador de sonido";
            case Accion.AbrirLocal: return "Abrir el local";
            default: return "Cerrar el local";
        }
    }

    public static string Grupo(Accion a)
    {
        if (a <= Accion.Agacharse) return "Movimiento";
        if (a <= Accion.Soltar) return "Acciones";
        if (a <= Accion.BajarGarra) return "Maquina de garra";

        return "Local";
    }

    // El nombre que se pinta en la tecla.
    //
    // KeyCode.ToString() suelta "LeftControl" y "Alpha4", que no es lo que pone
    // en ninguna tecla de ningun teclado. En una pantalla de controles hay que
    // poder leer lo que esta escrito en la tecla que vas a pulsar.
    public static string NombreTecla(KeyCode k)
    {
        switch (k)
        {
            case KeyCode.LeftControl: return "Ctrl izq";
            case KeyCode.RightControl: return "Ctrl der";
            case KeyCode.LeftShift: return "Mayus izq";
            case KeyCode.RightShift: return "Mayus der";
            case KeyCode.LeftAlt: return "Alt";
            case KeyCode.RightAlt: return "Alt Gr";
            case KeyCode.Space: return "Espacio";
            case KeyCode.Return: return "Intro";
            case KeyCode.KeypadEnter: return "Intro num";
            case KeyCode.Tab: return "Tabulador";
            case KeyCode.Backspace: return "Retroceso";
            case KeyCode.UpArrow: return "Arriba";
            case KeyCode.DownArrow: return "Abajo";
            case KeyCode.LeftArrow: return "Izquierda";
            case KeyCode.RightArrow: return "Derecha";
            case KeyCode.None: return "-";
        }

        string s = k.ToString();

        if (s.StartsWith("Alpha")) return s.Substring(5);
        if (s.StartsWith("Keypad")) return "Num " + s.Substring(6);

        return s;
    }

    // ------------------------------------------------------------- capturar

    // Devuelve la tecla que se acaba de pulsar, o None si no hay ninguna.
    //
    // Recorrer el enum entero es feo, pero solo corre mientras la pantalla esta
    // esperando una tecla, que son unos pocos fotogramas. Y es la unica forma
    // de saber CUAL se ha pulsado: Input no tiene "dame la ultima".
    public static KeyCode Capturar()
    {
        if (!Input.anyKeyDown) return KeyCode.None;

        foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
        {
            // Los botones de raton y de mando llegan tambien como KeyCode, y no
            // valen aqui: el clic izquierdo ya sirve para coger cosas, y
            // asignarlo dejaria el juego inservible sin forma clara de deshacer.
            if (k >= KeyCode.Mouse0) continue;

            if (Input.GetKeyDown(k)) return k;
        }

        return KeyCode.None;
    }

    // ---------------------------------------------------------------- guardar

    static string Clave(Accion a) { return "control_" + a.ToString(); }

    static void Cargar()
    {
        if (cargado) return;
        cargado = true;

        for (int i = 0; i < teclas.Length; i++)
        {
            teclas[i] = PorDefecto[i];

            string guardado = PlayerPrefs.GetString(Clave((Accion)i), "");
            if (string.IsNullOrEmpty(guardado)) continue;

            // Si lo guardado no se puede leer (un KeyCode que ya no existe, o
            // unas preferencias a medio escribir), se queda el de fabrica en
            // vez de dejar la accion sin tecla.
            try
            {
                teclas[i] = (KeyCode)Enum.Parse(typeof(KeyCode), guardado);
            }
            catch (ArgumentException)
            {
                teclas[i] = PorDefecto[i];
            }
        }
    }
}
