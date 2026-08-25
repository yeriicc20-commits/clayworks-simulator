using UnityEngine;

// Busca donde dejar una caja recien comprada para que se quede quieta.
//
// El pedido se soltaba a ciegas: una posicion fija por cada caja, separadas
// metro y medio, y a rezar. Si habia algo delante la caja aparecia dentro y
// PhysX la escupia; si el sitio caia fuera del suelo, se iba por el borde; y si
// aparecia medio metida en la pared, salia disparada al otro lado.
//
// Aqui no se suelta nada hasta haber comprobado cuatro cosas sobre el sitio:
//
//   1. que hay algo debajo en toda la huella, no solo en el centro
//   2. que eso de debajo esta plano
//   3. que no hay nada ocupando el hueco
//   4. que si el apoyo es otra caja, la de abajo es al menos igual de grande
//
// La cuarta es la que hace que las grandes queden debajo sin tener que ordenar
// el pedido: una caja grande simplemente no encuentra sitio encima de una
// pequena y se va al suelo, y una pequena si puede subirse a una grande.
public static class ApiladorCajas
{
    // Holgura entre cajas. Pegadas al milimetro, PhysX las considera en contacto
    // desde el primer paso y se pasa el rato separandolas: se ve el monton
    // vibrando.
    const float HOLGURA = 0.02f;

    // Cuanto se encoge la caja para preguntar si el hueco esta libre. Sin esto,
    // la propia holgura de contacto de los vecinos cuenta como ocupado y no cabe
    // nunca nada.
    const float ENCOGER = 0.94f;

    // Lo mas que puede desnivelarse el apoyo. 0,96 son unos 16 grados.
    const float LLANO = 0.96f;

    // Lo alto que puede llegar un monton, medido desde el punto de entrega.
    //
    // Sin tope, quince cajas en un sitio pequeno hacian una sola columna de seis
    // metros y medio: se apilaban una encima de otra porque tecnicamente cabian.
    // Una torre asi ni es estable ni se puede coger la de arriba. Con este
    // limite se reparten a lo ancho primero y solo suben lo que sube un monton
    // de verdad.
    const float ALTURA_MAX = 1.6f;

    static readonly Collider[] vecinos = new Collider[16];

    // Coloca la caja y devuelve si ha encontrado sitio.
    //
    // 'centro' es donde llega el pedido y 'radio' lo lejos que puede buscar. Si
    // no encuentra nada la deja en el centro: mas vale una caja mal puesta que
    // una compra que se pierde.
    public static bool Colocar(GameObject caja, Vector3 centro, float radio)
    {
        if (caja == null) return false;

        Vector3 tam = Tamano(caja);

        if (tam.x < 0.01f || tam.z < 0.01f)
        {
            Debug.LogWarning("[Cajas] " + caja.name + " no tiene ni renderers ni "
                             + "colliders con los que medirla. La dejo donde cae.", caja);
            return false;
        }

        // El paso de rejilla es la propia caja: asi las posiciones que se prueban
        // ya vienen sin solaparse entre ellas y el monton sale alineado en vez de
        // como si lo hubieran tirado.
        float paso = Mathf.Max(tam.x, tam.z) + HOLGURA;
        int anillos = Mathf.Max(1, Mathf.CeilToInt(radio / paso));

        // Tres vueltas, de mejor a peor, y el orden es lo importante:
        //
        //   1. solo suelo
        //   2. encima de otra caja, sin pasar de ALTURA_MAX
        //   3. encima de otra caja, a la altura que sea
        //
        // De una sola vuelta, la primera posicion que se prueba es el centro, y
        // ahi suele haber ya una caja: se apilaba la segunda encima de la
        // primera con todo el patio libre al lado. Amontonar es lo que se hace
        // cuando no queda sitio, no lo primero que se intenta.
        //
        // Y la tercera existe porque el tope de altura tenia que ceder ante algo
        // peor. En un sitio pequeno dejaba siete cajas de quince sin colocar, y
        // esas acababan soltadas en el centro unas dentro de otras. Una torre
        // alta se ve rara; una caja metida dentro de otra es un fallo.
        for (int vuelta = 0; vuelta < 3; vuelta++)
        {
            bool soloSuelo = vuelta == 0;
            bool conTope = vuelta <= 1;

            foreach (Vector2 punto in EnEspiral(anillos, paso))
            {
                Vector3 sitio;
                Vector3 columna = centro + new Vector3(punto.x, 0f, punto.y);

                if (!Apoyo(columna, tam, caja, soloSuelo, conTope, out sitio)) continue;

                Posar(caja, sitio);
                return true;
            }
        }

        Debug.LogWarning("[Cajas] No encuentro sitio libre para " + caja.name
                         + " en " + radio.ToString("F1") + " m. La dejo en el centro.", caja);

        Posar(caja, centro + Vector3.up * (tam.y * 0.5f));
        return false;
    }

    // Recorre la rejilla del centro hacia fuera, para que el monton crezca desde
    // el punto de entrega y no empiece por una esquina.
    static System.Collections.Generic.IEnumerable<Vector2> EnEspiral(int anillos, float paso)
    {
        yield return Vector2.zero;

        for (int r = 1; r <= anillos; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int z = -r; z <= r; z++)
                {
                    // Solo el borde del anillo: lo de dentro ya se probo.
                    if (Mathf.Abs(x) != r && Mathf.Abs(z) != r) continue;

                    yield return new Vector2(x * paso, z * paso);
                }
            }
        }
    }

    // Busca a que altura se queda la caja en esta columna, si es que cabe.
    static bool Apoyo(Vector3 columna, Vector3 tam, GameObject caja, bool soloSuelo,
                      bool conTope, out Vector3 sitio)
    {
        sitio = Vector3.zero;

        float mediaX = tam.x * 0.5f;
        float mediaZ = tam.z * 0.5f;

        // Las cuatro esquinas de la huella y el centro. Con el centro solo, una
        // caja puesta al borde de otra se sostiene sobre un pico y se cae en
        // cuanto arranca la fisica.
        Vector3[] patas =
        {
            columna,
            columna + new Vector3(-mediaX * 0.9f, 0f, -mediaZ * 0.9f),
            columna + new Vector3(mediaX * 0.9f, 0f, -mediaZ * 0.9f),
            columna + new Vector3(-mediaX * 0.9f, 0f, mediaZ * 0.9f),
            columna + new Vector3(mediaX * 0.9f, 0f, mediaZ * 0.9f),
        };

        float arriba = columna.y + 6f;
        float alto = float.MinValue;
        float bajo = float.MaxValue;
        Collider soporte = null;

        foreach (Vector3 pata in patas)
        {
            RaycastHit golpe;

            if (!Physics.Raycast(new Vector3(pata.x, arriba, pata.z), Vector3.down,
                                 out golpe, 14f, ~0, QueryTriggerInteraction.Ignore))
                return false;

            if (EsLaCaja(golpe.collider, caja)) return false;
            if (golpe.normal.y < LLANO) return false;

            if (golpe.point.y > alto)
            {
                alto = golpe.point.y;
                soporte = golpe.collider;
            }

            bajo = Mathf.Min(bajo, golpe.point.y);
        }

        // Las cinco patas tienen que apoyar casi a la misma altura. Si una cae
        // seis centimetros mas abajo, la caja esta a caballo de un escalon y se
        // va a volcar en cuanto la suelte.
        if (alto - bajo > 0.06f) return false;

        // Si el apoyo es otra caja, la de abajo manda: no se apila una grande
        // sobre una pequena porque no se sostiene, y ademas queda ridiculo.
        CarriableBox debajo = soporte != null
                              ? soporte.GetComponentInParent<CarriableBox>() : null;

        if (debajo != null)
        {
            // En la primera vuelta solo vale el suelo: amontonar es el plan B.
            if (soloSuelo) return false;

            if (conTope && alto > columna.y + ALTURA_MAX) return false;

            Vector3 suya = Tamano(debajo.gameObject);

            if (suya.x < tam.x - 0.02f || suya.z < tam.z - 0.02f) return false;
        }

        Vector3 candidato = new Vector3(columna.x, alto + tam.y * 0.5f + HOLGURA, columna.z);

        // Y que el hueco este libre de verdad. Aqui es donde se descarta meterla
        // dentro de una pared: la pared es un collider como cualquier otro.
        Vector3 media = tam * 0.5f * ENCOGER;

        int cuantos = Physics.OverlapBoxNonAlloc(candidato, media, vecinos,
                                                 Quaternion.identity, ~0,
                                                 QueryTriggerInteraction.Ignore);

        for (int i = 0; i < cuantos; i++)
        {
            if (!EsLaCaja(vecinos[i], caja)) return false;
        }

        sitio = candidato;
        return true;
    }

    static bool EsLaCaja(Collider col, GameObject caja)
    {
        return col != null && col.transform.IsChildOf(caja.transform);
    }

    // Deja la caja quieta de verdad.
    static void Posar(GameObject caja, Vector3 centro)
    {
        // El centro que se ha calculado es el de la envolvente, no el del
        // objeto: si el pivote de la caja esta en su base, colocar el pivote ahi
        // la deja media caja mas arriba flotando.
        Vector3 desfase = caja.transform.position - Centro(caja);

        caja.transform.position = centro + desfase;

        Rigidbody rb = caja.GetComponent<Rigidbody>();
        if (rb == null) return;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Que se separe despacio si algo la roza, en vez de salir rebotada.
        rb.maxDepenetrationVelocity = 0.4f;

        // Y dormida. El sitio ya se ha comprobado, asi que no hay nada que
        // simular: dejarla despierta solo sirve para que tiemble un segundo
        // mientras se acomoda. Se despierta sola en cuanto algo la toque.
        rb.Sleep();
    }

    public static Vector3 Tamano(GameObject go)
    {
        return Envolvente(go).size;
    }

    static Vector3 Centro(GameObject go)
    {
        return Envolvente(go).center;
    }

    static Bounds Envolvente(GameObject go)
    {
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        bool primero = true;

        // Los colliders mandan sobre los renderers: es con lo que va a chocar.
        foreach (Collider col in go.GetComponentsInChildren<Collider>())
        {
            if (col.isTrigger) continue;

            if (primero) { b = col.bounds; primero = false; }
            else b.Encapsulate(col.bounds);
        }

        if (primero)
        {
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (primero) { b = r.bounds; primero = false; }
                else b.Encapsulate(r.bounds);
            }
        }

        return b;
    }
}
