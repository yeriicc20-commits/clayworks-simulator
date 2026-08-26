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
    public string ceilingName = "Local_Techo";

    [Header("Techo")]

    [Tooltip("Si no, el local se queda abierto por arriba como antes.")]
    public bool conTecho = true;

    [Tooltip("A que altura va. En 0 se saca de lo alto que sea la pared.")]
    public float ceilingHeight = 0f;

    [Tooltip("Con esto el local se queda a oscuras por dentro, que es de lo\nque va tener techo. Quitalo si prefieres verlo todo.")]
    public bool techoDaSombra = true;

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
    private Transform ceiling;
    private bool contado = false;

    private float wallThickness = 0.5f;
    private Zone baseZone;
    private bool ready = false;
    private bool warned = false;
    private bool originalsSkinned = false;
    private int appliedLevel = -1;

    private readonly List<Zone> zones = new List<Zone>();

    // Que exista desde el arranque, sin depender de que abras nada.
    //
    // Ni esto ni ExpansionManager estan puestos en la escena: se creaban solos
    // la primera vez que abrias la pestana de ampliaciones en el ordenador.
    // Mientras solo servian para ampliar daba igual, porque hasta ese momento
    // no habia nada que hacer. Pero el techo tiene que estar desde el primer
    // segundo, y por eso al empezar una partida no habia ninguno.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Arrancar()
    {
        EnsureExists();
    }

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

            // Sin ampliaciones tambien hay techo: es solo el del local de
            // partida.
            zones.Clear();
            zones.Add(baseZone);

            BuildCeiling();
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

        // Despues de FitBaseFloor a proposito: el techo de la sala de partida
        // se copia del suelo, y ahi el suelo ya esta ajustado a su zona.
        BuildCeiling();

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

    // El techo: una losa por sala, con la textura del suelo.
    //
    // Se levanta por el mismo camino por el que se pone el suelo, y por eso
    // una ampliacion trae el suyo sin que haya un segundo sitio que se pueda
    // olvidar de actualizar.
    void BuildCeiling()
    {
        // Se rehace entero. Buscando tambien por nombre, que despues de
        // recompilar la referencia se pierde pero el objeto sigue en la
        // escena, y si no saldrian dos techos superpuestos.
        if (ceiling != null) Destroy(ceiling.gameObject);

        Transform viejo = Find(ceilingName);
        if (viejo != null) Destroy(viejo.gameObject);

        ceiling = null;

        if (!conTecho || floor == null || zones.Count == 0) return;

        GameObject holder = new GameObject(ceilingName);
        ceiling = holder.transform;
        ceiling.SetParent(root, false);

        float y = CeilingY();

        for (int i = 0; i < zones.Count; i++) CeilingPiece(zones[i], i, y);

        // Una linea y una sola vez. Sin ella, "no hay techo" y "el techo esta
        // pero no se ve" son el mismo sintoma, y ya me costo una vuelta.
        if (contado) return;
        contado = true;

        Debug.Log("[Local] Techo puesto: " + zones.Count + " tramo(s) a "
                  + y.ToString("0.00") + " m, sombra "
                  + (techoDaSombra ? "SI" : "NO") + ".", this);
    }

    // Lo alto que sea la pared: su centro mas media altura es justo el borde
    // de arriba, que es donde tiene que apoyar el techo.
    float CeilingY()
    {
        if (ceilingHeight > 0f) return ceilingHeight;

        Transform pared = east != null ? east : north;

        if (pared == null) return floor.localPosition.y + 3f;

        return pared.localPosition.y + pared.localScale.y * 0.5f;
    }

    // Una losa con grosor, no una lamina.
    //
    // Se clona una PARED y no el suelo, aunque lleve la textura del suelo. El
    // suelo es un Plane: una lamina sin canto, que vista desde el borde no
    // existe y desde arriba se ve por la cara de atras. Una pared es un cubo
    // con su grosor, su collider de caja y su canto, que es lo que se espera de
    // un techo cuando se mira desde el hueco de una ampliacion.
    //
    // Del suelo se toma solo lo que hace falta: el material y cada cuantos
    // metros repite la textura.
    void CeilingPiece(Zone z, int index, float y)
    {
        Transform plantilla = east != null ? east : north;
        if (plantilla == null) return;

        GameObject go = Instantiate(plantilla.gameObject, ceiling);
        go.name = "Techo_Zona";
        go.SetActive(true);

        Transform tr = go.transform;

        float ancho, fondo, cx, cz;

        if (index == 0)
        {
            // La sala de partida se saca del suelo, que es la unica forma de que
            // cuadren al milimetro. Un Plane mide 10 x 10 con escala 1, de ahi
            // el por diez.
            ancho = floor.localScale.x * 10f;
            fondo = floor.localScale.z * 10f;
            cx = floor.localPosition.x;
            cz = floor.localPosition.z;
        }
        else
        {
            ancho = z.Width + floorOverlap;
            fondo = z.Depth + floorOverlap;
            cx = (z.x0 + z.x1) * 0.5f;
            cz = (z.z0 + z.z1) * 0.5f;
        }

        // Apoyada ENCIMA de la pared: su cara de abajo queda justo al borde de
        // arriba del muro, asi que por dentro el techo esta a la altura de la
        // pared y el grosor sobresale por fuera, como en un edificio.
        tr.localPosition = new Vector3(cx, y + wallThickness * 0.5f, cz);
        tr.localScale = new Vector3(ancho, wallThickness, fondo);
        tr.localRotation = Quaternion.identity;

        PintarComoElSuelo(go);
        Sombra(go);

        // UV por cara y medidas en metros, igual que las paredes: sin esto el
        // canto de medio metro ensenaria la misma textura que la cara entera y
        // saldria aplastada.
        BoxWallMesh.Attach(tr, MetrosDeBaldosa());
    }

    void PintarComoElSuelo(GameObject go)
    {
        Renderer suelo = floor != null ? floor.GetComponent<Renderer>() : null;
        if (suelo == null || suelo.sharedMaterial == null) return;

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial = suelo.sharedMaterial;
        }
    }

    float MetrosDeBaldosa()
    {
        TileTextureByScale t = floor != null
            ? floor.GetComponent<TileTextureByScale>()
            : null;

        return t != null ? t.metersPerTile : wallMetersPerTile;
    }

    // El techo tapa la luz, y por eso dentro se hace de noche.
    //
    // En la escena hay UNA luz y es direccional, asi que el techo la corta
    // entera. Eso es lo que se busca -- un sitio cerrado se ve cerrado -- pero
    // conviene saber de donde viene: el dia que parezca demasiado oscuro lo que
    // hay que hacer no es quitar la sombra, sino poner luces dentro.
    //
    // Siendo una caja basta con la sombra normal. Cuando era una lamina volteada
    // hacia falta pedirla a dos caras, porque Unity descarta las caras traseras
    // al dibujar el mapa de sombras y la unica que tenia miraba al suelo.
    void Sombra(GameObject go)
    {
        var modo = techoDaSombra
            ? UnityEngine.Rendering.ShadowCastingMode.On
            : UnityEngine.Rendering.ShadowCastingMode.Off;

        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = modo;
        }
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
