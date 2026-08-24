using UnityEngine;

// Da a cada cliente su propia pinta. El modelo es el mismo para todos, asi que
// la variedad sale de tintar la ropa y de cambiarles un poco la estatura.
//
// El tinte va por bloque de propiedades y no tocando el material: si se asignara
// .material, Unity crearia una copia por NPC y con veinte clientes en el local
// serian veinte materiales y veinte lotes de dibujado de mas.
public class NPCAppearance : MonoBehaviour
{
    [Header("Que pieza es cada cosa")]
    [Tooltip("Nombres de malla que cuentan como camiseta.")]
    public string[] shirtNames = { "T-shirts", "T-shirt", "T_shirt" };
    [Tooltip("Nombres de malla que cuentan como pantalon.")]
    public string[] pantsNames = { "Pans", "Pants", "Pantalon" };
    [Tooltip("Nombres de malla que cuentan como piel.")]
    public string[] skinNames = { "Head", "Body" };

    [Header("Paletas")]
    public Color[] shirtColors =
    {
        new Color(0.86f, 0.25f, 0.24f),
        new Color(0.20f, 0.45f, 0.78f),
        new Color(0.95f, 0.76f, 0.20f),
        new Color(0.30f, 0.65f, 0.36f),
        new Color(0.55f, 0.32f, 0.70f),
        new Color(0.95f, 0.95f, 0.95f),
        new Color(0.22f, 0.24f, 0.28f),
        new Color(0.92f, 0.52f, 0.20f),
        new Color(0.28f, 0.72f, 0.72f),
        new Color(0.90f, 0.60f, 0.72f)
    };

    public Color[] pantsColors =
    {
        new Color(0.28f, 0.34f, 0.48f),
        new Color(0.18f, 0.20f, 0.24f),
        new Color(0.42f, 0.38f, 0.32f),
        new Color(0.55f, 0.55f, 0.58f),
        new Color(0.24f, 0.28f, 0.22f),
        new Color(0.36f, 0.24f, 0.20f)
    };

    [Tooltip("Tonos de piel. Se multiplican sobre la textura, asi que van claros.")]
    public Color[] skinTones =
    {
        new Color(1f, 0.96f, 0.92f),
        new Color(0.96f, 0.86f, 0.76f),
        new Color(0.82f, 0.66f, 0.52f),
        new Color(0.62f, 0.46f, 0.34f),
        new Color(0.45f, 0.32f, 0.24f)
    };

    [Header("Estatura")]
    [Tooltip("Cuanto puede variar la altura respecto al modelo original.")]
    [Range(0f, 0.2f)] public float heightVariation = 0.07f;

    [Tooltip("Nombre de la propiedad de color del shader. En URP es _BaseColor.")]
    public string colorProperty = "_BaseColor";

    void Start()
    {
        Paint();
        Resize();
    }

    void Paint()
    {
        Color shirt = Pick(shirtColors, Color.white);
        Color pants = Pick(pantsColors, Color.white);
        Color skin = Pick(skinTones, Color.white);

        foreach (Renderer rend in GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null) continue;

            if (Matches(rend.name, shirtNames)) Tint(rend, shirt);
            else if (Matches(rend.name, pantsNames)) Tint(rend, pants);
            else if (Matches(rend.name, skinNames)) Tint(rend, skin);
        }
    }

    // Solo se toca la parte visual, no la raiz: el CharacterController y el
    // NavMeshAgent cuelgan de ahi y escalarlos les cambia el radio de paso.
    void Resize()
    {
        if (heightVariation <= 0f) return;

        Renderer sample = GetComponentInChildren<Renderer>(true);
        if (sample == null) return;

        Transform model = sample.transform;

        while (model.parent != null && model.parent != transform) model = model.parent;

        if (model == transform) return;

        float factor = 1f + Random.Range(-heightVariation, heightVariation);

        model.localScale = model.localScale * factor;
    }

    Color Pick(Color[] palette, Color fallback)
    {
        if (palette == null || palette.Length == 0) return fallback;

        return palette[Random.Range(0, palette.Length)];
    }

    bool Matches(string name, string[] candidates)
    {
        if (candidates == null || string.IsNullOrEmpty(name)) return false;

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrEmpty(candidate)) continue;

            if (name.IndexOf(candidate, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    void Tint(Renderer rend, Color color)
    {
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        rend.GetPropertyBlock(block);
        block.SetColor(colorProperty, color);
        rend.SetPropertyBlock(block);
    }
}
