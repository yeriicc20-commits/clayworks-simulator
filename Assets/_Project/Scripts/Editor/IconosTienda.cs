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
    const string CARPETA = "Assets/_Project/UI/Iconos";

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
                if (it == null || (!todos && it.icon != null)) continue;

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
                if (it == null || (!todos && it.icon != null)) continue;
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

        tienda = null;
    }

    static Sprite Guardar(string nombre, Texture2D foto)
    {
        string ruta = CARPETA + "/" + Limpio(nombre) + ".png";

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

    static string Limpio(string nombre)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            nombre = nombre.Replace(c, '_');
        }

        return nombre.Replace(' ', '_');
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
