using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Monta los prefabs de peluche a partir de sus FBX.
//
// Mismo planteamiento que el de la maquina: el prefab es una pieza DERIVADA del
// modelo, asi que se genera y no se toca a mano. Si manana cambia el modelo se
// vuelve a pulsar y sale bien, en vez de quedar posiciones viejas apuntando a
// mallas nuevas.
//
// La tabla de abajo es lo unico que hay que tocar para anadir uno nuevo.
public static class PelucheBuilder
{
    const int CAPA_PLUSH = 9;

    const string CARPETA_MAT = "Assets/_Project/Materials/Peluches";
    const string CARPETA_PREFAB = "Assets/_Project/Prefabs";
    const string SHADER_URP = "Universal Render Pipeline/Lit";
    const string FISICA = "Assets/_Project/Physics/Peluche.physicMaterial";

    struct Peluche
    {
        public string nombre;
        public string fbx;
        public PlushItem.WeightCategory peso;

        // Piezas que cuelgan y se balancean solas. Vacio si no tiene.
        public string[] partesBlandas;
        public Dictionary<string, Color> colores;

        // Como se llama en la tienda. Vacio = no se vende.
        public string tienda;
    }

    static readonly Peluche[] TODOS =
    {
        new Peluche
        {
            nombre = "Panxeta_Toy",
            fbx = "Assets/_Project/Models/Panxeta.fbx",
            peso = PlushItem.WeightCategory.Medio,
            partesBlandas = new[] { "Oreja_Izq", "Oreja_Der" },
            tienda = "Panxeta",
            colores = new Dictionary<string, Color>
            {
                // Los mismos valores que en Blender. Se repiten aqui a
                // proposito: el FBX trae materiales, pero Unity los importa con
                // el shader que le parece y en URP salen rosas o planos.
                { "Panxeta_Blanco",     new Color(0.94f, 0.93f, 0.89f) },
                { "Panxeta_Naranja",    new Color(0.76f, 0.26f, 0.06f) },
                { "Panxeta_NaranjaOsc", new Color(0.48f, 0.15f, 0.04f) },
                { "Panxeta_Negro",      new Color(0.06f, 0.06f, 0.07f) },
            },
        },

        new Peluche
        {
            nombre = "Aguacate_Toy",
            fbx = "Assets/_Project/Models/Aguacate.fbx",
            peso = PlushItem.WeightCategory.Medio,

            // Sin piezas blandas: los brazos van rigidos, como los del oso.
            //
            // Se puede: son munones cosidos al costado y el sistema de cable
            // sabria moverlos. Pero un aguacate no es un peluche de orejas
            // colgando, y unos brazos meneandose le quitan el aire de pieza
            // maciza que tiene en la foto. Aqui se deja quieto a proposito.
            partesBlandas = null,
            tienda = "Aguacate",
            colores = new Dictionary<string, Color>
            {
                // Los mismos valores que en Blender. La piel MUCHO mas oscura
                // que la carne: en la foto es lo que separa la cascara del
                // relleno, y con los dos verdes parecidos deja de leerse como
                // un aguacate abierto.
                { "Aguacate_Piel",    new Color(0.22f, 0.38f, 0.10f) },
                { "Aguacate_Carne",   new Color(0.69f, 0.80f, 0.36f) },
                { "Aguacate_Hueso",   new Color(0.30f, 0.13f, 0.08f) },
                { "Aguacate_Marron",  new Color(0.30f, 0.18f, 0.10f) },
                { "Aguacate_Negro",   new Color(0.06f, 0.06f, 0.07f) },
                { "Aguacate_Rosa",    new Color(0.90f, 0.55f, 0.58f) },
                { "Aguacate_Blanco",  new Color(0.97f, 0.97f, 0.95f) },
            },
        },
    };

    // Igual que la maquina: el prefab se rehace solo cuando cambia el modelo o
    // este mismo archivo. Acordarse de pulsar un boton no es un sistema, y un
    // prefab viejo apuntando a mallas nuevas sale desmontado sin avisar.
    [InitializeOnLoadMethod]
    static void ComprobarAlArrancar()
    {
        EditorApplication.delayCall += Comprobar;
    }

    public static void Comprobar()
    {
        EditorApplication.delayCall -= Comprobar;

        const string YO = "Assets/_Project/Scripts/Editor/PelucheBuilder.cs";

        foreach (Peluche p in TODOS)
        {
            if (!File.Exists(p.fbx)) continue;

            string prefab = CARPETA_PREFAB + "/" + p.nombre + ".prefab";

            System.DateTime fuente = File.GetLastWriteTimeUtc(p.fbx);

            if (File.Exists(YO))
            {
                System.DateTime mio = File.GetLastWriteTimeUtc(YO);
                if (mio > fuente) fuente = mio;
            }

            if (File.Exists(prefab) && fuente <= File.GetLastWriteTimeUtc(prefab)) continue;

            Debug.Log("[Peluche] " + p.nombre + " esta desfasado, lo rehago.");
            Montar(p);
        }

        AssetDatabase.SaveAssets();
    }

    [MenuItem("Assets/Construir peluches", false, 31)]
    [MenuItem("ClayWorks/Construir peluches", false, 2)]
    public static void Construir()
    {
        foreach (Peluche p in TODOS) Montar(p);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static void Montar(Peluche p)
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(p.fbx);

        if (modelo == null)
        {
            Debug.LogError("[Peluche] No encuentro el modelo en " + p.fbx);
            return;
        }

        GameObject raiz = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        PrefabUtility.UnpackPrefabInstance(raiz, PrefabUnpackMode.Completely,
                                           InteractionMode.AutomatedAction);

        raiz.name = p.nombre;
        raiz.transform.position = Vector3.zero;

        // La capa va en la raiz Y en los hijos: la garra busca peluches por
        // capa, y si las mallas se quedan en Default no encuentra nada.
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = CAPA_PLUSH;
        }

        var materiales = Materiales(p.colores);
        Repartir(raiz, materiales);

        Rigidbody rb = raiz.AddComponent<Rigidbody>();
        rb.useGravity = true;

        PlushItem item = raiz.AddComponent<PlushItem>();
        item.weightCategory = p.peso;
        item.physicsMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(FISICA);

        if (item.physicsMaterial == null)
        {
            Debug.LogWarning("[Peluche] Falta " + FISICA + ": sin rozamiento, la "
                             + "garra no lo sujetara ni con el motor al maximo.");
        }

        Blandas(raiz, p.partesBlandas);

        MaquinaGarraBuilder.AsegurarCarpeta(CARPETA_PREFAB);

        string ruta = CARPETA_PREFAB + "/" + p.nombre + ".prefab";
        GameObject guardado = PrefabUtility.SaveAsPrefabAsset(raiz, ruta);

        Reenlazar(p, guardado);

        Bounds caja = Envolvente(raiz);
        Object.DestroyImmediate(raiz);

        Debug.Log(string.Format(
            "[Peluche] {0} montado en {1}\n"
            + "  medidas ...... {2:F3} x {3:F3} x {4:F3} m\n"
            + "  materiales ... {5}\n"
            + "  masa ......... {6:F2} kg\n"
            + "  piezas blandas {7}",
            p.nombre, ruta, caja.size.x, caja.size.y, caja.size.z,
            materiales.Count, item.GetWeightValue(),
            p.partesBlandas == null ? 0 : p.partesBlandas.Length));

        Selection.activeObject = guardado;
    }

    // Vuelve a colgar el peluche de la tienda si se ha quedado suelto.
    //
    // El prefab se regenera ENTERO cada vez que cambia el modelo, y la escena lo
    // referencia por el fileID de su raiz. Hasta ahora ese fileID ha aguantado
    // las regeneraciones, asi que la referencia sobrevive, pero eso lo decide
    // Unity y no nosotros.
    //
    // Y si algun dia no aguanta, no salta ningun error: la ficha se sigue
    // pintando en la tienda, con su nombre y su precio, solo que al comprarla no
    // aparece ningun peluche. Un fallo que no avisa y que ademas parece un
    // problema del carrito. Mejor comprobarlo aqui, que es donde se sabe cual es
    // el prefab bueno.
    static void Reenlazar(Peluche p, GameObject prefab)
    {
        if (string.IsNullOrEmpty(p.tienda) || prefab == null) return;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene escena = SceneManager.GetSceneAt(i);
            if (!escena.isLoaded) continue;

            foreach (GameObject raiz in escena.GetRootGameObjects())
            {
                foreach (ShopManager tienda in raiz.GetComponentsInChildren<ShopManager>(true))
                {
                    if (tienda.toyItems == null) continue;

                    foreach (ToyShopItem item in tienda.toyItems)
                    {
                        if (item == null || item.itemName != p.tienda) continue;
                        if (item.toyPrefab == prefab) continue;

                        item.toyPrefab = prefab;

                        EditorUtility.SetDirty(tienda);
                        EditorSceneManager.MarkSceneDirty(escena);

                        Debug.Log("[Peluche] " + p.tienda + " se habia quedado sin "
                                  + "prefab en la tienda de " + escena.name
                                  + "; lo he vuelto a enlazar. Guarda la escena.");
                    }
                }
            }
        }
    }

    static void Blandas(GameObject raiz, string[] nombres)
    {
        if (nombres == null || nombres.Length == 0) return;

        var encontradas = new List<Transform>();

        foreach (string n in nombres)
        {
            Transform t = Buscar(raiz.transform, n);

            if (t == null)
            {
                Debug.LogWarning("[Peluche] No encuentro la pieza blanda " + n);
                continue;
            }

            encontradas.Add(t);
        }

        if (encontradas.Count == 0) return;

        OrejasBlandas ob = raiz.AddComponent<OrejasBlandas>();
        ob.orejas = encontradas.ToArray();

        // Puestos a mano y no dejados por defecto.
        //
        // El prefab guarda estos numeros dentro, asi que cambiar el valor por
        // defecto en OrejasBlandas.cs no toca nada: el prefab sigue con el que
        // se guardo el dia que se monto, y solo se entera si algo lo obliga a
        // rehacerse. Este builder solo se fija en su propia fecha, con lo que un
        // ajuste en el script de las orejas se quedaba sin aplicar y parecia que
        // no habia servido de nada. Escribiendolos aqui, la fuente es una sola.
        ob.nudos = 6;
        ob.rigidez = 0.8f;
        ob.pasadas = 3;
        ob.inercia = 0.92f;
        ob.anguloPorNudo = 34f;

        // La mitad del canto de la oreja, que es de 24 mm.
        ob.radio = 0.012f;

        // El escenario y la maquina, no los peluches. Que las orejas de veinte
        // peluches se estorben entre ellas serian cientos de consultas por
        // fotograma para algo que dentro de un monton no se ve.
        ob.contra = (1 << 0)      // Default
                  | (1 << 6)      // Obstacle
                  | (1 << 7)      // Ground
                  | (1 << 10)     // ClawParts
                  | (1 << 11);    // MachineShell
    }

    static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == nombre) return t;
        }

        return null;
    }

    static Bounds Envolvente(GameObject go)
    {
        Bounds b = new Bounds();
        bool primero = true;

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            if (primero) { b = r.bounds; primero = false; }
            else b.Encapsulate(r.bounds);
        }

        return b;
    }

    static Dictionary<string, Material> Materiales(Dictionary<string, Color> colores)
    {
        Shader shader = Shader.Find(SHADER_URP);
        var tabla = new Dictionary<string, Material>();

        if (shader == null)
        {
            Debug.LogError("[Peluche] No encuentro el shader " + SHADER_URP);
            return tabla;
        }

        MaquinaGarraBuilder.AsegurarCarpeta(CARPETA_MAT);

        foreach (var par in colores)
        {
            string ruta = CARPETA_MAT + "/" + par.Key + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);

            bool nuevo = mat == null;
            if (nuevo) mat = new Material(shader);
            else mat.shader = shader;

            mat.SetColor("_BaseColor", par.Value);
            mat.SetFloat("_Metallic", 0f);

            // Felpa: la luz se dispersa en todas direcciones y no hay reflejo.
            // Con brillo, un peluche parece de plastico.
            mat.SetFloat("_Smoothness", 0.06f);

            if (nuevo) AssetDatabase.CreateAsset(mat, ruta);
            else EditorUtility.SetDirty(mat);

            tabla[par.Key] = mat;
        }

        return tabla;
    }

    static void Repartir(GameObject raiz, Dictionary<string, Material> tabla)
    {
        if (tabla.Count == 0) return;

        var sinReceta = new HashSet<string>();

        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool tocado = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;

                string clave = Mejor(mats[i].name, tabla);

                if (clave == null)
                {
                    sinReceta.Add(mats[i].name);
                    continue;
                }

                if (mats[i] != tabla[clave])
                {
                    mats[i] = tabla[clave];
                    tocado = true;
                }
            }

            if (tocado) r.sharedMaterials = mats;
        }

        // Que se entere si un material del modelo no tiene receta. Con esto
        // callado, una pieza se queda con el material que Unity le puso al
        // importar y en URP eso puede ser cualquier cosa.
        if (sinReceta.Count > 0)
        {
            Debug.LogWarning("[Peluche] Materiales sin receta, se quedan como los "
                             + "importo Unity: " + string.Join(", ", sinReceta));
        }
    }

    // La coincidencia MAS LARGA, no la primera.
    //
    // "Panxeta_Naranja" es prefijo de "Panxeta_NaranjaOsc", asi que buscando la
    // primera que encaje el ombligo salia naranja claro en vez de oscuro. Con
    // dos colores parecidos el fallo ni se ve; con uno claro y uno oscuro, si.
    static string Mejor(string nombre, Dictionary<string, Material> tabla)
    {
        string mejor = null;

        foreach (string clave in tabla.Keys)
        {
            if (!nombre.StartsWith(clave)) continue;
            if (mejor == null || clave.Length > mejor.Length) mejor = clave;
        }

        return mejor;
    }
}


// Rehace el prefab en cuanto se reimporta el modelo.
//
// Sin esto solo se comprobaba al recompilar, y si el modelo cambiaba sin tocar
// ningun .cs el prefab se quedaba con las posiciones viejas apuntando a las
// mallas nuevas: el peluche sale despiezado, con la cabeza flotando y las
// orejas por su cuenta. Desde fuera parece un fallo de fisica y se pierde el
// rato buscando donde no es. Ya paso con la maquina.
public class PelucheAutoBuild : AssetPostprocessor
{
    // Los modelos entran con Read/Write puesto, siempre.
    //
    // Sin el, Mesh.vertices devuelve un array vacio en ejecucion. No lanza
    // ninguna excepcion y no avisa de nada: el codigo que lee la malla
    // simplemente no encuentra nada y se calla o culpa a otra cosa. Las orejas
    // llevaban sin moverse desde que existen por esto, y el aviso que salia
    // hablaba del eje de la malla.
    //
    // Es una casilla del importador que hay que acordarse de marcar, y
    // acordarse no es un sistema.
    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith("Assets/_Project/Models/")) return;

        ModelImporter importador = assetImporter as ModelImporter;
        if (importador == null || importador.isReadable) return;

        importador.isReadable = true;
    }

    static void OnPostprocessAllAssets(string[] importados, string[] borrados,
                                       string[] movidos, string[] movidosDesde)
    {
        foreach (string ruta in importados)
        {
            if (!ruta.EndsWith(".fbx")) continue;
            if (!ruta.StartsWith("Assets/_Project/Models/")) continue;

            // En diferido: durante la importacion Unity no deja crear ni
            // guardar assets.
            EditorApplication.delayCall += Rehacer;
            return;
        }
    }

    static void Rehacer()
    {
        EditorApplication.delayCall -= Rehacer;
        PelucheBuilder.Comprobar();
    }
}
