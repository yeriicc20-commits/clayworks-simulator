using UnityEditor;
using UnityEngine;

// Ayuda para rellenar campos privados desde el constructor de la escena.
//
// Los campos de los componentes son [SerializeField] privados, que es como
// tienen que ser: si fueran publicos para que el constructor pueda escribirlos,
// cualquier script del proyecto podria cambiarle la altura a la garra en mitad
// de una partida. Desde el editor se llega igual con SerializedObject, que es
// para lo que esta esto.
//
// Se usa encadenado y en un using, para que no haya manera de olvidarse del
// ApplyModifiedProperties, que es el fallo clasico: el constructor "funciona",
// no da ningun error, y todos los campos salen vacios.
//
//   using (var a = new HashiCableado(garra))
//       a.Obj("clawBody", cuerpo).Num("alturaReposo", 0.6f);
public class HashiCableado : System.IDisposable
{
    readonly SerializedObject so;
    readonly Object objetivo;

    public HashiCableado(Object componente)
    {
        objetivo = componente;
        so = new SerializedObject(componente);
    }

    public HashiCableado Obj(string campo, Object valor)
    {
        SerializedProperty p = Buscar(campo);
        if (p != null) p.objectReferenceValue = valor;
        return this;
    }

    public HashiCableado Num(string campo, float valor)
    {
        SerializedProperty p = Buscar(campo);
        if (p != null) p.floatValue = valor;
        return this;
    }

    public HashiCableado Ent(string campo, int valor)
    {
        SerializedProperty p = Buscar(campo);
        if (p != null) p.intValue = valor;
        return this;
    }

    public HashiCableado Bul(string campo, bool valor)
    {
        SerializedProperty p = Buscar(campo);
        if (p != null) p.boolValue = valor;
        return this;
    }

    public HashiCableado Txt(string campo, string valor)
    {
        SerializedProperty p = Buscar(campo);
        if (p != null) p.stringValue = valor;
        return this;
    }

    public HashiCableado V3(string campo, Vector3 valor)
    {
        SerializedProperty p = Buscar(campo);
        if (p != null) p.vector3Value = valor;
        return this;
    }

    public HashiCableado V2(string campo, Vector2 valor)
    {
        SerializedProperty p = Buscar(campo);
        if (p != null) p.vector2Value = valor;
        return this;
    }

    public HashiCableado Col(string campo, Color valor)
    {
        SerializedProperty p = Buscar(campo);
        if (p != null) p.colorValue = valor;
        return this;
    }

    // Rellena una lista de referencias de golpe.
    public HashiCableado Lista(string campo, params Object[] valores)
    {
        SerializedProperty p = Buscar(campo);
        if (p == null) return this;

        p.arraySize = valores.Length;

        for (int i = 0; i < valores.Length; i++)
        {
            p.GetArrayElementAtIndex(i).objectReferenceValue = valores[i];
        }

        return this;
    }

    SerializedProperty Buscar(string campo)
    {
        SerializedProperty p = so.FindProperty(campo);

        // Avisar en vez de tragarselo. Un campo mal escrito aqui deja una
        // referencia vacia en la escena, y eso no se ve hasta que se le da a
        // Play y algo peta sin decir de donde viene.
        if (p == null)
        {
            Debug.LogWarning("[Hashi] " + objetivo.GetType().Name
                             + " no tiene ningun campo '" + campo
                             + "'. Se queda sin rellenar.");
        }

        return p;
    }

    public void Dispose()
    {
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
