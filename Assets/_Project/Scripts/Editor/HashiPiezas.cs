using UnityEditor;
using UnityEngine;

// Fabrica de piezas para el constructor de la maquina.
//
// Dos maneras de hacer una pieza, y la diferencia importa:
//
//   Cubo()    -> un cubo escalado con su BoxCollider dentro. Vale para paredes,
//                cristales y suelos: un BoxCollider escalado sigue siendo la
//                caja que se ve.
//
//   Cuerpo()  -> un objeto SIN escalar con el collider puesto a mano, y la malla
//                escalada dentro. Es mas trabajo, y hace falta en todo lo que
//                lleve capsula, bisagra o pivote descentrado: una CapsuleCollider
//                dentro de un transform escalado en dos ejes deja de ser una
//                capsula, y PhysX simula una cosa distinta de la que se ve.
public static class HashiPiezas
{
    public static GameObject Vacio(string nombre, Transform padre, Vector3 posLocal)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.localPosition = posLocal;
        return go;
    }

    // Pieza maciza: cubo escalado, con collider o sin el.
    public static GameObject Cubo(string nombre, Transform padre, Vector3 centro,
                                  Vector3 tamano, Material material,
                                  bool conCollider = true, int capa = -1)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = centro;
        go.transform.localScale = tamano;

        Preparar(go, material, conCollider, capa);
        return go;
    }

    public static GameObject Cilindro(string nombre, Transform padre, Vector3 centro,
                                      float diametro, float largo, Vector3 eulerRot,
                                      Material material, bool conCollider = false,
                                      int capa = -1)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = nombre;
        go.transform.SetParent(padre, false);
        go.transform.localPosition = centro;
        go.transform.localRotation = Quaternion.Euler(eulerRot);

        // El cilindro de Unity mide 2 de alto a escala 1, asi que la escala en Y
        // es la MITAD del largo. Es el despiste que deja las barras del doble.
        go.transform.localScale = new Vector3(diametro, largo * 0.5f, diametro);

        Preparar(go, material, conCollider, capa);
        return go;
    }

    // Objeto sin escalar con la malla escalada dentro. Devuelve la raiz; la
    // malla queda como primer hijo, que es donde la buscan BarRig,
    // PrizeController y ClawController.
    public static GameObject Cuerpo(string nombre, Transform padre, Vector3 posLocal,
                                    PrimitiveType formaMalla, Vector3 escalaMalla,
                                    Vector3 centroMalla, Material material, int capa = -1)
    {
        GameObject raiz = new GameObject(nombre);
        raiz.transform.SetParent(padre, false);
        raiz.transform.localPosition = posLocal;

        GameObject malla = GameObject.CreatePrimitive(formaMalla);
        malla.name = "Malla";
        malla.transform.SetParent(raiz.transform, false);
        malla.transform.localPosition = centroMalla;
        malla.transform.localScale = escalaMalla;

        Preparar(malla, material, false, capa);

        if (capa >= 0) raiz.layer = capa;

        return raiz;
    }

    static void Preparar(GameObject go, Material material, bool conCollider, int capa)
    {
        Collider col = go.GetComponent<Collider>();

        if (!conCollider && col != null) Object.DestroyImmediate(col);

        if (material != null)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = material;
        }

        if (capa >= 0) go.layer = capa;
    }

    // Pone la capa a un objeto y a todo lo que cuelga de el.
    public static void Capa(GameObject go, int capa)
    {
        if (go == null || capa < 0) return;

        go.layer = capa;

        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer = capa;
        }
    }

    // Quita las sombras de una pieza. Las piezas pequenas de adorno no aportan
    // nada sombreando y en el interior de la maquina, que esta lleno de trastos,
    // se nota en el coste.
    public static void SinSombras(GameObject go)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    public static void AsegurarCarpeta(string ruta)
    {
        HashiMateriales.AsegurarCarpeta(ruta);
    }

    // Crea o rehace un asset de ScriptableObject sin perder la referencia que
    // otros assets ya tengan a el.
    public static T Asset<T>(string ruta, System.Action<T> rellenar) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(ruta);
        bool nuevo = asset == null;

        if (nuevo) asset = ScriptableObject.CreateInstance<T>();

        rellenar(asset);

        if (nuevo) AssetDatabase.CreateAsset(asset, ruta);
        else EditorUtility.SetDirty(asset);

        return asset;
    }
}
