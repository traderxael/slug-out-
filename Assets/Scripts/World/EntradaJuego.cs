using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SlugOut.World
{
    public enum DispositivoEntrada { Teclado, Mando }

    /// Estado de entrada de un cuadro. Las acciones de gameplay (disparo, rodada, habilidad y
    /// paso cuidado) son "mantenido"; pausa, confirmar, cancelar y las flechas de menu son flancos.
    public struct EntradaFrame
    {
        public Vector2 mover;
        public bool disparo, rodada, habilidad, pasoCuidado;
        public bool pausa, confirmar, cancelar;
        public bool menuArriba, menuAbajo, menuIzquierda, menuDerecha;
        public DispositivoEntrada dispositivo;
    }

    /// Puerta unica de entrada del juego. El teclado conserva su comportamiento y el mando
    /// agrega las mismas acciones. Leer() cachea por cuadro: IMGUI llama varias veces por
    /// cuadro (Layout/Repaint) y un flanco de menu no puede repetirse en cada pasada.
    public static class EntradaJuego
    {
        public const float DeadzoneMando = 0.2f;
        public const float UmbralGatillo = 0.15f;
        public const float UmbralMenuStick = 0.6f;

        static int _frame = -1;
        static EntradaFrame _cache;
        static Vector2 _stickPrevio;
        static DispositivoEntrada _ultimo = DispositivoEntrada.Teclado;

        public static DispositivoEntrada UltimoDispositivo { get { return _ultimo; } }
        public static bool HayMando { get { return MandoActual() != null; } }

        public static EntradaFrame Leer()
        {
            int frame = Time.frameCount;
            if (_frame == frame) return _cache;
            _frame = frame;

            EntradaFrame teclado = LeerTeclado();
            EntradaFrame mando = LeerMando();
            EntradaFrame e = Combinar(teclado, mando);

            // El ultimo dispositivo que produjo entrada manda en la ayuda en pantalla.
            if (Activo(mando)) _ultimo = DispositivoEntrada.Mando;
            else if (Activo(teclado)) _ultimo = DispositivoEntrada.Teclado;
            e.dispositivo = _ultimo;
            _cache = e;
            return e;
        }

        /// Regla pura: el teclado manda en el movimiento si se movio; los botones se suman.
        public static EntradaFrame Combinar(EntradaFrame teclado, EntradaFrame mando)
        {
            EntradaFrame r = teclado;
            if (teclado.mover.sqrMagnitude < 0.0001f) r.mover = mando.mover;
            r.disparo |= mando.disparo;
            r.rodada |= mando.rodada;
            r.habilidad |= mando.habilidad;
            r.pasoCuidado |= mando.pasoCuidado;
            r.pausa |= mando.pausa;
            r.confirmar |= mando.confirmar;
            r.cancelar |= mando.cancelar;
            r.menuArriba |= mando.menuArriba;
            r.menuAbajo |= mando.menuAbajo;
            r.menuIzquierda |= mando.menuIzquierda;
            r.menuDerecha |= mando.menuDerecha;
            return r;
        }

        public static string TextoAyuda(DispositivoEntrada d)
        {
            return d == DispositivoEntrada.Mando
                ? "LS mover · LT paso · RT dispara · Y habilidad · START ruta"
                : "WASD · CTRL paso · ESPACIO dispara · Q habilidad · TAB ruta";
        }

        public static bool Activo(EntradaFrame e)
        {
            return e.mover.sqrMagnitude > 0.0001f || e.disparo || e.rodada || e.habilidad
                || e.pasoCuidado || e.pausa || e.confirmar || e.cancelar
                || e.menuArriba || e.menuAbajo || e.menuIzquierda || e.menuDerecha;
        }

        static EntradaFrame LeerTeclado()
        {
            EntradaFrame e = default;
            e.dispositivo = DispositivoEntrada.Teclado;
#if ENABLE_INPUT_SYSTEM
            Keyboard kb = Keyboard.current;
            Mouse ms = Mouse.current;
            if (kb != null)
            {
                float x = 0f, z = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) z -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) z += 1f;
                e.mover = new Vector2(x, z);
                e.disparo = kb.spaceKey.isPressed || kb.enterKey.isPressed;
                e.rodada = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                e.habilidad = kb.qKey.isPressed;
                e.pasoCuidado = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
                e.pausa = kb.tabKey.wasPressedThisFrame;
                e.confirmar = kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
                e.cancelar = kb.escapeKey.wasPressedThisFrame;
                e.menuArriba = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
                e.menuAbajo = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
                e.menuIzquierda = kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
                e.menuDerecha = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            }
            if (ms != null) e.disparo |= ms.leftButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            e.mover = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            e.disparo = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
            e.rodada = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            e.habilidad = Input.GetKey(KeyCode.Q);
            e.pasoCuidado = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            e.pausa = Input.GetKeyDown(KeyCode.Tab);
            e.confirmar = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
            e.cancelar = Input.GetKeyDown(KeyCode.Escape);
            e.menuArriba = Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
            e.menuAbajo = Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
            e.menuIzquierda = Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
            e.menuDerecha = Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
#endif
            return e;
        }

#if ENABLE_INPUT_SYSTEM
        static Gamepad MandoActual() { return Gamepad.current; }
#else
        static object MandoActual() { return null; }
#endif

        static EntradaFrame LeerMando()
        {
            EntradaFrame e = default;
            e.dispositivo = DispositivoEntrada.Mando;
#if ENABLE_INPUT_SYSTEM
            Gamepad gp = Gamepad.current;
            if (gp == null) return e;

            Vector2 stick = gp.leftStick.ReadValue();
            if (stick.magnitude < DeadzoneMando) stick = Vector2.zero;
            e.mover = stick;
            e.disparo = gp.rightTrigger.ReadValue() >= UmbralGatillo;
            e.rodada = gp.buttonEast.isPressed;
            e.habilidad = gp.buttonNorth.isPressed;
            e.pasoCuidado = gp.leftTrigger.ReadValue() >= UmbralGatillo;
            e.pausa = gp.startButton.wasPressedThisFrame;
            e.confirmar = gp.buttonSouth.wasPressedThisFrame;
            e.cancelar = gp.buttonEast.wasPressedThisFrame;

            bool arribaStick = stick.y >= UmbralMenuStick && _stickPrevio.y < UmbralMenuStick;
            bool abajoStick = stick.y <= -UmbralMenuStick && _stickPrevio.y > -UmbralMenuStick;
            bool izqStick = stick.x <= -UmbralMenuStick && _stickPrevio.x > -UmbralMenuStick;
            bool derStick = stick.x >= UmbralMenuStick && _stickPrevio.x < UmbralMenuStick;
            e.menuArriba = gp.dpad.up.wasPressedThisFrame || arribaStick;
            e.menuAbajo = gp.dpad.down.wasPressedThisFrame || abajoStick;
            e.menuIzquierda = gp.dpad.left.wasPressedThisFrame || izqStick;
            e.menuDerecha = gp.dpad.right.wasPressedThisFrame || derStick;
            _stickPrevio = stick;
#endif
            return e;
        }
    }

    /// Foco de listas de menu. Separado para poder probar el wrap sin escena.
    public static class NavegacionFoco
    {
        public static int Mover(int actual, int delta, int cantidad)
        {
            if (cantidad <= 0) return 0;
            int n = (actual + delta) % cantidad;
            return n < 0 ? n + cantidad : n;
        }
    }
}
