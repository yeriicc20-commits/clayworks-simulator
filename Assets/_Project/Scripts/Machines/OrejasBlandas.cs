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
//
// Las orejas salian apuntando al techo, y la razon no era que se movieran mal:
// en un monton hay peluches que caen BOCA ABAJO, y con el tope viejo en 52
// grados la oreja no llegaba ni de lejos a volcarse, asi que se quedaba en su
// postura de reposo, que respecto al mundo es hacia arriba. Una oreja de trapo
// se vuelca del todo.
//
// Subir el tope pedia arreglar antes otras dos cosas, porque tal como estaba se
// habria puesto a atravesar la cabeza:
//
//   - El muelle trabaja sobre la DIRECCION en la que cuelga la oreja y no sobre
//     dos angulos de Euler sueltos. Con dos angulos por separado el tope no es
//     un tope: dos limites de 73 grados combinados dejan la oreja a 95 del
//     sitio. Sobre una direccion el tope es un angulo de verdad.
//
//   - Hacia dentro no se va nunca, que es por donde esta el craneo.
//
// Lo de restar posiciones dos veces para sacar la aceleracion tampoco estaba
// bien, y se ha filtrado, pero eso solo valia 13 grados de temblor: molesto, no
// la causa.
public class OrejasBlandas : MonoBehaviour
{
    public Transform[] orejas;

    [Header("Muelle")]
    [Tooltip("Cuanto tiran de volver a su sitio. Mas alto = mas tiesas.")]
    public float rigidez = 90f;

    [Tooltip("Cuanto se les va el balanceo. Mas alto = se paran antes.")]
    public float amortiguacion = 14f;

    [Tooltip("Cuanto cuenta el movimiento del peluche frente a la gravedad. "
             + "1 = como algo colgado de verdad. 0 = solo miran a la gravedad.")]
    public float respuesta = 1f;

    [Tooltip("Lo mas que pueden apartarse de su sitio, en grados. Puede ser "
             + "grande porque hacia dentro no se van nunca: eso lo corta aparte.")]
    public float anguloMaximo = 80f;

    [Tooltip("Cuanto se diferencia una oreja de la otra. A cero se mueven las "
             + "dos igual y parece una sola pieza en vez de dos orejas.")]
    [Range(0f, 0.5f)] public float desigualdad = 0.18f;

    Quaternion[] reposo;
    Vector3[] colgado;      // hacia donde apunta cada oreja en reposo
    Vector3[] direccion;    // hacia donde apunta ahora
    Vector3[] velocidad;
    Vector3[] fuera;        // el lado por el que puede irse sin comerse la cabeza
    float[] dureza;

    Vector3 posPrevia;
    Vector3 velSuave;
    Vector3 velPrevia;
    Vector3 acelSuave;
    bool listo = false;

    void Start()
    {
        if (orejas == null || orejas.Length == 0) { enabled = false; return; }

        int n = orejas.Length;

        reposo = new Quaternion[n];
        colgado = new Vector3[n];
        direccion = new Vector3[n];
        velocidad = new Vector3[n];
        fuera = new Vector3[n];
        dureza = new float[n];

        for (int i = 0; i < n; i++)
        {
            if (orejas[i] == null) continue;

            reposo[i] = orejas[i].localRotation;

            // Hacia donde cuelga la oreja tal como esta modelada. No se da por
            // hecho que sea hacia abajo: se saca de su propia orientacion, y
            // asi vale igual para una oreja que nazca torcida.
            colgado[i] = (reposo[i] * Vector3.down).normalized;
            direccion[i] = colgado[i];

            // Por que lado esta cosida. De su posicion, no de su nombre: el FBX
            // invierte la X al exportar y la oreja que se llama izquierda acaba
            // a la derecha.
            float lado = orejas[i].localPosition.x >= 0f ? 1f : -1f;
            fuera[i] = Vector3.right * lado;

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

        // Como se mueve el peluche. No se le pregunta al Rigidbody a proposito:
        // asi funciona igual cuando lo lleva la garra, que lo mueve por
        // transform y no por fisica.
        //
        // Pero restar posiciones dos veces amplifica el ruido por 1/dt^2: a 60
        // fps, un temblor de un milimetro entre fotogramas ya sale como 4 m/s2.
        // Simulado, un peluche PARADO en el monton meneaba las orejas 13 grados
        // de puro ruido. Filtrado se queda en 0,1.
        float suavizado = 1f - Mathf.Exp(-dt * 14f);

        Vector3 vel = (transform.position - posPrevia) / dt;
        posPrevia = transform.position;

        velSuave = Vector3.Lerp(velSuave, vel, suavizado);

        Vector3 acel = (velSuave - velPrevia) / dt;
        velPrevia = velSuave;

        acelSuave = Vector3.Lerp(acelSuave, acel, suavizado);

        // Una oreja no cuelga hacia abajo: cuelga hacia la gravedad APARENTE,
        // que es la de verdad mas la aceleracion del peluche. Lo mismo que le
        // pasa a lo que llevas colgado del retrovisor cuando el coche frena.
        //
        // Y va normalizada a proposito. Antes se sumaba la aceleracion en bruto,
        // en m/s2, a un vector unitario y se multiplicaba por el angulo maximo:
        // mezclar una direccion con una magnitud hace que cualquier empujon
        // desborde la cuenta y el objetivo se vaya de cabeza al tope. Metida
        // dentro de la gravedad aparente no puede pasar, porque por mucha
        // aceleracion que haya una direccion sigue siendo una direccion.
        Vector3 aparente = Physics.gravity - acelSuave * respuesta;

        if (aparente.sqrMagnitude < 1e-6f) aparente = Vector3.down;

        // Con el peluche tumbado, "abajo" ya no es su abajo: por eso se mira en
        // SU espacio y no en el del mundo.
        Vector3 quiere = transform.InverseTransformDirection(aparente.normalized);

        float tope = anguloMaximo * Mathf.Deg2Rad;

        for (int i = 0; i < orejas.Length; i++)
        {
            if (orejas[i] == null) continue;

            // Hacia dentro no puede irse. Medido girando la oreja alrededor de
            // su costura y mirando si algun vertice acababa dentro del craneo:
            // hacia fuera, hacia delante y hacia atras aguanta mas de 120
            // grados, pero hacia dentro se lo come a los CINCO. Normal, si nace
            // en el costado. Asi que a la direccion a la que quiere ir se le
            // quita la parte que apunta al eje del peluche, y lo que le queda es
            // apoyarse contra la cabeza y resbalar, que es lo que haria.
            float haciaDentro = Vector3.Dot(quiere, fuera[i]);

            Vector3 puede = haciaDentro < 0f
                            ? (quiere - fuera[i] * haciaDentro).normalized
                            : quiere;

            // Justo boca abajo, donde cuelga y donde querria colgar son
            // opuestos, y entre dos direcciones opuestas el eje de giro no esta
            // definido: la oreja se iria por cualquier lado, incluso cruzando la
            // cabeza. Se la empuja hacia fuera, que es el unico lado libre. El
            // empujon crece segun se acerca a ese punto ciego, con lo que fuera
            // de el no desvia nada y dentro elige el camino sin dar tirones.
            float ciego = Mathf.Clamp01(-Vector3.Dot(puede, colgado[i]));
            puede = (puede + fuera[i] * (ciego * ciego * 0.35f)).normalized;

            // Es de tela, no de goma: por muy tumbado que este el peluche, la
            // oreja no puede darse la vuelta entera sobre la costura.
            Vector3 objetivo = Vector3.RotateTowards(colgado[i], puede, tope, 0f);

            float k = rigidez * dureza[i];
            Vector3 fuerza = -k * (direccion[i] - objetivo) - amortiguacion * velocidad[i];

            velocidad[i] += fuerza * dt;
            direccion[i] += velocidad[i] * dt;

            // Red de seguridad: con dt muy grande (una pausa, una carga) el
            // muelle explota, y un NaN en un Quaternion no se recupera solo.
            if (!Finito(direccion[i]) || !Finito(velocidad[i])
                || direccion[i].sqrMagnitude < 1e-8f)
            {
                direccion[i] = colgado[i];
                velocidad[i] = Vector3.zero;
            }

            // El muelle se puede pasar de largo, asi que el tope se vuelve a
            // aplicar sobre el resultado y no solo sobre el objetivo.
            direccion[i] = Vector3.RotateTowards(colgado[i], direccion[i].normalized,
                                                 tope * 1.25f, 0f);

            orejas[i].localRotation =
                Quaternion.FromToRotation(colgado[i], direccion[i]) * reposo[i];
        }
    }

    static bool Finito(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsInfinity(v.x)
            && !float.IsNaN(v.y) && !float.IsInfinity(v.y)
            && !float.IsNaN(v.z) && !float.IsInfinity(v.z);
    }
}
