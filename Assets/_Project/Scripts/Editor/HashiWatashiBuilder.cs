using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Monta la maquina de puente (hashi-watashi) entera y deja la escena guardada.
//
// Todo el mueble esta en metros y con medidas de maquina de verdad: 80 cm de
// hueco, barras de 8 mm, caja de 22 cm. No es por presumir de realismo, es que
// la fisica de PhysX se porta segun el tamano: con una maquina de 5 metros los
// mismos numeros dan una caja que flota, y con una de 5 cm da temblores. A
// escala real, los valores por defecto del motor son los correctos.
//
// Las medidas de aqui abajo se reparten a los componentes al montar, y son la
// unica copia: cambiar una y volver a pulsar el boton reconstruye la maquina
// coherente. Nadie tiene que acordarse de que si suben las barras hay que bajar
// la garra.
public static class HashiWatashiBuilder
{
    // ---------------------------------------------------------------- medidas

    // Hueco interior, desde el centro. El suelo del cajon esta en Y = 0.
    const float INT_X = 0.60f;      // media anchura

    const float INT_Z = 0.50f;      // media profundidad

    const float INT_ALTO = 1.15f;

    const float GROSOR_CRISTAL = 0.010f;
    const float GROSOR_CHAPA = 0.024f;

    // Las barras. Van de LADO A LADO (eje X) y estan separadas en PROFUNDIDAD
    // (eje Z): una cerca del cristal y otra al fondo, con el hueco alejandose
    // del jugador. Es como estan las de verdad.
    const float BARRA_ALTURA = 0.30f;
    const float BARRA_RADIO = 0.008f;
    const float BARRA_LARGO = INT_X * 2f - 0.02f;
    const float BARRA_SEPARACION = 0.17f;

    // La garra.
    const float CARRIL_ALTURA = INT_ALTO - 0.07f;
    const float GARRA_REPOSO = CARRIL_ALTURA - 0.09f;

    // Hasta donde baja el cuerpo de la garra, y por que ese numero y no otro.
    //
    // Las bisagras quedan 15 mm por debajo del cuerpo, o sea a 0,515. La tapa de
    // la caja mas alta esta a 0,428 y su base a 0,308. Con los brazos abiertos
    // 20 grados, que es lo poco que se separan de la vertical:
    //
    //   vertice del chevron:     x = 0,192   y = 0,463
    //   a la altura de la tapa:  x = 0,189
    //   punta del brazo:         x = 0,180   y = 0,336
    //   punta de la garra:       x = 0,160   y = 0,318
    //
    // La caja mas larga llega a 0,125, asi que el brazo baja por fuera con 64 mm
    // de aire a la altura de la tapa. Y el gancho termina a 0,318: diez
    // milimetros por encima de las barras y de la base de la caja, que es justo
    // la altura para empujarla por abajo y, en cuanto se inclina un poco,
    // meterse por debajo del canto.
    //
    // Ese margen es toda la diferencia entre el juego y un destrozo. Con los
    // brazos mas juntos habria que abrirlos en aspa para esquivar la caja, y
    // entonces el codo ya no llegaria por debajo de nada. Si se toca esta
    // altura, la separacion o el angulo, hay que rehacer las tres lineas.
    const float GARRA_MINIMA = 0.53f;

    // El largo maximo que puede tener una caja para que los brazos bajen por sus
    // lados en vez de encima. Sale de la cuenta de arriba, y lo usa HashiAssets
    // para comprobar las cajas al generarlas: escrito solo en un comentario, el
    // dia que alguien haga una caja de 30 cm no se entera nadie.
    public const float LARGO_MAXIMO_CAJA = 0.250f;

    // La garra saca 20,3 cm a cada lado en X (el vertice del chevron abierto) y
    // solo 7,5 en profundidad. De ahi los dos limites tan distintos; los dos
    // dejan 2 cm de aire hasta el cristal.
    const float GARRA_LIMITE_X = INT_X - 0.23f;
    const float GARRA_LIMITE_Z = INT_Z - 0.10f;
    // Posicion natural: la esquina de la IZQUIERDA pegada al FRENTE. De ahi
    // arranca el recorrido, hacia la derecha primero y hacia el fondo despues,
    // que es como se mueve el carro de una recreativa de estas.
    static readonly Vector2 GARRA_SALIDA = new Vector2(-GARRA_LIMITE_X, -GARRA_LIMITE_Z);

    // Cuerpo de la garra: un ovalo, no un platillo volante. La proporcion entre
    // el ovalo y los brazos es lo que hace que se reconozca la maquina: en las
    // fotos los brazos son casi tan largos como ancho el ovalo. Con el cuerpo
    // grande y los brazos cortos sale un plato con dos patas, que no se parece
    // en nada por mucho que las piezas sean las mismas.
    static readonly Vector3 CUERPO_TAMANO = new Vector3(0.24f, 0.10f, 0.15f);

    // Los brazos: varillas LARGAS y FINAS que cuelgan casi rectas.
    //
    // La separacion es lo importante y va contra la intuicion: las bisagras
    // tienen que quedar mas separadas que media caja (0,125 la mas larga). Asi
    // los brazos bajan por fuera de la caja colgando casi verticales, en vez de
    // tener que abrirse en aspa para esquivarla. Es como cuelgan en la foto, y
    // ademas es lo unico que permite que el codo llegue por debajo del canto.
    const float DEDO_SEPARACION = 0.115f;   // del eje a cada bisagra
    const float DEDO_LARGO = 0.190f;

    // Pletina, no varilla: plana y ancha, con la cara mirando al jugador.
    const float DEDO_ANCHO = 0.024f;        // lo alto de la pletina
    const float DEDO_GROSOR = 0.006f;       // lo fina que es la chapa

    // El chevron: cuanto sale el vertice hacia fuera y a que altura esta. Es lo
    // que hace que el brazo abombe hacia afuera y luego vuelva hacia dentro.
    const float PIE_SALIENTE = 0.055f;
    const float PIE_ALTURA_VERTICE = 0.075f;

    // La garra propiamente dicha: el trozo doblado hacia dentro con el que
    // termina cada brazo. No es adorno. Es la unica pieza que puede meterse por
    // debajo del canto de la caja y levantarla, que es la mitad de las tecnicas
    // que se ven en las fotos; con la punta recta solo se puede empujar de lado.
    const float PIE_LARGO = 0.025f;

    // Dedos ligeros: pesan poco comparados con la caja (400 g), asi que el
    // empujon que se nota es el del motor y no el del propio dedo cayendo.
    const float DEDO_MASA = 0.18f;

    const float DEDO_ANGULO_CERRADO = -20f;
    const float DEDO_ANGULO_ABIERTO = 20f;

    // ------------------------------------------- el camino del premio, abajo

    // Caras exteriores del mueble, para que todo lo de abajo cuadre con lo de
    // arriba sin repetir sumas.
    const float FRENTE = INT_Z + 0.03f;
    const float LATERAL = INT_X + 0.04f;
    const float PEDESTAL_ALTO = 1.05f;

    // El suelo del cajon de juego es una RAMPA, y solo cubre la mitad de atras.
    // La mitad de delante es el agujero por el que se va el premio.
    const float RAMPA_ATRAS = 0.17f;        // altura del suelo al fondo
    const float RAMPA_DELANTE = 0.03f;      // altura donde se acaba
    const float RAMPA_FIN_Z = 0.05f;        // donde se acaba y empieza el hueco

    // La bandeja de recogida, dentro del pedestal y con su boca al frente.
    const float BANDEJA_SUELO = -0.46f;
    const float BOCA_ALTO = 0.26f;
    const float BOCA_ANCHO = 0.27f;         // media anchura

    const string RUTA_ESCENA = "Assets/Scenes/Hashi_Watashi.unity";

    // ------------------------------------------------------------------ menus

    [MenuItem("ClayWorks/Hashi-Watashi/Montar escena", false, 40)]
    public static void MontarEscena()
    {
        // Se pregunta ANTES de tocar nada. Este boton cierra la escena abierta,
        // y perder el trabajo de otra escena por pulsar un boton de menu seria
        // imperdonable.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("[Hashi] Cancelado: no se ha tocado nada.");
            return;
        }

        AsegurarCapas();

        var materiales = HashiMateriales.CrearOActualizar();

        // Sin materiales no se sigue. El constructor los busca por nombre, y
        // seguir adelante daria una tanda de excepciones de diccionario a mitad
        // del montaje, con media maquina hecha y la escena vieja ya cerrada.
        if (materiales.Count == 0)
        {
            Debug.LogError("[Hashi] No se han podido crear los materiales, asi "
                           + "que no monto nada. Mira el error de arriba: casi "
                           + "seguro que el proyecto no esta en URP.");
            return;
        }

        var lote = HashiAssets.Generar(materiales);

        UnityEngine.SceneManagement.Scene escena =
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        Escenario(materiales);

        Maquina m = MontarMaquina(materiales, lote, false);
        Completar(m, lote);

        GameObject raiz = m.raiz;

        MatrizDeColisiones();

        HashiPiezas.AsegurarCarpeta("Assets/Scenes");
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena, RUTA_ESCENA);

        Selection.activeGameObject = raiz;
        EditorGUIUtility.PingObject(raiz);

        Debug.Log("[Hashi] Maquina montada y escena guardada en " + RUTA_ESCENA
                  + ". Dale a Play: ENTER mete credito, WASD mueve, ESPACIO suelta.");
    }

    // Lo que hay que subir la maquina para que su base quede en el suelo. El
    // zocalo llega a -0,93 respecto al suelo del cajon, que es el origen con el
    // que esta construida toda la maquina. El prefab del local, en cambio, tiene
    // que tener el origen en el suelo: el sistema de colocacion apoya ahi.
    const float ALTURA_BASE = PEDESTAL_ALTO + 0.02f;

    const string RUTA_PREFAB = "Assets/_Project/Prefabs/Machines/MaquinaPuente.prefab";

    [MenuItem("ClayWorks/Hashi-Watashi/Crear prefab para el local", false, 41)]
    public static GameObject MontarPrefab()
    {
        AsegurarCapas();

        var materiales = HashiMateriales.CrearOActualizar();

        if (materiales.Count == 0)
        {
            Debug.LogError("[Hashi] Sin materiales no monto el prefab. El "
                           + "proyecto no parece estar en URP.");
            return null;
        }

        var lote = HashiAssets.Generar(materiales);

        // Se monta en una escena de trabajo aparte y no en la que este abierta.
        // Creando y borrando cien objetos en la escena del jugador, Unity la deja
        // marcada como modificada aunque al final no quede nada, y entonces al
        // salir te pregunta si quieres guardar cambios que no has hecho.
        UnityEngine.SceneManagement.Scene taller = EditorSceneManager.NewPreviewScene();

        GameObject prefab = null;

        try
        {
            Maquina m = MontarMaquina(materiales, lote, true, taller);

            GameObject raiz = new GameObject("MaquinaPuente");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(raiz, taller);

            int capaPlaceable = LayerMask.NameToLayer("Placeable");
            if (capaPlaceable >= 0) raiz.layer = capaPlaceable;

            m.raiz.transform.SetParent(raiz.transform, false);
            m.raiz.transform.localPosition = new Vector3(0f, ALTURA_BASE, 0f);

            PrepararParaLaTienda(m, raiz);

            HashiPiezas.AsegurarCarpeta("Assets/_Project/Prefabs/Machines");
            prefab = PrefabUtility.SaveAsPrefabAsset(raiz, RUTA_PREFAB);

            // OJO: sourcePrefab se deja VACIO en el prefab, a proposito.
            //
            // Parece que lo suyo seria apuntarlo a si mismo aqui, pero no
            // funciona: una referencia guardada dentro de un prefab que apunta a
            // una pieza del mismo prefab se REMAPEA al instanciar, asi que en la
            // maquina de la escena acaba apuntandose a si misma en vez de al
            // asset. Y entonces, al recogerla, el juego destruye la maquina...
            // que es justo el molde que iba a usar para volver a ponerla. El
            // fantasma sale verde y el clic no hace nada, porque un objeto
            // destruido cuenta como null.
            //
            // Lo rellena quien la coloca, que es lo que hace PlacementManager al
            // confirmar y lo que hace HashiAutoInstalar al ponerla la primera
            // vez. Igual que la maquina de garra.
        }
        finally
        {
            // En un finally: si algo revienta a mitad, la escena de trabajo se
            // queda abierta e invisible y se van acumulando una por intento.
            EditorSceneManager.ClosePreviewScene(taller);
        }

        MatrizDeColisiones();
        AssetDatabase.SaveAssets();

        if (prefab != null)
        {
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);

            Debug.Log("[Hashi] Prefab del local guardado en " + RUTA_PREFAB
                      + ". Para poder comprarla, pulsa ahora "
                      + "ClayWorks/Hashi-Watashi/Ponerla a la venta en el local.");
        }

        return prefab;
    }

    [MenuItem("ClayWorks/Hashi-Watashi/Rehacer solo los assets", false, 42)]
    public static void SoloAssets()
    {
        AsegurarCapas();
        var materiales = HashiMateriales.CrearOActualizar();
        HashiAssets.Generar(materiales);

        Debug.Log("[Hashi] Materiales, cajas y dificultades al dia. La escena no "
                  + "se ha tocado.");
    }

    // ------------------------------------------------------------------ capas

    // Crea las capas que faltan en Project Settings. Respeta las que ya estan y
    // no toca ninguna de las del resto del proyecto.
    public static void AsegurarCapas()
    {
        Object activo = AssetDatabase.LoadAllAssetsAtPath(
            "ProjectSettings/TagManager.asset")[0];

        SerializedObject tm = new SerializedObject(activo);
        SerializedProperty capas = tm.FindProperty("layers");

        var puestas = new List<string>();

        foreach (string nombre in Hashi.HashiLayers.TODAS)
        {
            if (Existe(capas, nombre)) continue;

            int hueco = PrimerHueco(capas);

            if (hueco < 0)
            {
                Debug.LogError("[Hashi] No quedan capas libres en el proyecto, "
                               + "asi que falta '" + nombre + "'. Hay que "
                               + "liberar alguna a mano en Project Settings.");
                break;
            }

            capas.GetArrayElementAtIndex(hueco).stringValue = nombre;
            puestas.Add(nombre + " (" + hueco + ")");
        }

        tm.ApplyModifiedProperties();

        if (puestas.Count > 0)
        {
            Debug.Log("[Hashi] Capas nuevas: " + string.Join(", ", puestas));
        }
    }

    static bool Existe(SerializedProperty capas, string nombre)
    {
        for (int i = 0; i < capas.arraySize; i++)
        {
            if (capas.GetArrayElementAtIndex(i).stringValue == nombre) return true;
        }

        return false;
    }

    // De la 8 en adelante: de la 0 a la 7 son de Unity y sobrescribirlas rompe
    // cosas que no tienen nada que ver con este juego.
    static int PrimerHueco(SerializedProperty capas)
    {
        for (int i = 8; i < capas.arraySize; i++)
        {
            if (string.IsNullOrEmpty(capas.GetArrayElementAtIndex(i).stringValue)) return i;
        }

        return -1;
    }

    // ------------------------------------------------------- matriz de choques

    static void MatrizDeColisiones()
    {
        int maquina = Capa(Hashi.HashiLayers.NOMBRE_MAQUINA);
        int cristal = Capa(Hashi.HashiLayers.NOMBRE_CRISTAL);
        int garra = Capa(Hashi.HashiLayers.NOMBRE_GARRA);
        int premio = Capa(Hashi.HashiLayers.NOMBRE_PREMIO);
        int barras = Capa(Hashi.HashiLayers.NOMBRE_BARRAS);
        int bandeja = Capa(Hashi.HashiLayers.NOMBRE_BANDEJA);

        // Con una sola capa sin crear, IgnoreLayerCollision peta con un indice
        // -1 y deja la matriz a medias, que es peor que no tocarla.
        if (maquina < 0 || cristal < 0 || garra < 0
            || premio < 0 || barras < 0 || bandeja < 0)
        {
            Debug.LogError("[Hashi] Faltan capas, asi que dejo la matriz de "
                           + "colisiones como estaba.");
            return;
        }

        // La garra no choca con el mueble ni con el cristal: no le hace falta,
        // porque sus limites de recorrido ya la mantienen dentro. Dejandolo
        // encendido, el cuerpo cinematico se pelea con las paredes en cada
        // esquina y se queda vibrando.
        Physics.IgnoreLayerCollision(garra, maquina, true);
        Physics.IgnoreLayerCollision(garra, cristal, true);

        // La garra SI choca con las barras y con el premio: es el juego.
        Physics.IgnoreLayerCollision(garra, barras, false);
        Physics.IgnoreLayerCollision(garra, premio, false);

        // Las barras son fijas: entre ellas y contra el mueble no hay nada que
        // calcular.
        Physics.IgnoreLayerCollision(barras, barras, true);
        Physics.IgnoreLayerCollision(barras, maquina, true);
        Physics.IgnoreLayerCollision(barras, cristal, true);
        Physics.IgnoreLayerCollision(cristal, maquina, true);

        // El premio choca con todo lo que le puede parar: barras, mueble,
        // cristales y otros premios.
        Physics.IgnoreLayerCollision(premio, barras, false);
        Physics.IgnoreLayerCollision(premio, maquina, false);
        Physics.IgnoreLayerCollision(premio, cristal, false);
        Physics.IgnoreLayerCollision(premio, premio, false);

        // En el prefab del local, la carcasa pasa a la capa Placeable para que
        // el jugador pueda coger la maquina y moverla. Eso mueve de capa dos
        // piezas que SI le importan a la fisica de dentro:
        //
        //   - el suelo del cajon, que es donde aterriza el premio. Si deja de
        //     chocar, la caja cae por el hueco y sigue de largo hasta el vacio.
        //   - la carcasa entera, que la garra no tiene por que tocar.
        int placeable = LayerMask.NameToLayer("Placeable");

        if (placeable >= 0)
        {
            Physics.IgnoreLayerCollision(premio, placeable, false);
            Physics.IgnoreLayerCollision(garra, placeable, true);
        }

        // La bandeja es un detector y solo le interesa el premio. Con todo lo
        // demas apagado, no se cuelan avisos de la garra rozandola.
        for (int i = 0; i < 32; i++)
        {
            Physics.IgnoreLayerCollision(bandeja, i, i != premio);
        }

        AssetDatabase.SaveAssets();
    }

    static int Capa(string nombre)
    {
        int i = LayerMask.NameToLayer(nombre);

        if (i < 0)
        {
            Debug.LogError("[Hashi] Sigue faltando la capa '" + nombre + "'.");
        }

        return i;
    }

    // ------------------------------------------------------------- el montaje

    // Lo que sale de montar la maquina, para que quien la monte pueda seguir
    // enchufandole cosas sin volver a buscarlas por la jerarquia.
    public class Maquina
    {
        public GameObject raiz;
        public Hashi.MachineController controlador;
        public Hashi.ClawController garra;
        public Hashi.ClawFingerController pinzas;
        public Hashi.BarRig barras;
        public Hashi.DropZone zona;
        public Hashi.PrizeSpawner generador;
        public Hashi.AudioManager sonido;
        public Hashi.CreditsManager creditos;
        public Transform cuerpoGarra;
        public Light[] luces;
    }

    // El mueble y su fisica, y nada mas: ni camaras, ni interfaz, ni economia.
    //
    // Esa separacion es la que permite que la misma maquina sea un juego suelto
    // en su escena y un mueble comprable dentro del local. Si aqui se colara una
    // camara o un marcador de creditos, el prefab de la tienda los llevaria
    // dentro y habria que ir quitandolos a mano cada vez.
    static Maquina MontarMaquina(Dictionary<string, Material> mat,
                                 PrizeDefinitionLote lote, bool paraTienda,
                                 UnityEngine.SceneManagement.Scene? destino = null)
    {
        int capaMaquina = Capa(Hashi.HashiLayers.NOMBRE_MAQUINA);
        int capaCristal = Capa(Hashi.HashiLayers.NOMBRE_CRISTAL);
        int capaGarra = Capa(Hashi.HashiLayers.NOMBRE_GARRA);
        int capaBarras = Capa(Hashi.HashiLayers.NOMBRE_BARRAS);
        int capaBandeja = Capa(Hashi.HashiLayers.NOMBRE_BANDEJA);

        GameObject maquina = new GameObject("Machine");

        // Se muda a la escena de destino ANTES de colgarle nada. Todo lo que se
        // cree despues sera hijo suyo y se ira con el; moviendolo al final,
        // las cien piezas nacerian en la escena del jugador.
        if (destino.HasValue)
        {
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                maquina, destino.Value);
        }

        // ----------------------------------------------------------- carcasa
        GameObject cabinet = HashiPiezas.Vacio("Cabinet", maquina.transform, Vector3.zero);

        // El pedestal va HUECO: cinco caras en vez de un bloque macizo.
        //
        // Antes era un cubo lleno, y por eso el premio se quedaba donde caia: no
        // habia por donde bajar. Vaciandolo, la caja atraviesa el mueble y llega
        // a la bandeja de abajo por su propio peso, sin que nadie la mueva.
        Pedestal(cabinet.transform, mat, capaMaquina);

        // Trasera opaca: en las de verdad lleva el cartel del premio.
        HashiPiezas.Cubo("ParedTrasera", cabinet.transform,
                         new Vector3(0f, INT_ALTO * 0.5f, INT_Z + GROSOR_CRISTAL * 0.5f),
                         new Vector3(INT_X * 2f + 0.04f, INT_ALTO, GROSOR_CRISTAL),
                         mat["Azul"], true, capaMaquina);

        // Cuatro postes de esquina.
        foreach (int sx in new[] { -1, 1 })
        {
            foreach (int sz in new[] { -1, 1 })
            {
                GameObject poste = HashiPiezas.Cubo(
                    "Poste_" + (sx < 0 ? "I" : "D") + (sz < 0 ? "F" : "T"),
                    cabinet.transform,
                    new Vector3(sx * (INT_X + 0.012f), INT_ALTO * 0.5f, sz * (INT_Z + 0.012f)),
                    new Vector3(0.024f, INT_ALTO + 0.02f, 0.024f),
                    mat["Cromo"], false, capaMaquina);

                HashiPiezas.SinSombras(poste);
            }
        }

        // La bandeja de recogida y su boca, dentro del pedestal.
        Bandeja(cabinet.transform, mat, capaMaquina);

        // ------------------------------------------------------- suelo y techo
        // El suelo del cajon es una RAMPA, y solo cubre la mitad de atras. La
        // mitad de delante esta abierta: es el agujero por el que se va el
        // premio. La rampa esta para que una caja que caiga hacia el fondo no se
        // quede ahi muerta, sino que ruede hacia el hueco.
        //
        // El agujero mide 36 cm de fondo, mas que la caja mas grande (26). Si
        // fuera mas estrecho la caja haria puente sobre el y se quedaria
        // encallada justo despues de haberla ganado, que es la peor manera
        // posible de perder un premio.
        GameObject bottom = Rampa("BottomFrame", maquina.transform, mat, capaMaquina);

        GameObject top = HashiPiezas.Cubo("TopFrame", maquina.transform,
            new Vector3(0f, INT_ALTO + 0.015f, 0f),
            new Vector3(INT_X * 2f + 0.08f, 0.03f, INT_Z * 2f + 0.08f),
            mat["Blanco"], true, capaMaquina);

        // Tiras de LED bajo el techo. Son las que dan el aire de recreativa.
        HashiPiezas.Cubo("LED_Frente", top.transform, new Vector3(0f, -0.55f, -0.36f),
                         new Vector3(0.92f, 0.35f, 0.03f), mat["LEDRosa"], false, capaMaquina);

        HashiPiezas.Cubo("LED_Fondo", top.transform, new Vector3(0f, -0.55f, 0.36f),
                         new Vector3(0.92f, 0.35f, 0.03f), mat["LEDAzul"], false, capaMaquina);

        // Marquesina de arriba.
        GameObject marq = HashiPiezas.Cubo("Marquesina", maquina.transform,
            new Vector3(0f, INT_ALTO + 0.15f, 0.10f),
            new Vector3(INT_X * 2f - 0.04f, 0.24f, 0.16f), mat["Marquesina"], false, capaMaquina);

        HashiPiezas.Cubo("Marquesina_Banda", marq.transform, new Vector3(0f, -0.42f, -0.52f),
                         new Vector3(1.01f, 0.16f, 0.02f), mat["Rosa"], false, capaMaquina);

        HashiPiezas.SinSombras(marq);

        // ------------------------------------------------------------ cristal
        // El cristal empieza a la altura de las barras, no en el suelo. Lo de
        // debajo es el hueco de caida y tiene que estar abierto por delante: es
        // por donde se ve el premio bajar hacia la bandeja.
        //
        // No deja escapar nada antes de tiempo: la caja apoyada en las barras
        // tiene su base a 0,308 y el hueco acaba en 0,30. Solo puede salir por
        // ahi una caja que ya se haya caido.
        float altoCristal = INT_ALTO - BARRA_ALTURA;

        GameObject frente = HashiPiezas.Cubo("GlassFront", maquina.transform,
            new Vector3(0f, BARRA_ALTURA + altoCristal * 0.5f,
                        -(INT_Z + GROSOR_CRISTAL * 0.5f)),
            new Vector3(INT_X * 2f, altoCristal, GROSOR_CRISTAL),
            mat["Cristal"], true, capaCristal);

        GameObject lados = HashiPiezas.Vacio("GlassSides", maquina.transform, Vector3.zero);

        foreach (int s in new[] { -1, 1 })
        {
            HashiPiezas.Cubo(s < 0 ? "Glass_Izq" : "Glass_Der", lados.transform,
                new Vector3(s * (INT_X + GROSOR_CRISTAL * 0.5f),
                            BARRA_ALTURA + altoCristal * 0.5f, 0f),
                new Vector3(GROSOR_CRISTAL, altoCristal, INT_Z * 2f),
                mat["Cristal"], true, capaCristal);
        }

        // ----------------------------------------------------------- el faldon
        // Chapa maciza desde el suelo hasta la altura de las barras, por delante
        // y por los lados.
        //
        // Es lo que hace que la maquina se vea CERRADA. Con cristal hasta abajo
        // se veian las tripas: el hueco de caida, el interior del pedestal y,
        // por la boca del premio, la pared de enfrente. Ademas es como son las de
        // verdad, y por un motivo: el premio tiene que desaparecer al caer y
        // reaparecer en la puerta, no verse rodar por dentro del mueble.
        GameObject faldon = HashiPiezas.Vacio("Faldon", maquina.transform, Vector3.zero);

        HashiPiezas.Cubo("Faldon_Frente", faldon.transform,
            new Vector3(0f, BARRA_ALTURA * 0.5f, -(INT_Z + GROSOR_CRISTAL * 0.5f)),
            new Vector3(INT_X * 2f + 0.02f, BARRA_ALTURA, GROSOR_CRISTAL * 1.6f),
            mat["Blanco"], true, capaMaquina);

        foreach (int s in new[] { -1, 1 })
        {
            HashiPiezas.Cubo(s < 0 ? "Faldon_Izq" : "Faldon_Der", faldon.transform,
                new Vector3(s * (INT_X + GROSOR_CRISTAL * 0.5f),
                            BARRA_ALTURA * 0.5f, 0f),
                new Vector3(GROSOR_CRISTAL * 1.6f, BARRA_ALTURA, INT_Z * 2f),
                mat["Blanco"], true, capaMaquina);
        }

        // Una banda rosa arriba del faldon, justo bajo las barras, para que la
        // union entre chapa y cristal sea una linea a proposito y no un corte.
        HashiPiezas.Cubo("Faldon_Banda", faldon.transform,
            new Vector3(0f, BARRA_ALTURA - 0.015f, -(INT_Z + 0.012f)),
            new Vector3(INT_X * 2f + 0.03f, 0.03f, 0.012f),
            mat["Rosa"], false, capaMaquina);

        // El cristal no proyecta sombra: si la proyecta, dentro de la maquina se
        // ve una mancha rectangular que parece un fallo de iluminacion.
        HashiPiezas.SinSombras(frente);
        HashiPiezas.SinSombras(lados);

        // ---------------------------------------------------------- las barras
        GameObject area = HashiPiezas.Vacio("PrizeArea", maquina.transform, Vector3.zero);

        GameObject izq = Barra("LeftBar", area.transform, mat["Cromo"], capaBarras);
        GameObject der = Barra("RightBar", area.transform, mat["Cromo"], capaBarras);

        GameObject spawn = HashiPiezas.Vacio("PrizeSpawn", area.transform,
            new Vector3(0f, BARRA_ALTURA + BARRA_RADIO, 0f));

        // El detector va DENTRO de la bandeja de recogida, no debajo de las
        // barras. Antes cubria todo el hueco de caida, y con eso bastaba con que
        // la caja pasase entre las barras para cobrar: se ganaba en el aire.
        //
        // Ahora la caja tiene que caer, rebotar en la rampa de la bandeja,
        // resbalar y quedarse quieta ahi abajo. Todo el recorrido es fisica; el
        // detector solo mira el final. Es generoso de ancho a proposito: la
        // bandeja entera cuenta, no un punto exacto, porque una caja que llega y
        // se queda en una esquina sigue siendo un premio ganado.
        GameObject bandeja = HashiPiezas.Vacio("DropZone", area.transform,
            new Vector3(0f, BANDEJA_SUELO + 0.11f, -FRENTE * 0.45f));

        bandeja.layer = capaBandeja;

        BoxCollider trigger = bandeja.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(BOCA_ANCHO * 2f + 0.04f, 0.22f, FRENTE + 0.14f);

        var barras = area.AddComponent<Hashi.BarRig>();
        var zona = bandeja.AddComponent<Hashi.DropZone>();
        var generador = area.AddComponent<Hashi.PrizeSpawner>();

        using (var a = new HashiCableado(barras))
        {
            a.Obj("leftBar", izq.transform).Obj("rightBar", der.transform)
             .Num("barDistance", BARRA_SEPARACION).Num("barRadius", BARRA_RADIO)
             .Num("barHeight", BARRA_ALTURA).Num("barLength", BARRA_LARGO);
        }

        barras.Aplicar();

        using (var a = new HashiCableado(zona))
        {
            a.Obj("barras", barras).Num("tiempoConfirmacion", 0.45f)
             .Num("holgura", 0.01f).Bul("exigirQuietud", true);
        }

        using (var a = new HashiCableado(generador))
        {
            a.Obj("prefab", lote.prefabPremio).Obj("puntoDeAparicion", spawn.transform)
             .Obj("barras", barras).Ent("modeloInicial", 1)
             .Bul("generarAlArrancar", !paraTienda).Num("holguraApoyo", 0.002f);

            a.Lista("modelos", lote.premios);
        }

        // ------------------------------------------------------------ la garra
        GameObject sistema = HashiPiezas.Vacio("ClawSystem", maquina.transform, Vector3.zero);

        // Adorno: puente, carro y eje. Los mueve ClawController por escritura
        // directa, sin fisica ninguna.
        // Los dos rieles FIJOS del techo, uno delante y otro al fondo, sobre los
        // que corre el puente. No se mueven nunca, asi que cuelgan de ClawSystem
        // pero el controlador no los toca.
        //
        // Faltaban, y se notaba: el puente iba de lado a lado sin nada debajo,
        // como suspendido en el aire. Media sensacion de maquina esta en ver por
        // donde corre cada cosa.
        float rielZ = INT_Z - 0.03f;

        GameObject rieles = HashiPiezas.Vacio("Rieles", sistema.transform,
            new Vector3(0f, CARRIL_ALTURA, 0f));

        foreach (int s in new[] { -1, 1 })
        {
            HashiPiezas.Cubo(s < 0 ? "Riel_Frente" : "Riel_Fondo", rieles.transform,
                new Vector3(0f, 0.022f, s * rielZ),
                new Vector3(INT_X * 2f + 0.02f, 0.020f, 0.026f),
                mat["Cromo"], false, capaGarra);
        }

        HashiPiezas.SinSombras(rieles);

        // El puente: cruza de riel a riel y corre de lado a lado.
        GameObject railX = HashiPiezas.Vacio("RailX", sistema.transform,
            new Vector3(0f, CARRIL_ALTURA, 0f));

        HashiPiezas.Cubo("Puente", railX.transform, new Vector3(0f, 0.022f, 0f),
            new Vector3(0.042f, 0.026f, rielZ * 2f), mat["Cromo"], false, capaGarra);

        // Los patines de los extremos, que son los que se ven apoyados en el
        // riel y hacen entender de un vistazo que el puente corre por ahi.
        foreach (int s in new[] { -1, 1 })
        {
            HashiPiezas.Cubo(s < 0 ? "Patin_Frente" : "Patin_Fondo", railX.transform,
                new Vector3(0f, 0.022f, s * rielZ),
                new Vector3(0.07f, 0.042f, 0.05f), mat["MetalOsc"], false, capaGarra);
        }

        // El carro, que corre por el puente de delante a atras.
        GameObject railZ = HashiPiezas.Vacio("RailZ", railX.transform, Vector3.zero);

        HashiPiezas.Cubo("Carro", railZ.transform, new Vector3(0f, 0.018f, 0f),
            new Vector3(0.075f, 0.055f, 0.085f), mat["MetalOsc"], false, capaGarra);

        HashiPiezas.Cilindro("Polea", railZ.transform, new Vector3(0f, 0.030f, 0f),
            0.034f, 0.05f, new Vector3(0f, 0f, 90f), mat["Cromo"], false, capaGarra);

        GameObject eje = HashiPiezas.Cuerpo("VerticalAxis", railZ.transform, Vector3.zero,
            PrimitiveType.Cylinder, new Vector3(0.012f, 0.06f, 0.012f),
            new Vector3(0f, -0.06f, 0f), mat["Cromo"], capaGarra);

        HashiPiezas.SinSombras(railX);

        // Cuerpo de la garra: cinematico, y todo lo que cuelga de el son
        // cuerpos de verdad unidos por bisagras.
        Vector3 posCuerpo = new Vector3(GARRA_SALIDA.x, GARRA_REPOSO, GARRA_SALIDA.y);

        // El ovalo: una esfera aplastada, no un cubo. Es la silueta que tiene la
        // maquina de verdad y se reconoce de lejos.
        GameObject cuerpo = HashiPiezas.Cuerpo("ClawBody", sistema.transform, posCuerpo,
            PrimitiveType.Sphere, CUERPO_TAMANO, Vector3.zero, mat["Aluminio"], capaGarra);

        // La burbuja de arriba, con dos luces dentro. En la maquina real es una
        // cupula transparente con las tripas a la vista.
        GameObject cupula = HashiPiezas.Cuerpo("Cupula", cuerpo.transform,
            new Vector3(0f, 0.012f, 0f), PrimitiveType.Sphere,
            new Vector3(0.175f, 0.085f, 0.115f), Vector3.zero, mat["CristalOsc"], capaGarra);

        HashiPiezas.Cuerpo("Tripa_Rosa", cupula.transform, new Vector3(-0.03f, 0f, 0f),
            PrimitiveType.Sphere, new Vector3(0.05f, 0.03f, 0.04f), Vector3.zero,
            mat["Rosa"], capaGarra);

        HashiPiezas.Cuerpo("Tripa_Azul", cupula.transform, new Vector3(0.03f, 0f, 0.01f),
            PrimitiveType.Sphere, new Vector3(0.05f, 0.03f, 0.04f), Vector3.zero,
            mat["Azul"], capaGarra);

        // El piloto rojo de la punta.
        HashiPiezas.Cuerpo("Piloto", cuerpo.transform, new Vector3(0.095f, 0f, 0f),
            PrimitiveType.Sphere, new Vector3(0.045f, 0.045f, 0.045f), Vector3.zero,
            mat["Rosa"], capaGarra);

        // El cuello por el que cuelga del cable.
        HashiPiezas.Cilindro("Cuello", cuerpo.transform, new Vector3(0f, 0.05f, 0f),
            0.022f, 0.05f, Vector3.zero, mat["MetalOsc"], false, capaGarra);

        HashiPiezas.SinSombras(cupula);

        Rigidbody rbCuerpo = cuerpo.AddComponent<Rigidbody>();
        rbCuerpo.isKinematic = true;
        rbCuerpo.useGravity = false;
        rbCuerpo.interpolation = RigidbodyInterpolation.Interpolate;

        // La caja va INSCRITA en el ovalo, no ajustada a su bulto: una caja del
        // tamano completo sacaria las esquinas fuera de la silueta y la garra
        // empujaria la caja con aire, con un centimetro de separacion visible.
        BoxCollider colCuerpo = cuerpo.AddComponent<BoxCollider>();
        colCuerpo.size = CUERPO_TAMANO * 0.78f;

        // Delante y detras, no izquierda y derecha: las pinzas abren en
        // profundidad porque es en profundidad donde esta el hueco.
        HingeJoint dedoIzq = Dedo("LeftFinger", sistema.transform, posCuerpo, -1,
                                  rbCuerpo, mat, capaGarra);

        HingeJoint dedoDer = Dedo("RightFinger", sistema.transform, posCuerpo, 1,
                                  rbCuerpo, mat, capaGarra);

        var garra = sistema.AddComponent<Hashi.ClawController>();
        var pinzas = sistema.AddComponent<Hashi.ClawFingerController>();

        using (var a = new HashiCableado(garra))
        {
            a.Obj("railX", railX.transform).Obj("railZ", railZ.transform)
             .Obj("verticalAxis", eje.transform).Obj("clawBody", rbCuerpo)
             .Num("limitXMin", -GARRA_LIMITE_X).Num("limitXMax", GARRA_LIMITE_X)
             .Num("limitZMin", -GARRA_LIMITE_Z).Num("limitZMax", GARRA_LIMITE_Z)
             .Num("alturaCarril", CARRIL_ALTURA).Num("alturaReposo", GARRA_REPOSO)
             .Num("alturaMinima", GARRA_MINIMA).V2("posicionInicial", GARRA_SALIDA);
        }

        using (var a = new HashiCableado(pinzas))
        {
            a.Obj("clawBody", rbCuerpo).Obj("leftFinger", dedoIzq)
             .Obj("rightFinger", dedoDer)
             .Num("closedAngle", DEDO_ANGULO_CERRADO)
             .Num("openAngle", DEDO_ANGULO_ABIERTO);
        }

        // El motor y su mando. A partir de aqui el par de cada partida lo decide
        // el cuadro trasero, no un numero fijo en las pinzas.
        var fuerza = sistema.AddComponent<Hashi.ClawStrength>();

        using (var a = new HashiCableado(fuerza))
            a.Obj("pinzas", pinzas).Obj("bandeja", zona).Num("ajuste", 0.35f);

        using (var a = new HashiCableado(dedoIzq.GetComponent<Hashi.ClawFingerContact>()))
            a.Obj("pinzas", pinzas);

        using (var a = new HashiCableado(dedoDer.GetComponent<Hashi.ClawFingerContact>()))
            a.Obj("pinzas", pinzas);

        // ------------------------------------------------------------- mandos
        GameObject mandos = HashiPiezas.Vacio("PlayerControls", maquina.transform,
            new Vector3(0f, -0.10f, -FRENTE - 0.09f));

        Mandos(mandos.transform, mat, capaMaquina);

        // ------------------------------------------------- cuadro de servicio
        // Detras, mirando a la pared, igual que el de la maquina de garra: el
        // mando de la fuerza es del dueno. Delante seria dejar que el cliente se
        // regule a si mismo lo dificil que se lo pones.
        CuadroDeFuerza(maquina.transform, fuerza, mat, capaMaquina);

        // ------------------------------------------------------------ monedero
        GameObject monedero = HashiPiezas.Vacio("CoinSystem", maquina.transform,
            new Vector3(LATERAL - 0.16f, -0.26f, -FRENTE - 0.012f));

        HashiPiezas.Cubo("Monedero", monedero.transform, Vector3.zero,
            new Vector3(0.10f, 0.16f, 0.02f), mat["MetalOsc"], false, capaMaquina);

        HashiPiezas.Cubo("Ranura", monedero.transform, new Vector3(0f, 0.03f, -0.011f),
            new Vector3(0.006f, 0.05f, 0.006f), mat["Negro"], false, capaMaquina);

        // Las fichas son cosa de la escena de pruebas. Dentro del local se paga
        // con el dinero del negocio, y una maquina con dos monederos a la vez
        // seria una manera muy tonta de jugar gratis.
        Hashi.CreditsManager creditos = null;

        if (!paraTienda)
        {
            creditos = monedero.AddComponent<Hashi.CreditsManager>();

            using (var a = new HashiCableado(creditos))
                a.Ent("creditosIniciales", 5).Ent("precioPorPartida", 1).Ent("maximo", 99);
        }

        // -------------------------------------------------------------- luces
        // Todas las luces del interior salen del techo y alumbran hacia abajo,
        // como en la maquina de verdad. Antes habia dos bombillas sueltas a
        // media altura y se notaba: iluminaban los cristales de lado y dejaban
        // el premio, que es lo unico que hay que mirar, en penumbra.
        GameObject luces = HashiPiezas.Vacio("Luces", maquina.transform, Vector3.zero);

        Light foco = FocoAbajo(luces.transform, "Foco_Centro",
            new Vector3(0f, INT_ALTO - 0.03f, 0f),
            new Color(1f, 0.97f, 0.92f), 2.0f, 1.7f, 110f);

        // El unico con sombras. Tres luces con sombra dentro de una caja de 80
        // cm cuestan el triple y no se distingue ninguna.
        foco.shadows = LightShadows.Soft;

        Light rosa = FocoAbajo(luces.transform, "Foco_Rosa",
            new Vector3(-0.24f, INT_ALTO - 0.05f, -0.17f),
            new Color(1f, 0.45f, 0.75f), 1.2f, 1.3f, 85f);

        Light azul = FocoAbajo(luces.transform, "Foco_Azul",
            new Vector3(0.24f, INT_ALTO - 0.05f, 0.17f),
            new Color(0.40f, 0.65f, 1f), 1.2f, 1.3f, 85f);

        // -------------------------------------------------------------- estados
        var controlador = maquina.AddComponent<Hashi.MachineController>();

        using (var a = new HashiCableado(controlador))
        {
            a.Obj("garra", garra).Obj("pinzas", pinzas).Obj("fuerza", fuerza)
             .Num("tiempoTurno", 25f).Num("esperaAntesDeBajar", 1f).Num("esperaAbajo", 0.6f)
             .Num("esperaArriba", 0.3f).Bul("abrirSoloAlLlegarArriba", true)
             .Bul("controlesBloqueados", true)
             .Ent("mando", paraTienda ? 1 : 0);
        }

        // --------------------------------------------------------------- audio
        GameObject audio = HashiPiezas.Vacio("Audio", maquina.transform, Vector3.zero);

        AudioSource golpes = audio.AddComponent<AudioSource>();
        golpes.playOnAwake = false;

        // Dentro del local el sonido sale de la maquina, porque es un mueble en
        // una sala con mas cosas y tiene que oirse desde donde esta. En su
        // escena suelta no hay sala ni paseo, asi que va plano.
        golpes.spatialBlend = paraTienda ? 1f : 0f;
        golpes.rolloffMode = AudioRolloffMode.Linear;
        golpes.maxDistance = 12f;

        GameObject motorGo = HashiPiezas.Vacio("Motor", audio.transform, Vector3.zero);
        AudioSource motor = motorGo.AddComponent<AudioSource>();
        motor.playOnAwake = false;
        motor.loop = true;
        motor.volume = 0f;
        motor.spatialBlend = golpes.spatialBlend;
        motor.rolloffMode = AudioRolloffMode.Linear;
        motor.maxDistance = 12f;

        var sonido = audio.AddComponent<Hashi.AudioManager>();

        // Sin "juego": el GameManager de la maquina de puente solo existe en su
        // escena de pruebas, y se enchufa despues en Completar().
        using (var a = new HashiCableado(sonido))
        {
            a.Obj("maquina", controlador).Obj("garra", garra).Obj("pinzas", pinzas)
             .Obj("generador", generador).Obj("bandeja", zona)
             .Obj("fuente", golpes).Obj("motor", motor);
        }

        var m = new Maquina
        {
            raiz = maquina,
            controlador = controlador,
            garra = garra,
            pinzas = pinzas,
            barras = barras,
            zona = zona,
            generador = generador,
            sonido = sonido,
            creditos = creditos,
            cuerpoGarra = cuerpo.transform,
            luces = new[] { rosa, azul },
        };

        return m;
    }

    // ----------------------------------------------------- la version de tienda

    // Le pone lo que necesita para ser un mueble mas del local: el disparador
    // del cartel "E: jugar", el precio, el cobro del premio y la capa que mira
    // el sistema de colocacion.
    //
    // Todo va en la raiz del prefab y no en la maquina, que es como esta la de
    // garra: ClawMachineInteraction necesita el trigger en SU MISMO GameObject,
    // y HoldToPickup sube por los padres buscando el PlaceableObject. Repartirlo
    // en dos sitios distintos funciona hasta que alguien mueve algo.
    static void PrepararParaLaTienda(Maquina m, GameObject raiz)
    {
        int capaPlaceable = LayerMask.NameToLayer("Placeable");

        // HoldToPickup lanza un rayo contra la capa Placeable y desde el
        // collider que toca sube buscando el PlaceableObject. Si ningun collider
        // del mueble esta en esa capa, la maquina no se puede ni coger ni mover
        // y no hay nada en pantalla que explique por que.
        if (capaPlaceable >= 0)
        {
            // Por PREFIJO, no por lista de nombres exactos. La lista exacta ya
            // se quedo obsoleta una vez al partir el pedestal macizo en cinco
            // caras: seguia compilando, no daba ningun aviso, y la maquina
            // simplemente dejaba de poder cogerse.
            string[] carcasa = { "Pedestal", "TopFrame", "ParedTrasera", "Bandeja_",
                                 "Faldon", "Marquesina" };

            foreach (Transform t in m.raiz.GetComponentsInChildren<Transform>(true))
            {
                foreach (string prefijo in carcasa)
                {
                    if (!t.name.StartsWith(prefijo)) continue;

                    t.gameObject.layer = capaPlaceable;
                    break;
                }
            }
        }

        // El volumen de delante donde salta el cartel. Delante y no envolviendo
        // la maquina entera: envolviendola, el aviso salta tambien al pasar por
        // detras, que es lo que hace que un local se sienta ruidoso.
        BoxCollider trigger = raiz.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(1.30f, 2.00f, 1.10f);
        trigger.center = new Vector3(0f, 0.80f, -0.75f);

        var interaccion = raiz.AddComponent<Hashi.HashiMachineInteraction>();

        using (var a = new HashiCableado(interaccion))
            a.Obj("maquina", m.controlador).Num("cost", 5f).Num("esperaMoneda", 0.8f);

        MachinePricing precio = raiz.AddComponent<MachinePricing>();
        precio.price = 5f;
        precio.recommendedPrice = 5f;
        precio.competitionPrice = 5f;

        raiz.AddComponent<PlaceableObject>();

        var pago = raiz.AddComponent<Hashi.HashiPrizePayout>();

        using (var a = new HashiCableado(pago))
        {
            a.Obj("bandeja", m.zona).Obj("generador", m.generador)
             .Num("valorPremio", 90f / 5f).Bul("reponer", false).Num("esperaReponer", 5f);
        }
    }

    static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == nombre) return t;
        }

        return null;
    }

    // ---------------------------------------------- lo que solo va en su escena

    // Camaras, marcador, interfaz y economia de fichas. Todo esto es lo que
    // convierte el mueble en un juego suelto, y es justo lo que NO puede llevar
    // dentro el prefab de la tienda.
    static void Completar(Maquina m, PrizeDefinitionLote lote)
    {
        GameObject maquina = m.raiz;

        // ------------------------------------------------------------ camaras
        GameObject camaras = HashiPiezas.Vacio("Camera", maquina.transform, Vector3.zero);

        Transform mira = HashiPiezas.Vacio("LookTarget", camaras.transform,
            new Vector3(0f, BARRA_ALTURA + 0.04f, 0f)).transform;

        Transform pFrente = HashiPiezas.Vacio("CameraFront", camaras.transform,
            new Vector3(0f, 0.60f, -1.55f)).transform;

        Transform pAngulo = HashiPiezas.Vacio("CameraAngled", camaras.transform,
            new Vector3(0.95f, 1.10f, -1.25f)).transform;

        Transform pCenital = HashiPiezas.Vacio("CameraTop", camaras.transform,
            new Vector3(0f, 1.85f, -0.40f)).transform;

        GameObject camGo = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
        camGo.transform.SetParent(camaras.transform, false);
        camGo.tag = "MainCamera";
        camGo.transform.position = pFrente.position;
        camGo.transform.LookAt(mira);

        Camera cam = camGo.GetComponent<Camera>();
        cam.fieldOfView = 42f;
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 60f;

        var camCtrl = camaras.AddComponent<Hashi.CameraController>();

        using (var a = new HashiCableado(camCtrl))
        {
            a.Obj("camara", cam).Obj("puntoDeMira", mira).Obj("garra", m.cuerpoGarra)
             .Num("suavizado", 0.18f).Bul("seguirGarra", true)
             .Num("intensidadSeguimiento", 0.15f).Num("desplazamientoMaximo", 0.12f);

            a.Lista("posiciones", pFrente, pAngulo, pCenital);
        }

        // ------------------------------------------------------------ economia
        var juego = maquina.AddComponent<Hashi.GameManager>();

        using (var a = new HashiCableado(juego))
        {
            a.Obj("maquina", m.controlador).Obj("creditos", m.creditos)
             .Obj("bandeja", m.zona).Obj("generador", m.generador)
             .Obj("barras", m.barras)
             .Ent("dificultadInicial", 1).Num("esperaTrasGanar", 3.5f)
             .Bul("reponerPremio", true).Bul("modoDepuracion", false);

            a.Lista("dificultades", lote.dificultades);
        }

        using (var a = new HashiCableado(m.sonido))
            a.Obj("juego", juego);

        // ------------------------------------------------------------ interfaz
        HashiUIBuilder.Refs ui = HashiUIBuilder.Construir(maquina.transform);

        using (var a = new HashiCableado(ui.gestor))
        {
            a.Obj("juego", juego).Obj("creditos", m.creditos).Obj("maquina", m.controlador)
             .Obj("camaras", camCtrl).Obj("sonido", m.sonido)
             .Obj("textoCreditos", ui.creditos).Obj("textoPremios", ui.premios)
             .Obj("textoTiempo", ui.tiempo).Obj("textoEstado", ui.estado)
             .Obj("textoMensaje", ui.mensaje).Obj("textoDepuracion", ui.depuracion)
             .Obj("botonStart", ui.start).Obj("botonReset", ui.reset)
             .Obj("botonCamara", ui.camara).Obj("botonSonido", ui.sonido)
             .Obj("botonCredito", ui.credito)
             .Num("duracionMensaje", 2.5f);
        }

        ParticleSystem confeti = Confeti(maquina.transform);

        var fiesta = ui.canvas.gameObject.AddComponent<Hashi.WinEffects>();

        using (var a = new HashiCableado(fiesta))
        {
            a.Obj("juego", juego).Obj("confeti", confeti).Obj("cartel", ui.mensaje)
             .Num("duracion", 3.5f);

            a.Lista("luces", m.luces[0], m.luces[1]);
        }
    }


    // --------------------------------------------------------------- piezas

    // Una barra: raiz sin escalar con la capsula, malla dentro. Las medidas las
    // pone BarRig al aplicar, aqui solo se monta el esqueleto.
    static GameObject Barra(string nombre, Transform padre, Material material, int capa)
    {
        GameObject barra = HashiPiezas.Cuerpo(nombre, padre, Vector3.zero,
            PrimitiveType.Cylinder, Vector3.one, Vector3.zero, material, capa);

        CapsuleCollider col = barra.AddComponent<CapsuleCollider>();
        col.direction = 0;                 // eje X: de lado a lado
        col.radius = BARRA_RADIO;
        col.height = BARRA_LARGO;

        // Rozamiento propio de la barra: acero pulido. Combinado con el de la
        // caja (Average) sale el rozamiento real del contacto, que es lo que
        // decide si la caja pivota o resbala.
        PhysicsMaterial fisica = new PhysicsMaterial(nombre + "_Fisica")
        {
            dynamicFriction = 0.30f,
            staticFriction = 0.35f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Average,
            bounceCombine = PhysicsMaterialCombine.Minimum,
        };

        HashiPiezas.AsegurarCarpeta(HashiMateriales.CARPETA);

        // ".physicMaterial", sin la ese, aunque la clase se llame ahora
        // PhysicsMaterial. Unity 6 renombro el tipo pero no la extension: los
        // materiales que trae el propio editor siguen siendo .physicMaterial.
        // Con la extension equivocada se crea el archivo igual, pero Unity no lo
        // reconoce como asset y las barras se quedan sin rozamiento sin avisar.
        string ruta = HashiMateriales.CARPETA + "/" + nombre + "_Fisica.physicMaterial";

        PhysicsMaterial guardado = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ruta);

        if (guardado == null)
        {
            AssetDatabase.CreateAsset(fisica, ruta);
            guardado = fisica;
        }
        else
        {
            Object.DestroyImmediate(fisica);
        }

        col.sharedMaterial = guardado;

        return barra;
    }

    // Un brazo: cuerpo propio, collider, bisagra al cuerpo de la garra y rele de
    // contacto. El lado (-1 delante, +1 detras) invierte el eje de giro para que
    // un mismo signo de motor cierre los dos.
    //
    // No es una varilla recta: es una PLETINA EN CHEVRON. Sale del eje hacia
    // fuera hasta un vertice, y del vertice vuelve hacia dentro hasta la punta,
    // que queda otra vez debajo del eje. Los dos brazos juntos dibujan el rombo
    // que se ve en las fotos de la maquina.
    //
    // La forma no es solo estetica. El vertice de fuera es lo que hace que el
    // brazo baje por fuera de la caja aunque la punta acabe dentro de su
    // vertical, y el gancho del final es lo unico que puede meterse por debajo
    // del canto.
    static HingeJoint Dedo(string nombre, Transform padre, Vector3 posCuerpo, int lado,
                           Rigidbody cuerpo, Dictionary<string, Material> mat, int capa)
    {
        Vector3 pos = posCuerpo + new Vector3(lado * DEDO_SEPARACION, -0.015f, 0f);

        // El brazo es un objeto vacio; la forma la ponen los tramos, y cada
        // tramo lleva su propio collider. Los colliders de los hijos se cuelgan
        // solos del Rigidbody del padre, asi que el brazo entero es una sola
        // pieza rigida sin necesidad de mas articulaciones.
        GameObject dedo = HashiPiezas.Vacio(nombre, padre, pos);
        dedo.layer = capa;

        // Fuera es alejarse del centro de la maquina, o sea al reves para cada
        // brazo: de ahi el "lado" en la X del vertice y el "-lado" en la del
        // gancho, que va hacia dentro.
        Vector3 eje = Vector3.zero;
        Vector3 vertice = new Vector3(lado * PIE_SALIENTE, -PIE_ALTURA_VERTICE, 0f);
        Vector3 punta = new Vector3(0f, -DEDO_LARGO, 0f);
        Vector3 gancho = new Vector3(-lado * PIE_LARGO, -DEDO_LARGO - 0.010f, 0f);

        Tramo("Brazo_Alto", dedo.transform, eje, vertice, mat["MetalOsc"], capa);
        Tramo("Brazo_Bajo", dedo.transform, vertice, punta, mat["MetalOsc"], capa);
        Tramo("Garra", dedo.transform, punta, gancho, mat["Cromo"], capa);

        // El remache del vertice: en las fotos es un circulo blanco bien visible
        // y es lo que remata la silueta.
        GameObject remache = HashiPiezas.Cuerpo("Remache", dedo.transform, vertice,
            PrimitiveType.Cylinder, new Vector3(0.020f, DEDO_GROSOR * 0.8f, 0.020f),
            Vector3.zero, mat["Blanco"], capa);

        remache.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Rigidbody rb = dedo.AddComponent<Rigidbody>();
        rb.mass = DEDO_MASA;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        HingeJoint bisagra = dedo.AddComponent<HingeJoint>();
        bisagra.connectedBody = cuerpo;
        bisagra.anchor = Vector3.zero;

        // Eje Z: los brazos abren de lado a lado, en paralelo a las barras.
        //
        // El signo esta comprobado, no copiado: girando sobre +Z, un punto que
        // cuelga hacia abajo se va hacia +X. El brazo izquierdo (lado -1) tiene
        // que abrir hacia -X, asi que le toca -Z. Con el signo cambiado, "abrir"
        // junta las puntas y "cerrar" las separa, y la maquina hace exactamente
        // lo contrario de lo que dice sin dar ningun error.
        bisagra.axis = new Vector3(0f, 0f, lado < 0 ? -1f : 1f);
        bisagra.useLimits = true;
        bisagra.limits = new JointLimits
        {
            min = DEDO_ANGULO_CERRADO,
            max = DEDO_ANGULO_ABIERTO,
        };

        bisagra.useMotor = true;
        bisagra.enableCollision = false;

        dedo.AddComponent<Hashi.ClawFingerContact>();

        return bisagra;
    }

    // El pedestal, hueco: cinco caras y la boca de recogida recortada al frente.
    //
    // El frente va en tres trozos (encima de la boca, debajo, y dos jambas) en
    // vez de una cara con un agujero, porque un BoxCollider no tiene agujeros.
    // Es la manera de que exista un hueco por el que se pueda meter la mano y
    // por el que quepa el premio.
    static void Pedestal(Transform padre, Dictionary<string, Material> mat, int capa)
    {
        float medio = -PEDESTAL_ALTO * 0.5f;
        float bocaArriba = BANDEJA_SUELO + BOCA_ALTO;

        HashiPiezas.Cubo("Pedestal_Trasera", padre, new Vector3(0f, medio, FRENTE),
            new Vector3(LATERAL * 2f, PEDESTAL_ALTO, 0.03f), mat["Blanco"], true, capa);

        foreach (int s in new[] { -1, 1 })
        {
            HashiPiezas.Cubo(s < 0 ? "Pedestal_Izq" : "Pedestal_Der", padre,
                new Vector3(s * LATERAL, medio, 0f),
                new Vector3(0.03f, PEDESTAL_ALTO, FRENTE * 2f),
                mat["Blanco"], true, capa);
        }

        HashiPiezas.Cubo("Pedestal_Base", padre, new Vector3(0f, -PEDESTAL_ALTO, 0f),
            new Vector3(LATERAL * 2f, 0.04f, FRENTE * 2f), mat["MetalOsc"], true, capa);

        // Frente, en tres trozos alrededor de la boca.
        float altoDintel = -bocaArriba;

        HashiPiezas.Cubo("Pedestal_Dintel", padre,
            new Vector3(0f, bocaArriba + altoDintel * 0.5f, -FRENTE),
            new Vector3(LATERAL * 2f, altoDintel, 0.03f), mat["Blanco"], true, capa);

        float altoZocalo = PEDESTAL_ALTO + BANDEJA_SUELO;

        HashiPiezas.Cubo("Pedestal_Zocalo", padre,
            new Vector3(0f, -PEDESTAL_ALTO + altoZocalo * 0.5f, -FRENTE),
            new Vector3(LATERAL * 2f, altoZocalo, 0.03f), mat["Blanco"], true, capa);

        float anchoJamba = LATERAL - BOCA_ANCHO;

        foreach (int s in new[] { -1, 1 })
        {
            HashiPiezas.Cubo(s < 0 ? "Pedestal_JambaIzq" : "Pedestal_JambaDer", padre,
                new Vector3(s * (BOCA_ANCHO + anchoJamba * 0.5f),
                            BANDEJA_SUELO + BOCA_ALTO * 0.5f, -FRENTE),
                new Vector3(anchoJamba, BOCA_ALTO, 0.03f), mat["Blanco"], true, capa);
        }
    }

    // La bandeja: el suelo inclinado del fondo del pedestal, su labio y el marco
    // rosa de la boca. Aqui es donde acaba el premio y donde se recoge.
    static void Bandeja(Transform padre, Dictionary<string, Material> mat, int capa)
    {
        GameObject bandeja = HashiPiezas.Vacio("Bandeja", padre, Vector3.zero);

        // Suelo inclinado hacia la boca. La caja cae aqui desde arriba, rebota y
        // resbala sola hasta ponerse al alcance de la mano; con el suelo plano
        // se quedaba en el fondo, visible y fuera de alcance.
        // El suelo va de la trasera del pedestal HASTA LA PUERTA, y su centro se
        // calcula de esos dos extremos. Estaba escrito a ojo (centro en z=0,02)
        // y se quedaba 23 cm corto: la caja resbalaba, se caia por el borde
        // antes de llegar y aterrizaba en el fondo del mueble. Desde fuera
        // parecia que la maquina se tragaba el premio.
        const float FONDO_BANDEJA = 0.10f;      // cuanto entra hacia el fondo

        float atras = FONDO_BANDEJA;
        float delante = -FRENTE;
        float largo = atras - delante;
        float centro = (atras + delante) * 0.5f;

        // Y la altura se toma en el borde de DELANTE, no en el centro: con la
        // inclinacion, el centro queda mas alto que la puerta, y si se cuadra
        // por el centro el suelo acaba por debajo del umbral.
        const float INCLINACION = 6f;
        float caidaMedia = largo * 0.5f * Mathf.Sin(INCLINACION * Mathf.Deg2Rad);

        GameObject suelo = HashiPiezas.Cubo("Bandeja_Suelo", bandeja.transform,
            new Vector3(0f, BANDEJA_SUELO + caidaMedia - 0.015f, centro),
            new Vector3(BOCA_ANCHO * 2f + 0.04f, 0.03f, largo),
            mat["BlancoMate"], true, capa);

        suelo.transform.localRotation = Quaternion.Euler(-INCLINACION, 0f, 0f);
        suelo.GetComponent<BoxCollider>().sharedMaterial = Resbaladizo("Bandeja");

        // Labio para que el premio no se salga a la calle rodando.
        HashiPiezas.Cubo("Bandeja_Labio", bandeja.transform,
            new Vector3(0f, BANDEJA_SUELO + 0.025f, -FRENTE + 0.02f),
            new Vector3(BOCA_ANCHO * 2f, 0.05f, 0.02f), mat["MetalOsc"], true, capa);

        // Paredes de guia, para que no se cuele por los lados del pedestal.
        foreach (int s in new[] { -1, 1 })
        {
            HashiPiezas.Cubo(s < 0 ? "Bandeja_Izq" : "Bandeja_Der", bandeja.transform,
                new Vector3(s * (BOCA_ANCHO + 0.02f), BANDEJA_SUELO + 0.12f, centro),
                new Vector3(0.02f, 0.24f, largo), mat["BlancoMate"], true, capa);
        }

        // El marco rosa de la boca, que en las de verdad lleva el cartel de
        // "PRIZE OUT". Solo el marco: cuatro tiras alrededor del hueco, no una
        // placa por delante. Antes era una placa entera y tapaba la boca, asi
        // que la puerta parecia cerrada aunque no hubiera puerta ninguna.
        float marco = 0.03f;
        float mitadAncho = BOCA_ANCHO + marco * 0.5f;
        float mitadAlto = BOCA_ALTO * 0.5f + marco * 0.5f;
        float centroBoca = BANDEJA_SUELO + BOCA_ALTO * 0.5f;
        float zMarco = -FRENTE - 0.012f;

        HashiPiezas.Cubo("Boca_Marco_Arriba", bandeja.transform,
            new Vector3(0f, centroBoca + mitadAlto, zMarco),
            new Vector3(mitadAncho * 2f + marco, marco, 0.016f), mat["Rosa"], false, capa);

        HashiPiezas.Cubo("Boca_Marco_Abajo", bandeja.transform,
            new Vector3(0f, centroBoca - mitadAlto, zMarco),
            new Vector3(mitadAncho * 2f + marco, marco, 0.016f), mat["Rosa"], false, capa);

        foreach (int s in new[] { -1, 1 })
        {
            HashiPiezas.Cubo(s < 0 ? "Boca_Marco_Izq" : "Boca_Marco_Der",
                bandeja.transform, new Vector3(s * mitadAncho, centroBoca, zMarco),
                new Vector3(marco, BOCA_ALTO, 0.016f), mat["Rosa"], false, capa);
        }

        // La puerta de verdad: una hoja de plastico transparente con bisagra en
        // el canto de arriba, como una gatera. Se abre sola cuando hay un premio
        // esperando y se cierra cuando ya no queda ninguno.
        GameObject puerta = HashiPiezas.Vacio("Trampilla", bandeja.transform,
            new Vector3(0f, centroBoca, -FRENTE - 0.032f));

        GameObject hoja = HashiPiezas.Cubo("Hoja", puerta.transform, Vector3.zero,
            new Vector3(BOCA_ANCHO * 2f - 0.01f, BOCA_ALTO - 0.01f, 0.010f),
            mat["Cristal"], true, capa);

        HashiPiezas.SinSombras(hoja);

        var trampilla = puerta.AddComponent<Hashi.HashiTrampilla>();

        using (var a = new HashiCableado(trampilla))
        {
            a.Obj("hoja", hoja.transform)
             .Ent("capaPremio", 1 << LayerMask.NameToLayer(Hashi.HashiLayers.NOMBRE_PREMIO))
             .V3("zonaLocal", new Vector3(0f, -BOCA_ALTO * 0.25f, 0.22f))
             .Num("radioZona", 0.30f).Num("anguloAbierta", 72f).Num("sentido", 1f)
             .Num("velocidad", 150f).Num("esperaCierre", 1.5f);
        }
    }

    // El suelo del cajon: una rampa que solo cubre la mitad de atras.
    static GameObject Rampa(string nombre, Transform padre,
                            Dictionary<string, Material> mat, int capa)
    {
        Vector3 atras = new Vector3(0f, RAMPA_ATRAS, INT_Z + 0.02f);
        Vector3 delante = new Vector3(0f, RAMPA_DELANTE, RAMPA_FIN_Z);

        Vector3 direccion = atras - delante;
        float largo = direccion.magnitude;

        Vector3 centro = (atras + delante) * 0.5f;
        Quaternion giro = Quaternion.FromToRotation(Vector3.forward, direccion.normalized);

        // El cubo se coloca por su centro, asi que hay que bajarlo medio grosor
        // por su propia normal para que la CARA DE ARRIBA quede en la linea que
        // se ha calculado. Sin esto la rampa queda medio centimetro alta y la
        // caja aparece flotando.
        const float grosor = 0.03f;
        centro -= giro * Vector3.up * (grosor * 0.5f);

        GameObject rampa = HashiPiezas.Cubo(nombre, padre, centro,
            new Vector3(INT_X * 2f + 0.04f, grosor, largo),
            mat["BlancoMate"], true, capa);

        rampa.transform.localRotation = giro;
        rampa.GetComponent<BoxCollider>().sharedMaterial = Resbaladizo("Rampa");

        return rampa;
    }

    // Material de deslizamiento para rampa y bandeja.
    //
    // Con el rozamiento del carton (0,42) la caja se queda clavada en cualquier
    // pendiente que quepa dentro del mueble: haria falta inclinarla 23 grados
    // para que empezase a moverse. Los canales de premio de verdad son de
    // plastico liso justo por eso. Con Minimum manda el mas resbaladizo de los
    // dos materiales, asi que basta con ponerlo aqui.
    static PhysicsMaterial Resbaladizo(string nombre)
    {
        HashiPiezas.AsegurarCarpeta(HashiMateriales.CARPETA);

        string ruta = HashiMateriales.CARPETA + "/" + nombre + "_Fisica.physicMaterial";
        PhysicsMaterial guardado = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(ruta);

        if (guardado != null) return guardado;

        PhysicsMaterial m = new PhysicsMaterial(nombre + "_Fisica")
        {
            dynamicFriction = 0.10f,
            staticFriction = 0.12f,
            bounciness = 0.05f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Maximum,
        };

        AssetDatabase.CreateAsset(m, ruta);
        return m;
    }

    // El cuadro de servicio de la trasera: la esfera con su aguja y el volumen
    // desde el que se toca. Solo tiene sentido llegar por detras, asi que el
    // disparador esta detras y no envuelve la maquina.
    static void CuadroDeFuerza(Transform padre, Hashi.ClawStrength motor,
                               Dictionary<string, Material> mat, int capa)
    {
        GameObject cuadro = HashiPiezas.Vacio("CuadroFuerza", padre,
            new Vector3(0.20f, -0.30f, INT_Z + 0.045f));

        HashiPiezas.Cubo("Placa", cuadro.transform, Vector3.zero,
            new Vector3(0.17f, 0.15f, 0.012f), mat["MetalOsc"], false, capa);

        // La esfera: un cilindro tumbado de cara a la pared.
        GameObject esfera = HashiPiezas.Cuerpo("Esfera", cuadro.transform,
            new Vector3(0f, 0.015f, 0.009f), PrimitiveType.Cylinder,
            new Vector3(0.11f, 0.004f, 0.11f), Vector3.zero, mat["Blanco"], capa);

        esfera.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // La aguja cuelga del centro de la esfera y se sale hacia arriba. El
        // pivote tiene que estar en el EJE, no en el centro de la aguja, o al
        // girarla se desplaza en vez de apuntar.
        GameObject aguja = HashiPiezas.Cuerpo("Aguja", cuadro.transform,
            new Vector3(0f, 0.015f, 0.014f), PrimitiveType.Cube,
            new Vector3(0.006f, 0.042f, 0.003f), new Vector3(0f, 0.021f, 0f),
            mat["Rosa"], capa);

        HashiPiezas.SinSombras(cuadro);

        // El volumen desde el que se maneja, por detras de la maquina.
        BoxCollider zona = cuadro.AddComponent<BoxCollider>();
        zona.isTrigger = true;
        zona.size = new Vector3(1.0f, 2.0f, 0.9f);
        zona.center = new Vector3(-0.20f, 0.60f, 0.45f);

        var dial = cuadro.AddComponent<Hashi.HashiStrengthDial>();

        using (var a = new HashiCableado(dial))
        {
            a.Obj("motor", motor).Obj("aguja", aguja.transform)
             .V3("ejeAguja", Vector3.forward)
             .Num("anguloMin", -120f).Num("anguloMax", 120f).Num("paso", 0.05f);
        }
    }

    // Un tramo de pletina entre dos puntos, con su malla y su collider.
    //
    // Va en un hijo propio porque un collider no se puede girar por su cuenta:
    // solo tiene centro y medidas, alineadas con los ejes del objeto. Para un
    // tramo en diagonal hay que girar el OBJETO, y por eso cada tramo es su
    // propia pieza. El Rigidbody sigue estando solo en el brazo.
    static void Tramo(string nombre, Transform padre, Vector3 a, Vector3 b,
                      Material material, int capa)
    {
        Vector3 medio = (a + b) * 0.5f;
        Vector3 direccion = b - a;
        float largo = direccion.magnitude;

        if (largo < 0.0001f) return;

        Vector3 tamano = new Vector3(DEDO_ANCHO, largo, DEDO_GROSOR);

        GameObject t = HashiPiezas.Cuerpo(nombre, padre, medio, PrimitiveType.Cube,
                                          tamano, Vector3.zero, material, capa);

        // La malla del cubo crece a lo largo de su Y, asi que se gira el objeto
        // hasta que su Y apunte de a hacia b.
        t.transform.localRotation = Quaternion.FromToRotation(Vector3.up,
                                                              direccion.normalized);

        BoxCollider col = t.AddComponent<BoxCollider>();
        col.size = tamano;
    }

    // El panel de mandos: palanca y dos botones. Es adorno, no se toca con el
    // raton; se juega con el teclado.
    static void Mandos(Transform padre, Dictionary<string, Material> mat, int capa)
    {
        GameObject panel = HashiPiezas.Cubo("Panel", padre, Vector3.zero,
            new Vector3(0.58f, 0.05f, 0.22f), mat["Blanco"], false, capa);

        panel.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);

        HashiPiezas.Cubo("Filo", panel.transform, new Vector3(0f, 0f, -0.52f),
            new Vector3(1.02f, 1.1f, 0.06f), mat["Rosa"], false, capa);

        // Palanca.
        GameObject palanca = HashiPiezas.Vacio("Joystick", panel.transform,
            new Vector3(-0.28f, 0.6f, 0f));

        palanca.transform.localScale = new Vector3(1f / 0.58f, 1f / 0.05f, 1f / 0.22f);

        HashiPiezas.Cilindro("Base", palanca.transform, Vector3.zero, 0.06f, 0.012f,
            Vector3.zero, mat["MetalOsc"], false, capa);

        HashiPiezas.Cilindro("Barra", palanca.transform, new Vector3(0f, 0.03f, 0f),
            0.012f, 0.06f, Vector3.zero, mat["Cromo"], false, capa);

        GameObject bola = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bola.name = "Bola";
        bola.transform.SetParent(palanca.transform, false);
        bola.transform.localPosition = new Vector3(0f, 0.062f, 0f);
        bola.transform.localScale = Vector3.one * 0.035f;
        bola.layer = capa;
        Object.DestroyImmediate(bola.GetComponent<Collider>());
        bola.GetComponent<Renderer>().sharedMaterial = mat["Rosa"];

        // Dos botones.
        for (int i = 0; i < 2; i++)
        {
            GameObject b = HashiPiezas.Vacio("Boton_" + (i + 1), panel.transform,
                new Vector3(0.14f + i * 0.16f, 0.6f, 0f));

            b.transform.localScale = new Vector3(1f / 0.58f, 1f / 0.05f, 1f / 0.22f);

            HashiPiezas.Cilindro("Aro", b.transform, Vector3.zero, 0.055f, 0.010f,
                Vector3.zero, mat["MetalOsc"], false, capa);

            HashiPiezas.Cilindro("Tapa", b.transform, new Vector3(0f, 0.006f, 0f),
                0.045f, 0.012f, Vector3.zero,
                i == 0 ? mat["LEDAzul"] : mat["LEDRosa"], false, capa);
        }

        HashiPiezas.SinSombras(panel);
    }

    // Un foco pegado al techo apuntando al suelo. Girado 90 grados en X, que es
    // lo que hace que un Spot de Unity, que nace mirando a +Z, mire hacia abajo.
    static Light FocoAbajo(Transform padre, string nombre, Vector3 pos,
                           Color color, float intensidad, float alcance, float apertura)
    {
        GameObject go = HashiPiezas.Vacio(nombre, padre, pos);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Light l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.color = color;
        l.intensity = intensidad;
        l.range = alcance;
        l.spotAngle = apertura;
        l.innerSpotAngle = apertura * 0.6f;
        l.shadows = LightShadows.None;

        return l;
    }

    static ParticleSystem Confeti(Transform padre)
    {
        GameObject go = HashiPiezas.Vacio("Confeti", padre, new Vector3(0f, 1.15f, -0.35f));

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        // Apagado nada mas crearse: si no, dispara confeti en cuanto se abre la
        // escena y otra vez cada vez que se recompila.
        ParticleSystem.MainModule principal = ps.main;
        principal.duration = 1.2f;
        principal.loop = false;
        principal.playOnAwake = false;
        principal.startLifetime = 2.6f;
        principal.startSpeed = 1.6f;
        principal.startSize = 0.018f;
        principal.gravityModifier = 0.55f;
        principal.maxParticles = 400;
        principal.startRotation3D = true;
        principal.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emision = ps.emission;
        emision.rateOverTime = 0f;
        emision.SetBursts(new[] { new ParticleSystem.Burst(0f, 180) });

        ParticleSystem.ShapeModule forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Cone;
        forma.angle = 32f;
        forma.radius = 0.25f;
        forma.rotation = new Vector3(90f, 0f, 0f);   // hacia abajo

        ParticleSystem.ColorOverLifetimeModule colores = ps.colorOverLifetime;
        colores.enabled = true;

        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.45f, 0.75f), 0f),
                new GradientColorKey(new Color(0.4f, 0.7f, 1f), 0.5f),
                new GradientColorKey(new Color(1f, 0.9f, 0.35f), 1f),
            },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });

        colores.color = new ParticleSystem.MinMaxGradient(g);

        ParticleSystem.RotationOverLifetimeModule giro = ps.rotationOverLifetime;
        giro.enabled = true;
        giro.z = new ParticleSystem.MinMaxCurve(-4f, 4f);

        // Sin material, las particulas salen rosa chicle de "shader que falta".
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();

        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        if (sh != null)
        {
            string ruta = HashiMateriales.CARPETA + "/Confeti.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(ruta);

            if (m == null)
            {
                m = new Material(sh);
                m.color = Color.white;
                AssetDatabase.CreateAsset(m, ruta);
            }

            r.sharedMaterial = m;
        }

        return ps;
    }

    // Un suelo y una luz de sala, para que la maquina no salga flotando en el
    // vacio. Es lo minimo para que la escena se pueda abrir y entender.
    static void Escenario(Dictionary<string, Material> mat)
    {
        GameObject sala = new GameObject("Escenario");

        GameObject suelo = HashiPiezas.Cubo("Suelo", sala.transform,
            new Vector3(0f, -0.96f, 0f), new Vector3(8f, 0.06f, 8f), mat["Suelo"]);

        suelo.isStatic = true;

        HashiPiezas.Cubo("Fondo", sala.transform, new Vector3(0f, 0.4f, 2.2f),
            new Vector3(8f, 2.8f, 0.1f), mat["Pared"], false);

        GameObject solGo = HashiPiezas.Vacio("Sol", sala.transform,
            new Vector3(0f, 3f, 0f));

        solGo.transform.localRotation = Quaternion.Euler(48f, -35f, 0f);

        Light sol = solGo.AddComponent<Light>();
        sol.type = LightType.Directional;
        sol.intensity = 0.9f;
        sol.color = new Color(1f, 0.97f, 0.92f);
        sol.shadows = LightShadows.Soft;
    }
}
