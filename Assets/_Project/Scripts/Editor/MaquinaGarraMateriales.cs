using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Materiales de la maquina de garra, creados como assets de URP.
//
// Los que trae el FBX no valen: Unity los importa con el shader que le parece y
// en URP acaban saliendo rosas o planos. Aqui se crean a mano con el shader
// correcto, tomando de Blender el color y la rugosidad, y anadiendo lo que un
// Principled BSDF no sabe traducir: cuanto metal tiene cada cosa.
//
// Los nombres coinciden con los del .blend a proposito. Asi el reparto es por
// nombre y no hay que mantener una lista de que pieza lleva que.
public static class MaquinaGarraMateriales
{
    const string CARPETA = "Assets/_Project/Materials/Maquina";
    const string SHADER_URP = "Universal Render Pipeline/Lit";

    struct Receta
    {
        public Color color;
        public float metal;
        public float suavidad;     // 1 - rugosidad de Blender
        public float emision;      // 0 = no emite
        public float alfa;

        public Receta(float r, float g, float b, float metal, float rugosidad,
                      float emision = 0f, float alfa = 1f)
        {
            color = new Color(r, g, b, alfa);
            this.metal = metal;
            suavidad = 1f - rugosidad;
            this.emision = emision;
            this.alfa = alfa;
        }
    }

    // Los metales llevan aqui mas metalico que en Blender. Alli daba igual
    // porque el render tenia un entorno que reflejar; en el juego, una pieza
    // muy metalica dentro de una tienda oscura sale casi negra.
    static readonly Dictionary<string, Receta> RECETAS = new Dictionary<string, Receta>
    {
        { "Turquesa",     new Receta(0.13f, 0.72f, 0.80f, 0.00f, 0.45f) },
        { "Verde",        new Receta(0.42f, 0.85f, 0.45f, 0.00f, 0.45f) },
        { "VerdeOsc",     new Receta(0.10f, 0.45f, 0.38f, 0.00f, 0.45f) },
        { "Morado",       new Receta(0.55f, 0.22f, 0.72f, 0.00f, 0.45f) },
        { "Rosa",         new Receta(0.92f, 0.28f, 0.55f, 0.00f, 0.45f) },
        { "Amarillo",     new Receta(0.99f, 0.82f, 0.18f, 0.00f, 0.40f) },
        { "Blanco",       new Receta(0.95f, 0.96f, 0.96f, 0.00f, 0.45f) },
        { "Negro",        new Receta(0.06f, 0.07f, 0.09f, 0.00f, 0.40f) },
        { "Goma",         new Receta(0.10f, 0.10f, 0.11f, 0.00f, 0.80f) },
        { "Azul",         new Receta(0.10f, 0.35f, 0.85f, 0.00f, 0.45f) },

        { "Metal",        new Receta(0.72f, 0.74f, 0.78f, 0.60f, 0.30f) },
        { "MetalOsc",     new Receta(0.22f, 0.23f, 0.26f, 0.50f, 0.45f) },
        { "Dorado",       new Receta(0.85f, 0.66f, 0.16f, 0.80f, 0.30f) },
        { "Cromo",        new Receta(0.90f, 0.91f, 0.94f, 0.50f, 0.22f) },

        { "Marquesina",   new Receta(0.42f, 0.85f, 0.45f, 0.00f, 0.35f, 0.6f) },
        { "LED",          new Receta(0.85f, 0.45f, 0.95f, 0.00f, 0.30f, 3.0f) },
        { "LEDAzul",      new Receta(0.06f, 0.35f, 1.00f, 0.00f, 0.25f, 1.4f) },
        { "BotonAzul",    new Receta(0.06f, 0.35f, 1.00f, 0.00f, 0.20f, 1.2f) },
        { "BolaJoystick", new Receta(1.00f, 0.07f, 0.10f, 0.00f, 0.08f, 1.2f, 0.88f) },
        { "Cristal",      new Receta(0.75f, 0.85f, 0.90f, 0.00f, 0.05f, 0.0f, 0.14f) },
    };

    public static Dictionary<string, Material> CrearOActualizar()
    {
        Shader shader = Shader.Find(SHADER_URP);

        if (shader == null)
        {
            Debug.LogError("[Maquina] No encuentro el shader '" + SHADER_URP
                           + "'. El proyecto no parece estar en URP.");
            return new Dictionary<string, Material>();
        }

        MaquinaGarraBuilder.AsegurarCarpeta(CARPETA);

        var tabla = new Dictionary<string, Material>();

        foreach (var par in RECETAS)
        {
            string ruta = CARPETA + "/" + par.Key + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(ruta);

            bool nuevo = mat == null;
            if (nuevo) mat = new Material(shader);
            else mat.shader = shader;

            Aplicar(mat, par.Value);

            if (nuevo) AssetDatabase.CreateAsset(mat, ruta);
            else EditorUtility.SetDirty(mat);

            tabla[par.Key] = mat;
        }

        AssetDatabase.SaveAssets();
        return tabla;
    }

    static void Aplicar(Material mat, Receta r)
    {
        mat.SetColor("_BaseColor", r.color);
        mat.SetFloat("_Metallic", r.metal);
        mat.SetFloat("_Smoothness", r.suavidad);

        if (r.emision > 0f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.SetColor("_EmissionColor",
                         new Color(r.color.r, r.color.g, r.color.b) * r.emision);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        if (r.alfa < 1f) Transparente(mat);
        else Opaco(mat);
    }

    // URP no basta con bajarle el alfa al color: hay que cambiarle el modo de
    // superficie, las mezclas, la cola de dibujado y las palabras clave. Si se
    // deja a medias, el cristal sale opaco y sin avisar de nada.
    static void Transparente(Material mat)
    {
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        mat.SetShaderPassEnabled("ShadowCaster", false);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    static void Opaco(Material mat)
    {
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetFloat("_ZWrite", 1f);

        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetShaderPassEnabled("ShadowCaster", true);
        mat.renderQueue = -1;
    }

    // Reparte los materiales por el nombre que traen del FBX. Unity a veces les
    // pega sufijos al importar, asi que se compara por el principio.
    public static int Repartir(IEnumerable<Transform> piezas, Dictionary<string, Material> tabla)
    {
        if (tabla.Count == 0) return 0;

        int cambiados = 0;
        var sinReceta = new HashSet<string>();

        foreach (Transform t in piezas)
        {
            Renderer r = t.GetComponent<Renderer>();
            if (r == null) continue;

            Material[] actuales = r.sharedMaterials;
            bool tocado = false;

            for (int i = 0; i < actuales.Length; i++)
            {
                if (actuales[i] == null) continue;

                string nombre = Buscar(actuales[i].name, tabla);

                if (nombre == null)
                {
                    sinReceta.Add(actuales[i].name);
                    continue;
                }

                if (actuales[i] != tabla[nombre])
                {
                    actuales[i] = tabla[nombre];
                    tocado = true;
                }
            }

            if (tocado)
            {
                r.sharedMaterials = actuales;
                cambiados++;
            }
        }

        if (sinReceta.Count > 0)
        {
            Debug.LogWarning("[Maquina] Materiales del FBX sin receta propia, se "
                             + "quedan como los importo Unity: "
                             + string.Join(", ", sinReceta));
        }

        return cambiados;
    }

    static string Buscar(string nombreImportado, Dictionary<string, Material> tabla)
    {
        if (tabla.ContainsKey(nombreImportado)) return nombreImportado;

        string mejor = null;

        foreach (string clave in tabla.Keys)
        {
            if (!nombreImportado.StartsWith(clave)) continue;
            if (mejor == null || clave.Length > mejor.Length) mejor = clave;
        }

        return mejor;
    }
}
