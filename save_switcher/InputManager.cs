using SharpDX.XInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;

namespace save_switcher
{
    public static class InputManager
    {
        public enum ButtonTravel
        {
            Up,
            Down,
            Press,
        }

        public enum InputType
        {
            Mouse,
            Keyboard,
            Controller,
        }

        public static InputType CurrentInputType { get; private set; }

        public delegate void ButtonInput(ButtonTravel travel);
        public delegate void CharacterButtonInput(char character);
        public delegate void PositionChanged(Point position);
        public delegate void InputDelta(int delta);


        private static Dictionary<int, List<WeakReference>> characterInputEvents = new Dictionary<int, List<WeakReference>>();
        public static event CharacterButtonInput OnCharacterInput { add => addEvent(characterInputEvents, value); remove => removeEvent(characterInputEvents, value); }

        private static Dictionary<int, List<WeakReference>> leftInputEvents = new Dictionary<int, List<WeakReference>>();
        public static event ButtonInput OnLeftInput { add => addEvent(leftInputEvents, value); remove => removeEvent(leftInputEvents, value); }

        private static Dictionary<int, List<WeakReference>> rightInputEvents = new Dictionary<int, List<WeakReference>>();
        public static event ButtonInput OnRightInput { add => addEvent(rightInputEvents, value); remove => removeEvent(rightInputEvents, value); }

        private static Dictionary<int, List<WeakReference>> upInputEvents = new Dictionary<int, List<WeakReference>>();
        public static event ButtonInput OnUpInput { add => addEvent(upInputEvents, value); remove => removeEvent(upInputEvents, value); }

        private static Dictionary<int, List<WeakReference>> downInputEvents = new Dictionary<int, List<WeakReference>>();
        public static event ButtonInput OnDownInput { add => addEvent(downInputEvents, value); remove => removeEvent(downInputEvents, value); }

        private static Dictionary<int, List<WeakReference>> enterInputEvents = new Dictionary<int, List<WeakReference>>();
        public static event ButtonInput OnEnterInput { add => addEvent(enterInputEvents, value); remove => removeEvent(enterInputEvents, value); }

        private static Dictionary<int, List<WeakReference>> spaceInputEvents = new Dictionary<int, List<WeakReference>>();
        public static event ButtonInput OnAltEnterInput { add => addEvent(spaceInputEvents, value); remove => removeEvent(spaceInputEvents, value); }

        private static Dictionary<int, List<WeakReference>> backInputEvents = new Dictionary<int, List<WeakReference>>();
        public static event ButtonInput OnBackInput { add => addEvent(backInputEvents, value); remove => removeEvent(backInputEvents, value); }

        private static Dictionary<int, List<WeakReference>> leftMouseInputEvents = new Dictionary<int, List<WeakReference>>();
        public static event ButtonInput OnLeftMouseInput { add => addEvent(leftMouseInputEvents, value); remove => removeEvent(leftMouseInputEvents, value); }

        private static Dictionary<int, List<WeakReference>> rightMouseInputEvents = new Dictionary<int, List<WeakReference>>();
        public static event ButtonInput OnRightMouseInput { add => addEvent(rightMouseInputEvents, value); remove => removeEvent(rightMouseInputEvents, value); }

        private static Dictionary<int, List<WeakReference>> mousePosChanged = new Dictionary<int, List<WeakReference>>();
        public static event PositionChanged OnMousePosChanged { add => addEvent(mousePosChanged, value); remove => removeEvent(mousePosChanged, value); }

        private static Dictionary<int, List<WeakReference>> mouseScrollChanged = new Dictionary<int, List<WeakReference>>();
        public static event InputDelta OnMouseScroll { add => addEvent(mouseScrollChanged, value); remove => removeEvent(mouseScrollChanged, value); }

        private static ConditionalWeakTable<object, List<object>> keepAlive = new ConditionalWeakTable<object, List<object>>();
        private static List<Dictionary<int, List<WeakReference>>> allEventLists;

        private static Controller[] controllers;
        private static State[] oldControllerState;

        static InputManager()
        {
            allEventLists = new List<Dictionary<int, List<WeakReference>>>()
            {
                leftInputEvents,
                rightInputEvents,
                upInputEvents,
                downInputEvents,
                enterInputEvents,
                spaceInputEvents,
                backInputEvents,
                leftMouseInputEvents,
                rightMouseInputEvents,
                mousePosChanged,
                mouseScrollChanged,
            };

            Timer timer = new Timer() { Interval = 50 };
            timer.Tick += (_,__) => { update(); };
            timer.Start();

            Timer controllerReconnectTimer = new Timer() { Interval = 30000 };
            controllerReconnectTimer.Tick += (_, __) => { reconnectControllers(); };
            controllerReconnectTimer.Start();

            reconnectControllers();


            Application.ApplicationExit += (_, __) =>
            {
                timer.Stop();
                controllerReconnectTimer.Stop();

                timer.Dispose();
                controllerReconnectTimer.Dispose();
            };
        }

        public static void CleanupNullEvents()
        {
            foreach (var list in allEventLists)
            {
                cleanupEvents(list);
            }
        }

        public static void Initialize(Form form)
        {
            form.MouseDown += (object sender, MouseEventArgs e) =>
            {
                CurrentInputType = InputType.Mouse;

                if (e.Button == MouseButtons.Left)
                    invokeEvents(leftMouseInputEvents, ButtonTravel.Down);
                else if (e.Button == MouseButtons.Right)
                    invokeEvents(rightMouseInputEvents, ButtonTravel.Down);
            };

            form.MouseUp += (object sender, MouseEventArgs e) =>
            {
                CurrentInputType = InputType.Mouse;

                if (e.Button == MouseButtons.Left)
                    invokeEvents(leftMouseInputEvents, ButtonTravel.Up);
                else if (e.Button == MouseButtons.Right)
                    invokeEvents(rightMouseInputEvents, ButtonTravel.Up);
            };

            form.MouseWheel += (object sender, MouseEventArgs e) =>
            {
                CurrentInputType = InputType.Mouse;
                invokeEvents(mouseScrollChanged, e.Delta);
            };

            form.MouseMove += (object sender, MouseEventArgs e) =>
            {
                CurrentInputType = InputType.Mouse;
                invokeEvents(mousePosChanged, e.Location);
            };



            void invokeKeyInput(KeyEventArgs e, ButtonTravel buttonTravel)
            {
                IDictionary<int, List<WeakReference>> list = null;
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
                        list = spaceInputEvents;
                        break;

                    case Keys.Escape:
                        list = backInputEvents;
                        break;
                }

                if (list != null)
                    invokeEvents(list, buttonTravel);
            }

            form.KeyDown += (object sender, KeyEventArgs e) =>
            {
                CurrentInputType = InputType.Keyboard;
                invokeKeyInput(e, ButtonTravel.Down);
            };

            form.KeyUp += (object sender, KeyEventArgs e) =>
            {
                CurrentInputType = InputType.Keyboard;
                invokeKeyInput(e, ButtonTravel.Up);
            };

            form.KeyPress += (object sender, KeyPressEventArgs e) =>
            {
                invokeEvents(characterInputEvents, e.KeyChar);
            };
        }

        public static void RemoveEventsFromObject(object obj)
        {
            foreach(var list in allEventLists)
            {
                list.Remove(obj.GetHashCode());
            }
        }


        private static void addEvent(IDictionary<int, List<WeakReference>> list, Delegate addEvent)
        {
            List<WeakReference> eventList;
            if (!list.TryGetValue(addEvent.Target.GetHashCode(), out eventList))
                list.Add(addEvent.Target.GetHashCode(), new List<WeakReference>() { new WeakReference(addEvent) });
            else
                eventList.Add(new WeakReference(addEvent));

            keepAlive.GetOrCreateValue(addEvent.Target).Add(addEvent);
        }


        private static void cleanupEvents(IDictionary<int, List<WeakReference>> eventList)
        {
            List<int> removeParent = new List<int>();

            foreach (var list in eventList)
            {
                List<WeakReference> removeEvent = new List<WeakReference>();

                foreach (var item in list.Value)
                {
                    if(item.Target == null)
                        removeEvent.Add(item);
                }

                foreach (var item in removeEvent)
                {
                    list.Value.Remove(item);
                }

                if(list.Value.Count == 0)
                    removeParent.Add(list.Key);
            }

            foreach(var removeEvent in removeParent)
            {
                eventList.Remove(removeEvent);
            }

        }

        private static void invokeEvents(IDictionary<int, List<WeakReference>> eventList, params object[] args)
        {
            var list = eventList.ToList();

            foreach (var pair in list)
            {
                foreach (var e in pair.Value)
                {
                    var d = (Delegate)e.Target;
                    if (d != null)
                        d.Method.Invoke(d.Target, args);
                }
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

        private static void removeEvent(IDictionary<int, List<WeakReference>> list, Delegate addEvent)
        {
            List<WeakReference> eventList;
            if (list.TryGetValue(addEvent.Target.GetHashCode(), out eventList))
                foreach (WeakReference e in eventList)
                {
                    if (e.Target != null)
                        if (((Delegate)e.Target).Method.GetHashCode() == addEvent.Method.GetHashCode())
                            eventList.Remove(e);
                }

            keepAlive.TryGetValue(addEvent.Target, out var l);
            l?.Remove(addEvent);
        }

        private static void update()
        {
            float controllerDeadZone = 0.7f;
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
                        CurrentInputType = InputType.Controller;
                        invokeEvents(rightInputEvents, ButtonTravel.Down);
                    }
                    //same for this one except for left
                    else if (((float)currentControllerState.Gamepad.LeftThumbX / short.MaxValue * -1f > controllerDeadZone && (float)compareState.Gamepad.LeftThumbX / short.MaxValue * -1f < controllerDeadZone) ||
                        (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadLeft) == GamepadButtonFlags.DPadLeft && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadLeft) == 0)
                    {
                        CurrentInputType = InputType.Controller;
                        invokeEvents(leftInputEvents, ButtonTravel.Down);
                    }
                    //and for down
                    else if (((float)currentControllerState.Gamepad.LeftThumbY / short.MaxValue * -1f > controllerDeadZone && (float)compareState.Gamepad.LeftThumbY / short.MaxValue * -1f < controllerDeadZone) ||
                        (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadDown) == GamepadButtonFlags.DPadDown && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadDown) == 0)
                    {
                        CurrentInputType = InputType.Controller;
                        invokeEvents(downInputEvents, ButtonTravel.Down);
                    }
                    //and for up
                    else if (((float)currentControllerState.Gamepad.LeftThumbY / short.MaxValue > controllerDeadZone && (float)compareState.Gamepad.LeftThumbY / short.MaxValue < controllerDeadZone) ||
                        (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.DPadUp) == GamepadButtonFlags.DPadUp && (compareState.Gamepad.Buttons & GamepadButtonFlags.DPadUp) == 0)
                    {
                        CurrentInputType = InputType.Controller;
                        invokeEvents(upInputEvents, ButtonTravel.Down);
                    }
                    //see if the 'A' or 'X' button was pressed this frame
                    else if (((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.A) == GamepadButtonFlags.A || (currentControllerState.Gamepad.Buttons & GamepadButtonFlags.X) == GamepadButtonFlags.X) &&
                        (compareState.Gamepad.Buttons & (GamepadButtonFlags.X | GamepadButtonFlags.A)) == 0)
                    {
                        CurrentInputType = InputType.Controller;
                        invokeEvents(enterInputEvents, ButtonTravel.Down);
                    }
                    //see if the 'Y' button was pressed this frame
                    else if ((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.Y) == GamepadButtonFlags.Y && (compareState.Gamepad.Buttons & GamepadButtonFlags.Y) == 0)
                    {
                        CurrentInputType = InputType.Controller;
                        invokeEvents(spaceInputEvents, ButtonTravel.Down);
                    }
                    //see if the 'B' button was pressed this frame
                    else if ((currentControllerState.Gamepad.Buttons & GamepadButtonFlags.B) == GamepadButtonFlags.B && (compareState.Gamepad.Buttons & GamepadButtonFlags.B) == 0)
                    {
                        CurrentInputType = InputType.Controller;
                        invokeEvents(backInputEvents, ButtonTravel.Down);
                    }

                    oldControllerState[controller] = currentControllerState;
                }
            }
        }
    }
}
