using UnityEngine;

// Parpadeo de los mandos: la bola del joystick en rojo y el boton en azul.
//
// Esto no puede venir del modelo. Un FBX lleva geometria y materiales, pero no
// animacion de emision, asi que en Blender solo se puede dejar el color en
// reposo. El pulso tiene que salir de aqui.
//
// Va por MaterialPropertyBlock y no tocando el material: cambiar
// renderer.material clona el material para esa instancia, y con quince
// maquinas en la tienda son quince materiales y quince draw calls de mas.
// Con el bloque de propiedades todas siguen compartiendo el mismo material.
[RequireComponent(typeof(Renderer))]
public class ArcadeBlink : MonoBehaviour
{
    public enum Forma { Suave, Bombilla }

    [Tooltip("Color del pulso. Rojo para la bola, azul para el boton.")]
    public Color color = Color.red;

    [Tooltip("Segundos que tarda un ciclo completo.")]
    public float periodo = 1.1f;

    [Tooltip("Suave = respiracion. Bombilla = encendido y apagado seco.")]
    public Forma forma = Forma.Suave;

    [Tooltip("Brillo en el punto mas apagado del ciclo.")]
    public float minimo = 0.12f;

    [Tooltip("Brillo en el pico.")]
    public float maximo = 2.6f;

    [Tooltip("Que material del renderer se ilumina. -1 = todos. Se usa cuando "
             + "la pieza trae varios materiales, como el joystick, que es vastago "
             + "y bola en una sola malla: sin esto se encenderia entero.")]
    public int materialIndex = -1;

    [Tooltip("Si esta encendida, cada mando arranca en un punto distinto del "
             + "ciclo. Sin esto una fila de maquinas parpadea a la vez y parece "
             + "un estroboscopio en vez de una sala de recreativos.")]
    public bool desfasar = true;

    static readonly int ID_EMISION = Shader.PropertyToID("_EmissionColor");
    static readonly int ID_BASE = Shader.PropertyToID("_BaseColor");

    Renderer rend;
    MaterialPropertyBlock bloque;
    float desfase;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        bloque = new MaterialPropertyBlock();
        desfase = desfasar ? Random.value : 0f;
    }

    void OnEnable()
    {
        // El material tiene que traer la emision habilitada o el shader ignora
        // _EmissionColor por mucho que se lo escribamos. Se hace sobre el
        // material compartido a proposito: es una vez y vale para todas.
        Material objetivo = MaterialObjetivo();

        if (objetivo != null)
        {
            objetivo.EnableKeyword("_EMISSION");
            objetivo.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        Aplicar(minimo);
    }

    void OnDisable()
    {
        // Se deja en reposo, no a oscuras: una maquina apagada del todo parece
        // rota, y este componente se desactiva por LOD cuando el jugador se
        // aleja, no porque la maquina deje de funcionar.
        Aplicar(minimo);
    }

    void Update()
    {
        float t = Time.time / Mathf.Max(0.05f, periodo) + desfase;

        float onda = forma == Forma.Suave
            ? Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f
            : (Mathf.Repeat(t, 1f) < 0.5f ? 1f : 0f);

        Aplicar(Mathf.Lerp(minimo, maximo, onda));
    }

    void Aplicar(float intensidad)
    {
        if (rend == null) return;

        int i = Indice();

        if (i < 0) rend.GetPropertyBlock(bloque);
        else rend.GetPropertyBlock(bloque, i);

        bloque.SetColor(ID_EMISION, color * intensidad);
        bloque.SetColor(ID_BASE, color);

        if (i < 0) rend.SetPropertyBlock(bloque);
        else rend.SetPropertyBlock(bloque, i);
    }

    // -1 si hay que iluminar el renderer entero; si no, la submalla pedida,
    // siempre que exista de verdad.
    int Indice()
    {
        if (materialIndex < 0) return -1;

        Material[] mats = rend.sharedMaterials;
        return materialIndex < mats.Length ? materialIndex : -1;
    }

    Material MaterialObjetivo()
    {
        if (rend == null) return null;

        Material[] mats = rend.sharedMaterials;
        if (mats.Length == 0) return null;

        int i = Indice();
        return i < 0 ? mats[0] : mats[i];
    }
}
