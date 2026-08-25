using System;
using UnityEngine;

// Volumenes del juego, por canales, de 0 a 100.
//
// Existe porque afinar un volumen a ciegas es imposible: hacen falta tres o
// cuatro pasadas de "sube un poco, baja un poco" y cada una cuesta una partida.
// Con esto se mueve con el juego corriendo y se oye el efecto al momento.
//
// Se guarda en PlayerPrefs: lo que ajustes sigue ahi la proxima vez que abras,
// que si no habria que repetirlo cada arranque.
public static class AjustesSonido
{
    public enum Canal { General, Musica, Motores, Efectos }

    // Valores de partida, ya afinados a mano en las ultimas pasadas.
    static readonly float[] PorDefecto = { 1.00f, 0.55f, 0.35f, 0.90f };

    static readonly float[] valores = new float[4];
    static bool cargado = false;

    // Quien reproduzca sonido continuo (musica, motores) tiene que enterarse en
    // el momento: si solo mirase el valor al empezar a sonar, mover el mando no
    // haria nada hasta la siguiente partida.
    public static event Action Cambiado;

    public static string Nombre(Canal c)
    {
        switch (c)
        {
            case Canal.General: return "General";
            case Canal.Musica: return "Musica";
            case Canal.Motores: return "Motores";
            default: return "Efectos";
        }
    }

    public static float Get(Canal c)
    {
        Cargar();
        return valores[(int)c];
    }

    // Volumen final de un canal: el suyo por el general.
    public static float Mezcla(Canal c)
    {
        return Get(c) * Get(Canal.General);
    }

    public static void Set(Canal c, float valor)
    {
        Cargar();

        valor = Mathf.Clamp01(valor);
        if (Mathf.Approximately(valores[(int)c], valor)) return;

        valores[(int)c] = valor;
        PlayerPrefs.SetFloat(Clave(c), valor);

        if (Cambiado != null) Cambiado();
    }

    public static void Sumar(Canal c, float delta)
    {
        Set(c, Get(c) + delta);
    }

    static string Clave(Canal c) { return "sonido_" + c.ToString(); }

    static void Cargar()
    {
        if (cargado) return;
        cargado = true;

        for (int i = 0; i < valores.Length; i++)
        {
            valores[i] = PlayerPrefs.GetFloat(Clave((Canal)i), PorDefecto[i]);
        }
    }
}
