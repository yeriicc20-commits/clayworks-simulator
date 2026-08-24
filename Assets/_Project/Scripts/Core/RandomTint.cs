using UnityEngine;

// Le da a cada peluche o pelota un color distinto al aparecer, para que una
// maquina llena no parezca un monton de copias del mismo juguete.
//
// Las piezas se reparten por nombre y no adivinando cual es cual por su tamano:
// una malla puede ser varios parches sueltos repartidos por todo el muneco, y
// entonces su caja envolvente abarca el cuerpo entero aunque se vea diminuta.
// Medir eso llevaba a tenir la pieza equivocada.
//
// El tinte va por bloque de propiedades y no tocando .material: si se asignara
// el material, Unity crearia una copia por juguete y con una maquina cargada
// serian veinte materiales y veinte lotes de dibujado de mas.
public class RandomTint : MonoBehaviour
{
    [Header("Piezas de color fijo")]
    [Tooltip("Mallas que van siempre en blanco: ojos, hocico, parches.")]
    public string[] whiteParts;

    [Tooltip("Mallas que van siempre en negro: las pupilas.")]
    public string[] blackParts;

    public Color whiteColor = new Color(0.97f, 0.97f, 0.95f);
    public Color blackColor = new Color(0.06f, 0.06f, 0.07f);

    [Header("El resto")]
    [Tooltip("De donde sale el color del cuerpo. Vacio = se deja como venga.")]
    public Color[] palette;

    [Tooltip("Variacion de brillo dentro del color elegido.")]
    [Range(0f, 0.4f)] public float shadeVariation = 0.12f;

    [Tooltip("Nombre de la propiedad de color del shader. En URP es _BaseColor.")]
    public string colorProperty = "_BaseColor";

    void Start()
    {
        Apply();
    }

    public void Apply()
    {
        Color body = BodyColor();

        foreach (Renderer rend in GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null || rend is ParticleSystemRenderer) continue;

            if (Matches(rend.name, whiteParts)) Tint(rend, whiteColor);
            else if (Matches(rend.name, blackParts)) Tint(rend, blackColor);
            else if (palette != null && palette.Length > 0) Tint(rend, body);
        }
    }

    Color BodyColor()
    {
        if (palette == null || palette.Length == 0) return Color.white;

        Color color = palette[Random.Range(0, palette.Length)];

        if (shadeVariation <= 0f) return color;

        // Dos juguetes del mismo color de la paleta tampoco salen identicos.
        float shade = 1f + Random.Range(-shadeVariation, shadeVariation);

        return new Color(
            Mathf.Clamp01(color.r * shade),
            Mathf.Clamp01(color.g * shade),
            Mathf.Clamp01(color.b * shade),
            color.a);
    }

    bool Matches(string name, string[] candidates)
    {
        if (candidates == null || string.IsNullOrEmpty(name)) return false;

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate)) continue;

            if (name.Equals(candidate, System.StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    void Tint(Renderer rend, Color color)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        rend.GetPropertyBlock(block);
        block.SetColor(colorProperty, color);

        // Los materiales que genera el importador dentro de un FBX pueden salir
        // con el shader antiguo, que llama _Color a lo que URP llama _BaseColor.
        // Poner las dos no cuesta nada: la que no exista se ignora.
        if (colorProperty != "_Color") block.SetColor("_Color", color);

        rend.SetPropertyBlock(block);
    }
}
