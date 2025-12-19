using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Keys = Microsoft.Xna.Framework.Input.Keys;
using ButtonState = Microsoft.Xna.Framework.Input.ButtonState;

namespace MonoGameEditor.Core
{
    public static class Input
    {
        private static KeyboardState _currentKeyboardState;
        private static KeyboardState _previousKeyboardState;
        private static MouseState _currentMouseState;
        private static MouseState _previousMouseState;
        
#if RUNTIME_BUILD
        private static bool _useWin32Fallback = false; 
#else
        private static bool _useWin32Fallback = true; 
#endif

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private static KeyboardState GetWin32KeyboardState()
        {
            var pressedKeys = new System.Collections.Generic.List<Keys>();
            
            for (int i = 8; i <= 255; i++)
            {
                if ((GetAsyncKeyState(i) & 0x8000) != 0)
                {
                    pressedKeys.Add((Keys)i);
                }
            }
            
            return new KeyboardState(pressedKeys.ToArray());
        }

        public static void Update()
        {
            _previousKeyboardState = _currentKeyboardState;
            
            if (_useWin32Fallback)
            {
                _currentKeyboardState = GetWin32KeyboardState();
            }
            else
            {
                _currentKeyboardState = Keyboard.GetState();
            }

            _previousMouseState = _currentMouseState;
            _currentMouseState = Mouse.GetState();
        }

        public static bool GetKey(Keys key)
        {
            return _currentKeyboardState.IsKeyDown(key);
        }

        public static bool GetKeyDown(Keys key)
        {
            return _currentKeyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key);
        }

        public static bool GetKeyUp(Keys key)
        {
            return _currentKeyboardState.IsKeyUp(key) && _previousKeyboardState.IsKeyDown(key);
        }

        public static bool GetMouseButton(int button)
        {
            return button switch
            {
                0 => _currentMouseState.LeftButton == ButtonState.Pressed,
                1 => _currentMouseState.RightButton == ButtonState.Pressed,
                2 => _currentMouseState.MiddleButton == ButtonState.Pressed,
                _ => false
            };
        }

        public static Vector2 MousePosition => new Vector2(_currentMouseState.X, _currentMouseState.Y);
        public static Vector2 MouseDelta => new Vector2(_currentMouseState.X - _previousMouseState.X, _currentMouseState.Y - _previousMouseState.Y);
        public static int MouseScrollWheelDelta => _currentMouseState.ScrollWheelValue - _previousMouseState.ScrollWheelValue;
    }
}
