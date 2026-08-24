using UnityEngine;

// Ajusta el tiling de la textura al tamaño real del objeto, para que un trozo
// de pared pequeño (el dintel de la puerta) muestre la textura al mismo tamaño
// que la pared entera. Sin esto, un cubo con UV 0..1 estira la textura segun su
// escala y cada pieza se ve distinta.
[ExecuteAlways]
public class TileTextureByScale : MonoBehaviour
{
    [Tooltip("Cuantos metros ocupa una repeticion de la textura.")]
    public float metersPerTile = 2f;

    [Tooltip("Nombre de la textura en el shader. En URP es _BaseMap.")]
    public string textureProperty = "_BaseMap";

    void Start()
    {
        Apply();
    }

    void OnValidate()
    {
        if (metersPerTile < 0.05f) metersPerTile = 0.05f;

        Apply();
    }

    public void Apply()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) return;

        Vector3 size = rend.bounds.size;
        Vector3 min = rend.bounds.min;

        float u, v, originU, originV;

        // Si lo fino es el eje vertical estamos ante un suelo o techo; si no,
        // es una pared y lo vertical es la altura.
        if (size.y <= size.x && size.y <= size.z)
        {
            u = size.x;
            v = size.z;
            originU = min.x;
            originV = min.z;
        }
        else
        {
            // El eje fino de una pared es su grosor: el largo es el otro.
            bool alongX = size.x >= size.z;

            u = alongX ? size.x : size.z;
            v = size.y;
            originU = alongX ? min.x : min.z;
            originV = min.y;
        }

        // El desfase se ata a la posicion en el mundo, no al objeto. Asi las
        // hiladas de ladrillo continuan de una pieza a la siguiente en vez de
        // reiniciarse en cada trozo.
        Vector4 st = new Vector4(
            Mathf.Max(0.01f, u / metersPerTile),
            Mathf.Max(0.01f, v / metersPerTile),
            originU / metersPerTile,
            originV / metersPerTile);

        // Con un bloque de propiedades no se crea una copia del material por
        // objeto: todos comparten el mismo y solo cambia el tiling.
        MaterialPropertyBlock block = new MaterialPropertyBlock();

        rend.GetPropertyBlock(block);
        block.SetVector(textureProperty + "_ST", st);
        rend.SetPropertyBlock(block);
    }
}
