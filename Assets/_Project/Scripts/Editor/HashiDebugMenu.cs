using UnityEditor;
using UnityEngine;

// Menu de pruebas de la maquina.
//
// Para lo que mas se hace al afinar una fisica: poner otra caja, devolverla a su
// sitio y cambiar de dificultad, veinte veces seguidas. A mano son cuatro clics
// por vuelta en el inspector, y a la decima vuelta se deja de probar y se empieza
// a suponer.
//
// Todo funciona con la escena parada y con ella corriendo. En marcha es donde de
// verdad sirve: se cambia la dificultad sin salir de Play y se ve al momento si
// la caja aguanta mas o menos.
public static class HashiDebugMenu
{
    const string MENU = "ClayWorks/Hashi-Watashi/Pruebas/";

    // ------------------------------------------------------------- el premio

    [MenuItem(MENU + "Poner otra caja (al azar)", false, 100)]
    static void PremioAlAzar()
    {
        Hashi.PrizeSpawner g = Buscar<Hashi.PrizeSpawner>();
        if (g == null) return;

        g.Quitar();
        Hashi.PrizeController p = g.GenerarAleatorio();

        Aplicar(p);
        Debug.Log("[Hashi] Caja nueva: " + (p != null ? p.name : "ninguna"));
    }

    [MenuItem(MENU + "Devolver la caja a su sitio", false, 101)]
    static void ReiniciarPremio()
    {
        Hashi.PrizeSpawner g = Buscar<Hashi.PrizeSpawner>();
        if (g == null) return;

        g.Reiniciar();
        Debug.Log("[Hashi] Caja otra vez sobre las barras.");
    }

    [MenuItem(MENU + "Quitar la caja", false, 102)]
    static void QuitarPremio()
    {
        Hashi.PrizeSpawner g = Buscar<Hashi.PrizeSpawner>();
        if (g == null) return;

        g.Quitar();
    }

    // Una entrada por caja, generada a partir de los assets que haya. Asi
    // anadir una caja nueva la mete en el menu sola.
    [MenuItem(MENU + "Caja siguiente", false, 103)]
    static void CajaSiguiente()
    {
        Hashi.PrizeSpawner g = Buscar<Hashi.PrizeSpawner>();
        if (g == null || g.Modelos == null || g.Modelos.Length == 0) return;

        int siguiente = (g.IndiceActual + 1) % g.Modelos.Length;
        Aplicar(g.Generar(siguiente));

        Debug.Log("[Hashi] Caja: " + g.Modelos[siguiente].nombre
                  + " - " + g.Modelos[siguiente].notas);
    }

    // ---------------------------------------------------------- la dificultad

    [MenuItem(MENU + "Dificultad/Facil", false, 200)]
    static void Facil() { Dificultad(0); }

    [MenuItem(MENU + "Dificultad/Normal", false, 201)]
    static void Normal() { Dificultad(1); }

    [MenuItem(MENU + "Dificultad/Dificil", false, 202)]
    static void Dificil() { Dificultad(2); }

    [MenuItem(MENU + "Dificultad/Extremo", false, 203)]
    static void Extremo() { Dificultad(3); }

    static void Dificultad(int i)
    {
        Hashi.GameManager j = Buscar<Hashi.GameManager>();
        if (j == null) return;

        j.CambiarDificultad(i);

        Debug.Log("[Hashi] Dificultad: "
                  + (j.Dificultad != null ? j.Dificultad.nombre : "?")
                  + (j.Dificultad != null ? "\n" + j.Dificultad.notas : ""));

        Marcar(j);
    }

    // ---------------------------------------------------------- la depuracion

    [MenuItem(MENU + "Modo depuracion (F1)", false, 300)]
    static void Depuracion()
    {
        Hashi.GameManager j = Buscar<Hashi.GameManager>();
        if (j == null) return;

        j.CambiarDepuracion(!j.ModoDepuracion);
        Debug.Log("[Hashi] Depuracion " + (j.ModoDepuracion ? "encendida" : "apagada"));

        Marcar(j);
    }

    [MenuItem(MENU + "Ver el centro de masas de la caja", false, 301)]
    static void CentroDeMasas()
    {
        Hashi.PrizeSpawner g = Buscar<Hashi.PrizeSpawner>();
        if (g == null || g.Actual == null)
        {
            Debug.LogWarning("[Hashi] No hay ninguna caja puesta.");
            return;
        }

        // Los gizmos del premio solo salen con el objeto seleccionado
        // (OnDrawGizmosSelected), asi que ademas de encenderlos hay que
        // seleccionarlo. Si no, se enciende la opcion y no se ve nada.
        var so = new SerializedObject(g.Actual);
        SerializedProperty p = so.FindProperty("mostrarGizmos");

        if (p != null)
        {
            p.boolValue = !p.boolValue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Selection.activeGameObject = g.Actual.gameObject;
        SceneView.RepaintAll();

        Debug.Log("[Hashi] Gizmos de la caja "
                  + (p != null && p.boolValue ? "encendidos" : "apagados")
                  + ". La bola roja es el centro de masas y la linea, su vertical: "
                  + "mientras caiga entre las dos barras, la caja aguanta.");
    }

    [MenuItem(MENU + "Medir el hueco de las barras", false, 302)]
    static void MedirHueco()
    {
        Hashi.BarRig b = Buscar<Hashi.BarRig>();
        if (b == null) return;

        Hashi.PrizeSpawner g = Buscar<Hashi.PrizeSpawner>();
        Hashi.PrizeController p = g != null ? g.Actual : null;

        string texto = "[Hashi] Barras a " + b.BarDistance.ToString("0.000")
                       + " m de eje a eje, hueco libre de "
                       + b.HuecoLibre.ToString("0.000") + " m.";

        if (p != null)
        {
            texto += "\nCaja de " + p.Tamano.x.ToString("0.000") + " x "
                     + p.Tamano.y.ToString("0.000") + " x "
                     + p.Tamano.z.ToString("0.000") + " m. ";

            texto += b.CabePorElHueco(p.Tamano, out string motivo)
                ? "Se puede ganar."
                : "NO se puede jugar: " + motivo + ".";
        }

        Debug.Log(texto);
    }

    // ----------------------------------------------------------------- ayuda

    static T Buscar<T>() where T : Component
    {
        T t = Object.FindFirstObjectByType<T>();

        if (t == null)
        {
            Debug.LogWarning("[Hashi] No encuentro ningun " + typeof(T).Name
                             + " en la escena abierta. Abre Hashi_Watashi o "
                             + "montala con ClayWorks/Hashi-Watashi/Montar escena.");
        }

        return t;
    }

    // La dificultad tiene que caer sobre la caja recien puesta tambien cuando
    // la pone este menu, o se prueba con los valores de fabrica sin saberlo.
    static void Aplicar(Hashi.PrizeController p)
    {
        if (p == null) return;

        Hashi.GameManager j = Object.FindFirstObjectByType<Hashi.GameManager>();
        if (j != null && j.Dificultad != null) p.AplicarDificultad(j.Dificultad);

        Marcar(p);
    }

    // Con la escena parada, un cambio que no se marca sucio no se guarda.
    static void Marcar(Component c)
    {
        if (Application.isPlaying || c == null) return;

        EditorUtility.SetDirty(c);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(c.gameObject.scene);
    }
}
