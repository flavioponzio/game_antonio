using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RecreioEspacial.Core
{
    /// <summary>
    /// Leitura de entrada que funciona tanto com o Input System novo quanto com o Input Manager antigo
    /// (o Unity define ENABLE_INPUT_SYSTEM / ENABLE_LEGACY_INPUT_MANAGER conforme Project Settings > Player).
    /// </summary>
    public static class GameInput
    {
        /// <summary>Clique do mouse ou toque começou neste frame.</summary>
        public static bool PointerPressed(out Vector2 screenPos)
        {
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
            {
                screenPos = touch.primaryTouch.position.ReadValue();
                return true;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPos = mouse.position.ReadValue();
                return true;
            }
            screenPos = default;
            return false;
#else
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                screenPos = Input.GetTouch(0).position;
                return true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                screenPos = Input.mousePosition;
                return true;
            }
            screenPos = default;
            return false;
#endif
        }

        /// <summary>Posição do mouse para o "hover". Falso em aparelhos só de toque.</summary>
        public static bool HoverPosition(out Vector2 screenPos)
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPos = mouse.position.ReadValue();
                return true;
            }
            screenPos = default;
            return false;
#else
            if (Input.mousePresent)
            {
                screenPos = Input.mousePosition;
                return true;
            }
            screenPos = default;
            return false;
#endif
        }

        /// <summary>Direção das setas / WASD. x = direita, y = para o fundo (cima na tela).</summary>
        public static Vector2 MoveAxis()
        {
            float x = 0, y = 0;
            if (IsHeld(KeyId.Left) || IsHeld(KeyId.A)) x -= 1;
            if (IsHeld(KeyId.Right) || IsHeld(KeyId.D)) x += 1;
            if (IsHeld(KeyId.Up) || IsHeld(KeyId.W)) y += 1;
            if (IsHeld(KeyId.Down) || IsHeld(KeyId.S)) y -= 1;
            return new Vector2(x, y);
        }

        public static bool InteractPressed => IsDown(KeyId.E) || IsDown(KeyId.Space);
        public static bool HintPressed => IsDown(KeyId.H);
        public static bool CancelPressed => IsDown(KeyId.Escape);

        /// <summary>Teclas 1–4: índice do item do inventário (0..3), ou -1.</summary>
        public static int ItemKeyPressed()
        {
            if (IsDown(KeyId.Digit1)) return 0;
            if (IsDown(KeyId.Digit2)) return 1;
            if (IsDown(KeyId.Digit3)) return 2;
            if (IsDown(KeyId.Digit4)) return 3;
            return -1;
        }

        public static bool AnyKeyDown()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
#else
            return Input.anyKeyDown;
#endif
        }

        // Atalhos de teste (só no Editor / build de desenvolvimento)
        public static bool DebugKeyDown(int fKey)
        {
            switch (fKey)
            {
                case 1: return IsDown(KeyId.F1);
                case 5: return IsDown(KeyId.F5);
                case 6: return IsDown(KeyId.F6);
                case 7: return IsDown(KeyId.F7);
                case 8: return IsDown(KeyId.F8);
                default: return false;
            }
        }

        enum KeyId { Left, Right, Up, Down, W, A, S, D, E, Space, H, Escape, Digit1, Digit2, Digit3, Digit4, F1, F5, F6, F7, F8 }

#if ENABLE_INPUT_SYSTEM
        static UnityEngine.InputSystem.Key Map(KeyId k)
        {
            switch (k)
            {
                case KeyId.Left: return UnityEngine.InputSystem.Key.LeftArrow;
                case KeyId.Right: return UnityEngine.InputSystem.Key.RightArrow;
                case KeyId.Up: return UnityEngine.InputSystem.Key.UpArrow;
                case KeyId.Down: return UnityEngine.InputSystem.Key.DownArrow;
                case KeyId.W: return UnityEngine.InputSystem.Key.W;
                case KeyId.A: return UnityEngine.InputSystem.Key.A;
                case KeyId.S: return UnityEngine.InputSystem.Key.S;
                case KeyId.D: return UnityEngine.InputSystem.Key.D;
                case KeyId.E: return UnityEngine.InputSystem.Key.E;
                case KeyId.Space: return UnityEngine.InputSystem.Key.Space;
                case KeyId.H: return UnityEngine.InputSystem.Key.H;
                case KeyId.Escape: return UnityEngine.InputSystem.Key.Escape;
                case KeyId.Digit1: return UnityEngine.InputSystem.Key.Digit1;
                case KeyId.Digit2: return UnityEngine.InputSystem.Key.Digit2;
                case KeyId.Digit3: return UnityEngine.InputSystem.Key.Digit3;
                case KeyId.Digit4: return UnityEngine.InputSystem.Key.Digit4;
                case KeyId.F1: return UnityEngine.InputSystem.Key.F1;
                case KeyId.F5: return UnityEngine.InputSystem.Key.F5;
                case KeyId.F6: return UnityEngine.InputSystem.Key.F6;
                case KeyId.F7: return UnityEngine.InputSystem.Key.F7;
                default: return UnityEngine.InputSystem.Key.F8;
            }
        }

        static bool IsHeld(KeyId k) => Keyboard.current != null && Keyboard.current[Map(k)].isPressed;
        static bool IsDown(KeyId k) => Keyboard.current != null && Keyboard.current[Map(k)].wasPressedThisFrame;
#else
        static KeyCode Map(KeyId k)
        {
            switch (k)
            {
                case KeyId.Left: return KeyCode.LeftArrow;
                case KeyId.Right: return KeyCode.RightArrow;
                case KeyId.Up: return KeyCode.UpArrow;
                case KeyId.Down: return KeyCode.DownArrow;
                case KeyId.W: return KeyCode.W;
                case KeyId.A: return KeyCode.A;
                case KeyId.S: return KeyCode.S;
                case KeyId.D: return KeyCode.D;
                case KeyId.E: return KeyCode.E;
                case KeyId.Space: return KeyCode.Space;
                case KeyId.H: return KeyCode.H;
                case KeyId.Escape: return KeyCode.Escape;
                case KeyId.Digit1: return KeyCode.Alpha1;
                case KeyId.Digit2: return KeyCode.Alpha2;
                case KeyId.Digit3: return KeyCode.Alpha3;
                case KeyId.Digit4: return KeyCode.Alpha4;
                case KeyId.F1: return KeyCode.F1;
                case KeyId.F5: return KeyCode.F5;
                case KeyId.F6: return KeyCode.F6;
                case KeyId.F7: return KeyCode.F7;
                default: return KeyCode.F8;
            }
        }

        static bool IsHeld(KeyId k) => Input.GetKey(Map(k));
        static bool IsDown(KeyId k) => Input.GetKeyDown(Map(k));
#endif
    }
}
