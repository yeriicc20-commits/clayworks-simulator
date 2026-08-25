using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

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
    }

    static readonly Peluche[] TODOS =
    {
        new Peluche
        {
            nombre = "Panxeta_Toy",
            fbx = "Assets/_Project/Models/Panxeta.fbx",
            peso = PlushItem.WeightCategory.Medio,
            partesBlandas = new[] { "Oreja_Izq", "Oreja_Der" },
            colores = new Dictionary<string, Color>
            {
                // Los mismos valores que en Blender. Se repiten aqui a
                // proposito: el FBX trae materiales, pero Unity los importa con
                // el shader que le parece y en URP salen rosas o planos.
                { "Panxeta_Blanco", new Color(0.94f, 0.93f, 0.89f) },
                { "Panxeta_Marron", new Color(0.70f, 0.28f, 0.09f) },
                { "Panxeta_Negro",  new Color(0.07f, 0.07f, 0.08f) },
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

    static void Comprobar()
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

        foreach (Renderer r in raiz.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool tocado = false;

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;

                foreach (var par in tabla)
                {
                    if (!mats[i].name.StartsWith(par.Key)) continue;

                    mats[i] = par.Value;
                    tocado = true;
                    break;
                }
            }

            if (tocado) r.sharedMaterials = mats;
        }
    }
}
