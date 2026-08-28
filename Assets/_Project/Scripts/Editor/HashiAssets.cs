using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Los assets del juego: el prefab de la caja, las cajas y las dificultades.
//
// Se generan con codigo y no a mano por lo de siempre: a mano hay que acordarse
// de que la caja mas alta siga cabiendo por el hueco de la dificultad mas
// dificil, y ese numero se le escapa a cualquiera. Aqui estan los dos juntos,
// se comprueban al generarlos y si algo no cuadra sale un aviso en vez de una
// partida imposible de ganar que parece un fallo de fisica.
public static class HashiAssets
{
    public const string CARPETA_PREFABS = "Assets/_Project/Prefabs/Hashi";
    public const string CARPETA_AJUSTES = "Assets/_Project/Ajustes/Hashi";
    public const string CARPETA_PREMIOS = CARPETA_AJUSTES + "/Premios";
    public const string CARPETA_DIFICULTAD = CARPETA_AJUSTES + "/Dificultad";

    public const string RUTA_PREFAB_PREMIO = CARPETA_PREFABS + "/PrizeBox.prefab";

    // ------------------------------------------------------------ las cajas

    struct Caja
    {
        public string nombre;
        public Vector3 tamano;
        public float masa;
        public Vector3 com;
        public float rozamiento;
        public Color color;
        public string notas;
    }

    // Regla que cumplen todas y que no se puede romper:
    //   tamano.z > separacion mayor de las barras (0,190 en Facil)  -> apoya
    //   tamano.x < 0,250                                            -> los brazos
    //                                                                  abiertos bajan
    //                                                                  por sus lados
    //   tamano.y < hueco menor de las barras      (0,134 en Extremo) -> cabe
    static readonly Caja[] CAJAS =
    {
        new Caja
        {
            nombre = "Pequena", tamano = new Vector3(0.15f, 0.095f, 0.21f),
            masa = 0.22f, com = Vector3.zero, rozamiento = 0.38f,
            color = new Color(0.35f, 0.75f, 0.95f),
            notas = "La de aprender. Poca masa y poca altura: con un empujon "
                    + "decente en una esquina ya se pone de canto.",
        },
        new Caja
        {
            nombre = "Normal", tamano = new Vector3(0.17f, 0.115f, 0.22f),
            masa = 0.40f, com = Vector3.zero, rozamiento = 0.42f,
            color = new Color(0.30f, 0.45f, 0.90f),
            notas = "La de referencia. Los numeros de la dificultad estan "
                    + "pensados con esta puesta.",
        },
        new Caja
        {
            nombre = "Grande", tamano = new Vector3(0.21f, 0.120f, 0.26f),
            masa = 0.65f, com = Vector3.zero, rozamiento = 0.45f,
            color = new Color(0.20f, 0.70f, 0.55f),
            notas = "Ancha, asi que sobresale mucho por los lados de las barras "
                    + "y hay que girarla mas grados antes de que empiece a caer "
                    + "sola. A cambio se agarra bien por la esquina.",
        },
        new Caja
        {
            nombre = "Larga", tamano = new Vector3(0.24f, 0.110f, 0.22f),
            masa = 0.55f, com = Vector3.zero, rozamiento = 0.45f,
            color = new Color(0.85f, 0.55f, 0.20f),
            notas = "Larga a lo largo de las barras, o sea mucha inercia para "
                    + "girarla sobre la vertical, que es el giro que hace falta. "
                    + "Apretarla por el centro no sirve de nada: hay que pillarla "
                    + "de un extremo para sacarle par.",
        },
        new Caja
        {
            nombre = "Pesada", tamano = new Vector3(0.18f, 0.120f, 0.23f),
            masa = 1.10f, com = new Vector3(0f, -0.030f, 0f), rozamiento = 0.55f,
            color = new Color(0.65f, 0.20f, 0.30f),
            notas = "Kilo y pico y el centro de masas por los suelos. Se mueve "
                    + "poco por golpe, asi que va de acumular: cada intento la "
                    + "deja un poco mas girada que el anterior.",
        },
        new Caja
        {
            nombre = "Ligera", tamano = new Vector3(0.16f, 0.120f, 0.21f),
            masa = 0.16f, com = new Vector3(0f, 0.020f, 0f), rozamiento = 0.30f,
            color = new Color(0.95f, 0.80f, 0.25f),
            notas = "Poco peso, poco rozamiento y el centro de masas alto: "
                    + "vuelca casi sola. El problema es el contrario, que "
                    + "resbala sobre las barras en vez de girar.",
        },
    };

    // ------------------------------------------------------- las dificultades

    struct Nivel
    {
        public string nombre;
        public float par, cierre, mover, bajar, subir;
        public float masa, rozamiento;
        public Vector3 com;
        public float barras;
        public float turno;
        public string notas;
    }

    static readonly Nivel[] NIVELES =
    {
        new Nivel
        {
            nombre = "Facil", par = 0.75f, cierre = 90f, mover = 0.17f,
            bajar = 0.28f, subir = 0.35f, masa = 0.75f, rozamiento = 0.85f,
            com = new Vector3(0f, 0.008f, 0f), barras = 0.190f, turno = 30f,
            notas = "Barras muy separadas (la caja apoya por los pelos), pinza "
                    + "fuerte y lenta, caja mas ligera y con el centro de masas "
                    + "un poco alto. Todo empuja hacia que vuelque.",
        },
        new Nivel
        {
            nombre = "Normal", par = 0.45f, cierre = 120f, mover = 0.22f,
            bajar = 0.30f, subir = 0.35f, masa = 1f, rozamiento = 1f,
            com = Vector3.zero, barras = 0.170f, turno = 20f,
            notas = "Los valores de fabrica de cada caja, sin tocar.",
        },
        new Nivel
        {
            nombre = "Dificil", par = 0.32f, cierre = 160f, mover = 0.30f,
            bajar = 0.35f, subir = 0.40f, masa = 1.35f, rozamiento = 1.15f,
            com = new Vector3(0f, -0.010f, 0f), barras = 0.158f, turno = 15f,
            notas = "Barras mas juntas: la caja apoya con mas margen y hay que "
                    + "girarla mas. La pinza cierra rapido y con poco par, asi "
                    + "que da un toque en vez de empujar.",
        },
        new Nivel
        {
            nombre = "Extremo", par = 0.22f, cierre = 210f, mover = 0.38f,
            bajar = 0.42f, subir = 0.50f, masa = 1.8f, rozamiento = 1.30f,
            com = new Vector3(0f, -0.018f, 0f), barras = 0.150f, turno = 12f,
            notas = "Casi el doble de masa, centro de masas hundido y el hueco "
                    + "justo. Con la caja Pesada aqui hacen falta muchos "
                    + "intentos seguidos bien colocados.",
        },
    };

    // ------------------------------------------------------------- generacion

    public static PrizeDefinitionLote Generar(Dictionary<string, Material> materiales)
    {
        HashiPiezas.AsegurarCarpeta(CARPETA_PREFABS);
        HashiPiezas.AsegurarCarpeta(CARPETA_PREMIOS);
        HashiPiezas.AsegurarCarpeta(CARPETA_DIFICULTAD);

        var lote = new PrizeDefinitionLote();

        lote.prefabPremio = CrearPrefabPremio(materiales);
        lote.prefabFunko = HashiFunko.Construir(materiales);
        lote.premios = CrearPremios();
        lote.dificultades = CrearDificultades();

        Comprobar(lote);

        AssetDatabase.SaveAssets();
        return lote;
    }

    // El prefab de la caja: raiz sin escalar con el collider, malla escalada
    // dentro. PrizeController da por hecho ese reparto.
    static Hashi.PrizeController CrearPrefabPremio(Dictionary<string, Material> materiales)
    {
        int capa = LayerMask.NameToLayer(Hashi.HashiLayers.NOMBRE_PREMIO);

        materiales.TryGetValue("Carton", out Material carton);

        // La caja Normal. PrizeController recoloca malla y collider al arrancar,
        // pero dejandolos ya bien el prefab suelto se ve como lo que es en la
        // ventana de proyecto, en vez de como un cubo de un metro.
        Vector3 tamano = new Vector3(0.17f, 0.115f, 0.22f);

        GameObject raiz = HashiPiezas.Cuerpo("PrizeBox", null, Vector3.zero,
                                             PrimitiveType.Cube, tamano,
                                             Vector3.zero, carton, capa);

        Rigidbody rb = raiz.AddComponent<Rigidbody>();
        rb.mass = 0.4f;

        BoxCollider col = raiz.AddComponent<BoxCollider>();
        col.size = tamano;

        Hashi.PrizeController pc = raiz.AddComponent<Hashi.PrizeController>();

        // Los valores de la caja Normal, para que el prefab suelto ya sirva.
        using (var a = new HashiCableado(pc))
        {
            a.Num("mass", 0.40f)
             .V3("size", tamano)
             .V3("centerOfMass", Vector3.zero)
             .Num("friction", 0.42f)
             .Num("bounciness", 0.02f);
        }

        GameObject guardado = PrefabUtility.SaveAsPrefabAsset(raiz, RUTA_PREFAB_PREMIO);
        Object.DestroyImmediate(raiz);

        return guardado != null ? guardado.GetComponent<Hashi.PrizeController>() : null;
    }

    static Hashi.PrizeDefinition[] CrearPremios()
    {
        var lista = new List<Hashi.PrizeDefinition>();

        foreach (Caja c in CAJAS)
        {
            Caja copia = c;   // no se puede capturar la variable del foreach

            lista.Add(HashiPiezas.Asset<Hashi.PrizeDefinition>(
                CARPETA_PREMIOS + "/Premio_" + c.nombre + ".asset",
                def =>
                {
                    def.nombre = copia.nombre;
                    def.notas = copia.notas;
                    def.size = copia.tamano;
                    def.mass = copia.masa;
                    def.centerOfMassOffset = copia.com;
                    def.friction = copia.rozamiento;
                    def.bounciness = 0.02f;
                    def.linearDamping = 0.02f;
                    def.angularDamping = 0.06f;
                    def.color = copia.color;
                }));
        }

        return lista.ToArray();
    }

    static Hashi.DifficultySettings[] CrearDificultades()
    {
        var lista = new List<Hashi.DifficultySettings>();

        foreach (Nivel n in NIVELES)
        {
            Nivel copia = n;

            lista.Add(HashiPiezas.Asset<Hashi.DifficultySettings>(
                CARPETA_DIFICULTAD + "/Dificultad_" + n.nombre + ".asset",
                d =>
                {
                    d.nombre = copia.nombre;
                    d.notas = copia.notas;
                    d.clawGripForce = copia.par;
                    d.clawCloseSpeed = copia.cierre;
                    d.clawMoveSpeed = copia.mover;
                    d.dropSpeed = copia.bajar;
                    d.riseSpeed = copia.subir;
                    d.prizeMass = copia.masa;
                    d.prizeFriction = copia.rozamiento;
                    d.centerOfMassOffset = copia.com;
                    d.barDistance = copia.barras;
                    d.turnTime = copia.turno;
                }));
        }

        return lista.ToArray();
    }

    // La comprobacion que justifica que esto sea codigo y no trabajo a mano:
    // cada caja contra CADA dificultad. Basta con que un par no cuadre para que
    // esa partida sea imposible o se regale sola, y jugando no se distingue de
    // un fallo de fisica.
    static void Comprobar(PrizeDefinitionLote lote)
    {
        if (lote.premios == null || lote.dificultades == null) return;

        float radio = 0.008f;   // el mismo que usa el constructor de las barras
        var quejas = new List<string>();

        // Esta no depende de la dificultad: es puro tamano de la garra.
        foreach (Hashi.PrizeDefinition p in lote.premios)
        {
            if (p.size.x < HashiWatashiBuilder.LARGO_MAXIMO_CAJA) continue;

            quejas.Add(p.nombre + ": mide " + p.size.x.ToString("0.000")
                       + " m de largo y los brazos abiertos solo llegan a "
                       + HashiWatashiBuilder.LARGO_MAXIMO_CAJA.ToString("0.000")
                       + " m, asi que bajarian encima de la caja en vez de por "
                       + "sus lados y la aplastarian contra las barras");
        }

        foreach (Hashi.DifficultySettings d in lote.dificultades)
        {
            float hueco = d.barDistance - 2f * radio;

            foreach (Hashi.PrizeDefinition p in lote.premios)
            {
                if (p.size.y >= hueco)
                {
                    quejas.Add(p.nombre + " en " + d.nombre + ": de canto mide "
                               + p.size.y.ToString("0.000") + " m y el hueco es de "
                               + hueco.ToString("0.000") + " m, no cae nunca");
                }

                if (p.size.z <= d.barDistance)
                {
                    quejas.Add(p.nombre + " en " + d.nombre + ": mide "
                               + p.size.z.ToString("0.000")
                               + " m de fondo y las barras estan a "
                               + d.barDistance.ToString("0.000") + " m, se cae sola");
                }
            }
        }

        if (quejas.Count == 0) return;

        Debug.LogWarning("[Hashi] Hay combinaciones de caja y dificultad que no "
                         + "se pueden jugar:\n  " + string.Join("\n  ", quejas));
    }
}

// Lo que devuelve la generacion, para que el constructor de la escena pueda
// enchufarlo sin volver a buscarlo por disco.
public class PrizeDefinitionLote
{
    public Hashi.PrizeController prefabPremio;
    public GameObject prefabFunko;
    public Hashi.PrizeDefinition[] premios;
    public Hashi.DifficultySettings[] dificultades;
}
