using System.Collections.Generic;
using UnityEngine;

// Una bombilla del local: da luz de verdad y se apaga con el interruptor.
//
// Se apunta sola en una lista al encenderse y se borra al destruirse, para que
// el interruptor no tenga que ir buscandolas por la escena cada vez que alguien
// le da. Buscar con FindObjectsByType es barato una vez y caro cada pulsacion,
// y ademas encontraria tambien las que esten dentro de una caja sin colocar.
public class Bombilla : MonoBehaviour
{
    static readonly List<Bombilla> todas = new List<Bombilla>();

    public static IList<Bombilla> Todas { get { return todas; } }

    [Tooltip("La luz. Si esta vacio se busca en el propio objeto.")]
    public Light luz;

    [Tooltip("Empieza encendida.")]
    public bool encendida = true;

    [Tooltip("Color de la parte que se ve encendida.")]
    public Color brillo = new Color(1f, 0.95f, 0.85f);

    [Tooltip("Lo que se ilumina. Vacio = todo el objeto.")]
    public Renderer[] brillantes;

    [Tooltip("Lo que tarda en encenderse del todo.")]
    public float suavizado = 12f;

    float nivel = -1f;
    Renderer[] pieles;
    MaterialPropertyBlock bloque;

    void Awake()
    {
        if (luz == null) luz = GetComponentInChildren<Light>(true);

        // Solo lo que brilla, si se ha dicho cual.
        //
        // En la pantalla LED la chapa y el difusor son piezas distintas: sin
        // esto, encender la luz encenderia tambien la carcasa y la luminaria
        // entera brillaria como una barra de neon.
        pieles = brillantes != null && brillantes.Length > 0
            ? brillantes
            : GetComponentsInChildren<Renderer>(true);
        bloque = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        if (!todas.Contains(this)) todas.Add(this);
    }

    void OnDisable()
    {
        todas.Remove(this);
    }

    public void Encender(bool si)
    {
        encendida = si;
    }

    public void Alternar()
    {
        encendida = !encendida;
    }

    void Update()
    {
        float objetivo = encendida ? 1f : 0f;

        if (nivel < 0f)
        {
            // La primera vez sin transicion: encendiendose desde cero al entrar
            // en la escena parpadearia cada vez que se carga la partida.
            nivel = objetivo;
        }
        else if (!Mathf.Approximately(nivel, objetivo))
        {
            nivel = Mathf.Lerp(nivel, objetivo,
                               1f - Mathf.Exp(-suavizado * Time.deltaTime));

            if (Mathf.Abs(nivel - objetivo) < 0.01f) nivel = objetivo;
        }
        else
        {
            return;
        }

        Aplicar();
    }

    void Aplicar()
    {
        if (luz != null)
        {
            // Apagada del todo se desactiva, que una luz a intensidad cero sigue
            // costando lo mismo de calcular.
            luz.enabled = nivel > 0.01f;
            luz.intensity = intensidadMaxima * nivel;
        }

        if (pieles == null) return;

        // El vidrio se enciende con el resto. Sin esto la bombilla ilumina la
        // habitacion pero ella misma se ve gris, que es lo que delata al momento
        // que la luz no sale de ahi.
        Color emision = brillo * nivel;

        foreach (Renderer r in pieles)
        {
            if (r == null) continue;

            r.GetPropertyBlock(bloque);
            bloque.SetColor("_EmissionColor", emision);
            r.SetPropertyBlock(bloque);
        }
    }

    // La intensidad que tenia puesta el prefab, que es la de "encendida".
    float intensidadMaxima
    {
        get
        {
            if (guardada < 0f) guardada = luz != null ? luz.intensity : 1f;

            return guardada;
        }
    }

    float guardada = -1f;

    // ------------------------------------------------------------- de fuera

    public static void EncenderTodas(bool si)
    {
        for (int i = 0; i < todas.Count; i++)
        {
            if (todas[i] != null) todas[i].Encender(si);
        }
    }

    // Si hay alguna encendida. Es lo que mira el interruptor para decidir: con
    // media casa encendida, darle al interruptor apaga -- que es lo que espera
    // cualquiera -- en vez de encender las que faltaban.
    public static bool AlgunaEncendida()
    {
        for (int i = 0; i < todas.Count; i++)
        {
            if (todas[i] != null && todas[i].encendida) return true;
        }

        return false;
    }
}
