using System.IO;
using UnityEditor;
using UnityEngine;

// Monta los prefabs de la bombilla y el interruptor a partir de sus FBX.
//
// A mano seria arrastrar el modelo, anadirle la luz, ponerle el collider,
// ajustar el alcance y acordarse de la regla de colocacion, y repetirlo cada vez
// que se retoque el modelo en Blender. Aqui se vuelve a pulsar y sale igual.
public static class LucesBuilder
{
    const string MODELOS = "Assets/_Project/Models/";
    // En Resources, y esto es lo que hace que no haga falta guardar nada.
    //
    // Registrarlas en la tienda de la escena obliga a acordarse de guardar la
    // escena, y si no se guarda la pestana sale vacia sin que nada diga por
    // que. Desde Resources la tienda las carga por su cuenta al arrancar: si
    // el prefab esta, se venden.
    const string PREFABS = "Assets/_Project/Resources/Luces/";

    // La bombilla cuelga 0,335 m de la roseta. La luz se pone a esa altura y no
    // en el origen: naciendo en el techo, la propia bombilla se queda a oscuras
    // por debajo y las sombras salen al reves.
    const float CAIDA_LUZ = 0.30f;

    // Solas al abrir Unity, y solo si faltan.
    //
    // Un paso manual que hay que recordar es un paso que un dia no se da, y
    // entonces la pestana de luces sale vacia. Rehacerlas siempre seria
    // reescribir dos prefabs en cada recompilacion, asi que solo si no estan.
    [InitializeOnLoadMethod]
    static void AlArrancar()
    {
        EditorApplication.delayCall += () =>
        {
            if (Falta("Bombilla") || Falta("Interruptor")) Construir();
        };
    }

    static bool Falta(string nombre)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(
            PREFABS + nombre + ".prefab") == null;
    }

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

        if (bombilla != null && interruptor != null)
        {
            Debug.Log("[Luces] Bombilla e interruptor listos y a la venta.");
        }
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

}
