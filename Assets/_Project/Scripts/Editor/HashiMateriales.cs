using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Materiales de la maquina de puente, como assets de URP.
//
// Van en su propia carpeta y no se reaprovechan los de la maquina de garra a
// proposito: comparten nombre (Blanco, Cromo, Cristal) pero no son la misma
// cosa. El dia que alguien afine el blanco de esta maquina no tiene por que
// cambiarle el blanco a la otra sin enterarse.
public static class HashiMateriales
{
    public const string CARPETA = "Assets/_Project/Materials/Hashi";
    const string SHADER_URP = "Universal Render Pipeline/Lit";

    struct Receta
    {
        public Color color;
        public float metal;
        public float suavidad;     // 1 - rugosidad
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

    static readonly Dictionary<string, Receta> RECETAS = new Dictionary<string, Receta>
    {
        // Carcasa: blanco brillante de plastico de maquina japonesa.
        { "Blanco",     new Receta(0.95f, 0.96f, 0.97f, 0.00f, 0.35f) },
        { "BlancoMate", new Receta(0.90f, 0.91f, 0.93f, 0.00f, 0.75f) },
        { "Rosa",       new Receta(0.96f, 0.42f, 0.68f, 0.00f, 0.35f) },
        { "Azul",       new Receta(0.25f, 0.55f, 0.95f, 0.00f, 0.35f) },

        // Para la figura de dentro de la caja de premio.
        { "Rojo",       new Receta(0.78f, 0.11f, 0.13f, 0.00f, 0.40f) },
        { "AzulOsc",    new Receta(0.10f, 0.16f, 0.45f, 0.00f, 0.40f) },
        { "Piel",       new Receta(0.95f, 0.95f, 0.95f, 0.00f, 0.55f) },

        // Las barras. Muy metalicas y muy pulidas: son la pieza que mas se
        // mira de toda la maquina, porque es donde esta el juego.
        { "Cromo",      new Receta(0.93f, 0.94f, 0.97f, 0.90f, 0.12f) },
        { "MetalOsc",   new Receta(0.20f, 0.21f, 0.24f, 0.60f, 0.40f) },
        { "Negro",      new Receta(0.07f, 0.07f, 0.09f, 0.00f, 0.45f) },
        { "Goma",       new Receta(0.10f, 0.10f, 0.11f, 0.00f, 0.85f) },

        // El cuerpo de la garra. Aluminio de verdad, no cromo espejo: bajo el
        // foco del techo, el cromo se va a blanco puro y la garra sale como una
        // mancha sin forma. Un gris medio con algo de rugosidad conserva el
        // volumen del ovalo.
        { "Aluminio",   new Receta(0.62f, 0.64f, 0.68f, 0.85f, 0.35f) },

        // Cristal. El alfa bajo es lo que deja ver dentro; lo demas lo hace
        // Transparente() ahi abajo, que es donde esta la miga.
        { "Cristal",    new Receta(0.80f, 0.88f, 0.92f, 0.00f, 0.03f, 0f, 0.10f) },

        // La cupula de la garra: cristal ahumado. En la maquina real es una
        // burbuja oscura, no un cristal limpio, y es lo que hace que se le vean
        // las tripas sin deslumbrar.
        { "CristalOsc", new Receta(0.10f, 0.11f, 0.14f, 0.00f, 0.05f, 0f, 0.55f) },

        // Luces. La emision alta es lo que engancha el bloom.
        { "LEDRosa",    new Receta(1.00f, 0.35f, 0.70f, 0.00f, 0.25f, 3.0f) },
        { "LEDAzul",    new Receta(0.30f, 0.60f, 1.00f, 0.00f, 0.25f, 3.0f) },
        { "Marquesina", new Receta(1.00f, 0.85f, 0.95f, 0.00f, 0.30f, 1.2f) },

        // El premio. El color de verdad lo pone cada caja al aparecer; este es
        // solo el punto de partida.
        { "Carton",     new Receta(0.25f, 0.45f, 0.85f, 0.00f, 0.55f) },

        { "Suelo",      new Receta(0.30f, 0.31f, 0.35f, 0.00f, 0.70f) },
        { "Pared",      new Receta(0.16f, 0.17f, 0.21f, 0.00f, 0.85f) },
    };

    public static Dictionary<string, Material> CrearOActualizar()
    {
        var tabla = new Dictionary<string, Material>();

        Shader shader = Shader.Find(SHADER_URP);

        if (shader == null)
        {
            Debug.LogError("[Hashi] No encuentro el shader '" + SHADER_URP
                           + "'. El proyecto no parece estar en URP y la "
                           + "maquina saldria rosa entera.");
            return tabla;
        }

        AsegurarCarpeta(CARPETA);

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

    // En URP no basta con bajar el alfa: hay que cambiar el modo de superficie,
    // las mezclas, la cola de dibujado y las palabras clave. Dejandolo a medias,
    // el cristal sale opaco y no avisa de nada.
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

        // Sin esto el cristal proyecta una sombra cuadrada sobre el premio y
        // dentro de la maquina se ve como una mancha que no se explica.
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

    public static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;

        string padre = System.IO.Path.GetDirectoryName(ruta).Replace('\\', '/');
        string hoja = System.IO.Path.GetFileName(ruta);

        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, hoja);
    }
}
