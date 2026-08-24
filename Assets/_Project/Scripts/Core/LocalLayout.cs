using UnityEngine;
using System.Collections.Generic;

// Las ampliaciones del local, al estilo Supermarket Simulator: cada compra no
// estira las paredes que ya hay, sino que engancha UN recuadro nuevo al local,
// abierto de par en par por el lado que toca a la sala vieja.
//
// Los recuadros se van pegando primero por el fondo (+X, hacia donde miras al
// entrar por la puerta) de sur a norte, luego por la izquierda (+Z), y al final
// la esquina que cuadra el rectangulo. Cuando la vuelta se completa el local es
// mas grande y empieza otra vuelta por fuera.
public class LocalLayout : MonoBehaviour
{
    public static LocalLayout Instance;

    [Header("Tamano del recuadro")]
    [Tooltip("Lado aproximado de cada zona nueva, en metros.")]
    public float blockSize = 5f;

    [Header("Nombres en la jerarquia")]
    public string rootName = "Local_Estructura";
    public string eastName = "Pared_Este";
    public string northName = "Pared_Norte";
    public string southName = "Pared_Sur";
    public string westSouthName = "Pared_Oeste_Sur";
    public string floorName = "Suelo_Interior";
    public string containerName = "Local_Ampliaciones";

    [Tooltip("Cuanto se mete el suelo por debajo de las paredes.")]
    public float floorOverlap = 0.4f;

    [Tooltip("Metros que ocupa una repeticion de la textura de pared.")]
    public float wallMetersPerTile = 2f;

    private struct Zone
    {
        public float x0, x1, z0, z1;

        public Zone(float x0, float x1, float z0, float z1)
        {
            this.x0 = x0; this.x1 = x1; this.z0 = z0; this.z1 = z1;
        }

        public float Width { get { return x1 - x0; } }
        public float Depth { get { return z1 - z0; } }
    }

    private enum Side { East, West, North, South }

    private Transform root;
    private Transform east, north, south, westSouth, floor;
    private Transform container;

    private float wallThickness = 0.5f;
    private Zone baseZone;
    private bool ready = false;
    private bool warned = false;
    private bool originalsSkinned = false;
    private int appliedLevel = -1;

    private readonly List<Zone> zones = new List<Zone>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        int level = ExpansionManager.Instance != null ? ExpansionManager.Instance.currentLevel : 0;

        ApplyLevel(level);
    }

    public static LocalLayout EnsureExists()
    {
        if (Instance != null) return Instance;

        LocalLayout existing = FindAnyObjectByType<LocalLayout>();
        if (existing != null)
        {
            Instance = existing;
            return Instance;
        }

        return new GameObject("LocalLayout").AddComponent<LocalLayout>();
    }

    public bool HasGeometry
    {
        get { Resolve(); return ready; }
    }

    // ------------------------------------------------------------------
    // Lectura de la escena
    // ------------------------------------------------------------------

    void Resolve()
    {
        if (ready) return;

        GameObject found = GameObject.Find(rootName);
        if (found == null)
        {
            if (!warned) Debug.LogWarning("[LocalLayout] No encuentro " + rootName + ": el local no se ampliara.", this);
            warned = true;
            return;
        }

        root = found.transform;

        east = Find(eastName);
        north = Find(northName);
        south = Find(southName);
        westSouth = Find(westSouthName);
        floor = Find(floorName);

        if (east == null || north == null || south == null || westSouth == null || floor == null)
        {
            if (!warned) Debug.LogWarning("[LocalLayout] Faltan piezas del local: no se ampliara.", this);
            warned = true;
            return;
        }

        wallThickness = east.localScale.x;

        float half = wallThickness * 0.5f;

        // La zona util del local: de cara interior a cara interior.
        baseZone = new Zone(
            westSouth.localPosition.x + half,
            east.localPosition.x - half,
            south.localPosition.z + half,
            north.localPosition.z - half);

        ready = true;

        SkinOriginalWalls();
    }

    Transform Find(string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName)) return null;

        Transform direct = root.Find(childName);
        if (direct != null) return direct;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == childName) return t;
        }

        return null;
    }

    // ------------------------------------------------------------------
    // Que recuadro toca en cada ampliacion
    // ------------------------------------------------------------------

    // Recorre las vueltas hasta juntar upTo recuadros. Dentro de una franja los
    // trozos se reparten por igual, para que no quede una tira raquitica al final.
    List<Zone> Sequence(int upTo)
    {
        List<Zone> list = new List<Zone>();

        if (upTo <= 0 || !ready) return list;

        float bx0 = baseZone.x0, bx1 = baseZone.x1;
        float bz0 = baseZone.z0, bz1 = baseZone.z1;

        float block = Mathf.Max(1f, blockSize);
        int guard = 0;

        while (list.Count < upTo && guard++ < 64)
        {
            float backX0 = bx1;
            float backX1 = bx1 + block;

            int backSlices = Mathf.Max(1, Mathf.RoundToInt((bz1 - bz0) / block));
            float backStep = (bz1 - bz0) / backSlices;

            for (int i = 0; i < backSlices && list.Count < upTo; i++)
            {
                list.Add(new Zone(backX0, backX1, bz0 + backStep * i, bz0 + backStep * (i + 1)));
            }

            if (list.Count >= upTo) break;

            float leftZ0 = bz1;
            float leftZ1 = bz1 + block;

            int leftSlices = Mathf.Max(1, Mathf.RoundToInt((bx1 - bx0) / block));
            float leftStep = (bx1 - bx0) / leftSlices;

            for (int i = 0; i < leftSlices && list.Count < upTo; i++)
            {
                list.Add(new Zone(bx0 + leftStep * i, bx0 + leftStep * (i + 1), leftZ0, leftZ1));
            }

            if (list.Count >= upTo) break;

            // La esquina cierra la vuelta y deja el local rectangular otra vez.
            list.Add(new Zone(backX0, backX1, leftZ0, leftZ1));

            bx1 = backX1;
            bz1 = leftZ1;
        }

        return list;
    }

    // Texto para el ordenador: que se compra exactamente.
    public string DescribeLevel(int level)
    {
        Resolve();

        if (!ready) return "Se compran en orden, una detras de otra.";

        List<Zone> list = Sequence(level);

        if (list.Count < level || level < 1) return "Se compran en orden, una detras de otra.";

        Zone z = list[level - 1];

        string where = z.x0 >= baseZone.x1 - 0.01f ? "al fondo" : "a la izquierda";

        return "Anade una zona de " + z.Width.ToString("0.#") + " x " + z.Depth.ToString("0.#") +
               " m " + where + ", abierta al resto del local.";
    }

    // ------------------------------------------------------------------
    // Construccion
    // ------------------------------------------------------------------

    public void ApplyLevel(int level)
    {
        Resolve();

        if (!ready) return;
        if (level == appliedLevel) return;

        appliedLevel = level;

        // Se tira todo lo generado antes y se rehace entero: mucho mas simple
        // que parchear pieza a pieza, y solo pasa al comprar una ampliacion.
        if (container != null) Destroy(container.gameObject);

        // Sin ampliaciones el local se queda exactamente como esta en la escena:
        // no se toca ni una pared.
        if (level <= 0)
        {
            east.gameObject.SetActive(true);
            north.gameObject.SetActive(true);
            south.gameObject.SetActive(true);
            return;
        }

        zones.Clear();
        zones.Add(baseZone);
        zones.AddRange(Sequence(level));

        GameObject holder = new GameObject(containerName);
        container = holder.transform;
        container.SetParent(root, false);

        // El oeste no se toca nunca: ahi vive la puerta. Las otras tres se
        // rehacen a medida. El este y el norte porque son los que tienen que
        // abrirse, y el sur porque el original sobresale un poco por la esquina
        // y se solaparia con la pared de la sala nueva.
        east.gameObject.SetActive(false);
        north.gameObject.SetActive(false);
        south.gameObject.SetActive(false);

        for (int i = 0; i < zones.Count; i++)
        {
            if (i == 0)
            {
                BuildEdge(i, Side.East);
                BuildEdge(i, Side.North);
                BuildEdge(i, Side.South);
                continue;
            }

            BuildFloor(zones[i], i);

            BuildEdge(i, Side.East);
            BuildEdge(i, Side.West);
            BuildEdge(i, Side.North);
            BuildEdge(i, Side.South);
        }

        FitBaseFloor(level > 0);

        if (NavMeshBaker.Instance != null) NavMeshBaker.Instance.Rebuild();
    }

    // Levanta la pared de una arista saltandose los tramos que dan a otra zona
    // comprada: ahi es justo por donde se pasa de una sala a la otra.
    void BuildEdge(int index, Side side)
    {
        Zone r = zones[index];

        bool alongZ = side == Side.East || side == Side.West;

        float from, to, line;

        if (alongZ)
        {
            from = r.z0;
            to = r.z1;
            line = side == Side.East ? r.x1 : r.x0;
        }
        else
        {
            from = r.x0;
            to = r.x1;
            line = side == Side.North ? r.z1 : r.z0;
        }

        List<Vector2> openings = new List<Vector2>();

        for (int i = 0; i < zones.Count; i++)
        {
            if (i == index) continue;

            Zone s = zones[i];

            float neighbourLine = alongZ
                ? (side == Side.East ? s.x0 : s.x1)
                : (side == Side.North ? s.z0 : s.z1);

            if (Mathf.Abs(neighbourLine - line) > 0.02f) continue;

            float a = alongZ ? s.z0 : s.x0;
            float b = alongZ ? s.z1 : s.x1;

            float lo = Mathf.Max(from, a);
            float hi = Mathf.Min(to, b);

            if (hi - lo > 0.05f) openings.Add(new Vector2(lo, hi));
        }

        openings.Sort((p, q) => p.x.CompareTo(q.x));

        float cursor = from;

        foreach (Vector2 gap in openings)
        {
            if (gap.x - cursor > 0.05f) EmitWall(side, line, cursor, gap.x, from, to, alongZ);

            cursor = Mathf.Max(cursor, gap.y);
        }

        if (to - cursor > 0.05f) EmitWall(side, line, cursor, to, from, to, alongZ);
    }

    void EmitWall(Side side, float line, float from, float to, float edgeFrom, float edgeTo, bool alongZ)
    {
        float half = wallThickness * 0.5f;
        float outward = (side == Side.East || side == Side.North) ? half : -half;

        bool startIsCorner = Mathf.Abs(from - edgeFrom) < 0.02f;
        bool endIsCorner = Mathf.Abs(to - edgeTo) < 0.02f;

        // Esquina de la zona: la rellenan las paredes que corren en Z, que se
        // alargan un grosor hasta la cara exterior de la vecina. Las que corren
        // en X acaban a ras del rincon y se apoyan contra ellas. Si se alargaran
        // las dos, en los lados abiertos quedaria una punta al aire.
        //
        // Extremo que nace de un hueco: se retranquea un grosor, que es justo lo
        // que ocupa la pared de la sala de al lado. Sin esto las dos se meten en
        // el mismo sitio y la costura queda a trozos.
        float a = startIsCorner ? (alongZ ? from - wallThickness : from) : from + wallThickness;
        float b = endIsCorner ? (alongZ ? to + wallThickness : to) : to - wallThickness;

        float length = b - a;

        if (length < 0.05f) return;

        float mid = (a + b) * 0.5f;

        // Se clona la pared original de ese lado, no siempre la misma: cada una
        // puede llevar su material y sus UV.
        Transform template = alongZ ? east : (side == Side.North ? north : south);

        GameObject go = Instantiate(template.gameObject, container);
        go.name = "Pared_Zona";
        go.SetActive(true);

        Transform tr = go.transform;

        if (alongZ)
        {
            tr.localPosition = new Vector3(line + outward, template.localPosition.y, mid);
            tr.localScale = new Vector3(wallThickness, template.localScale.y, length);
        }
        else
        {
            tr.localPosition = new Vector3(mid, template.localPosition.y, line + outward);
            tr.localScale = new Vector3(length, template.localScale.y, wallThickness);
        }

        tr.localRotation = template.localRotation;

        SkinWall(tr);
    }

    // El suelo es un Plane: 10x10 unidades a escala 1. Cada zona baja un pelo
    // respecto a la anterior para que los solapes no parpadeen.
    void BuildFloor(Zone z, int index)
    {
        GameObject go = Instantiate(floor.gameObject, container);
        go.name = "Suelo_Zona";
        go.SetActive(true);

        Transform tr = go.transform;

        tr.localPosition = new Vector3(
            (z.x0 + z.x1) * 0.5f,
            floor.localPosition.y - 0.0015f * index,
            (z.z0 + z.z1) * 0.5f);

        tr.localScale = new Vector3(
            (z.Width + floorOverlap) / 10f,
            floor.localScale.y,
            (z.Depth + floorOverlap) / 10f);

        tr.localRotation = floor.localRotation;

        Retile(tr);
    }

    // Con ampliaciones el suelo original se ajusta justo a su zona: si sobresale
    // se solapa con el suelo nuevo y aparece la franja parpadeante.
    void FitBaseFloor(bool expanded)
    {
        if (!expanded) return;

        floor.localPosition = new Vector3(
            (baseZone.x0 + baseZone.x1) * 0.5f,
            floor.localPosition.y,
            (baseZone.z0 + baseZone.z1) * 0.5f);

        floor.localScale = new Vector3(
            (baseZone.Width + floorOverlap) / 10f,
            floor.localScale.y,
            (baseZone.Depth + floorOverlap) / 10f);

        Retile(floor);
    }

    // El suelo es un Plane y le vale el tiling por _BaseMap_ST de siempre.
    void Retile(Transform piece)
    {
        if (piece == null) return;

        TileTextureByScale tile = piece.GetComponent<TileTextureByScale>();
        if (tile != null) tile.Apply();
    }

    // Las paredes son cubos y necesitan UV por cara: si no, el canto que queda
    // a la vista en el hueco de una ampliacion muestra la textura aplastada.
    void SkinWall(Transform piece)
    {
        if (piece == null) return;

        BoxWallMesh.Attach(piece, wallMetersPerTile);
    }

    // Tambien las de la escena, aunque no se hayan tocado: asi las hiladas de
    // las paredes nuevas continuan las de las viejas y no se nota el corte.
    void SkinOriginalWalls()
    {
        if (originalsSkinned || !ready) return;

        originalsSkinned = true;

        foreach (string name in new string[]
        {
            eastName, northName, southName,
            westSouthName, "Pared_Oeste_Norte", "Pared_Oeste_UP"
        })
        {
            SkinWall(Find(name));
        }
    }
}
