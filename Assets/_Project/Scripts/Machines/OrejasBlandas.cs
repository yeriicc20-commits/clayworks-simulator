using UnityEngine;

// Orejas que cuelgan y se balancean solas.
//
// Va por muelle y no por articulaciones fisicas, y es una decision, no un
// atajo. Una oreja con Rigidbody colgada del cuerpo del peluche seria un cuerpo
// dentro de otro cuerpo, que es exactamente lo que dejo la garra dando tumbos
// durante media sesion: la jerarquia de transforms mueve al hijo por un lado y
// PhysX lo mueve por otro. Ademas serian dos cuerpos mas por peluche, y en la
// maquina hay veinte.
//
// Un muelle por oreja da el mismo resultado a la vista, no puede reventar, y no
// cuesta nada. La fisica de verdad se reserva para lo que decide el juego: si
// la garra agarra o no.
public class OrejasBlandas : MonoBehaviour
{
    public Transform[] orejas;

    [Header("Muelle")]
    [Tooltip("Cuanto tiran de volver a su sitio. Mas alto = mas tiesas.")]
    public float rigidez = 110f;

    [Tooltip("Cuanto se les va el balanceo. Mas alto = se paran antes.")]
    public float amortiguacion = 9f;

    [Tooltip("Cuanto reaccionan a que el peluche se mueva.")]
    public float sensibilidad = 26f;

    [Tooltip("Lo mas que pueden apartarse de su sitio, en grados.")]
    public float anguloMaximo = 52f;

    [Tooltip("Cuanto se diferencia una oreja de la otra. A cero se mueven las "
             + "dos igual y parece una sola pieza en vez de dos orejas.")]
    [Range(0f, 0.5f)] public float desigualdad = 0.18f;

    Quaternion[] reposo;
    Vector2[] inclinacion;
    Vector2[] velocidad;
    float[] dureza;

    Vector3 posPrevia;
    Vector3 velPrevia;
    bool listo = false;

    void Start()
    {
        if (orejas == null || orejas.Length == 0) { enabled = false; return; }

        int n = orejas.Length;

        reposo = new Quaternion[n];
        inclinacion = new Vector2[n];
        velocidad = new Vector2[n];
        dureza = new float[n];

        for (int i = 0; i < n; i++)
        {
            if (orejas[i] == null) continue;

            reposo[i] = orejas[i].localRotation;

            // Cada oreja con su propia dureza. Sin esto se mueven las dos
            // exactamente igual y se nota que es un truco.
            dureza[i] = 1f + Random.Range(-desigualdad, desigualdad);
        }

        posPrevia = transform.position;
        listo = true;
    }

    void LateUpdate()
    {
        if (!listo) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // Aceleracion del peluche, sacada de como se mueve. No se pregunta al
        // Rigidbody a proposito: asi funciona igual cuando lo lleva la garra,
        // que lo mueve por transform y no por fisica.
        Vector3 vel = (transform.position - posPrevia) / dt;
        posPrevia = transform.position;

        Vector3 acel = (vel - velPrevia) / dt;
        velPrevia = vel;

        // Adonde querrian estar: colgando hacia abajo, y echadas hacia atras
        // por la aceleracion. Con el peluche tumbado, "abajo" ya no es su abajo,
        // y por eso se miran en SU espacio y no en el del mundo.
        Vector3 abajo = transform.InverseTransformDirection(Vector3.down);
        Vector3 empuje = transform.InverseTransformDirection(acel) * (sensibilidad * 0.01f);

        float objetivoX = Mathf.Clamp((abajo.z - empuje.z) * anguloMaximo, -anguloMaximo, anguloMaximo);
        float objetivoZ = Mathf.Clamp((-abajo.x + empuje.x) * anguloMaximo, -anguloMaximo, anguloMaximo);

        for (int i = 0; i < orejas.Length; i++)
        {
            if (orejas[i] == null) continue;

            float k = rigidez * dureza[i];

            Vector2 objetivo = new Vector2(objetivoX, objetivoZ);
            Vector2 fuerza = -k * (inclinacion[i] - objetivo) - amortiguacion * velocidad[i];

            velocidad[i] += fuerza * dt;
            inclinacion[i] += velocidad[i] * dt;

            // Red de seguridad: con dt muy grande (una pausa, una carga) el
            // muelle explota, y un NaN en un Quaternion no se recupera solo.
            if (!Finito(inclinacion[i]) || !Finito(velocidad[i]))
            {
                inclinacion[i] = Vector2.zero;
                velocidad[i] = Vector2.zero;
            }

            inclinacion[i] = new Vector2(
                Mathf.Clamp(inclinacion[i].x, -anguloMaximo * 1.4f, anguloMaximo * 1.4f),
                Mathf.Clamp(inclinacion[i].y, -anguloMaximo * 1.4f, anguloMaximo * 1.4f));

            orejas[i].localRotation = reposo[i]
                * Quaternion.Euler(inclinacion[i].x, 0f, inclinacion[i].y);
        }
    }

    static bool Finito(Vector2 v)
    {
        return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y);
    }
}
