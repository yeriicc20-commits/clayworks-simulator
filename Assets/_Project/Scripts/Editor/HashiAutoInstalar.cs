using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Instala la maquina de puente en el local sola, sin que nadie pulse nada.
//
// Es el mismo truco que ya usa LucesBuilder para sus prefabs: engancharse a la
// recarga de scripts y hacer el trabajo si falta. Aqui hace falta ademas por un
// motivo practico: quien escribe el codigo no puede pulsar los menus de Unity,
// asi que un paso manual obligatorio es un paso que se queda sin dar y una
// maquina que "no esta" aunque este todo hecho.
//
// Se ejecuta UNA vez y se apunta que ya lo hizo. Sin esa marca, borrar la
// maquina del local la haria reaparecer en la siguiente recompilacion, que es
// una manera muy desagradable de que un editor te lleve la contraria.
[InitializeOnLoad]
public static class HashiAutoInstalar
{
    const string CLAVE = "Hashi.YaInstalada.";
    const string RUTA_PREFAB = "Assets/_Project/Prefabs/Machines/MaquinaPuente.prefab";

    // Separacion respecto a la maquina que se coja de referencia.
    const float HUECO = 1.4f;

    static string Marca => CLAVE + Application.dataPath.GetHashCode();

    static HashiAutoInstalar()
    {
        EditorApplication.delayCall += Comprobar;
    }

    [MenuItem("ClayWorks/Hashi-Watashi/Instalar en el local ahora", false, 44)]
    static void AMano()
    {
        // A mano se rehace aunque ya estuviera marcada: si alguien lo pide
        // expresamente es que quiere otra.
        EditorPrefs.DeleteKey(Marca);
        Instalar(true);
    }

    static void Comprobar()
    {
        EditorApplication.delayCall -= Comprobar;

        // En modo de juego no se toca la escena: todo lo que se cambie durante
        // el Play se pierde al salir, asi que se colocaria una maquina que
        // desaparece sola y no habria manera de entender por que.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged += AlSalirDeJuego;
            return;
        }

        // La reparacion va ANTES de todo lo demas y no depende de nada.
        //
        // Estaba despues del bloque que rehace el prefab, y ese bloque termina
        // en return: justo la vez que hacia falta arreglar la maquina, el prefab
        // estaba caducado, se rehacia, y la reparacion no llegaba a correr.
        GameObject yaHecho = AssetDatabase.LoadAssetAtPath<GameObject>(RUTA_PREFAB);

        if (yaHecho != null && RepararColocadas(yaHecho))
        {
            Scene abierta = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(abierta);
            EditorSceneManager.SaveScene(abierta);
        }

        // El prefab es una pieza DERIVADA de los scripts que lo construyen. Si
        // se toca una medida y el prefab no se rehace, la maquina del local se
        // queda con la geometria vieja y desde fuera parece que el cambio no ha
        // servido de nada. Es la misma leccion que MaquinaGarraAutoBuild.
        if (Caducado())
        {
            Debug.Log("[Hashi] El prefab es mas viejo que el codigo que lo monta. "
                      + "Lo rehago para que la maquina del local no se quede con "
                      + "las medidas antiguas.");

            Rehacer();
            return;
        }

        if (EditorPrefs.GetBool(Marca, false)) return;

        Instalar(false);
    }

    // ------------------------------------------------------ rehacer el prefab

    static readonly string[] FUENTES =
    {
        "Assets/_Project/Scripts/Hashi",
        "Assets/_Project/Scripts/Editor",
    };

    static bool Caducado()
    {
        if (!System.IO.File.Exists(RUTA_PREFAB)) return false;

        System.DateTime prefab = System.IO.File.GetLastWriteTimeUtc(RUTA_PREFAB);

        foreach (string carpeta in FUENTES)
        {
            if (!System.IO.Directory.Exists(carpeta)) continue;

            foreach (string f in System.IO.Directory.GetFiles(carpeta, "*.cs",
                                                              System.IO.SearchOption.AllDirectories))
            {
                // Solo los de esta maquina: el resto de scripts del proyecto se
                // tocan a diario y no tienen nada que ver con su geometria.
                if (carpeta.EndsWith("Editor")
                    && !System.IO.Path.GetFileName(f).StartsWith("Hashi")) continue;

                if (System.IO.File.GetLastWriteTimeUtc(f) > prefab) return true;
            }
        }

        return false;
    }

    // Rehacer el prefab entero le cambia los identificadores internos, y con
    // ellos se pierden los retoques que tuviera la maquina ya colocada en el
    // local: lo primero que se pierde es DONDE estaba, y aparece de golpe en el
    // origen del mundo, a saber donde. Asi que se apunta antes y se devuelve
    // despues.
    static void Rehacer()
    {
        var sitios = new System.Collections.Generic.List<(Transform t, Vector3 p, Quaternion r)>();

        foreach (Hashi.MachineController m
                 in Object.FindObjectsByType<Hashi.MachineController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Transform raiz = m.transform.root;
            sitios.Add((raiz, raiz.position, raiz.rotation));
        }

        if (HashiWatashiBuilder.MontarPrefab() == null) return;

        // Y de paso el catalogo, por si hay fichas nuevas que registrar. Es
        // barato: Registrar() solo guarda la escena si de verdad cambia algo.
        HashiTiendaSetup.Registrar(false);

        bool tocada = false;

        foreach (var s in sitios)
        {
            if (s.t == null) continue;

            if (s.t.position != s.p || s.t.rotation != s.r)
            {
                s.t.SetPositionAndRotation(s.p, s.r);
                tocada = true;
            }
        }

        if (!tocada) return;

        Scene escena = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        Debug.Log("[Hashi] Maquina del local actualizada y devuelta a su sitio.");
    }

    static void AlSalirDeJuego(PlayModeStateChange estado)
    {
        if (estado != PlayModeStateChange.EnteredEditMode) return;

        EditorApplication.playModeStateChanged -= AlSalirDeJuego;
        EditorApplication.delayCall += Comprobar;
    }

    static void Instalar(bool aMano)
    {
        // 1. El prefab. Esto no depende de que escena este abierta y es
        //    inofensivo: se monta en una escena de trabajo aparte.
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RUTA_PREFAB);

        if (prefab == null)
        {
            prefab = HashiWatashiBuilder.MontarPrefab();
            if (prefab == null) return;
        }

        // 2. La ficha del catalogo. Solo si la escena de la tienda ya esta
        //    abierta: abrirla por su cuenta seria pasarse.
        ShopManager tienda = Object.FindFirstObjectByType<ShopManager>();

        if (tienda == null)
        {
            Debug.Log("[Hashi] Prefab de la maquina listo en " + RUTA_PREFAB
                      + ". Abre Local_01 y se instalara sola, o pulsa "
                      + "ClayWorks/Hashi-Watashi/Instalar en el local ahora.");

            // Sin marca: se vuelve a intentar cuando abran la tienda.
            return;
        }

        HashiTiendaSetup.Registrar(false);

        // 3. Una unidad ya colocada, para poder verla sin tener que comprarla.
        Scene escena = tienda.gameObject.scene;
        bool colocada = Colocar(prefab, escena, aMano);

        EditorPrefs.SetBool(Marca, true);

        if (colocada) EditorSceneManager.SaveScene(escena);
    }

    static bool Colocar(GameObject prefab, Scene escena, bool aMano)
    {
        if (!aMano && YaHayUna(escena)) return false;

        Vector3 sitio = BuscarSitio(out Quaternion giro);

        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);

        if (inst == null) return false;

        inst.transform.SetPositionAndRotation(sitio, giro);

        Enlazar(inst, prefab);

        // Para que se pueda deshacer con Ctrl+Z como cualquier otra cosa.
        Undo.RegisterCreatedObjectUndo(inst, "Colocar la maquina de puente");

        Selection.activeGameObject = inst;
        EditorGUIUtility.PingObject(inst);

        EditorSceneManager.MarkSceneDirty(escena);

        Debug.Log("[Hashi] Maquina de puente colocada en el local, en "
                  + sitio.ToString("0.00") + ". Si estorba ahi, muevela o "
                  + "borrala: ya esta en el catalogo por 550 y se puede comprar "
                  + "otra cuando quieras.");

        return true;
    }

    // Le dice a la maquina de que prefab ha salido. Es lo que mira el juego para
    // poder recogerla y volver a ponerla, y lo hace normalmente PlacementManager
    // al confirmar la colocacion; una maquina puesta desde el editor, como esta,
    // se queda sin ello si no se rellena aqui.
    //
    // Tiene que apuntar al ASSET. Guardarlo dentro del prefab no vale: la
    // referencia se remapea al instanciar y la maquina acaba apuntandose a si
    // misma, con lo que al recogerla se destruye su propio molde.
    static void Enlazar(GameObject inst, GameObject prefab)
    {
        PlaceableObject colocable = inst.GetComponentInChildren<PlaceableObject>(true);

        if (colocable == null || colocable.sourcePrefab == prefab) return;

        colocable.sourcePrefab = prefab;
        EditorUtility.SetDirty(colocable);
    }

    // Repara las que ya estuvieran puestas de antes, que se quedaron con la
    // referencia mala. Sin esto habria que borrarlas y volver a ponerlas a mano.
    static bool RepararColocadas(GameObject prefab)
    {
        bool tocado = false;

        foreach (Hashi.MachineController m
                 in Object.FindObjectsByType<Hashi.MachineController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            GameObject raiz = m.transform.root.gameObject;

            PlaceableObject colocable = raiz.GetComponentInChildren<PlaceableObject>(true);
            if (colocable == null || colocable.sourcePrefab == prefab) continue;

            colocable.sourcePrefab = prefab;
            EditorUtility.SetDirty(colocable);
            tocado = true;

            Debug.Log("[Hashi] '" + raiz.name + "' tenia mal la referencia a su "
                      + "prefab y no se podia recolocar. Arreglada.");
        }

        return tocado;
    }

    static bool YaHayUna(Scene escena)
    {
        foreach (Hashi.MachineController m
                 in Object.FindObjectsByType<Hashi.MachineController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (m.gameObject.scene == escena) return true;
        }

        return false;
    }

    // Al lado de una maquina que ya este puesta, mirando hacia donde mire ella.
    // Es lo unico que se sabe seguro que esta dentro del local, sobre el suelo y
    // orientado hacia el pasillo; cualquier posicion inventada acaba dentro de
    // una pared la mitad de las veces.
    static Vector3 BuscarSitio(out Quaternion giro)
    {
        giro = Quaternion.identity;

        Vector3 sitio = Vector3.zero;

        ClawController referencia = Object.FindFirstObjectByType<ClawController>();

        if (referencia != null)
        {
            Transform t = referencia.transform.root;
            giro = t.rotation;
            sitio = t.position + t.right * HUECO;
        }
        else
        {
            GameObject jugador = GameObject.FindGameObjectWithTag("Player");

            if (jugador != null)
            {
                giro = Quaternion.Euler(0f, jugador.transform.eulerAngles.y + 180f, 0f);
                sitio = jugador.transform.position + jugador.transform.forward * 2.5f;
            }
        }

        // Y apoyada en el suelo, venga de donde venga la posicion. El prefab
        // tiene el origen en su base, asi que basta con dejarla donde toque el
        // rayo.
        if (Physics.Raycast(sitio + Vector3.up * 3f, Vector3.down,
                            out RaycastHit suelo, 12f,
                            ~0, QueryTriggerInteraction.Ignore))
        {
            sitio.y = suelo.point.y;
        }

        return sitio;
    }
}
