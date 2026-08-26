using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Monta los prefabs de la bombilla y el interruptor a partir de sus FBX, y los
// deja puestos en la tienda.
//
// A mano seria arrastrar el modelo, anadirle la luz, ponerle el collider,
// ajustar el alcance y acordarse de la regla de colocacion, y repetirlo cada vez
// que se retoque el modelo en Blender. Aqui se vuelve a pulsar y sale igual.
public static class LucesBuilder
{
    const string MODELOS = "Assets/_Project/Models/";
    const string PREFABS = "Assets/_Project/Prefabs/Local/";

    // La bombilla cuelga 0,335 m de la roseta. La luz se pone a esa altura y no
    // en el origen: naciendo en el techo, la propia bombilla se queda a oscuras
    // por debajo y las sombras salen al reves.
    const float CAIDA_LUZ = 0.30f;

    [MenuItem("ClayWorks/Construir luces", false, 5)]
    public static void Construir()
    {
        if (!AssetDatabase.IsValidFolder(PREFABS.TrimEnd('/')))
        {
            Directory.CreateDirectory(PREFABS);
            AssetDatabase.Refresh();
        }

        GameObject bombilla = ConstruirBombilla();
        GameObject interruptor = ConstruirInterruptor();

        AssetDatabase.SaveAssets();

        Registrar(bombilla, interruptor);
    }

    // ------------------------------------------------------------- bombilla

    static GameObject ConstruirBombilla()
    {
        GameObject modelo = Cargar("Bombilla");
        if (modelo == null) return null;

        GameObject raiz = Object.Instantiate(modelo);
        raiz.name = "Bombilla";

        // Una luz de punto colgada donde esta el vidrio.
        GameObject nodo = new GameObject("Luz");
        nodo.transform.SetParent(raiz.transform, false);
        nodo.transform.localPosition = new Vector3(0f, -CAIDA_LUZ, 0f);

        Light luz = nodo.AddComponent<Light>();
        luz.type = LightType.Point;
        luz.color = new Color(1f, 0.90f, 0.72f);
        luz.intensity = 3.2f;
        luz.range = 9f;

        // Con sombras: sin ellas las maquinas no se apoyan en el suelo y todo
        // parece flotar, que es justo lo que delata a una luz falsa.
        luz.shadows = LightShadows.Soft;
        luz.shadowStrength = 0.75f;

        Bombilla script = raiz.AddComponent<Bombilla>();
        script.luz = luz;
        script.encendida = true;

        ReglaDeColocacion regla = raiz.AddComponent<ReglaDeColocacion>();
        regla.donde = ReglaDeColocacion.Donde.Techo;

        Colisionador(raiz);

        return Guardar(raiz, "Bombilla");
    }

    // ---------------------------------------------------------- interruptor

    static GameObject ConstruirInterruptor()
    {
        GameObject modelo = Cargar("Interruptor");
        if (modelo == null) return null;

        GameObject raiz = Object.Instantiate(modelo);
        raiz.name = "Interruptor";

        Interruptor script = raiz.AddComponent<Interruptor>();
        script.tecla = Buscar(raiz.transform, "Tecla");

        if (script.tecla == null)
        {
            Debug.LogWarning("[Luces] El modelo del interruptor no trae la pieza "
                             + "'Tecla' suelta: no basculara al pulsarlo. Vuelve a "
                             + "exportar Modelos/interruptor.py.");
        }

        ReglaDeColocacion regla = raiz.AddComponent<ReglaDeColocacion>();

        // Libre: el jugador lo pone donde quiera, y se orienta a la superficie.
        regla.donde = ReglaDeColocacion.Donde.Libre;

        // Un pelo separado, que pegado del todo al muro los dos planos pelean por
        // el mismo pixel y sale un parpadeo.
        regla.separacion = 0.002f;

        Colisionador(raiz);

        return Guardar(raiz, "Interruptor");
    }

    // ------------------------------------------------------------- ayudantes

    static GameObject Cargar(string nombre)
    {
        string ruta = MODELOS + nombre + ".fbx";
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);

        if (modelo == null)
        {
            Debug.LogError("[Luces] No encuentro " + ruta + ". Genera el modelo con "
                           + "blender --background --python Modelos/" + nombre.ToLower()
                           + ".py");
        }

        return modelo;
    }

    // Una caja que envuelve lo que se ve.
    //
    // Hace falta para que el colocador sepa el tamano y para poder mirar el
    // interruptor. Se calcula de los renderers y no a mano: cambiando el modelo
    // en Blender, un collider escrito a mano se queda del tamano de antes.
    static void Colisionador(GameObject raiz)
    {
        Bounds limites = new Bounds();
        bool primero = true;

        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            if (primero)
            {
                limites = r.bounds;
                primero = false;
                continue;
            }

            limites.Encapsulate(r.bounds);
        }

        if (primero) return;

        BoxCollider caja = raiz.AddComponent<BoxCollider>();

        caja.center = raiz.transform.InverseTransformPoint(limites.center);
        caja.size = limites.size;
    }

    static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == nombre) return t;
        }

        return null;
    }

    static GameObject Guardar(GameObject raiz, string nombre)
    {
        string ruta = PREFABS + nombre + ".prefab";

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
        Object.DestroyImmediate(raiz);

        Debug.Log("[Luces] " + nombre + " listo en " + ruta);

        return prefab;
    }

    // --------------------------------------------------------- ponerlo a la venta

    // Se registran en la tienda de la escena abierta.
    //
    // Si no, la pestana de luces sale vacia y no hay nada que diga por que: el
    // prefab existe, el modelo existe, pero nadie los vende.
    static void Registrar(GameObject bombilla, GameObject interruptor)
    {
        if (bombilla == null || interruptor == null) return;

        ShopManager tienda = BuscarTienda();

        if (tienda == null)
        {
            Debug.LogWarning("[Luces] No hay ShopManager en la escena abierta: los "
                             + "prefabs estan hechos pero no se venden todavia.");
            return;
        }

        tienda.decoItems = new DecoShopItem[]
        {
            Ficha("Bombilla", 45, bombilla, tienda),
            Ficha("Interruptor", 25, interruptor, tienda),
        };

        EditorUtility.SetDirty(tienda);
        EditorSceneManager.MarkSceneDirty(tienda.gameObject.scene);

        Debug.Log("[Luces] Puestas a la venta. Guarda la escena para que se quede.");
    }

    static DecoShopItem Ficha(string nombre, int precio, GameObject prefab,
                              ShopManager tienda)
    {
        DecoShopItem ficha = new DecoShopItem();

        ficha.itemName = nombre;
        ficha.price = precio;
        ficha.itemPrefab = prefab;

        // La caja de las maquinas, que es la que lleva PickupBox. La de juguetes
        // lleva ToyBox y suelta peluches al abrirla, que no es lo que queremos.
        ficha.boxPrefab = CajaDeMaquina(tienda);

        return ficha;
    }

    static GameObject CajaDeMaquina(ShopManager tienda)
    {
        if (tienda.items == null) return null;

        foreach (ShopItem it in tienda.items)
        {
            if (it != null && it.boxPrefab != null) return it.boxPrefab;
        }

        return null;
    }

    static ShopManager BuscarTienda()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene esc = SceneManager.GetSceneAt(i);
            if (!esc.isLoaded) continue;

            foreach (GameObject raiz in esc.GetRootGameObjects())
            {
                ShopManager s = raiz.GetComponentInChildren<ShopManager>(true);
                if (s != null) return s;
            }
        }

        return null;
    }
}
