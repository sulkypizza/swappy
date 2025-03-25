using save_switcher.Panels.Subpanels;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace save_switcher.Elements
{
    public enum KeyboardButtonType
    {
        Character,
        Special,
    }

    internal class KeyboardButton : Button
    {
        private OnScreenKeyboard parentKeyboard;

        private static readonly Color baseColor = new Color(115, 19, 122);
        private static readonly Color hoverColor = new Color(138, 42, 145);
        private static readonly Color pressedColor = new Color(99, 24, 104);
        private Color currentColor;

        private SolidColorBrush colorBrush;

        public KeyboardButton(OnScreenKeyboard parent, DeviceContext deviceContext, Vector2 position, Size2 size, string inputString) : base(deviceContext, position, size)
        {
            base.deviceContext = deviceContext;
            this.parentKeyboard = parent;

            currentColor = baseColor;
            colorBrush = new SolidColorBrush(deviceContext, currentColor);

            OnHover += () => { currentColor = hoverColor; };

            OnHeld += () => { currentColor = pressedColor; };

            OnPressed += () => 
            { 
                currentColor = hoverColor;

                if (parentKeyboard.ShiftStates.activated)
                    SendKeys.SendWait(inputString.ToUpper());
                else if (!parentKeyboard.ShiftStates.activated)
                    SendKeys.SendWait(inputString.ToLower());
            };
        }

        public override void Deselect()
        {
            currentColor = baseColor;
        }

        public override void Update()
        {
           
        }

        public override void Select()
        {
            OnHover?.Invoke();
            base.Select();
        }

        public override void Draw()
        {
            colorBrush.Color = currentColor;

            var rect = new RoundedRectangle()
            {
                Rect = new SharpDX.Mathematics.Interop.RawRectangleF(Position.X, Position.Y, Position.X + Size.Width, Position.Y + Size.Height),
                RadiusX = 10f,
                RadiusY = 10f
            };

            deviceContext.FillRoundedRectangle(rect, colorBrush);
        }

        public override void Dispose()
        {
            OnPressed = null;
            OnHover = null;
            OnHeld = null;

            parentKeyboard = null;

            colorBrush?.Dispose();

            base.Dispose();
        }
    }
}
