using UnityEngine;
using UnityEngine.InputSystem;

namespace Hashi
{
    // Lectura del mando y del teclado con el Input System nuevo.
    //
    // Se lee aqui y en ningun sitio mas. Cuando el control esta repartido por
    // cinco scripts, anadir un mando obliga a tocar los cinco y siempre queda
    // uno sin enterarse; asi es un solo sitio.
    //
    // Va contra el Input System nuevo sin fichero de acciones. Un .inputactions
    // es un asset binario mas que mantener y aqui solo hacen falta seis teclas,
    // asi que se leen los dispositivos directamente. El proyecto tiene
    // activeInputHandler = 2 (los dos sistemas), asi que esto funciona tal cual.
    public static class InputReader
    {
        static bool avisoDado;

        // Devuelve true si hay con que jugar. Si alguien pone el proyecto en
        // "Input Manager (old)" a secas, esto se queda mudo, y quedarse mudo sin
        // decir por que es media hora perdida.
        static bool HayTeclado()
        {
            if (Keyboard.current != null) return true;

            if (!avisoDado)
            {
                avisoDado = true;
                Debug.LogWarning("[Hashi] No hay teclado para el Input System. "
                                 + "Comprueba en Project Settings > Player que "
                                 + "'Active Input Handling' sea 'Both' o 'Input "
                                 + "System Package'.");
            }

            return false;
        }

        // Movimiento horizontal del carro. X = izquierda/derecha, Y = fondo.
        public static Vector2 Movimiento()
        {
            Vector2 v = Vector2.zero;

            if (HayTeclado())
            {
                Keyboard k = Keyboard.current;

                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
            }

            // El mando se suma al teclado en vez de sustituirlo: asi se puede
            // soltar uno y coger el otro sin cambiar nada.
            Gamepad g = Gamepad.current;
            if (g != null) v += g.leftStick.ReadValue();

            // Clamp y no normalize: en diagonal con teclado daria 1,41 y el
            // carro correria mas en diagonal que recto.
            return Vector2.ClampMagnitude(v, 1f);
        }

        // Soltar la garra.
        public static bool Bajar()
        {
            if (HayTeclado() && Keyboard.current.spaceKey.wasPressedThisFrame) return true;

            Gamepad g = Gamepad.current;
            return g != null && (g.buttonSouth.wasPressedThisFrame
                                 || g.rightTrigger.wasPressedThisFrame);
        }

        // Meter credito / empezar.
        public static bool Empezar()
        {
            if (!HayTeclado()) return false;
            return Keyboard.current.enterKey.wasPressedThisFrame
                   || Keyboard.current.numpadEnterKey.wasPressedThisFrame;
        }

        // Reiniciar la partida entera (premio incluido).
        public static bool Reiniciar()
        {
            return HayTeclado() && Keyboard.current.rKey.wasPressedThisFrame;
        }

        // 1, 2 o 3 para cambiar de camara. Devuelve 0 si no se ha pulsado nada.
        public static int Camara()
        {
            if (!HayTeclado()) return 0;

            Keyboard k = Keyboard.current;
            if (k.digit1Key.wasPressedThisFrame) return 1;
            if (k.digit2Key.wasPressedThisFrame) return 2;
            if (k.digit3Key.wasPressedThisFrame) return 3;
            return 0;
        }

        // Credito de regalo para probar.
        public static bool AnadirCredito()
        {
            return HayTeclado() && Keyboard.current.cKey.wasPressedThisFrame;
        }

        // Enciende y apaga los gizmos y el panel de fisica.
        public static bool Depuracion()
        {
            return HayTeclado() && Keyboard.current.f1Key.wasPressedThisFrame;
        }
    }
}
