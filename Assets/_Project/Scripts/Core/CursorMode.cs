using UnityEngine;
using System.Collections.Generic;

// Un unico sitio decide si el raton esta suelto o atrapado. Cada pantalla que
// necesita raton se apunta mientras esta abierta y se borra al cerrarse.
//
// Lo importante es que el estado se vuelve a imponer cada frame: si el editor,
// una pantalla que se cierra sola o cualquier otra cosa suelta el raton por su
// cuenta, al frame siguiente vuelve a como tiene que estar. Antes cada pantalla
// lo tocaba a mano al salir y bastaba con que una se lo saltara para quedarte
// con el raton fuera y la camara siguiendolo.
public class CursorMode : MonoBehaviour
{
    private static readonly HashSet<Object> owners = new HashSet<Object>();
    private static CursorMode instance;

    public static bool FreeCursor { get { return owners.Count > 0; } }

    static void EnsureExists()
    {
        if (instance != null) return;

        instance = FindAnyObjectByType<CursorMode>();
        if (instance != null) return;

        GameObject go = new GameObject("CursorMode");
        instance = go.AddComponent<CursorMode>();
    }

    // Pide raton libre. Llamalo al abrir una pantalla.
    public static void Free(Object owner)
    {
        if (owner == null) return;

        owners.Add(owner);
        EnsureExists();
        Apply();
    }

    // Devuelve el raton al juego. Llamalo al cerrar la pantalla.
    public static void Release(Object owner)
    {
        if (owner == null) return;

        owners.Remove(owner);
        EnsureExists();
        Apply();
    }

    static void Apply()
    {
        // Una pantalla destruida sin avisar no puede dejar el raton bloqueado.
        owners.RemoveWhere(o => o == null);

        bool free = owners.Count > 0;

        CursorLockMode wanted = free ? CursorLockMode.None : CursorLockMode.Locked;

        if (Cursor.lockState != wanted) Cursor.lockState = wanted;
        if (Cursor.visible != free) Cursor.visible = free;
    }

    void Awake()
    {
        instance = this;
    }

    void LateUpdate()
    {
        Apply();
    }
}
