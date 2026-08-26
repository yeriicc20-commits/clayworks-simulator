using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Saca una foto de cada cosa que se vende y se la pega a su ficha.
//
// Las fichas de la tienda tenian el icono vacio, asi que se compraba a ciegas:
// un nombre y un precio, y a ver que llega. Con veinte maquinas y varios
// peluches eso deja de ser una tienda para ser una lista de la compra.
//
// La foto se saca del propio prefab con el previsualizador de Unity, el mismo
// que dibuja las miniaturas de la ventana de proyecto. Montar una camara y
// renderizar a mano seria pelearse con URP para acabar en lo mismo, y ademas
// habria que iluminar cada modelo por su cuenta.
//
// El previsualizador tarda en tener la imagen lista, y ahi esta el unico
// enredo: no se puede pedir y usar en la misma linea. Por eso esto va por
// pasadas, enganchado a EditorApplication.update, en vez de en un bucle.
public static class IconosTienda
{
    // En Resources, y esto es el arreglo de fondo.
    //
    // Antes la foto se guardaba en el campo icon de la ficha, dentro de la
    // escena. El generador la creaba y la asignaba, pero si la escena no se
    // guardaba se perdia en la siguiente recarga: por eso no se veia ninguna.
    // Un dato que solo existe si alguien se acuerda de pulsar Guardar no es
    // un dato.
    //
    // Desde Resources se pide por nombre al usarlo y da igual lo que tenga
    // guardado la escena: si el PNG esta, la foto sale.
    const string CARPETA = "Assets/_Project/Resources/Iconos";

    class Pendiente
    {
        public string nombre;
        public GameObject prefab;
        public System.Action<Sprite> asignar;
    }

    static List<Pendiente> cola;
    static ShopManager tienda;
    static int intentos;

    [MenuItem("ClayWorks/Generar iconos de la tienda", false, 4)]
    public static void Generar()
    {
        Reunir(true);
    }

    [InitializeOnLoadMethod]
    static void AlArrancar()
    {
        // Solo los que falten. Rehacerlos todos en cada recompilacion seria
        // reescribir un puñado de PNG cada vez que se toca un script.
        EditorApplication.delayCall += () => Reunir(false);
    }

    static void Reunir(bool todos)
    {
        tienda = BuscarTienda();

        if (tienda == null) return;

        cola = new List<Pendiente>();

        if (tienda.items != null)
        {
            foreach (ShopItem it in tienda.items)
            {
                if (it == null || (!todos && YaTiene(it.itemName))) continue;

                // La maquina antes que la caja: lo que se compra es la maquina,
                // y una foto de una caja de carton no distingue una de otra.
                GameObject que = it.machinePrefab != null ? it.machinePrefab : it.boxPrefab;
                if (que == null) continue;

                ShopItem guardado = it;
                cola.Add(new Pendiente
                {
                    nombre = it.itemName,
                    prefab = que,
                    asignar = s => guardado.icon = s,
                });
            }
        }

        if (tienda.toyItems != null)
        {
            foreach (ToyShopItem it in tienda.toyItems)
            {
                if (it == null || (!todos && YaTiene(it.itemName))) continue;
                if (it.toyPrefab == null) continue;

                ToyShopItem guardado = it;
                cola.Add(new Pendiente
                {
                    nombre = it.itemName,
                    prefab = it.toyPrefab,
                    asignar = s => guardado.icon = s,
                });
            }
        }

        if (cola.Count == 0) return;

        AsegurarCarpeta();

        intentos = 0;
        EditorApplication.update -= Paso;
        EditorApplication.update += Paso;
    }

    static void Paso()
    {
        if (cola == null || cola.Count == 0)
        {
            Terminar();
            return;
        }

        Pendiente p = cola[0];

        Texture2D foto = AssetPreview.GetAssetPreview(p.prefab);

        if (foto == null)
        {
            intentos++;

            // El previsualizador puede no tener nunca la imagen (un prefab sin
            // nada que dibujar, por ejemplo). Se le da un margen y se pasa al
            // siguiente en vez de quedarse colgado para siempre.
            if (intentos < 200 || AssetPreview.IsLoadingAssetPreviews()) return;

            Debug.LogWarning("[Iconos] No consigo la miniatura de " + p.nombre
                             + ". Su ficha se queda sin foto.");

            cola.RemoveAt(0);
            intentos = 0;
            return;
        }

        Sprite sprite = Guardar(p.nombre, foto);

        if (sprite != null)
        {
            p.asignar(sprite);
            Debug.Log("[Iconos] " + p.nombre + " ya tiene foto.");
        }

        cola.RemoveAt(0);
        intentos = 0;
    }

    static void Terminar()
    {
        EditorApplication.update -= Paso;

        if (tienda == null) return;

        EditorUtility.SetDirty(tienda);
        EditorSceneManager.MarkSceneDirty(tienda.gameObject.scene);

        AssetDatabase.SaveAssets();

        // Que la tienda deje de dar por inexistentes los que se acaban de
        // hacer: sin esto no salen hasta reiniciar Unity.
        IconoTienda.Olvidar();

        tienda = null;
    }

    static Sprite Guardar(string nombre, Texture2D foto)
    {
        // El nombre de archivo lo decide IconoTienda, que es quien luego lo
        // busca. Con la regla escrita en dos sitios, el dia que una cambie el
        // PNG existira y aun asi no se encontrara.
        string ruta = CARPETA + "/" + IconoTienda.Limpio(nombre) + ".png";

        // La textura del previsualizador no se puede leer directamente, asi que
        // se copia pasando por una RenderTexture. Es el camino de siempre para
        // sacar pixeles de algo que vive en la GPU.
        RenderTexture rt = RenderTexture.GetTemporary(foto.width, foto.height, 0,
                                                      RenderTextureFormat.ARGB32);

        Graphics.Blit(foto, rt);

        RenderTexture antes = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D copia = new Texture2D(foto.width, foto.height, TextureFormat.RGBA32, false);
        copia.ReadPixels(new Rect(0, 0, foto.width, foto.height), 0, 0);
        copia.Apply();

        RenderTexture.active = antes;
        RenderTexture.ReleaseTemporary(rt);

        File.WriteAllBytes(ruta, copia.EncodeToPNG());
        Object.DestroyImmediate(copia);

        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);

        TextureImporter imp = AssetImporter.GetAtPath(ruta) as TextureImporter;

        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    // Si ya hay PNG, no hay nada que hacer.
    //
    // Antes se miraba si la ficha tenia icon puesto, y eso era mirar al sitio
    // equivocado desde que las fotos viven en Resources: la ficha puede estar
    // vacia y el archivo estar ahi, o al reves.
    static bool YaTiene(string nombre)
    {
        return File.Exists(CARPETA + "/" + IconoTienda.Limpio(nombre) + ".png");
    }

    static void AsegurarCarpeta()
    {
        if (AssetDatabase.IsValidFolder(CARPETA)) return;

        Directory.CreateDirectory(CARPETA);
        AssetDatabase.Refresh();
    }

    static ShopManager BuscarTienda()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene esc = SceneManager.GetSceneAt(i);
            if (!esc.isLoaded) continue;

            foreach (GameObject raiz in esc.GetRootGameObjects())
            {
                ShopManager s = raiz.GetComponentInChildren<ShopManager>(true);
                if (s != null) return s;
            }
        }

        return null;
    }
}
