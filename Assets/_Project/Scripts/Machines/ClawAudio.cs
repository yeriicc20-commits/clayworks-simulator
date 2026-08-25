using UnityEngine;

// Sonido de la maquina de garra.
//
// Dos fuentes y no una: los motores suenan EN BUCLE mientras algo se mueve, y
// los golpes y avisos son disparos sueltos. Con una sola fuente, cada moneda
// cortaria el zumbido del motor a media partida.
//
// Todo en 3D con caida por distancia: una sala con seis maquinas tiene que
// sonar a sala, no a las seis a la vez dentro de tu cabeza.
public class ClawAudio : MonoBehaviour
{
    [Header("Golpes y avisos")]
    public AudioClip moneda;
    public AudioClip aviso;
    public AudioClip alarma;
    public AudioClip boton;
    public AudioClip garraCierra;
    public AudioClip garraAbre;
    public AudioClip premio;
    public AudioClip fallo;
    public AudioClip pelucheCae;
    public AudioClip tope;

    [Header("Motores, en bucle")]
    public AudioClip motorCarro;
    public AudioClip motorCable;

    [Tooltip("Volumen de los motores respecto al resto. Van de fondo: son un "
             + "zumbido continuo, y un zumbido continuo cansa mucho antes que "
             + "un golpe suelto al mismo volumen.")]
    [Range(0f, 1f)] public float volumenMotor = 0.10f;

    [Tooltip("El motor del cable, el que sube y baja la garra. Apagado: se "
             + "solapaba con el del carro y con la musica justo en el momento "
             + "de mas tension, y no aportaba nada. El sonido sigue ahi por si "
             + "algun dia se quiere recuperar.")]
    public bool motorCableAudible = false;

    [Header("Musica")]
    [Tooltip("Suena mientras hay partida en marcha, no todo el rato: una sala "
             + "con seis maquinas sonando a la vez es ruido, no ambiente.")]
    public AudioClip musica;

    [Range(0f, 1f)] public float volumenMusica = 0.26f;

    [Header("Mezcla")]
    [Range(0f, 1f)] public float volumen = 0.8f;

    [Tooltip("A partir de aqui empieza a bajar con la distancia.")]
    public float distanciaMinima = 1.8f;

    [Tooltip("Mas lejos de esto ya no se oye un sonido suelto: la moneda, el "
             + "premio, el fallo. Duran poco, asi que pueden llegar mas lejos.")]
    public float distanciaMaxima = 11f;

    [Tooltip("Lo mismo para lo que suena SIN PARAR: el motor y la musica. Mucho "
             + "mas corto a proposito. Son los npc jugando todo el rato, y si "
             + "llegan lejos el local entero acaba lleno de musica de "
             + "recreativa.")]
    public float distanciaContinua = 6f;

    AudioSource sueltos;
    AudioSource bucle;
    AudioSource musical;

    void OnEnable() { AjustesSonido.Cambiado += Remezclar; }
    void OnDisable() { AjustesSonido.Cambiado -= Remezclar; }

    // Los sonidos que ya estan sonando tienen que cambiar de volumen sobre la
    // marcha. Los sueltos no hace falta: duran menos que el tiempo que tarda
    // alguien en soltar el boton.
    void Remezclar()
    {
        if (musical != null)
        {
            musical.volume = volumenMusica * AjustesSonido.Mezcla(AjustesSonido.Canal.Musica);
        }

        if (bucle != null && bucle.isPlaying)
        {
            bucle.volume = volumen * volumenMotor
                           * AjustesSonido.Mezcla(AjustesSonido.Canal.Motores);
        }
    }

    void Awake()
    {
        sueltos = Crear("Audio_Sueltos", false, distanciaMaxima);
        bucle = Crear("Audio_Motor", true, distanciaContinua);

        // Tercera fuente para la musica: si compartiese la de los motores, el
        // zumbido del carro la cortaria en seco cada vez que te mueves.
        musical = Crear("Audio_Musica", true, distanciaContinua);
        musical.volume = volumenMusica;
    }

    AudioSource Crear(string nombre, bool enBucle, float lejos)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(transform, false);

        AudioSource a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = enBucle;
        a.volume = volumen;

        // 3D del todo. A 0 sonaria igual de fuerte desde la otra punta del local.
        a.spatialBlend = 1f;
        float cerca = Mathf.Min(distanciaMinima, lejos * 0.4f);

        a.maxDistance = lejos;
        a.minDistance = cerca;
        a.rolloffMode = AudioRolloffMode.Custom;
        a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Caida(cerca, lejos));

        return a;
    }

    // Como baja el volumen con la distancia.
    //
    // Iba en linea recta, que es lo que trae Unity por defecto, y eso es lo que
    // hacia que no hubiera forma de escaparse del ruido: en linea recta, a mitad
    // de camino del corte todavia se oye al 50%. Con una maquina sola se
    // aguanta; con varias y los npc echando partidas sin parar, el local entero
    // queda cubierto y siempre estas dentro de alguna.
    //
    // El sonido de verdad no baja en linea recta, baja con el cuadrado de la
    // distancia: a mitad de camino ya va por menos de la cuarta parte. Esta
    // curva imita eso y ademas llega a cero de verdad en el corte, para que no
    // se oiga el salto al cruzar el limite.
    //
    // La curva va en distancia NORMALIZADA de 0 a 1, donde el 1 es maxDistance:
    // es lo que espera SetCustomCurve.
    //
    // Y la meseta del principio tiene que ir DENTRO de la curva. Con rolloff
    // personalizado, minDistance deja de recortar nada: manda la curva y solo la
    // curva. Sin la meseta, la primera version bajaba al 27% estando pegado a la
    // maquina, o sea que dejaba de oirse justo donde tiene que oirse.
    static AnimationCurve Caida(float cerca, float lejos)
    {
        float meseta = Mathf.Clamp01(cerca / Mathf.Max(0.01f, lejos));

        AnimationCurve c = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(meseta, 1f),
            new Keyframe(Mathf.Lerp(meseta, 1f, 0.22f), 0.42f),
            new Keyframe(Mathf.Lerp(meseta, 1f, 0.45f), 0.15f),
            new Keyframe(Mathf.Lerp(meseta, 1f, 0.72f), 0.04f),
            new Keyframe(1f, 0f));

        // La meseta se deja recta: suavizandola tambien, la curva se pasa por
        // encima de 1 y el sonido saldria mas fuerte de lo que le toca.
        for (int i = 2; i < c.length; i++)
        {
            c.SmoothTangents(i, 0f);
        }

        return c;
    }

    // ---------------------------------------------------------------- disparos

    public void Sonar(AudioClip clip, float vol = 1f)
    {
        if (clip == null || sueltos == null) return;

        // PlayOneShot y no Play: dos golpes seguidos tienen que solaparse, no
        // cortarse el uno al otro.
        sueltos.PlayOneShot(clip, volumen * vol
                            * AjustesSonido.Mezcla(AjustesSonido.Canal.Efectos));
    }

    public void Moneda() { Sonar(moneda); }
    public void Boton() { Sonar(boton, 0.7f); }
    public void Cierra() { Sonar(garraCierra); }
    public void Abre() { Sonar(garraAbre, 0.8f); }
    public void Premio() { Sonar(premio); }
    public void Fallo() { Sonar(fallo, 0.7f); }
    public void PelucheCae() { Sonar(pelucheCae, 0.9f); }
    public void Tope() { Sonar(tope, 0.6f); }

    // ----------------------------------------------------------------- motores

    public void Aviso() { Sonar(aviso); }

    public void MotorCarro(bool encendido) { Motor(encendido ? motorCarro : null); }

    // La alarmita de la bajada comparte la fuente de los motores a proposito:
    // mientras la garra baja el carro esta quieto, asi que nunca coinciden.
    public void Alarma(bool encendida)
    {
        if (encendida) Motor(alarma, 0.5f);
        else Motor(null);
    }
    public void MotorCable(bool encendido)
    {
        Motor(encendido && motorCableAudible ? motorCable : null);
    }

    // ----------------------------------------------------------------- musica

    public void Musica(bool encendida)
    {
        if (musical == null || musica == null) return;

        if (!encendida)
        {
            if (musical.isPlaying) musical.Stop();
            return;
        }

        if (musical.isPlaying) return;

        musical.clip = musica;
        musical.volume = volumenMusica * AjustesSonido.Mezcla(AjustesSonido.Canal.Musica);
        musical.Play();
    }

    void Motor(AudioClip clip, float vol = -1f)
    {
        if (bucle == null) return;

        if (clip == null)
        {
            if (bucle.isPlaying) bucle.Stop();
            return;
        }

        // Si ya esta sonando ese mismo, no se reinicia: rearrancarlo cada
        // fotograma dejaria el motor tartamudeando.
        if (bucle.clip == clip && bucle.isPlaying) return;

        bucle.clip = clip;
        bucle.volume = volumen * (vol < 0f ? volumenMotor : vol)
                       * AjustesSonido.Mezcla(AjustesSonido.Canal.Motores);
        bucle.Play();
    }
}
