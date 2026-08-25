using UnityEngine;

// El ruido de la caja de carton al caer.
//
// Va por colision y no por "el jugador ha soltado la caja" a proposito: asi
// suena tambien cuando rebota, cuando cae de un monton o cuando la tiras contra
// algo, que es cuando uno espera oirla.
//
// El problema de ir por colision es que una caja apoyada genera contactos sin
// parar, y sonaria como una ametralladora. Por eso hay dos filtros, y hacen
// falta los dos:
//
//   - por velocidad, para que un roce no suene como un golpe
//   - por tiempo, para que un solo aterrizaje con tres rebotes de un golpe y no
//     tres
//
// Solo con el de velocidad seguia repicando: una caja que se posa genera varios
// contactos fuertes seguidos en el mismo cuarto de segundo.
[RequireComponent(typeof(Rigidbody))]
public class GolpeCaja : MonoBehaviour
{
    public AudioClip golpe;

    [Tooltip("A partir de que velocidad de impacto suena, en m/s. Por debajo es "
             + "un roce y no se oye.")]
    public float velocidadMinima = 1.2f;

    [Tooltip("A que velocidad suena a todo volumen.")]
    public float velocidadFuerte = 4.5f;

    [Tooltip("Lo minimo entre dos golpes, en segundos.")]
    public float descanso = 0.22f;

    [Range(0f, 1f)] public float volumen = 0.7f;

    AudioSource altavoz;
    float proximo = 0f;

    void Awake()
    {
        altavoz = GetComponent<AudioSource>();

        if (altavoz == null) altavoz = gameObject.AddComponent<AudioSource>();

        altavoz.playOnAwake = false;

        // En 3D: una caja que cae al otro lado del local no tiene que sonar como
        // si la tuvieras en la cara.
        altavoz.spatialBlend = 1f;
        altavoz.rolloffMode = AudioRolloffMode.Linear;
        altavoz.minDistance = 1.5f;
        altavoz.maxDistance = 14f;
    }

    void OnCollisionEnter(Collision choque)
    {
        // La velocidad relativa del impacto, no la del rigidbody: una caja que
        // aterriza encima de otra que sube tambien es un golpe.
        Sonar(choque.relativeVelocity.magnitude);
    }

    // Tambien se puede pedir a mano. Hace falta para dejar la caja con el
    // fantasma de colocacion: ahi la caja aparece ya apoyada y no llega a chocar
    // con nada, asi que sin esto dejarla en el suelo era el unico caso mudo.
    public void Sonar(float fuerza)
    {
        if (golpe == null || Time.time < proximo) return;
        if (fuerza < velocidadMinima) return;

        proximo = Time.time + descanso;

        float t = Mathf.InverseLerp(velocidadMinima, velocidadFuerte, fuerza);

        float vol = Mathf.Lerp(0.25f, 1f, t) * volumen
                    * AjustesSonido.Mezcla(AjustesSonido.Canal.Efectos);

        // Un poco de tono distinto en cada golpe. Con el mismo clip exacto tres
        // veces seguidas se nota que es un archivo, no una caja.
        altavoz.pitch = Random.Range(0.92f, 1.08f);

        altavoz.PlayOneShot(golpe, vol);
    }
}
