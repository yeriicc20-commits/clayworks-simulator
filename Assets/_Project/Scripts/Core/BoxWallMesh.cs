using UnityEngine;
using System.Collections.Generic;

// Rehace la malla de una pared con UV medidas en metros de verdad.
//
// El cubo de Unity mapea la textura 0..1 a cada una de sus seis caras por igual,
// asi que en una pared de 14 x 5 x 0,5 el canto de medio metro muestra la misma
// cantidad de ladrillos que la cara larga: salen aplastados. Se nota justo donde
// una pared se corta y ensena el grueso, como en el hueco de una ampliacion.
//
// Aqui cada cara recibe sus UV a partir de su tamano y su posicion en el mundo,
// asi que los ladrillos miden lo mismo en todas las caras y las hiladas siguen
// de una pieza a la siguiente.
public class BoxWallMesh : MonoBehaviour
{
    [Tooltip("Cuantos metros ocupa una repeticion de la textura.")]
    public float metersPerTile = 2f;

    private Mesh generated;

    void Start()
    {
        Apply();
    }

    void OnDestroy()
    {
        if (generated != null) Destroy(generated);
    }

    public static void Attach(Transform piece, float metersPerTile)
    {
        if (piece == null) return;

        // El componente viejo mandaba el tiling por _BaseMap_ST, que es uno solo
        // para todo el objeto: no puede dar una escala distinta al canto.
        TileTextureByScale old = piece.GetComponent<TileTextureByScale>();
        if (old != null) old.enabled = false;

        BoxWallMesh box = piece.GetComponent<BoxWallMesh>();
        if (box == null) box = piece.gameObject.AddComponent<BoxWallMesh>();

        box.metersPerTile = metersPerTile;
        box.Apply();
    }

    public void Apply()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null) return;

        float tile = Mathf.Max(0.05f, metersPerTile);

        Vector3 size = transform.lossyScale;
        size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));

        // Esquina inferior del cubo en el mundo. El pivote del cubo esta en el
        // centro, y las paredes no van rotadas, asi que basta con restar la mitad.
        Vector3 origin = transform.position - size * 0.5f;

        List<Vector3> verts = new List<Vector3>(24);
        List<Vector3> normals = new List<Vector3>(24);
        List<Vector2> uvs = new List<Vector2>(24);
        List<int> tris = new List<int>(36);

        // Para las caras verticales lo de arriba es Y; para el suelo y el techo
        // se usa Z, que es lo que deja las hiladas en horizontal.
        AddFace(0, 1f, 2, 1, size, origin, tile, verts, normals, uvs, tris);
        AddFace(0, -1f, 2, 1, size, origin, tile, verts, normals, uvs, tris);
        AddFace(1, 1f, 0, 2, size, origin, tile, verts, normals, uvs, tris);
        AddFace(1, -1f, 0, 2, size, origin, tile, verts, normals, uvs, tris);
        AddFace(2, 1f, 0, 1, size, origin, tile, verts, normals, uvs, tris);
        AddFace(2, -1f, 0, 1, size, origin, tile, verts, normals, uvs, tris);

        if (generated != null) Destroy(generated);

        generated = new Mesh();
        generated.name = "ParedUV";
        generated.hideFlags = HideFlags.DontSave;

        generated.SetVertices(verts);
        generated.SetNormals(normals);
        generated.SetUVs(0, uvs);
        generated.SetTriangles(tris, 0);
        generated.RecalculateTangents();
        generated.RecalculateBounds();

        filter.mesh = generated;

        ClearTilingBlock();
    }

    void AddFace(int axis, float sign, int uAxis, int vAxis, Vector3 size, Vector3 origin, float tile,
        List<Vector3> verts, List<Vector3> normals, List<Vector2> uvs, List<int> tris)
    {
        Vector3 normal = Vector3.zero;
        normal[axis] = sign;

        int start = verts.Count;

        for (int i = 0; i < 4; i++)
        {
            float u = (i == 1 || i == 2) ? 0.5f : -0.5f;
            float v = i >= 2 ? 0.5f : -0.5f;

            Vector3 point = Vector3.zero;
            point[axis] = sign * 0.5f;
            point[uAxis] = u;
            point[vAxis] = v;

            verts.Add(point);
            normals.Add(normal);

            // De coordenada local (-0.5..0.5) a metros en el mundo. Atar las UV
            // al mundo y no al objeto es lo que hace que las hiladas de ladrillo
            // continuen de una pieza a la siguiente en vez de reiniciarse.
            float worldU = origin[uAxis] + (u + 0.5f) * size[uAxis];
            float worldV = origin[vAxis] + (v + 0.5f) * size[vAxis];

            uvs.Add(new Vector2(worldU / tile, worldV / tile));
        }

        // El orden de los vertices depende de si los ejes elegidos forman un
        // triple a derechas o no, y eso cambia cara a cara. En vez de llevar la
        // cuenta a mano se comprueba y se le da la vuelta si hace falta.
        Vector3 facing = Vector3.Cross(verts[start + 1] - verts[start], verts[start + 2] - verts[start]);

        if (Vector3.Dot(facing, normal) >= 0f)
        {
            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
        }
        else
        {
            tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
            tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
        }
    }

    // Si el objeto venia con el tiling metido por bloque de propiedades, hay que
    // dejarlo a 1: si no, se multiplicaria por las UV nuevas.
    void ClearTilingBlock()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();

        rend.GetPropertyBlock(block);
        block.SetVector("_BaseMap_ST", new Vector4(1f, 1f, 0f, 0f));
        rend.SetPropertyBlock(block);
    }
}
