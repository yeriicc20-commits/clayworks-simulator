using UnityEngine;

namespace Hashi
{
    // Las capas de fisica de la maquina, buscadas por NOMBRE.
    //
    // Por numero seria mas corto, pero el numero de una capa depende del orden
    // en que esten en Project Settings, y ese orden lo cambia cualquiera sin
    // enterarse. El dia que alguien mete una capa nueva en medio, la garra
    // empieza a atravesar el cristal y no hay nada en el codigo que lo explique.
    // Por nombre, o funciona o avisa.
    public static class HashiLayers
    {
        public const string NOMBRE_MAQUINA = "Machine";
        public const string NOMBRE_CRISTAL = "Glass";
        public const string NOMBRE_GARRA = "Claw";
        public const string NOMBRE_PREMIO = "Prize";
        public const string NOMBRE_BARRAS = "Bars";
        public const string NOMBRE_BANDEJA = "DropZone";

        // Todas las que hace falta crear, en el orden en que se crean.
        public static readonly string[] TODAS =
        {
            NOMBRE_MAQUINA, NOMBRE_CRISTAL, NOMBRE_GARRA,
            NOMBRE_PREMIO, NOMBRE_BARRAS, NOMBRE_BANDEJA,
        };

        static int maquina = -1, cristal = -1, garra = -1;
        static int premio = -1, barras = -1, bandeja = -1;

        public static int Maquina => Cache(ref maquina, NOMBRE_MAQUINA);
        public static int Cristal => Cache(ref cristal, NOMBRE_CRISTAL);
        public static int Garra => Cache(ref garra, NOMBRE_GARRA);
        public static int Premio => Cache(ref premio, NOMBRE_PREMIO);
        public static int Barras => Cache(ref barras, NOMBRE_BARRAS);
        public static int Bandeja => Cache(ref bandeja, NOMBRE_BANDEJA);

        public static LayerMask MascaraPremio => 1 << Premio;
        public static LayerMask MascaraBarras => 1 << Barras;
        public static LayerMask MascaraGarra => 1 << Garra;

        static int Cache(ref int guardado, string nombre)
        {
            if (guardado >= 0) return guardado;

            guardado = LayerMask.NameToLayer(nombre);

            if (guardado < 0)
            {
                Debug.LogError("[Hashi] Falta la capa '" + nombre + "'. Pulsa "
                               + "ClayWorks/Hashi-Watashi/Montar escena, que la "
                               + "crea, o anadela a mano en Project Settings > "
                               + "Tags and Layers.");

                // Se devuelve Default para no petar, pero con el error delante:
                // devolver un numero malo en silencio deja la maquina jugando
                // rarisimo sin que nada apunte a la causa.
                guardado = 0;
            }

            return guardado;
        }

        // Al recargar dominio en el editor los estaticos no se limpian solos si
        // esta desactivado el "Reload Domain". Sin esto, cambiar las capas y
        // darle a Play seguiria usando los numeros viejos.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Limpiar()
        {
            maquina = cristal = garra = premio = barras = bandeja = -1;
        }
    }
}
