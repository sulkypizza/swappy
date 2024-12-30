using save_switcher.Panels.Subpanels;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows;

namespace save_switcher.Elements
{
    public enum KeyboardButtonType
    {
        Character,
        Special,
    }

    internal class KeyboardButton : Button, IDisposable
    {
        private OnScreenKeyboard parentKeyboard;

        private string inputString;

        private static readonly Color baseColor = new Color(115, 19, 122);
        private static readonly Color hoverColor = new Color(138, 42, 145);
        private static readonly Color pressedColor = new Color(99, 24, 104);
        private Color currentColor;

        private SolidColorBrush colorBrush;

        protected static Controller[] controllers;
        protected static readonly float deadZone = 0.8f;
        protected static State[] oldControllerState;

        private static readonly Dictionary<Keys, NavigateDirection> keyToNavigateDirection = new Dictionary<Keys, NavigateDirection>()
        {   {Keys.Left, NavigateDirection.Left},
            {Keys.Right, NavigateDirection.Right},
            {Keys.Up, NavigateDirection.Up},
            {Keys.Down, NavigateDirection.Down}
        };

        public void ConnectControllers()
        {
            List<Controller> connectedControllers = new List<Controller>();
            List<State> connectedControllersState = new List<State>();

            //for each controller that is connected, save it into the list
            for (int i = 0; i < 4; i++)
            {
                Controller testController = new Controller((UserIndex)i - 1);


                if (testController.IsConnected)
                {
                    connectedControllers.Add(testController);
                    connectedControllersState.Add(testController.GetState());
                }
            }

            //use those lists to populate these arrays
            controllers = connectedControllers.ToArray();
            oldControllerState = connectedControllersState.ToArray();
        }

        public KeyboardButton(OnScreenKeyboard parent, DeviceContext deviceContext, Vector2 position, Size2 size, string inputString) : base(deviceContext, position, size)
        {
            base.deviceContext = deviceContext;
            this.parentKeyboard = parent;
            this.inputString = inputString;

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

            Form form = Program.GetProgramForm();

            form.MouseDown += mouseDownFunction;
            form.MouseUp += mouseUpFunction;
            form.MouseMove += mouseMoveFunction;
            form.KeyDown += keyDownFunction;
        }

        private void mouseDownFunction(object e, System.Windows.Forms.MouseEventArgs args)
        {
            if (pointWithinButtonBounds(args.Location))
                if (args.Button == System.Windows.Forms.MouseButtons.Left)
                    OnHeld?.Invoke();
        }

        private void mouseUpFunction(object e, System.Windows.Forms.MouseEventArgs args)
        {
            if (pointWithinButtonBounds(args.Location))
            {
                if (args.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    OnPressed?.Invoke();
                }
            }
        }

        private void mouseMoveFunction(object e, System.Windows.Forms.MouseEventArgs args)
        {
            if (pointWithinButtonBounds(args.Location))
            {
                Select();
                OnHover?.Invoke();
            }
            else if (currentColor != baseColor)
            {
                currentColor = baseColor;
            }
        }

        private void keyDownFunction(object e, System.Windows.Forms.KeyEventArgs args)
        {
            if (CurrentSelectedObject != this)
                return;

            if (args.KeyCode == Keys.Enter)
            {
                OnPressed?.Invoke();
            }
            else if (!args.Handled)
            {
                NavigateDirection direction;
                if (keyToNavigateDirection.TryGetValue(args.KeyCode, out direction))
                {
                    if (SelectNeighbor(direction))
                    {
                        Deselect();
                        args.Handled = true;
                    }
                }
            }
        }

        public override void Deselect()
        {
            currentColor = baseColor;
        }

        public override void Update()
        {
            if(CurrentSelectedObject == this)
            {
                for (int controller = 0; controller < controllers.Length; controller++)
                {
                    //don't do anything if the controller isn't connected
                    if (controllers[controller].IsConnected)
                    {
                        //get the current state and the old state for comparison
                        State currentControllerState = controllers[controller].GetState();
                        State compareState = oldControllerState[controller];

                        //if the left stick is past the dead zone this time and short the dead zone last time OR the d-pad is pressed
                        if (((float)currentControllerState.Gamepad.LeftThumbX / short.MaxValue > deadZone && ((float)compareState.Gamepad.LeftThumbX / short.MaxValue) < deadZone) ||
                                (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadRight) == GamepadButtonFlags.DPadRight && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadRight) == 0)
                        {
                            SelectNeighbor(NavigateDirection.Right);
                        }
                        //same for this one except for left
                        else if (((float)currentControllerState.Gamepad.LeftThumbX / short.MaxValue * -1f > deadZone && (float)compareState.Gamepad.LeftThumbX / short.MaxValue * -1f < deadZone) ||
                            (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadLeft) == GamepadButtonFlags.DPadLeft && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadLeft) == 0)
                        {
                            SelectNeighbor(NavigateDirection.Left);
                        }
                        //and for down
                        else if (((float)currentControllerState.Gamepad.LeftThumbY / short.MaxValue * -1f > deadZone && (float)compareState.Gamepad.LeftThumbY / short.MaxValue * -1f < deadZone) ||
                            (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadDown) == GamepadButtonFlags.DPadDown && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadDown) == 0)
                        {
                            SelectNeighbor(NavigateDirection.Down);
                        }
                        //and for up
                        else if (((float)currentControllerState.Gamepad.LeftThumbY / short.MaxValue > deadZone && (float)compareState.Gamepad.LeftThumbY / short.MaxValue < deadZone) ||
                            (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadUp) == GamepadButtonFlags.DPadUp && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadUp) == 0)
                        {
                            SelectNeighbor(NavigateDirection.Up);
                        }
                        //see if the 'A' button was pressed this frame
                        else if ((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.A) == GamepadButtonFlags.A && (compareState.Gamepad.Buttons & GamepadButtonFlags.A) == 0)
                        {
                            OnPressed();
                        }
                        //see if the 'X' button was pressed this frame
                        else if ((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.X) == GamepadButtonFlags.X && (compareState.Gamepad.Buttons & GamepadButtonFlags.X) == 0)
                        {
                            SendKeys.SendWait("{BACKSPACE}");
                        }
                        //see if the 'Y' button was pressed this frame
                        else if ((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.Y) == GamepadButtonFlags.Y && (compareState.Gamepad.Buttons & GamepadButtonFlags.Y) == 0)
                        {
                            SendKeys.SendWait(" ");
                        }
                        //see if the 'B' button was pressed this frame
                        else if ((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.B) == GamepadButtonFlags.B && (compareState.Gamepad.Buttons & GamepadButtonFlags.B) == 0)
                        {
                            parentKeyboard.Deactivate();
                        }

                        oldControllerState[controller] = currentControllerState;
                    }
                }
            }
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

        private bool pointWithinButtonBounds(System.Drawing.Point point)
        {
            var form = Program.GetProgramForm();
            if (form == null || form.Disposing || form.IsDisposed)
                return false;

            var mousePos = form.PointToClient(System.Windows.Forms.Cursor.Position);

            if (mousePos.X >= Position.X && mousePos.X <= Position.X + Size.Width && mousePos.Y >= Position.Y && mousePos.Y <= Position.Y + Size.Height)
                return true;
            else
                return false;
        }

        public virtual void Dispose()
        {
            OnPressed = null;
            OnHover = null;
            OnHeld = null;

            Form form = Program.GetProgramForm();
            form.MouseDown -= mouseDownFunction;
            form.MouseUp -= mouseUpFunction;
            form.MouseMove -= mouseMoveFunction;
            form.KeyUp -= keyDownFunction;

            parentKeyboard = null;

            colorBrush?.Dispose();
        }
    }
}
