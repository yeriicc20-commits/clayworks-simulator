using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// La caja de figura coleccionable que se juega en la maquina de puente.
//
// Es el premio "de verdad": una caja de carton con ventana de plastico y el
// muneco dentro, en 3D, en vez del cubo de color que habia. Se monta con
// primitivas porque no hay modelo que importar, y porque asi las medidas salen
// de las mismas constantes que comprueban que la caja se pueda jugar.
//
// MEDIDAS, que no son libres. La caja va TUMBADA sobre las barras, boca arriba,
// asi que:
//
//   Z = 0,223  el alto de la caja, cruzando las dos barras. Tiene que ser MAYOR
//              que la separacion mayor (0,190) o no apoya.
//   X = 0,155  el ancho, a lo largo de las barras. Menor que 0,250 o los brazos
//              bajarian encima.
//   Y = 0,122  el fondo, que es lo que pasa por el hueco al volcar. Menor que
//              0,134 o no cae nunca.
//
// Son las proporciones de una caja de figura de verdad (16,5 x 11,5 x 9 cm)
// multiplicadas por 1,35, que es mas o menos lo que esta agrandada la maquina.
public static class HashiFunko
{
    public const string RUTA = "Assets/_Project/Prefabs/Hashi/Premio_Funko.prefab";

    static readonly Vector3 TAMANO = new Vector3(0.155f, 0.122f, 0.223f);

    // El muneco va mirando hacia arriba, que es donde esta la ventana con la
    // caja tumbada. Su "arriba" apunta hacia +Z.
    const float CABEZA_Z = 0.052f;

    public static GameObject Construir(Dictionary<string, Material> mat)
    {
        HashiPiezas.AsegurarCarpeta("Assets/_Project/Prefabs/Hashi");

        int capa = LayerMask.NameToLayer(Hashi.HashiLayers.NOMBRE_PREMIO);

        // La caja de carton. Tiene que ser el PRIMER hijo: PrizeController da
        // por hecho que el hijo 0 es la malla y le escribe la escala.
        GameObject raiz = HashiPiezas.Cuerpo("Premio_Funko", null, Vector3.zero,
            PrimitiveType.Cube, TAMANO, Vector3.zero, mat["AzulOsc"], capa);

        Transform p = raiz.transform;

        float medioY = TAMANO.y * 0.5f;
        float medioZ = TAMANO.z * 0.5f;

        // --------------------------------------------------------- la ventana
        // Hundida un pelo respecto a la cara, para que se vea el reborde de
        // carton alrededor y no un cristal pegado por fuera.
        HashiPiezas.Cubo("Ventana", p, new Vector3(0f, medioY - 0.004f, -0.012f),
            new Vector3(0.112f, 0.006f, 0.150f), mat["Cristal"], false, capa);

        // ------------------------------------------------------- el impreso
        // Banda de arriba con el hueco del numero, la marca de abajo y el lomo.
        HashiPiezas.Cubo("Banda_Marca", p,
            new Vector3(0f, medioY + 0.001f, medioZ - 0.022f),
            new Vector3(TAMANO.x - 0.006f, 0.002f, 0.038f), mat["Rojo"], false, capa);

        HashiPiezas.Cubo("Casilla_Numero", p,
            new Vector3(TAMANO.x * 0.5f - 0.026f, medioY + 0.002f, medioZ - 0.022f),
            new Vector3(0.036f, 0.002f, 0.030f), mat["Blanco"], false, capa);

        HashiPiezas.Cubo("Bocadillo_Logo", p,
            new Vector3(-TAMANO.x * 0.5f + 0.030f, medioY + 0.002f, medioZ - 0.062f),
            new Vector3(0.046f, 0.002f, 0.030f), mat["Blanco"], false, capa);

        HashiPiezas.Cubo("Pie_Nombre", p,
            new Vector3(0f, medioY + 0.001f, -medioZ + 0.020f),
            new Vector3(TAMANO.x - 0.006f, 0.002f, 0.032f), mat["Blanco"], false, capa);

        // ---------------------------------------------------------- el muneco
        // Cabeza enorme y cuerpo pequeno: es toda la gracia de estas figuras.
        // Va dentro de la caja, asi que todo cabe por debajo de la ventana.
        GameObject figura = HashiPiezas.Vacio("Figura", p, new Vector3(0f, -0.012f, 0f));

        HashiPiezas.Cubo("Cabeza", figura.transform,
            new Vector3(0f, 0.012f, CABEZA_Z),
            new Vector3(0.084f, 0.055f, 0.078f), mat["Rojo"], false, capa);

        // Los ojos, que es lo unico que hace que se lea como una cara. Blancos
        // con reborde negro y en diagonal, como los de la mascara.
        foreach (int s in new[] { -1, 1 })
        {
            HashiPiezas.Cubo(s < 0 ? "Ojo_Izq" : "Ojo_Der", figura.transform,
                new Vector3(s * 0.019f, 0.041f, CABEZA_Z + 0.004f),
                new Vector3(0.030f, 0.003f, 0.022f), mat["Negro"], false, capa);

            HashiPiezas.Cubo(s < 0 ? "Ojo_Izq_Luz" : "Ojo_Der_Luz", figura.transform,
                new Vector3(s * 0.019f, 0.043f, CABEZA_Z + 0.004f),
                new Vector3(0.024f, 0.003f, 0.016f), mat["Blanco"], false, capa);
        }

        HashiPiezas.Cubo("Cuerpo", figura.transform,
            new Vector3(0f, 0.006f, -0.008f),
            new Vector3(0.048f, 0.042f, 0.052f), mat["Rojo"], false, capa);

        // Brazos abiertos, como la pose de la caja.
        foreach (int s in new[] { -1, 1 })
        {
            GameObject brazo = HashiPiezas.Cubo(s < 0 ? "Brazo_Izq" : "Brazo_Der",
                figura.transform, new Vector3(s * 0.040f, 0.004f, 0.004f),
                new Vector3(0.042f, 0.024f, 0.020f), mat["Rojo"], false, capa);

            brazo.transform.localRotation = Quaternion.Euler(0f, 0f, s * -18f);
        }

        foreach (int s in new[] { -1, 1 })
        {
            HashiPiezas.Cubo(s < 0 ? "Pierna_Izq" : "Pierna_Der", figura.transform,
                new Vector3(s * 0.016f, 0.002f, -0.056f),
                new Vector3(0.024f, 0.022f, 0.056f), mat["AzulOsc"], false, capa);
        }

        HashiPiezas.SinSombras(figura);

        // ------------------------------------------------------------- fisica
        Rigidbody rb = raiz.AddComponent<Rigidbody>();
        rb.mass = 0.30f;

        BoxCollider col = raiz.AddComponent<BoxCollider>();
        col.size = TAMANO;

        Hashi.PrizeController pc = raiz.AddComponent<Hashi.PrizeController>();

        using (var a = new HashiCableado(pc))
        {
            a.Num("mass", 0.30f).V3("size", TAMANO)
             .V3("centerOfMass", new Vector3(0f, -0.012f, 0.010f))
             .Num("friction", 0.40f).Num("bounciness", 0.02f);
        }

        Comprobar();

        GameObject guardado = PrefabUtility.SaveAsPrefabAsset(raiz, RUTA);
        Object.DestroyImmediate(raiz);

        return guardado;
    }

    // Las mismas tres reglas que las demas cajas. Si alguien retoca el tamano
    // para que se vea mejor y la deja injugable, que salga aqui y no jugando.
    static void Comprobar()
    {
        const float SEPARACION_MAYOR = 0.190f;   // la de Facil
        const float HUECO_MENOR = 0.134f;        // el de Extremo

        if (TAMANO.z <= SEPARACION_MAYOR)
        {
            Debug.LogWarning("[Hashi] La caja del Funko mide " + TAMANO.z.ToString("0.000")
                             + " m cruzando las barras y en Facil estan a "
                             + SEPARACION_MAYOR.ToString("0.000") + ": se caeria sola.");
        }

        if (TAMANO.y >= HUECO_MENOR)
        {
            Debug.LogWarning("[Hashi] La caja del Funko mide " + TAMANO.y.ToString("0.000")
                             + " m de fondo y en Extremo el hueco es de "
                             + HUECO_MENOR.ToString("0.000") + ": no caeria nunca.");
        }

        if (TAMANO.x >= HashiWatashiBuilder.LARGO_MAXIMO_CAJA)
        {
            Debug.LogWarning("[Hashi] La caja del Funko es mas larga de lo que "
                             + "abarcan los brazos abiertos.");
        }
    }
}
