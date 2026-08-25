using UnityEditor;
using UnityEngine;

// Temporal: mide la maquina y los peluches para saber que esta mal de verdad.
public static class _TempMedir
{
    public static void Medir()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/Machines/MaquinaGarra.prefab");

        ClawController c = prefab.GetComponent<ClawController>();

        // --- que lado es el de delante ---------------------------------------
        string lados = "";
        foreach (string n in new[] { "Consola_Cuerpo", "Trampilla", "Cartel_Frente",
                                     "Monedero_1", "Joystick" })
        {
            Transform t = Buscar(prefab.transform, n);
            if (t == null) { lados += "\n      " + n + ": NO ESTA"; continue; }

            Renderer r = t.GetComponent<Renderer>();
            Vector3 p = r != null ? r.bounds.center : t.position;
            lados += string.Format("\n      {0,-16} x={1,6:F3}  y={2,6:F3}  z={3,6:F3}", n, p.x, p.y, p.z);
        }

        BoxCollider zona = prefab.GetComponent<BoxCollider>();
        lados += string.Format("\n      {0,-16} x={1,6:F3}  y={2,6:F3}  z={3,6:F3}",
                               "ZONA E", zona.center.x, zona.center.y, zona.center.z);
        lados += string.Format("\n      {0,-16} x={1,6:F3}  y={2,6:F3}  z={3,6:F3}",
                               "npcSpot", c.npcSpot.localPosition.x, c.npcSpot.localPosition.y,
                               c.npcSpot.localPosition.z);

        Debug.Log("[Medir] DONDE ESTA CADA COSA (centro del renderer, local)" + lados);

        // --- hueco de juego y recorrido --------------------------------------
        Bounds cristal = Caja(prefab.transform, "Cristal_Frente", "Cristal_Atras",
                              "Cristal_Izq", "Cristal_Der");
        Bounds suelo = Caja(prefab.transform, "Suelo_Juego_");
        Bounds garra = Caja(prefab.transform, "Dedo_");

        Debug.Log(string.Format(
            "[Medir] ESPACIO\n"
            + "  hueco de cristal .... {0:F3} x {1:F3} m\n"
            + "  suelo de juego a .... y={2:F3}\n"
            + "  recorrido X ......... {3:F3} a {4:F3}  (total {5:F3} m)\n"
            + "  recorrido Z ......... {6:F3} a {7:F3}  (total {8:F3} m)\n"
            + "  % del hueco cubierto  {9:F0}% en X, {10:F0}% en Z",
            cristal.size.x, cristal.size.z, suelo.max.y,
            c.limitXMin, c.limitXMax, c.limitXMax - c.limitXMin,
            c.limitZMin, c.limitZMax, c.limitZMax - c.limitZMin,
            100f * (c.limitXMax - c.limitXMin) / cristal.size.x,
            100f * (c.limitZMax - c.limitZMin) / cristal.size.z));

        // --- garra: cuanto abre ----------------------------------------------
        Debug.Log(string.Format(
            "[Medir] GARRA\n"
            + "  envergadura ......... {0:F3} m\n"
            + "  alto de los dedos ... {1:F3} m\n"
            + "  puntas a y= ......... {2:F3}\n"
            + "  bisagra a y= ........ {3:F3}\n"
            + "  angulo de cierre .... {4:F0} grados\n"
            + "  bajada .............. {5:F3} m",
            Mathf.Max(garra.size.x, garra.size.z), garra.size.y,
            garra.min.y, c.hingePoint.position.y, c.fingerCloseAngle, -c.armDownY));

        // --- peluches ---------------------------------------------------------
        string peluches = "";
        foreach (string ruta in new[] { "Assets/_Project/Prefabs/MISHKA_Toy.prefab",
                                        "Assets/_Project/Prefabs/Ball_Toy.prefab" })
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
            if (p == null) { peluches += "\n      " + ruta + ": NO CARGA"; continue; }

            Bounds b = new Bounds();
            bool primero = true;
            foreach (Renderer r in p.GetComponentsInChildren<Renderer>(true))
            {
                if (primero) { b = r.bounds; primero = false; }
                else b.Encapsulate(r.bounds);
            }

            PlushItem pi = p.GetComponent<PlushItem>();

            peluches += string.Format(
                "\n      {0,-12} {1:F3} x {2:F3} x {3:F3} m   escala {4:F3}   masa {5:F2} kg",
                p.name, b.size.x, b.size.y, b.size.z,
                p.transform.localScale.x, pi != null ? pi.GetWeightValue() : -1f);
        }

        Debug.Log("[Medir] PELUCHES" + peluches
                  + string.Format("\n      La garra abre {0:F3} m: un peluche mas ancho que eso "
                                  + "no se puede agarrar de ninguna manera.",
                                  Mathf.Max(garra.size.x, garra.size.z)));

        // --- de donde caen ----------------------------------------------------
        Debug.Log(string.Format(
            "[Medir] REPARTO DE PELUCHES\n"
            + "  toySpawnPoint a y= .. {0:F3}\n"
            + "  toyDropHeight ....... {1:F3}\n"
            + "  o sea, caen desde ... y={2:F3}\n"
            + "  la garra esta a ..... y={3:F3}  <-- si coinciden, nacen dentro de la garra\n"
            + "  suelo a ............. y={4:F3}\n"
            + "  dispersion .......... {5:F0}% de {6:F3} m = +-{7:F3} m",
            c.toySpawnPoint.position.y, c.toyDropHeight,
            c.toySpawnPoint.position.y + c.toyDropHeight,
            garra.min.y, suelo.max.y,
            c.toyScatterSpread * 100f, c.limitXMax - c.limitXMin,
            (c.limitXMax - c.limitXMin) * 0.5f * c.toyScatterSpread));
    }

    static Transform Buscar(Transform raiz, string nombre)
    {
        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == nombre) return t;
        }
        return null;
    }

    static Bounds Caja(Transform raiz, params string[] prefijos)
    {
        Bounds b = new Bounds();
        bool primero = true;

        foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
        {
            bool vale = false;
            foreach (string p in prefijos) if (t.name.StartsWith(p)) vale = true;
            if (!vale) continue;

            Renderer r = t.GetComponent<Renderer>();
            if (r == null) continue;

            if (primero) { b = r.bounds; primero = false; }
            else b.Encapsulate(r.bounds);
        }

        return b;
    }
}
