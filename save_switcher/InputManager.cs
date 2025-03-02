using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace save_switcher
{
    public static class InputManager
    {
        public enum ButtonTravel
        {
            Up,
            Down,
        }

        public delegate void ButtonInput(ButtonTravel travel);
        public delegate void PositionChanged(Point position);
        public delegate void InputDelta(int delta);


        private static Dictionary<int, WeakReference> leftInputEvents = new Dictionary<int, WeakReference>();
        public static event ButtonInput OnLeftInput { add => addEvent(leftInputEvents, value); remove => removeEvent(leftInputEvents, value); }

        private static Dictionary<int, WeakReference> rightInputEvents = new Dictionary<int, WeakReference>();
        public static event ButtonInput OnRightInput { add => addEvent(rightInputEvents, value); remove => removeEvent(rightInputEvents, value); }

        private static Dictionary<int, WeakReference> upInputEvents = new Dictionary<int, WeakReference>();
        public static event ButtonInput OnUpInput { add => addEvent(upInputEvents, value); remove => removeEvent(upInputEvents, value); }

        private static Dictionary<int, WeakReference> downInputEvents = new Dictionary<int, WeakReference>();
        public static event ButtonInput OnDownInput { add => addEvent(downInputEvents, value); remove => removeEvent(downInputEvents, value); }

        private static Dictionary<int, WeakReference> enterInputEvents = new Dictionary<int, WeakReference>();
        public static event ButtonInput OnEnterInput { add => addEvent(enterInputEvents, value); remove => removeEvent(enterInputEvents, value); }

        private static Dictionary<int, WeakReference> altEnterInputEvents = new Dictionary<int, WeakReference>();
        public static event ButtonInput OnAltEnterInput { add => addEvent(altEnterInputEvents, value); remove => removeEvent(altEnterInputEvents, value); }

        private static Dictionary<int, WeakReference> backInputEvents = new Dictionary<int, WeakReference>();
        public static event ButtonInput OnBackInput { add => addEvent(backInputEvents, value); remove => removeEvent(backInputEvents, value); }

        private static Dictionary<int, WeakReference> leftMouseInputEvents = new Dictionary<int, WeakReference>();
        public static event ButtonInput OnLeftMouseInput { add => addEvent(leftMouseInputEvents, value); remove => removeEvent(leftMouseInputEvents, value); }

        private static Dictionary<int, WeakReference> rightMouseInputEvents = new Dictionary<int, WeakReference>();
        public static event ButtonInput OnRightMouseInput { add => addEvent(rightMouseInputEvents, value); remove => removeEvent(rightMouseInputEvents, value); }

        private static Dictionary<int, WeakReference> mousePosChanged = new Dictionary<int, WeakReference>();
        public static event PositionChanged OnMousePosChanged { add => addEvent(mousePosChanged, value); remove => removeEvent(mousePosChanged, value); }

        private static Dictionary<int, WeakReference> mouseScrollChanged = new Dictionary<int, WeakReference>();
        public static event PositionChanged OnMouseScroll { add => addEvent(mouseScrollChanged, value); remove => removeEvent(mouseScrollChanged, value); }


        private static Controller[] controllers;
        private static State[] oldControllerState;

        static InputManager()
        {
            Timer timer = new Timer() { Interval = 250 };
            timer.Tick += (_,__) => { update(); };
            timer.Start();

            Timer controllerReconnectTimer = new Timer() { Interval = 30000 };
            controllerReconnectTimer.Tick += (_, __) => { reconnectControllers(); };
            controllerReconnectTimer.Start();

            reconnectControllers();
        }

        public static void CleanupNullEvents()
        {
            cleanupEvents(leftInputEvents);
            cleanupEvents(rightInputEvents);
            cleanupEvents(downInputEvents);
            cleanupEvents(upInputEvents);
            cleanupEvents(enterInputEvents);
            cleanupEvents(altEnterInputEvents);
            cleanupEvents(backInputEvents);
            cleanupEvents(leftMouseInputEvents);
            cleanupEvents(rightMouseInputEvents);
        }

        public static void Initialize(Form form)
        {
            form.MouseDown += (object sender, MouseEventArgs e) =>
            {
                if (e.Button == MouseButtons.Left)
                    invokeEvents(leftMouseInputEvents, ButtonTravel.Down);
                else if (e.Button == MouseButtons.Right)
                    invokeEvents(rightMouseInputEvents, ButtonTravel.Down);
            };

            form.MouseUp += (object sender, MouseEventArgs e) =>
            {
                if (e.Button == MouseButtons.Left)
                    invokeEvents(leftMouseInputEvents, ButtonTravel.Up);
                else if (e.Button == MouseButtons.Right)
                    invokeEvents(rightMouseInputEvents, ButtonTravel.Up);
            };

            form.MouseWheel += (object sender, MouseEventArgs e) =>
            {
                invokeEvents(mouseScrollChanged, e.Delta);
            };

            form.MouseMove += (object sender, MouseEventArgs e) =>
            {
                invokeEvents(mousePosChanged, e.Location);
            };



            void invokeKeyInput(KeyEventArgs e, ButtonTravel buttonTravel)
            {
                IDictionary<int, WeakReference> list = null;
                switch (e.KeyCode)
                {
                    case Keys.Left:
                        list = leftInputEvents;
                        break;

                    case Keys.Right:
                        list = rightInputEvents;
                        break;

                    case Keys.Up:
                        list = upInputEvents;
                        break;

                    case Keys.Down:
                        list = downInputEvents;
                        break;

                    case Keys.Enter:
                        list = enterInputEvents;
                        break;

                    case Keys.Space:
                        list = altEnterInputEvents;
                        break;

                    case Keys.Escape:
                        list = backInputEvents;
                        break;
                }

                if (list != null)
                    invokeEvents(list, ButtonTravel.Down);
            }

            form.KeyDown += (object sender, KeyEventArgs e) =>
            {
                invokeKeyInput(e, ButtonTravel.Down);
            };

            form.KeyUp += (object sender, KeyEventArgs e) =>
            {
                invokeKeyInput(e, ButtonTravel.Up);
            };

            //form.KeyPress += new System.Windows.Forms.KeyPressEventHandler(OnKeyPress);
        }


        private static void addEvent<T>(IDictionary<int, WeakReference> list, T addEvent)
        {
            list.Add(addEvent.GetHashCode(), new WeakReference(addEvent));
        }

        private static void cleanupEvents(IDictionary<int, WeakReference> eventList)
        {
            foreach(var pair in eventList)
            {
                if(pair.Value.Target == null)
                    eventList.Remove(pair.Key);
            }
        }

        private static void invokeEvents(IDictionary<int, WeakReference> eventList, params object[] args)
        {
            foreach (var pair in eventList)
            {
                var d = (Delegate)pair.Value.Target;
                if (d != null)
                    d.Method.Invoke(d.Target, args);
            }
        }

        private static void reconnectControllers()
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

        private static void removeEvent<T>(IDictionary<int, WeakReference> list, T addEvent)
        {
            list.Remove(addEvent.GetHashCode());
        }

        private static void update()
        {
            float controllerDeadZone = 0.8f;
            for (int controller = 0; controller < controllers.Length; controller++)
            {
                //don't do anything if the controller isn't connected
                if (controllers[controller].IsConnected)
                {
                    //get the current state and the old state for comparison
                    State currentControllerState = controllers[controller].GetState();
                    State compareState = oldControllerState[controller];

                    //if the left stick is past the dead zone this time and short the dead zone last time OR the d-pad is pressed
                    if (((float)currentControllerState.Gamepad.LeftThumbX / short.MaxValue > controllerDeadZone && ((float)compareState.Gamepad.LeftThumbX / short.MaxValue) < controllerDeadZone) ||
                            (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadRight) == GamepadButtonFlags.DPadRight && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadRight) == 0)
                    {
                        invokeEvents(leftInputEvents, ButtonTravel.Down);
                    }
                    //same for this one except for left
                    else if (((float)currentControllerState.Gamepad.LeftThumbX / short.MaxValue * -1f > controllerDeadZone && (float)compareState.Gamepad.LeftThumbX / short.MaxValue * -1f < controllerDeadZone) ||
                        (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadLeft) == GamepadButtonFlags.DPadLeft && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadLeft) == 0)
                    {
                        invokeEvents(rightInputEvents, ButtonTravel.Down);
                    }
                    //and for down
                    else if (((float)currentControllerState.Gamepad.LeftThumbY / short.MaxValue * -1f > controllerDeadZone && (float)compareState.Gamepad.LeftThumbY / short.MaxValue * -1f < controllerDeadZone) ||
                        (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadDown) == GamepadButtonFlags.DPadDown && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadDown) == 0)
                    {
                        invokeEvents(downInputEvents, ButtonTravel.Down);
                    }
                    //and for up
                    else if (((float)currentControllerState.Gamepad.LeftThumbY / short.MaxValue > controllerDeadZone && (float)compareState.Gamepad.LeftThumbY / short.MaxValue < controllerDeadZone) ||
                        (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadUp) == GamepadButtonFlags.DPadUp && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadUp) == 0)
                    {
                        invokeEvents(upInputEvents, ButtonTravel.Down);
                    }
                    //see if the 'A' or 'X' button was pressed this frame
                    else if (((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.A) == GamepadButtonFlags.A || (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.X) == GamepadButtonFlags.X) &&
                        (compareState.Gamepad.Buttons & (GamepadButtonFlags.X | GamepadButtonFlags.A)) == 0)
                    {
                        invokeEvents(enterInputEvents, ButtonTravel.Down);
                    }
                    //see if the 'Y' button was pressed this frame
                    else if ((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.Y) == GamepadButtonFlags.Y && (compareState.Gamepad.Buttons & GamepadButtonFlags.Y) == 0)
                    {
                        invokeEvents(altEnterInputEvents, ButtonTravel.Down);
                    }
                    //see if the 'B' button was pressed this frame
                    else if ((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.B) == GamepadButtonFlags.B && (compareState.Gamepad.Buttons & GamepadButtonFlags.B) == 0)
                    {
                        invokeEvents(backInputEvents, ButtonTravel.Down);
                    }

                    oldControllerState[controller] = currentControllerState;
                }
            }
        }
    }
}
