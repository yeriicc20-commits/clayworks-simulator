using UnityEngine;

// Los focos de dentro de la maquina, los que alumbran el monton de peluches.
//
// Una maquina de garra sin luz dentro no se ve. El cristal refleja, el mueble
// hace sombra y los peluches quedan en penumbra -- que es justo lo que hay que
// ver para decidir a cual tirar. Con el local a oscuras, mas todavia: la
// maquina pasa a ser un armario negro.
//
// Se pone sola en cada maquina que encuentre. Hacerlo solo en el prefab dejaria
// sin luz a las que ya estan colocadas en la partida, y no se puede pedir a
// nadie que las tire y las vuelva a comprar por esto.
public class LucesEscaparate : MonoBehaviour
{
    [Tooltip("Cuantos focos se reparten a lo largo del escaparate.")]
    [Range(1, 4)] public int cuantos = 2;

    [Tooltip("Cuanto por debajo del techo del mueble van.")]
    public float bajoElTecho = 0.12f;

    [Tooltip("Color de la luz de dentro.")]
    public Color color = new Color(1f, 0.96f, 0.86f);

    public float intensidad = 2.2f;

    [Tooltip("Hasta donde llega. Es una vitrina, no hace falta mas.")]
    public float alcance = 2.6f;

    const string NOMBRE = "Luz_Escaparate";

    // Se instalan solas en todo lo que sea una maquina de garra.
    //
    // Al cargar la escena para las que ya estan, y desde el prefab para las que
    // se compren despues.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AlArrancar()
    {
        foreach (ClawController maquina in
                 FindObjectsByType<ClawController>(FindObjectsSortMode.None))
        {
            if (maquina == null) continue;
            if (maquina.GetComponent<LucesEscaparate>() != null) continue;

            maquina.gameObject.AddComponent<LucesEscaparate>();
        }
    }

    void Start()
    {
        Montar();
    }

    void Montar()
    {
        // Si ya estan puestas no se duplican: esto puede correr al arrancar y
        // otra vez si alguien recoloca la maquina.
        if (transform.Find(NOMBRE + "_1") != null) return;

        ClawController claw = GetComponent<ClawController>();
        if (claw == null) return;

        Bounds caja = LimitesLocales();
        if (caja.size == Vector3.zero) return;

        // Justo bajo el techo del mueble, que es donde van en las de verdad.
        float alto = caja.max.y - bajoElTecho;

        float centroX = (claw.limitXMin + claw.limitXMax) * 0.5f;
        float centroZ = (claw.limitZMin + claw.limitZMax) * 0.5f;

        // Repartidos a lo ancho de la zona por la que se mueve la garra, que es
        // exactamente donde estan los peluches.
        float ancho = claw.limitXMax - claw.limitXMin;

        for (int i = 0; i < cuantos; i++)
        {
            float t = cuantos == 1 ? 0.5f : (float)i / (cuantos - 1);
            float x = centroX + (t - 0.5f) * ancho * 0.62f;

            Crear(NOMBRE + "_" + (i + 1), new Vector3(x, alto, centroZ));
        }
    }

    void Crear(string nombre, Vector3 donde)
    {
        GameObject nodo = new GameObject(nombre);
        nodo.transform.SetParent(transform, false);
        nodo.transform.localPosition = donde;

        // Apuntando al suelo del monton. Un foco y no una luz de punto: dentro
        // de una vitrina, una luz de punto se cuela por el cristal e ilumina la
        // sala desde dentro de la maquina.
        nodo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        Light luz = nodo.AddComponent<Light>();

        luz.type = LightType.Spot;
        luz.spotAngle = 110f;
        luz.innerSpotAngle = 55f;
        luz.color = color;
        luz.intensity = intensidad;
        luz.range = alcance;

        // Sin sombras, y a proposito. Son dos focos por maquina: con cinco
        // maquinas serian diez luces con sombras, y eso se nota en cuanto el
        // local crece. Dentro de una vitrina, con todo a la vista y a medio
        // metro, no se echan de menos.
        luz.shadows = LightShadows.None;
    }

    // Los limites de la maquina en sus propias coordenadas.
    Bounds LimitesLocales()
    {
        Bounds b = new Bounds();
        bool primero = true;

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is LineRenderer) continue;

            Bounds local = new Bounds(
                transform.InverseTransformPoint(r.bounds.center), r.bounds.size);

            if (primero)
            {
                b = local;
                primero = false;
                continue;
            }

            b.Encapsulate(local);
        }

        return primero ? new Bounds() : b;
    }
}
