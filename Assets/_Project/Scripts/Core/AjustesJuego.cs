using System;
using UnityEngine;

// Los ajustes que no son ni sonido ni teclas.
//
// Hoy es la sensibilidad del raton y poco mas, pero vive aparte por lo mismo
// que AjustesSonido: el valor lo lee quien mueve la camara, lo escribe el menu,
// y ninguno de los dos tiene por que conocer al otro.
public static class AjustesJuego
{
    public const float SENSIBILIDAD_MIN = 0.4f;
    public const float SENSIBILIDAD_MAX = 8f;

    const float SENSIBILIDAD_DEF = 2f;

    static float sensibilidad = SENSIBILIDAD_DEF;
    static bool invertirY = false;
    static bool cargado = false;

    public static event Action Cambiado;

    public static float Sensibilidad
    {
        get { Cargar(); return sensibilidad; }
        set
        {
            Cargar();

            float v = Mathf.Clamp(value, SENSIBILIDAD_MIN, SENSIBILIDAD_MAX);
            if (Mathf.Approximately(sensibilidad, v)) return;

            sensibilidad = v;
            PlayerPrefs.SetFloat("juego_sensibilidad", v);

            if (Cambiado != null) Cambiado();
        }
    }

    // Hay quien no puede jugar sin esto, y cuesta cuatro lineas.
    public static bool InvertirY
    {
        get { Cargar(); return invertirY; }
        set
        {
            Cargar();
            if (invertirY == value) return;

            invertirY = value;
            PlayerPrefs.SetInt("juego_invertir_y", value ? 1 : 0);

            if (Cambiado != null) Cambiado();
        }
    }

    public static void Restaurar()
    {
        Sensibilidad = SENSIBILIDAD_DEF;
        InvertirY = false;
    }

    static void Cargar()
    {
        if (cargado) return;
        cargado = true;

        sensibilidad = PlayerPrefs.GetFloat("juego_sensibilidad", SENSIBILIDAD_DEF);
        invertirY = PlayerPrefs.GetInt("juego_invertir_y", 0) == 1;
    }
}
