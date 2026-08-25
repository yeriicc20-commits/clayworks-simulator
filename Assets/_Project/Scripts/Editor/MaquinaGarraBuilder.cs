using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Monta el prefab de la maquina de garra a partir del FBX de Blender.
//
// El FBX llega plano: 106 piezas sueltas colgando de la raiz, porque en Blender
// no tienen padre. Aqui se les da la jerarquia que espera ClawController, se
// les ponen colliders y capas, y se rellenan todas las referencias.
//
// Todo lo que se puede MEDIR se mide del propio modelo: limites de recorrido,
// cuanto baja el brazo, donde esta la boca del premio. Nada de numeros a ojo.
// Si manana se cambia una medida en Blender, se vuelve a pulsar el boton y sale
// bien sin tocar nada aqui.
public static class MaquinaGarraBuilder
{
    const string RUTA_FBX = "Assets/_Project/Models/MaquinaGarra.fbx";
    const string CARPETA_PREFAB = "Assets/_Project/Prefabs/Machines";
    const string RUTA_PREFAB = CARPETA_PREFAB + "/MaquinaGarra.prefab";

    const int CAPA_PLACEABLE = 8;
    const int CAPA_PLUSH = 9;
    const int CAPA_GARRA = 10;
    const int CAPA_CARCASA = 11;

    // Holgura de la garra con el cristal, para que no lo roce al ir al limite.
    const float MARGEN_CRISTAL = 0.02f;

    // A que radio del eje quedan las puntas con la garra cerrada del todo.
    // No cero: si se cruzan, los dedos se atraviesan entre ellos.
    const float CIERRE_OBJETIVO = 0.02f;

    // A que altura sobre el suelo se para la garra al bajar del todo.
    const float HOLGURA_SUELO = 0.015f;

    // Piezas pequenas de adorno que no necesitan collider. Un tornillo de 9 mm
    // no cambia nada y son 8 colliders por maquina.
    static readonly string[] SIN_COLLIDER =
    {
        "Tornillo_", "LED_", "Onda_", "Ranura_", "Cartel_WIN",
        "Devolucion_Labio_", "Monedero_Bisel_", "Joystick_Aro", "Boton_Aro",
        "Motor_Puente_Polea", "Riel_Tope_",
    };

    // Dos sitios a proposito: el menu de arriba y el boton derecho en la
    // ventana de Proyecto. Un menu nuevo en la barra de Unity aparece al final
    // del todo y es facil no verlo.
    [MenuItem("Assets/Construir maquina de garra", false, 30)]
    public static void ConstruirDesdeProyecto()
    {
        Construir();
    }

    [MenuItem("ClayWorks/Construir maquina de garra", false, 1)]
    public static void Construir()
    {
        GameObject modelo = AssetDatabase.LoadAssetAtPath<GameObject>(RUTA_FBX);

        if (modelo == null)
        {
            Debug.LogError("[Maquina] No encuentro el modelo en " + RUTA_FBX);
            return;
        }

        GameObject raiz = (GameObject)PrefabUtility.InstantiatePrefab(modelo);
        PrefabUtility.UnpackPrefabInstance(raiz, PrefabUnpackMode.Completely,
                                           InteractionMode.AutomatedAction);
        raiz.name = "MaquinaGarra";
        raiz.transform.position = Vector3.zero;
        raiz.layer = CAPA_PLACEABLE;

        var piezas = new Dictionary<string, Transform>();
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t != raiz.transform) piezas[t.name] = t;
        }

        string[] imprescindibles = { "Puente", "Carro", "Cabeza", "Dedo_1", "Dedo_2", "Dedo_3" };
        foreach (string n in imprescindibles)
        {
            if (!piezas.ContainsKey(n))
            {
                Debug.LogError("[Maquina] Al modelo le falta la pieza '" + n
                               + "'. Se cancela: sin ella el prefab saldria roto y mudo.");
                Object.DestroyImmediate(raiz);
                return;
            }
        }

        // ------------------------------------------------------ mediciones
        // Se toman ANTES de mover nada de sitio.
        Bounds suelo = Envolvente(piezas, "Suelo_Juego_");
        Bounds cristal = Envolvente(piezas, "Cristal_Frente", "Cristal_Atras",
                                    "Cristal_Izq", "Cristal_Der");
        Bounds boca = Envolvente(piezas, "Boca_Labio_");

        // Hueco LIBRE, de cara interior a cara interior. Antes se usaba la
        // envolvente de los cuatro cristales, que incluye su grosor: se perdia
        // un centimetro de recorrido por cada lado sin motivo.
        Bounds cIzq = Envolvente(piezas, "Cristal_Izq");
        Bounds cDer = Envolvente(piezas, "Cristal_Der");
        Bounds cFre = Envolvente(piezas, "Cristal_Frente");
        Bounds cAtr = Envolvente(piezas, "Cristal_Atras");

        float huecoX0 = cIzq.max.x, huecoX1 = cDer.min.x;
        float huecoZ0 = Mathf.Min(cFre.max.z, cAtr.max.z);
        float huecoZ1 = Mathf.Max(cFre.min.z, cAtr.min.z);

        if (huecoZ0 > huecoZ1) { float t = huecoZ0; huecoZ0 = huecoZ1; huecoZ1 = t; }
        Bounds cajaCarro = Envolvente(piezas, "Carro");

        Transform puente = piezas["Puente"];
        Transform carro = piezas["Carro"];
        Transform cabeza = piezas["Cabeza"];
        Transform[] dedos = { piezas["Dedo_1"], piezas["Dedo_2"], piezas["Dedo_3"] };

        Bounds garra = Envolvente(piezas, "Dedo_", "Cabeza");
        Bounds todo = Envolvente(piezas, "");

        // Radio de la garra: lo que sobresale de su eje. Es lo que decide hasta
        // donde puede llegar sin rozar el cristal.
        float radioGarra = Mathf.Max(
            Mathf.Max(Mathf.Abs(garra.max.x - carro.position.x), Mathf.Abs(garra.min.x - carro.position.x)),
            Mathf.Max(Mathf.Abs(garra.max.z - carro.position.z), Mathf.Abs(garra.min.z - carro.position.z)));

        // ------------------------------------------------------- jerarquia
        GameObject estructura = new GameObject("Estructura");
        estructura.transform.SetParent(raiz.transform, false);

        // El carro cuelga del puente: al mover el puente en X, el carro va con
        // el. Es el orden que da por hecho ClawController.
        carro.SetParent(puente, true);

        // Brazo: pivote nuevo para la bajada. Se deja en el origen del carro,
        // asi armBaseLocalPos queda en cero y armDownY es directamente cuanto
        // baja, en metros y en negativo. Un numero que se lee.
        GameObject brazo = new GameObject("Brazo");
        brazo.transform.SetParent(carro, false);

        cabeza.SetParent(brazo.transform, true);
        foreach (Transform d in dedos) d.SetParent(brazo.transform, true);

        // Punto de bisagra: el eje comun de los tres dedos. ClawController saca
        // de aqui el eje de giro de cada dedo, cruzando su direccion radial con
        // la vertical, asi que basta con que este centrado y a su altura.
        Vector3 centroBisagras = (dedos[0].position + dedos[1].position + dedos[2].position) / 3f;

        GameObject hinge = new GameObject("HingePoint");
        hinge.transform.SetParent(brazo.transform, false);
        hinge.transform.position = centroBisagras;

        // Puntas de los dedos, para las comprobaciones de contacto.
        var puntas = new Transform[dedos.Length];
        for (int i = 0; i < dedos.Length; i++)
        {
            GameObject punta = new GameObject("Punta_" + (i + 1));
            punta.transform.SetParent(dedos[i], false);
            punta.transform.position = PuntaDe(dedos[i], centroBisagras);
            puntas[i] = punta.transform;
        }

        // Anclaje del balanceo: donde sale el cable, o sea la base del carro.
        // Tiene que ser hermano del brazo porque ClawController compara sus
        // posiciones LOCALES.
        GameObject anclaje = new GameObject("SwingAnchor");
        anclaje.transform.SetParent(carro, false);
        anclaje.transform.position = new Vector3(cajaCarro.center.x, cajaCarro.min.y, cajaCarro.center.z);

        // ------------------------------------------------------- marcadores
        GameObject zonaPremio = new GameObject("PrizeDropZone");
        zonaPremio.transform.SetParent(raiz.transform, false);
        zonaPremio.transform.position = new Vector3(boca.center.x, suelo.max.y - 0.06f, boca.center.z);

        BoxCollider trigger = zonaPremio.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(boca.size.x * 0.9f, 0.12f, boca.size.z * 0.9f);

        GameObject spawn = new GameObject("ToySpawnPoint");
        spawn.transform.SetParent(raiz.transform, false);
        // A un palmo del suelo. Antes estaba a 35 cm y ClawController le suma
        // otros 20 de caida: los peluches nacian justo a la altura de la garra,
        // dentro de ella, y salian disparados en cuanto PhysX los separaba.
        spawn.transform.position = new Vector3(cristal.center.x, suelo.max.y + 0.05f, cristal.center.z);

        // De que lado esta el frente. NO se da por supuesto: se mira donde ha
        // quedado la consola.
        //
        // Aqui me equivoque la primera vez. Di por hecho que el frente era -Z
        // porque en Blender la consola esta en -Y, pero la exportacion a FBX
        // invierte ese eje: acabo en +Z. Resultado, los avisos y el sitio del
        // NPC quedaron detras de la maquina. Deducirlo de una pieza que solo
        // puede estar delante no se puede equivocar.
        Transform consola = piezas.ContainsKey("Consola_Cuerpo") ? piezas["Consola_Cuerpo"] : null;

        float frente = -1f;

        if (consola != null)
        {
            frente = Mathf.Sign(consola.position.z - todo.center.z);
        }
        else
        {
            Debug.LogWarning("[Maquina] No encuentro Consola_Cuerpo para saber que "
                             + "lado es el de delante. Tiro de -Z, que puede estar mal.");
        }

        float bordeFrente = frente > 0f ? todo.max.z : todo.min.z;

        GameObject sitioNPC = new GameObject("NPC_MachineSpot");
        sitioNPC.transform.SetParent(raiz.transform, false);
        sitioNPC.transform.position = new Vector3(todo.center.x, 0f, bordeFrente + frente * 0.55f);
        sitioNPC.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, -frente));

        // -------------------------------------------------- cable de la garra
        Transform cable = piezas.ContainsKey("Cable") ? piezas["Cable"] : null;
        if (cable != null)
        {
            // El cilindro del modelo se queda de adorno estatico: lo que se ve
            // moverse es un LineRenderer, que si sabe estirarse.
            Renderer rc = cable.GetComponent<Renderer>();
            if (rc != null) rc.enabled = false;
            cable.SetParent(carro, true);
        }

        GameObject cableVivo = new GameObject("Cable_Visual");
        cableVivo.transform.SetParent(raiz.transform, false);

        LineRenderer lr = cableVivo.AddComponent<LineRenderer>();

        // Le pasamos el material del cable del modelo. Sin material propio un
        // LineRenderer sale magenta, que es el color de "aqui falta algo".
        if (cable != null)
        {
            Renderer fuente = cable.GetComponent<Renderer>();
            if (fuente != null) lr.sharedMaterial = fuente.sharedMaterial;
        }

        lr.useWorldSpace = true;
        lr.startWidth = 0.006f;
        lr.endWidth = 0.006f;
        lr.numCapVertices = 2;
        lr.textureMode = LineTextureMode.Stretch;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        CableLineRenderer cableScript = cableVivo.AddComponent<CableLineRenderer>();
        cableScript.topPoint = anclaje.transform;
        cableScript.bottomPoint = cabeza;
        cableScript.segmentCount = 6;
        cableScript.sagAmount = 0.004f;

        // ------------------------------------------------- zona de interaccion
        // ClawMachineInteraction va con OnTriggerEnter, asi que necesita un
        // trigger EN SU MISMO GameObject. Sin el no aparece nunca el "E: jugar"
        // y la maquina parece muerta aunque este perfectamente montada.
        //
        // Se pone delante, del lado de la consola, no envolviendo la maquina
        // entera: si envuelve, el aviso salta tambien cuando pasas por detras.
        const float FONDO_ZONA = 0.9f;

        BoxCollider zona = raiz.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        zona.size = new Vector3(todo.size.x + 0.4f, todo.size.y, FONDO_ZONA);
        zona.center = new Vector3(todo.center.x,
                                  todo.center.y,
                                  bordeFrente + frente * FONDO_ZONA * 0.5f);

        // ------------------------------------------------------- materiales
        // Los del FBX se descartan: Unity los importa con el shader que le
        // parece y en URP salen rosas. Estos son de URP y estan controlados.
        var tablaMats = MaquinaGarraMateriales.CrearOActualizar();
        int repintadas = MaquinaGarraMateriales.Repartir(piezas.Values, tablaMats);

        // ------------------------------------------- colliders, capas, sombras
        int puestos = 0, saltados = 0;

        foreach (var par in piezas)
        {
            Transform t = par.Value;
            if (t == null) continue;

            bool esGarra = t == cabeza || dedos.Contains(t);
            bool esMovil = esGarra || t == carro || t == puente;

            t.gameObject.layer = esMovil ? CAPA_GARRA : CAPA_CARCASA;

            // Lo que no se mueve va a la subcarpeta, para no tener 106 hijos
            // sueltos en la raiz cada vez que se abre el prefab.
            if (!esMovil && t.parent == raiz.transform && t != cable)
            {
                t.SetParent(estructura.transform, true);
            }

            if (PonerCollider(t, esGarra)) puestos++;
            else saltados++;
        }

        // -------------------------------------------------- luces de los mandos
        // El joystick es UNA malla con dos materiales, vastago y bola. Se le
        // apunta a la bola por su material; si no, parpadearia entero.
        Parpadeo(piezas, "Joystick", new Color(1f, 0.07f, 0.10f), 1.0f, "BolaJoystick");
        Parpadeo(piezas, "Boton_Jugar", new Color(0.06f, 0.35f, 1f), 1.4f);
        Parpadeo(piezas, "Boton_Moneda_1", new Color(0.06f, 0.35f, 1f), 1.8f);
        Parpadeo(piezas, "Boton_Moneda_2", new Color(0.06f, 0.35f, 1f), 1.8f);

        // ------------------------------------------------------- componentes
        ClawController claw = raiz.AddComponent<ClawController>();

        claw.railX = puente;
        claw.railZ = carro;
        claw.clawArm = brazo.transform;
        claw.clawHead = cabeza;
        claw.fingers = dedos;
        claw.fingerTips = puntas;
        claw.hingePoint = hinge.transform;
        claw.swingAnchor = anclaje.transform;
        claw.prizeZone = zonaPremio.transform;
        claw.toySpawnPoint = spawn.transform;
        claw.npcSpot = sitioNPC.transform;

        // Limites de recorrido, medidos del cristal y del tamano de la garra.
        // El puente lleva el carro, asi que su X manda; el carro pone la Z.
        float alcanceX = (huecoX1 - huecoX0) * 0.5f - radioGarra - MARGEN_CRISTAL;
        float alcanceZ = (huecoZ1 - huecoZ0) * 0.5f - radioGarra - MARGEN_CRISTAL;

        float centroX = puente.localPosition.x;
        float centroZ = carro.localPosition.z;

        claw.limitXMin = centroX - alcanceX;
        claw.limitXMax = centroX + alcanceX;
        claw.limitZMin = centroZ - alcanceZ;
        claw.limitZMax = centroZ + alcanceZ;

        // Cuanto baja: hasta dejar la punta de los dedos a un pelo del suelo.
        float bajada = garra.min.y - (suelo.max.y + HOLGURA_SUELO);

        claw.armUpY = 0f;
        claw.armDownY = -Mathf.Max(0.05f, bajada);

        claw.moveSpeed = 0.8f;
        claw.armMoveSpeed = 0.5f;
        claw.detectionRadius = 0.24f;
        claw.gripHeightOffset = 0.05f;
        claw.fingerCloseAngle = AnguloDeCierre(dedos, puntas, centroBisagras);
        claw.insideGripRadius = 0.176f;
        claw.toyScatterSpread = 0.85f;
        claw.insideGripHeightOffset = 0.08f;
        claw.obstacleCheckRadius = 0.096f;
        claw.forceNewtonsPerUnit = 1f;
        claw.plushLayer = 1 << CAPA_PLUSH;
        claw.obstacleLayerMask = 0;
        claw.usePhysicalCable = false;
        claw.isControllable = false;

        MachinePricing precio = raiz.AddComponent<MachinePricing>();
        precio.price = 5f;
        precio.recommendedPrice = 5f;
        precio.competitionPrice = 5f;

        ClawMachineInteraction inter = raiz.AddComponent<ClawMachineInteraction>();
        inter.clawController = claw;
        inter.cost = 5f;

        raiz.AddComponent<PlaceableObject>();

        PlushDropZone premio = zonaPremio.AddComponent<PlushDropZone>();
        premio.clawController = claw;
        premio.moneyReward = 20;

        // ------------------------------------------------------------ guardar
        AsegurarCarpeta(CARPETA_PREFAB);

        GameObject guardado = PrefabUtility.SaveAsPrefabAsset(raiz, RUTA_PREFAB);
        Object.DestroyImmediate(raiz);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(string.Format(
            "[Maquina] Prefab montado en {0}\n"
            + "  piezas .............. {1}\n"
            + "  colliders ........... {2} puestos, {3} saltados por decorativos\n"
            + "  recorrido X ......... {4:F3} a {5:F3} m\n"
            + "  recorrido Z ......... {6:F3} a {7:F3} m\n"
            + "  bajada del brazo .... {8:F3} m\n"
            + "  radio de la garra ... {9:F3} m\n"
            + "  boca del premio ..... {10:F2} x {11:F2} m",
            RUTA_PREFAB, piezas.Count, puestos, saltados,
            claw.limitXMin, claw.limitXMax, claw.limitZMin, claw.limitZMax,
            -claw.armDownY, radioGarra, boca.size.x, boca.size.z));

        Selection.activeObject = guardado;
        EditorGUIUtility.PingObject(guardado);
    }

    // Crea la carpeta por la via de Unity. Con Directory.CreateDirectory la
    // carpeta aparece en disco pero AssetDatabase no la conoce todavia, y el
    // guardado falla sin explicar por que.
    public static void AsegurarCarpeta(string ruta)
    {
        if (AssetDatabase.IsValidFolder(ruta)) return;

        // GetDirectoryName devuelve barras de Windows y AssetDatabase solo
        // entiende las normales.
        string padre = Path.GetDirectoryName(ruta).Replace('\\', '/');
        string hoja = Path.GetFileName(ruta);

        AsegurarCarpeta(padre);
        AssetDatabase.CreateFolder(padre, hoja);
    }

    // Cuanto tiene que girar cada dedo para cerrar del todo.
    //
    // Se saca de la geometria, no a ojo. El dedo gira alrededor de un eje
    // tangencial que pasa por su bisagra, asi que su punta describe un arco en
    // el plano radial-vertical. Con la punta a distancia "a" hacia fuera de la
    // bisagra y "b" hacia abajo, al girar un angulo f hacia dentro queda a
    // radio  R + a*cos(f) - b*sin(f)  del eje de la garra.
    //
    // Se busca el primer angulo que deja las puntas a CIERRE_OBJETIVO del eje.
    // El valor que habia puesto, -48 grados, venia del modelo anterior: con
    // esta garra las puntas se cruzaban 2,3 cm PASADO el eje, o sea que los
    // dedos se atravesaban entre si en vez de cerrar.
    static float AnguloDeCierre(Transform[] dedos, Transform[] puntas, Vector3 bisagra)
    {
        float peor = 90f;

        for (int i = 0; i < dedos.Length; i++)
        {
            if (dedos[i] == null || puntas[i] == null) continue;

            float radioBisagra = Horizontal(dedos[i].position - bisagra);
            float radioPunta = Horizontal(puntas[i].position - bisagra);

            float a = radioPunta - radioBisagra;
            float b = dedos[i].position.y - puntas[i].position.y;

            if (b <= 0.001f) continue;   // punta por encima de la bisagra: raro

            float encontrado = 90f;

            for (float f = 0f; f <= 90f; f += 0.5f)
            {
                float r = radioBisagra + a * Mathf.Cos(f * Mathf.Deg2Rad)
                                       - b * Mathf.Sin(f * Mathf.Deg2Rad);

                if (r <= CIERRE_OBJETIVO) { encontrado = f; break; }
            }

            peor = Mathf.Min(peor, encontrado);
        }

        // Negativo = hacia dentro. ClawController gira sobre cross(radial, up),
        // y con esa convencion el signo positivo ABRE la garra.
        return -peor;
    }

    static float Horizontal(Vector3 v)
    {
        v.y = 0f;
        return v.magnitude;
    }

    // ------------------------------------------------------------- ayudantes

    static Bounds Envolvente(Dictionary<string, Transform> piezas, params string[] prefijos)
    {
        Bounds b = new Bounds();
        bool primero = true;

        foreach (var par in piezas)
        {
            if (!prefijos.Any(p => par.Key.StartsWith(p))) continue;

            Renderer r = par.Value.GetComponent<Renderer>();
            if (r == null) continue;

            if (primero) { b = r.bounds; primero = false; }
            else b.Encapsulate(r.bounds);
        }

        if (primero)
        {
            Debug.LogWarning("[Maquina] No hay ninguna pieza que empiece por '"
                             + string.Join(", ", prefijos) + "'. Se mide como vacio.");
        }

        return b;
    }

    // La punta del dedo es su vertice mas lejano a la bisagra. Con el centro de
    // la caja envolvente no vale: el dedo es un arco, y su centro cae en el aire
    // por dentro de la curva, no en el gancho.
    static Vector3 PuntaDe(Transform dedo, Vector3 bisagra)
    {
        MeshFilter mf = dedo.GetComponent<MeshFilter>();

        if (mf == null || mf.sharedMesh == null)
        {
            Renderer r = dedo.GetComponent<Renderer>();
            return r != null ? new Vector3(r.bounds.center.x, r.bounds.min.y, r.bounds.center.z)
                             : dedo.position;
        }

        Vector3 mejor = dedo.position;
        float lejos = -1f;

        foreach (Vector3 v in mf.sharedMesh.vertices)
        {
            Vector3 mundo = dedo.TransformPoint(v);
            float d = (mundo - bisagra).sqrMagnitude;
            if (d > lejos) { lejos = d; mejor = mundo; }
        }

        return mejor;
    }

    static bool PonerCollider(Transform t, bool esGarra)
    {
        if (t.GetComponent<Collider>() != null) return false;

        MeshFilter mf = t.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return false;

        if (SIN_COLLIDER.Any(p => t.name.StartsWith(p))) return false;

        Bounds local = mf.sharedMesh.bounds;

        // Nada por debajo de 4 cm lleva collider. Son adornos, y cada uno es una
        // pareja de contacto mas que PhysX tiene que mirar en cada paso.
        if (!esGarra && Mathf.Max(local.size.x, Mathf.Max(local.size.y, local.size.z)) < 0.04f)
            return false;

        if (esGarra && t.name.StartsWith("Dedo_"))
        {
            // Los dedos son arcos: una caja los convierte en un ladrillo y ya no
            // pasan entre los peluches. Casco convexo, que es lo mas parecido a
            // la forma que admite un cuerpo que se mueve.
            MeshCollider mc = t.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh;
            mc.convex = true;
            return true;
        }

        BoxCollider bc = t.gameObject.AddComponent<BoxCollider>();
        bc.center = local.center;
        bc.size = local.size;
        return true;
    }

    static void Parpadeo(Dictionary<string, Transform> piezas, string nombre, Color color,
                         float periodo, string material = null)
    {
        if (!piezas.ContainsKey(nombre)) return;

        Transform t = piezas[nombre];

        ArcadeBlink b = t.gameObject.AddComponent<ArcadeBlink>();
        b.color = color;
        b.periodo = periodo;
        b.materialIndex = material == null ? -1 : IndiceMaterial(t, material);

        if (material != null && b.materialIndex < 0)
        {
            Debug.LogWarning("[Maquina] En '" + nombre + "' no hay ningun material '"
                             + material + "'. Parpadeara la pieza entera.");
        }
    }

    static int IndiceMaterial(Transform t, string nombre)
    {
        Renderer r = t.GetComponent<Renderer>();
        if (r == null) return -1;

        Material[] mats = r.sharedMaterials;

        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] != null && mats[i].name.StartsWith(nombre)) return i;
        }

        return -1;
    }
}
