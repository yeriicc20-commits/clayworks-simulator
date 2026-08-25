using System.IO;
using UnityEditor;

// Rehace el prefab de la maquina en cuanto cambia el FBX del que sale.
//
// Por que existe: el prefab es una pieza DERIVADA del modelo. Guarda dentro las
// posiciones de las 106 piezas, pero las mallas las referencia al FBX. Si el
// FBX cambia y el prefab no se rehace, quedan las posiciones viejas apuntando a
// mallas nuevas y la maquina sale desmontada, con los dedos en sitios que no
// tocan.
//
// Eso ya paso una vez y desde fuera parece un fallo de fisica, no un descuadre
// de piezas, asi que se pierde un buen rato buscando donde no es. Un artefacto
// derivado tiene que regenerarse solo; acordarse de pulsar un boton no es un
// sistema.
public class MaquinaGarraAutoBuild : AssetPostprocessor
{
    const string RUTA_FBX = "Assets/_Project/Models/MaquinaGarra.fbx";

    static void OnPostprocessAllAssets(string[] importados, string[] borrados,
                                       string[] movidos, string[] movidosDesde)
    {
        foreach (string ruta in importados)
        {
            if (ruta != RUTA_FBX) continue;

            // En diferido a proposito: durante la importacion no se puede crear
            // ni guardar assets, y Unity protesta o se lo traga sin hacer nada.
            EditorApplication.delayCall += Rehacer;
            return;
        }
    }

    // Ademas de reaccionar al reimportado, se comprueba al recompilar. Asi se
    // arregla solo el caso de haber cambiado el modelo con Unity cerrado, que
    // es justo cuando no hay ningun evento de importacion que escuchar.
    [InitializeOnLoadMethod]
    static void ComprobarAlArrancar()
    {
        EditorApplication.delayCall += Comprobar;
    }

    static void Comprobar()
    {
        EditorApplication.delayCall -= Comprobar;

        const string RUTA_PREFAB = "Assets/_Project/Prefabs/Machines/MaquinaGarra.prefab";

        if (!File.Exists(RUTA_FBX)) return;

        // Sin prefab todavia no hay nada que rehacer: ya lo montara quien toque
        // el boton la primera vez.
        if (!File.Exists(RUTA_PREFAB)) return;

        // El prefab depende de DOS cosas: del modelo y del propio constructor.
        // Comparar solo con el FBX dejaba fuera los cambios de ajustes, que no
        // descolocan piezas pero si dejan valores viejos dentro sin avisar.
        const string RUTA_BUILDER = "Assets/_Project/Scripts/Editor/MaquinaGarraBuilder.cs";

        System.DateTime fuente = File.GetLastWriteTimeUtc(RUTA_FBX);

        if (File.Exists(RUTA_BUILDER))
        {
            System.DateTime b = File.GetLastWriteTimeUtc(RUTA_BUILDER);
            if (b > fuente) fuente = b;
        }

        if (fuente <= File.GetLastWriteTimeUtc(RUTA_PREFAB)) return;

        UnityEngine.Debug.Log("[Maquina] El prefab es mas viejo que el modelo o que el constructor. "
                              + "Lo rehago para que no queden piezas descolocadas.");
        MaquinaGarraBuilder.Construir();
    }

    static void Rehacer()
    {
        EditorApplication.delayCall -= Rehacer;

        UnityEngine.Debug.Log("[Maquina] El modelo ha cambiado, rehago el prefab.");
        MaquinaGarraBuilder.Construir();
    }
}
