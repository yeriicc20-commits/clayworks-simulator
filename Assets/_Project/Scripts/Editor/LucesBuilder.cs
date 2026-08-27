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

    // La capa de lo que se puede recoger. HoldToPickup solo mira ahi.
    const int CAPA_PLACEABLE = 8;

    // La caja en la que llega una luz.
    //
    // Copiada a Resources porque la tienda la carga en tiempo de ejecucion y
    // desde ahi no se puede leer Assets/_Project/Prefabs. Se copia una vez y
    // solo si falta.
    const string CAJA_ORIGEN = "Assets/_Project/Prefabs/Box_Pequena.prefab";
    const string CAJA_LUZ = "Assets/_Project/Resources/Luces/Caja_Luz.prefab";

    // Cuanto por debajo de la luminaria va el punto de luz.
    //
    // No pegado a ella: la pantalla va contra el techo, y una luz a tres
    // centimetros del techo lo abrasa y deja la sala igual de oscura que
    // antes. Un palmo mas abajo reparte, y sigue sin verse porque queda
    // dentro del cono que tapa la propia carcasa.
    const float LUZ_DEBAJO = 0.10f;

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
            if (Falta("LedTecho") || Falta("Interruptor") || FaltaCaja()) Construir();
        };
    }

    static bool Falta(string nombre)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>(
            PREFABS + nombre + ".prefab") == null;
    }

    static bool FaltaCaja()
    {
        GameObject caja = AssetDatabase.LoadAssetAtPath<GameObject>(CAJA_LUZ);

        // Cuenta como que falta si esta pero sin PickupBox. Eso paso la
        // primera vez: la caja se copiaba tal cual y la tienda la tiraba a la
        // basura al ver que no se podia abrir. Comprobandolo aqui se arregla
        // sola en vez de tener que borrarla a mano.
        return caja == null || caja.GetComponent<PickupBox>() == null;
    }

    // La caja en la que llega la pantalla.
    //
    // Llegaba en la de las maquinas, que es la grande, y una regleta no ocupa
    // eso ni de lejos. Se copia la pequena... pero la pequena es la de
    // JUGUETES: lleva ToyBox y al abrirla suelta peluches. Hay que cambiarle
    // el componente por el que saca lo que lleve dentro.
    static void PrepararCaja()
    {
        if (!FaltaCaja()) return;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(CAJA_ORIGEN) == null)
        {
            Debug.LogWarning("[Luces] No encuentro " + CAJA_ORIGEN
                             + ": la luz llegara en la caja de las maquinas.");
            return;
        }

        // Se borra antes: CopyAsset no pisa un archivo que ya este, y si
        // hemos llegado aqui es que el que hay no sirve.
        AssetDatabase.DeleteAsset(CAJA_LUZ);

        if (!AssetDatabase.CopyAsset(CAJA_ORIGEN, CAJA_LUZ))
        {
            Debug.LogWarning("[Luces] No he podido copiar la caja pequena.");
            return;
        }

        GameObject copia = PrefabUtility.LoadPrefabContents(CAJA_LUZ);

        ToyBox juguetes = copia.GetComponent<ToyBox>();
        if (juguetes != null) Object.DestroyImmediate(juguetes);

        if (copia.GetComponent<PickupBox>() == null)
        {
            copia.AddComponent<PickupBox>();
        }

        PrefabUtility.SaveAsPrefabAsset(copia, CAJA_LUZ);
        PrefabUtility.UnloadPrefabContents(copia);

        Debug.Log("[Luces] Caja de la luz lista: la pequena, pero de las que "
                  + "se abren para sacar lo de dentro.");
    }

    [MenuItem("ClayWorks/Construir luces", false, 5)]
    public static void Construir()
    {
        if (!AssetDatabase.IsValidFolder(PREFABS.TrimEnd('/')))
        {
            Directory.CreateDirectory(PREFABS);
            AssetDatabase.Refresh();
        }

        PrepararCaja();

        GameObject led = ConstruirLed();
        GameObject interruptor = ConstruirInterruptor();

        AssetDatabase.SaveAssets();

        if (led != null && interruptor != null)
        {
            Debug.Log("[Luces] Pantalla LED e interruptor listos.");
        }
    }

    // ----------------------------------------------------------- pantalla LED

    static GameObject ConstruirLed()
    {
        GameObject modelo = Cargar("LedTecho");
        if (modelo == null) return null;

        GameObject raiz = Object.Instantiate(modelo);
        raiz.name = "LedTecho";

        // El punto de luz, por debajo de la luminaria. El sitio se mide del
        // propio modelo: escrito a mano, cambiar la pantalla en Blender dejaria
        // la luz metida dentro de la chapa.
        GameObject nodo = new GameObject("Luz");
        nodo.transform.SetParent(raiz.transform, false);
        nodo.transform.localPosition = new Vector3(0f, FondoDe(raiz) - LUZ_DEBAJO, 0f);

        // Un foco apuntando al suelo, y no una luz de punto.
        //
        // Una luz de punto emite en todas las direcciones, tambien hacia
        // arriba: dibujaba un redondel brillante en el techo justo encima de
        // la regleta, que es lo que se veia raro. Una regleta de verdad no
        // manda luz hacia arriba, y un foco tampoco.
        //
        // Muy abierto -- 130 grados -- para que reparta por la sala en vez de
        // hacer un circulito de discoteca debajo.
        nodo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Light luz = nodo.AddComponent<Light>();
        luz.type = LightType.Spot;
        luz.spotAngle = 130f;
        luz.innerSpotAngle = 70f;

        // Blanco frio tirando a neutro, que es de lo que va un LED. La
        // bombilla de antes era amarilla; una regleta de local no lo es.
        luz.color = new Color(0.97f, 0.98f, 1f);

        // Alcance largo, que son 5 m de techo: con el corto la luz se apaga
        // antes de llegar al suelo. Y un foco reparte su fuerza dentro del cono,
        // asi que necesita mas numero que una luz de punto para dar lo mismo.
        luz.intensity = 5.5f;
        luz.range = 14f;

        // Con sombras: sin ellas las maquinas no se apoyan en el suelo y todo
        // parece flotar, que es justo lo que delata a una luz falsa.
        luz.shadows = LightShadows.Soft;
        luz.shadowStrength = 0.7f;

        // Pero la propia luminaria no proyecta.
        //
        // El punto de luz esta justo debajo de ella, asi que la carcasa le
        // tapa todo lo de arriba y dibujaba en el techo una franja negra con
        // la forma de la pantalla, justo alrededor de la luz. Sin proyectar,
        // el techo de alrededor se ilumina como corresponde.
        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        Bombilla script = raiz.AddComponent<Bombilla>();
        script.luz = luz;
        script.encendida = true;
        script.brillo = new Color(1f, 0.98f, 0.92f);

        // Solo el difusor se enciende. Con la chapa dentro, la luminaria
        // entera brillaria como una barra de neon.
        Transform difusor = Buscar(raiz.transform, "Difusor");

        if (difusor != null)
        {
            script.brillantes = difusor.GetComponents<Renderer>();
        }
        else
        {
            Debug.LogWarning("[Luces] La pantalla no trae la pieza 'Difusor' "
                             + "suelta: se encendera entera. Vuelve a exportar "
                             + "Modelos/led.py.");
        }

        ReglaDeColocacion regla = raiz.AddComponent<ReglaDeColocacion>();
        regla.donde = ReglaDeColocacion.Donde.Techo;

        SePuedeRecoger(raiz);
        Colisionador(raiz);

        return Guardar(raiz, "LedTecho");
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

        SePuedeRecoger(raiz);
        Colisionador(raiz);

        return Guardar(raiz, "Interruptor");
    }

    // Se recoge manteniendo el clic derecho, igual que las maquinas.
    //
    // Le habia puesto una tecla propia, y eso era inventar un segundo sistema
    // para lo mismo: quien juega ya sabe que las cosas colocadas se levantan
    // con el clic derecho mantenido. Basta con la pieza que ya existe y con
    // estar en la capa que mira HoldToPickup.
    //
    // sourcePrefab no se pone aqui: lo rellena el colocador al soltarlo, que
    // es quien sabe de que prefab salio.
    static void SePuedeRecoger(GameObject raiz)
    {
        raiz.AddComponent<PlaceableObject>();

        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = CAPA_PLACEABLE;
        }
    }

    // ------------------------------------------------------------- ayudantes

    // Lo mas bajo del modelo, en coordenadas del propio objeto.
    static float FondoDe(GameObject raiz)
    {
        float fondo = 0f;
        bool alguno = false;

        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            float y = raiz.transform.InverseTransformPoint(
                new Vector3(0f, r.bounds.min.y, 0f)).y;

            if (!alguno || y < fondo)
            {
                fondo = y;
                alguno = true;
            }
        }

        return fondo;
    }

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
