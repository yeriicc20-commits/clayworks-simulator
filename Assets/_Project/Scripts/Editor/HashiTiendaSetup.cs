using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Mete la maquina de puente en el catalogo del ordenador del local.
//
// El catalogo vive en el ShopManager de la escena, no en un asset, asi que
// anadir una maquina es editar Local_01. A mano son ocho campos en un array del
// inspector y acordarse de guardar la escena; y el dia que se rehace el prefab
// hay que comprobar que la ficha sigue apuntando al sitio.
//
// Aqui se hace en un paso, no duplica si ya esta, y copia la caja de reparto de
// una maquina que ya se venda en vez de traerse una propia: si manana cambian la
// caja de las maquinas, esta viaja en la nueva sin tocar nada.
public static class HashiTiendaSetup
{
    const string ESCENA = "Assets/Scenes/Local_01.unity";
    const string NOMBRE = "Maquina de puente";
    const int PRECIO = 550;

    [MenuItem("ClayWorks/Hashi-Watashi/Ponerla a la venta en el local", false, 43)]
    public static void PonerALaVenta()
    {
        Registrar(true);
    }

    // abrirEscena = false lo usa la instalacion automatica: si la escena de la
    // tienda no esta abierta, se calla y lo deja para luego. Abrir escenas por
    // su cuenta, sin que nadie lo haya pedido, es de las cosas que hacen que se
    // pierda trabajo.
    public static bool Registrar(bool abrirEscena)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/Machines/MaquinaPuente.prefab");

        // Si no esta hecho, se hace. Pedirle al usuario que pulse dos botones en
        // el orden correcto es pedirle que se acuerde de un orden.
        if (prefab == null)
        {
            Debug.Log("[Hashi] Todavia no hay prefab de la maquina, lo monto antes.");
            prefab = HashiWatashiBuilder.MontarPrefab();
        }

        if (prefab == null)
        {
            Debug.LogError("[Hashi] No hay prefab que vender. Mira el error de arriba.");
            return false;
        }

        ShopManager tienda = Object.FindFirstObjectByType<ShopManager>();
        Scene escena;

        if (tienda != null)
        {
            escena = tienda.gameObject.scene;
        }
        else if (!abrirEscena)
        {
            return false;
        }
        else
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("[Hashi] Cancelado: no se ha tocado nada.");
                return false;
            }

            escena = EditorSceneManager.OpenScene(ESCENA, OpenSceneMode.Single);
            tienda = Object.FindFirstObjectByType<ShopManager>();
        }

        if (tienda == null)
        {
            Debug.LogError("[Hashi] No encuentro ningun ShopManager en " + ESCENA
                           + ". Sin el no hay catalogo donde meterla.");
            return false;
        }

        List<ShopItem> fichas = tienda.items != null
            ? tienda.items.ToList()
            : new List<ShopItem>();

        // La caja de reparto: la misma que usen las maquinas que ya se venden.
        GameObject caja = fichas
            .FirstOrDefault(f => f != null && f.machinePrefab != null && f.boxPrefab != null)
            ?.boxPrefab;

        if (caja == null)
        {
            Debug.LogWarning("[Hashi] Ninguna maquina del catalogo dice en que "
                             + "caja llega, asi que esta se queda sin caja. "
                             + "Rellena boxPrefab a mano en el ShopManager.");
        }

        ShopItem ficha = fichas.FirstOrDefault(
            f => f != null && f.machinePrefab == prefab);

        bool nueva = ficha == null;

        Undo.RecordObject(tienda, "Poner la maquina de puente a la venta");

        if (nueva)
        {
            ficha = new ShopItem();
            fichas.Add(ficha);
        }

        // Se mira si de verdad cambia algo antes de tocar nada. Esto lo llama
        // ahora tambien la instalacion automatica en cada recompilacion, y
        // guardar Local_01 cada vez que alguien toca un script seria una manera
        // excelente de pisar trabajo sin querer.
        bool cambiado = nueva
                        || ficha.itemName != NOMBRE
                        || ficha.price != PRECIO
                        || ficha.machinePrefab != prefab
                        || ficha.boxPrefab == null;

        ficha.itemName = NOMBRE;
        ficha.price = PRECIO;
        ficha.machinePrefab = prefab;

        // El icono NO se toca. Lo genera IconosTienda sacandole una foto al
        // prefab y lo deja en Resources; escribir aqui un icono vacio pisaria
        // el que ya hubiera.
        if (ficha.boxPrefab == null) ficha.boxPrefab = caja;

        tienda.items = fichas.ToArray();

        cambiado |= RegistrarJuguete(tienda);

        if (!cambiado) return true;

        EditorUtility.SetDirty(tienda);
        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);

        Debug.Log("[Hashi] Catalogo al dia: '" + NOMBRE + "' por " + PRECIO
                  + " y '" + JUGUETE + "' por " + PRECIO_JUGUETE + " la caja de "
                  + POR_CAJA + ". Escena guardada. Los iconos se los saca solo "
                  + "IconosTienda; si salen en blanco, pulsa "
                  + "ClayWorks/Generar iconos de la tienda.");

        return true;
    }

    const string JUGUETE = "Figura en caja";
    const int PRECIO_JUGUETE = 90;
    const int POR_CAJA = 5;

    // La ficha del premio en la pestana de juguetes.
    //
    // Cinco por caja y no diez como los peluches: en la maquina de puente solo
    // cabe UNO cada vez, asi que una caja de diez seria una caja que no se acaba
    // nunca. Con cinco se repone cinco veces y hay que volver a comprar.
    static bool RegistrarJuguete(ShopManager tienda)
    {
        GameObject premio = AssetDatabase.LoadAssetAtPath<GameObject>(HashiFunko.RUTA);

        if (premio == null)
        {
            Debug.LogWarning("[Hashi] No encuentro el premio en " + HashiFunko.RUTA
                             + ", asi que no lo pongo a la venta.");
            return false;
        }

        List<ToyShopItem> juguetes = tienda.toyItems != null
            ? tienda.toyItems.ToList()
            : new List<ToyShopItem>();

        ToyShopItem ficha = juguetes.FirstOrDefault(
            f => f != null && f.itemName == JUGUETE);

        bool nueva = ficha == null;

        if (nueva)
        {
            ficha = new ToyShopItem();
            juguetes.Add(ficha);
        }

        // La caja de un JUGUETE tiene que llevar ToyBox. Aqui le puse la de las
        // maquinas, que lleva PickupBox y sirve para desplegar un mueble:
        // ShopManager le busca el ToyBox, no lo encuentra, se sale sin rellenar
        // nada, y al usarla suelta "Esta caja esta vacia". Dejandola vacia, el
        // propio ShopManager pone la caja de juguetes que ya tiene configurada.
        bool cajaMala = ficha.boxPrefab != null
                        && ficha.boxPrefab.GetComponent<ToyBox>() == null;

        bool cambiado = nueva
                        || cajaMala
                        || ficha.price != PRECIO_JUGUETE
                        || ficha.toyPrefab != premio
                        || ficha.boxToyCount != POR_CAJA;

        if (cajaMala)
        {
            Debug.Log("[Hashi] '" + JUGUETE + "' tenia asignada la caja de las "
                      + "maquinas, que no sirve para juguetes. La quito para que "
                      + "use la de la tienda.");
        }

        ficha.itemName = JUGUETE;
        ficha.price = PRECIO_JUGUETE;
        ficha.toyPrefab = premio;
        ficha.boxToyCount = POR_CAJA;

        // El icono no se toca: lo saca IconosTienda del propio prefab.
        if (cajaMala) ficha.boxPrefab = null;

        tienda.toyItems = juguetes.ToArray();

        return cambiado;
    }
}
