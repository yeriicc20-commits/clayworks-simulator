using System.IO;
using UnityEditor;
using UnityEngine;

// Le pone a cada caja el componente del golpe y su sonido.
//
// Igual que los otros builders del proyecto: se hace desde codigo y no a mano
// porque una caja nueva que alguien anada manana se quedaria muda, y un sonido
// que no suena no da ningun error. Aqui, o esta puesto en las cinco o salta un
// aviso por consola diciendo cual falta.
public static class CajasBuilder
{
    const string SONIDO = "Assets/_Project/Audio/Caja_Golpe.wav";
    const string YO = "Assets/_Project/Scripts/Editor/CajasBuilder.cs";

    static readonly string[] CAJAS =
    {
        "Assets/_Project/Prefabs/Box_Pequena.prefab",
        "Assets/_Project/Prefabs/Box_Mediana.prefab",
        "Assets/_Project/Prefabs/Box_Grande.prefab",
        "Assets/_Project/Prefabs/Caja_Compra.prefab",
        "Assets/_Project/Prefabs/ToyBox.prefab",
    };

    [InitializeOnLoadMethod]
    static void AlArrancar()
    {
        EditorApplication.delayCall += Comprobar;
    }

    public static void Comprobar()
    {
        EditorApplication.delayCall -= Comprobar;

        if (!File.Exists(SONIDO)) return;

        System.DateTime fuente = File.GetLastWriteTimeUtc(SONIDO);

        if (File.Exists(YO))
        {
            System.DateTime mio = File.GetLastWriteTimeUtc(YO);
            if (mio > fuente) fuente = mio;
        }

        bool algo = false;

        foreach (string ruta in CAJAS)
        {
            if (!File.Exists(ruta)) continue;

            // Solo si la caja es mas vieja que el sonido o que este archivo. Sin
            // esto se reescriben los cinco prefabs en cada recompilacion y git
            // se llena de cambios que no cambian nada.
            if (File.GetLastWriteTimeUtc(ruta) >= fuente) continue;

            if (Montar(ruta)) algo = true;
        }

        if (algo) AssetDatabase.SaveAssets();
    }

    [MenuItem("ClayWorks/Poner sonido a las cajas", false, 3)]
    public static void Construir()
    {
        foreach (string ruta in CAJAS) Montar(ruta);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static bool Montar(string ruta)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SONIDO);

        if (clip == null)
        {
            Debug.LogWarning("[Cajas] No encuentro " + SONIDO + ", las cajas se "
                             + "quedan mudas al caer.");
            return false;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);

        if (prefab == null)
        {
            Debug.LogWarning("[Cajas] No encuentro el prefab " + ruta);
            return false;
        }

        GameObject copia = PrefabUtility.LoadPrefabContents(ruta);
        bool tocado = false;

        try
        {
            // El AudioSource lo pide GolpeCaja, pero se crea aqui para dejarlo
            // configurado: en 3D y con caida lineal. El que crea Unity por su
            // cuenta sale en 2D y se oiria igual de fuerte desde la otra punta
            // del local.
            AudioSource altavoz = copia.GetComponent<AudioSource>();

            if (altavoz == null)
            {
                altavoz = copia.AddComponent<AudioSource>();
                tocado = true;
            }

            altavoz.playOnAwake = false;
            altavoz.spatialBlend = 1f;
            altavoz.rolloffMode = AudioRolloffMode.Linear;
            altavoz.minDistance = 1.5f;
            altavoz.maxDistance = 14f;

            GolpeCaja golpe = copia.GetComponent<GolpeCaja>();

            if (golpe == null)
            {
                golpe = copia.AddComponent<GolpeCaja>();
                tocado = true;
            }

            if (golpe.golpe != clip)
            {
                golpe.golpe = clip;
                tocado = true;
            }

            if (tocado) PrefabUtility.SaveAsPrefabAsset(copia, ruta);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(copia);
        }

        if (tocado) Debug.Log("[Cajas] Sonido de golpe puesto en " + Path.GetFileName(ruta));

        return tocado;
    }
}
