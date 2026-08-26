using System.Collections.Generic;
using UnityEngine;

// De donde salen las fotos de la tienda.
//
// Estaban guardadas en el campo icon de cada ficha, dentro de la escena, y por
// eso no se veia ninguna: el editor las generaba y las asignaba, pero si la
// escena no se guardaba se perdian en la siguiente recarga. Un dato que solo
// existe si alguien se acuerda de pulsar Guardar no es un dato.
//
// Ahora los PNG viven en Resources y se piden por NOMBRE al usarlos. Con eso da
// igual lo que tenga la escena guardado: si el archivo esta, la foto sale.
//
// Se sigue respetando el icon de la ficha si alguien le pone uno a mano; solo
// se busca en Resources cuando no hay ninguno.
public static class IconoTienda
{
    const string CARPETA = "Iconos/";

    // Se guarda tambien lo que NO se encuentra. Sin eso, cada fila de cada
    // refresco vuelve a rebuscar en Resources un archivo que no existe, y la
    // lista se repinta entera cada vez que se abre el carrito.
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    public static Sprite Para(string nombre, Sprite yaPuesto)
    {
        if (yaPuesto != null) return yaPuesto;
        if (string.IsNullOrEmpty(nombre)) return null;

        Sprite s;
        if (cache.TryGetValue(nombre, out s)) return s;

        s = Resources.Load<Sprite>(CARPETA + Limpio(nombre));
        cache[nombre] = s;

        return s;
    }

    // Olvidar lo aprendido. Lo llama el generador de iconos del editor despues
    // de crear los PNG: si no, en la misma sesion se seguirian dando por
    // inexistentes los que se acaban de hacer.
    public static void Olvidar()
    {
        cache.Clear();
    }

    // El mismo nombre de archivo que usa el generador. Vive aqui y no alli para
    // que no puedan discrepar: si uno cambia la regla y el otro no, la foto
    // existe y aun asi no se encuentra, que es de lo peor que hay para dar con
    // el fallo.
    public static string Limpio(string nombre)
    {
        return nombre.Replace(' ', '_')
                     .Replace('/', '_')
                     .Replace('\\', '_')
                     .Replace(':', '_')
                     .Replace('*', '_')
                     .Replace('?', '_')
                     .Replace('"', '_')
                     .Replace('<', '_')
                     .Replace('>', '_')
                     .Replace('|', '_');
    }
}
