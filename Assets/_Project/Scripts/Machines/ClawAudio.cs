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

    [Header("Mezcla")]
    [Range(0f, 1f)] public float volumen = 0.8f;

    [Tooltip("A partir de aqui empieza a bajar con la distancia.")]
    public float distanciaMinima = 2f;

    [Tooltip("Mas lejos de esto ya no se oye.")]
    public float distanciaMaxima = 14f;

    AudioSource sueltos;
    AudioSource bucle;

    void Awake()
    {
        sueltos = Crear("Audio_Sueltos", false);
        bucle = Crear("Audio_Motor", true);
    }

    AudioSource Crear(string nombre, bool enBucle)
    {
        GameObject go = new GameObject(nombre);
        go.transform.SetParent(transform, false);

        AudioSource a = go.AddComponent<AudioSource>();
        a.playOnAwake = false;
        a.loop = enBucle;
        a.volume = volumen;

        // 3D del todo. A 0 sonaria igual de fuerte desde la otra punta del local.
        a.spatialBlend = 1f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.minDistance = distanciaMinima;
        a.maxDistance = distanciaMaxima;

        return a;
    }

    // ---------------------------------------------------------------- disparos

    public void Sonar(AudioClip clip, float vol = 1f)
    {
        if (clip == null || sueltos == null) return;

        // PlayOneShot y no Play: dos golpes seguidos tienen que solaparse, no
        // cortarse el uno al otro.
        sueltos.PlayOneShot(clip, volumen * vol);
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

    public void MotorCarro(bool encendido) { Motor(encendido ? motorCarro : null); }
    public void MotorCable(bool encendido) { Motor(encendido ? motorCable : null); }

    void Motor(AudioClip clip)
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
        bucle.volume = volumen * 0.55f;
        bucle.Play();
    }
}
