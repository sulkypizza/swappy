using save_switcher.Elements;
using SharpDX;
using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace save_switcher.Panels.Subpanels
{

    internal class OnScreenKeyboard : Subpanel<char, object>
    {

        char[][] keyLayout =
            {
                new char[] { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0' },
                new char[] { 'Q', 'W', 'E', 'R', 'T', 'Y', 'U', 'I', 'O', 'P' },
                new char[] { 'A', 'S', 'D', 'F', 'G', 'H', 'J', 'K', 'L', '\'' },
                new char[] { 'Z', 'X', 'C', 'V', 'B', 'N', 'M', ',', '.', '?'},
            };

        const int sizeWidth = 70;
        const int sizeHeight = 70;
        const int padding = 10;
        private float keyboardTop;

        public (bool activated, bool locked) ShiftStates = (false, false);

        Dictionary<char, KeyboardButton> buttons;

        DeviceContext deviceContext;

        SolidColorBrush colorBrush;

        public OnScreenKeyboard(DeviceContext deviceContext) 
        {
            colorBrush = new SolidColorBrush(deviceContext, new Color(63, 36, 122));

            buttons = new Dictionary<char, KeyboardButton>();

            this.deviceContext = deviceContext;

            keyboardTop = deviceContext.Size.Height / 3;
            float startX = deviceContext.Size.Width / 2 - (keyLayout[0].Length * (sizeWidth + padding) + (sizeWidth * 1.5f)) / 2;

            //add special keys
            var specialButtonSize = new Size2((int)(sizeWidth * 1.5f), sizeHeight);
            //go back
            var goBackPosition = new Vector2(startX + keyLayout[0].Length * (sizeWidth + padding), deviceContext.Size.Height * 0.68f);
            KeyboardButtonGoBack goBack = new KeyboardButtonGoBack(this, deviceContext, goBackPosition, specialButtonSize, "{ESC}");
            buttons.Add('\u001b', goBack);

            //backspace
            var backspacePosition = new Vector2(startX + keyLayout[0].Length * (sizeWidth + padding), deviceContext.Size.Height * 0.68f + sizeHeight + padding);
            KeyboardButtonBackspace backspace = new KeyboardButtonBackspace(this, deviceContext, backspacePosition, specialButtonSize, "{BACKSPACE}");
            buttons.Add('\b', backspace);

            //shift
            var shiftPosition = new Vector2(startX + keyLayout[0].Length * (sizeWidth + padding), deviceContext.Size.Height * 0.68f + 2 * (sizeHeight + padding));
            KeyboardButtonShift shift = new KeyboardButtonShift(this, deviceContext, shiftPosition, specialButtonSize, "");
            buttons.Add('\u240f', shift);

            shift.OnShiftPressed += () =>
            {
                if(!ShiftStates.locked)
                    ShiftStates.activated = ShiftStates.activated ? false : true;
            };
            
            shift.OnShiftLockPressed += () =>
            { 
                ShiftStates.locked = ShiftStates.locked ? false : true;
                ShiftStates.activated = true;
            };

            //space
            var spacePosition = new Vector2(startX + (sizeWidth + padding) * (keyLayout[0].Length * 0.2f), deviceContext.Size.Height * 0.68f + (keyLayout.Length * (sizeHeight + padding)));
            var spaceSize = new Size2((int)((sizeWidth + padding) * (keyLayout[0].Length * 0.6) - padding), sizeHeight);
            KeyboardButtonSpace space = new KeyboardButtonSpace(this, deviceContext, spacePosition, spaceSize, " ");
            buttons.Add(' ', space);

            //special button navigation
            backspace.AddNeighbor(NavigateDirection.Up, goBack);
            goBack.AddNeighbor(NavigateDirection.Down, backspace);

            backspace.AddNeighbor(NavigateDirection.Down, shift);
            shift.AddNeighbor(NavigateDirection.Up, backspace);

            
            for (int i = 0; i < keyLayout.Length; i++)
            {
                for (int key = 0; key < keyLayout[i].Length; key++)
                {
                    Vector2 pos = new Vector2(startX + key * (sizeWidth + padding), deviceContext.Size.Height * 0.68f + i * (sizeHeight + padding));
                    Size2 size = new Size2(sizeWidth, sizeHeight);

                    KeyboardButtonCharacter kb = new KeyboardButtonCharacter(this, deviceContext, pos, size, keyLayout[i][key]);

                    kb.OnPressed += characterButtonPress;

                    //add naigation paths
                    KeyboardButton outButton;
                    if (key > 0)
                    {
                        //get button to the left
                        if (buttons.TryGetValue(keyLayout[i][key - 1], out outButton))
                        {
                            outButton.AddNeighbor(NavigateDirection.Right, kb);
                            kb.AddNeighbor(NavigateDirection.Left, outButton);
                        }
                    }

                    if (i > 0)
                    {
                        //get button above us
                        if (buttons.TryGetValue(keyLayout[i - 1][key], out outButton))
                        {
                            outButton.AddNeighbor(NavigateDirection.Down, kb);
                            kb.AddNeighbor(NavigateDirection.Up, outButton);
                        }
                    }

                    if(i == keyLayout.Length - 1 && 
                        (float)(key + 1) / keyLayout[i].Length > 0.2f && (float)(key + 1) / keyLayout[i].Length <= 0.8f)
                    {
                        kb.AddNeighbor(NavigateDirection.Down, space);
                    }

                    //add navigation to special buttons
                    //go back
                    if (i == 0 && key == keyLayout[i].Length - 1)
                    {
                        kb.AddNeighbor(NavigateDirection.Right, goBack);
                        goBack.AddNeighbor(NavigateDirection.Left, kb);
                    }

                    //backspace
                    if (i == 1 && key == keyLayout[i].Length - 1)
                    {
                        kb.AddNeighbor(NavigateDirection.Right, backspace);
                        backspace.AddNeighbor(NavigateDirection.Left, kb);
                    }

                    //shift
                    if (i == 2 && key == keyLayout[i].Length - 1)
                    {
                        kb.AddNeighbor(NavigateDirection.Right, shift);
                        shift.AddNeighbor(NavigateDirection.Left, kb);
                    }

                    buttons.Add(keyLayout[i][key], kb);
                }
            }

            //initialize gamepad controllers
            buttons.First().Value.ConnectControllers();
            buttons.First().Value.Select();

            Program.GetProgramForm().KeyPress += keyPressFunction;

            Program.GetProgramForm().KeyUp += keyUpFunction;
        }

        public event SubpanelExitEvent<object> OnExit;

        public event SubpanelUpdateEvent<char> OnUpdate;

        public event SubpanelExitEvent<KeyEventArgs> OnRawKey;

        private void keyUpFunction(object sender, KeyEventArgs args)
        {
            if (args.KeyCode == Keys.Escape)
                    Deactivate();
                else OnRawKey(args);
        }

        private void keyPressFunction(object sender, KeyPressEventArgs args)
        {
            OnUpdate(args.KeyChar);
        }

        private void characterButtonPress()
        {
            if (ShiftStates.activated && !ShiftStates.locked)
            {
                ShiftStates.activated = false;
            }
        }

        public void Activate()
        {
            
        }

        public void Deactivate()
        {
            Form form = Program.GetProgramForm();
            form.KeyPress -= keyPressFunction;
            form.KeyUp -= keyUpFunction;

            foreach(var b in buttons)
            {
                b.Value.OnPressed -= characterButtonPress;
                b.Value.Dispose();
            }
            OnExit?.Invoke(this);
        }

        public void Update()
        {
            foreach(var button in buttons)
            {
                button.Value.Update();
            }
        }

        public void Resize(DeviceContext deviceContext)
        {
            foreach (var button in buttons)
            {
                button.Value.Resize(deviceContext);
            }

            this.deviceContext = deviceContext;
        }

        public void Draw()
        {
            deviceContext.Transform = Matrix3x2.Identity;
            deviceContext.BeginDraw();

            var rect = new SharpDX.Mathematics.Interop.RawRectangleF(0f, deviceContext.Size.Height - keyboardTop, deviceContext.Size.Width, deviceContext.Size.Height);
            deviceContext.FillRectangle(rect, colorBrush);

            foreach(var button in buttons)
            {
                button.Value.Draw();
            }

            deviceContext.EndDraw();
        }
    }
}
