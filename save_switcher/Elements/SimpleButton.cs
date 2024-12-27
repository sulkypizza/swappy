using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System;
using System.Windows.Forms;

namespace save_switcher.Elements
{

    internal class SimpleButton : Button, IDisposable
    {
        private enum InputType
        {
            Mouse,
            Keyboard,
            Controller,
        }

        private SimpleButtonProperties properties;

        private SolidColorBrush colorBrush;

        private TextLayout buttonText;

        private (Color button, Color border) currentColors;

        private Action buttonAction;

        private Vector2 lastMousePosition;
        private InputType lastInputType;
        private bool cursorWithinBounds = false;

        public SimpleButton(DeviceContext deviceContext, SimpleButtonProperties properties, Action buttonAction) 
            : base (deviceContext, properties.Position, properties.Size)
        {
            this.properties = properties;
            this.buttonAction = buttonAction;

            currentColors = (properties.ButtonColor, properties.BorderColor);

            colorBrush = new SolidColorBrush(deviceContext, Color.White);

            createSizeDependantResources();

            Program.GetProgramForm().MouseDown += OnMouseDown;
            Program.GetProgramForm().MouseUp += OnMouseUp;
            Program.GetProgramForm().KeyDown += OnKeyDown;
            Program.GetProgramForm().KeyUp += OnKeyUp;
        }

        public void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (CurrentSelectedObject == this && cursorWithinBounds)
                pressButton();

        }

        public void OnMouseUp(object sender, MouseEventArgs e)
        {
            if(CurrentSelectedObject == this)
                currentColors = (properties.ButtonHoverColor, properties.BorderHoverColor);

        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (CurrentSelectedObject == this)
            {
                if (e.KeyCode == Keys.Left)
                    SelectNeighbor(NavigateDirection.Left);
                else if (e.KeyCode == Keys.Right)
                    SelectNeighbor(NavigateDirection.Right);
                else if (e.KeyCode == Keys.Up)
                    SelectNeighbor(NavigateDirection.Up);
                else if (e.KeyCode == Keys.Down)
                    SelectNeighbor(NavigateDirection.Down);
                else if (e.KeyCode == Keys.Enter)
                    pressButton();
            }

        }

        public void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (CurrentSelectedObject == this)
                currentColors = (properties.ButtonHoverColor, properties.BorderHoverColor);
        }

        public override void Deselect()
        {
            currentColors = (properties.ButtonColor, properties.BorderColor);
        }

        public void Dispose()
        {
            Program.GetProgramForm().MouseDown -= OnMouseDown;
            Program.GetProgramForm().MouseUp -= OnMouseUp;
            Program.GetProgramForm().KeyDown -= OnKeyDown;
            Program.GetProgramForm().KeyUp -= OnKeyUp;

            buttonText.Dispose();
            colorBrush.Dispose();
        }

        public override void Draw()
        {
            var rect = new RoundedRectangle()
            {
                Rect = new RawRectangleF(properties.Position.X, properties.Position.Y, properties.Position.X + properties.Size.Width, properties.Position.Y + properties.Size.Height),
                RadiusX = properties.CornerRadius,
                RadiusY = properties.CornerRadius,
            };

            colorBrush.Color = currentColors.button;
            deviceContext.FillRoundedRectangle(rect, colorBrush);

            colorBrush.Color = currentColors.border;
            deviceContext.DrawRoundedRectangle(rect, colorBrush);

            colorBrush.Color = properties.TextColor;
            deviceContext.DrawTextLayout(properties.Position, buttonText, colorBrush);
        }

        public override void Select()
        {
            base.Select();

            currentColors = (properties.ButtonHoverColor, properties.BorderHoverColor);
        }

        public override void Update()
        {
            Form activeForm = Form.ActiveForm;
            if (activeForm == null)
                return;

            System.Drawing.Point currentMousePos = Cursor.Position;
            System.Drawing.Point mouseToScreen = activeForm.PointToClient(currentMousePos);
            Vector2 mousePos = new Vector2(mouseToScreen.X, mouseToScreen.Y);

            if (lastInputType == InputType.Mouse && mousePos.X >= properties.Position.X &&
                mousePos.X <= properties.Position.X + properties.Size.Width && mousePos.Y >= properties.Position.Y &&
                mousePos.Y <= properties.Position.Y + properties.Size.Height)
            {
                if (CurrentSelectedObject != this)
                {
                    Select();
                }

                cursorWithinBounds = true;
            }
            else
                cursorWithinBounds = false;

            if (mousePos != lastMousePosition)
                lastInputType = InputType.Mouse;

            lastMousePosition = mousePos;

        }

        private void createSizeDependantResources()
        {
            var writeFactory = Program.GetDirectWriteFactory();
            var fontCollection = Program.GetFontCollection();
            var textFormat = new TextFormat(writeFactory, "Gabarito", fontCollection, SharpDX.DirectWrite.FontWeight.Normal, 
                SharpDX.DirectWrite.FontStyle.Normal, SharpDX.DirectWrite.FontStretch.Normal, properties.FontSize);

            buttonText = new TextLayout(writeFactory, properties.Text, textFormat, properties.Size.Width, properties.Size.Height);
            buttonText.ParagraphAlignment = ParagraphAlignment.Center;
            buttonText.TextAlignment = TextAlignment.Center;
        }

        private void pressButton()
        {
            currentColors = (properties.ButtonPressedColor, properties.BorderPressedColor);

            buttonAction.Invoke();
        }
    }

    public struct SimpleButtonProperties
    {
        public Vector2 Position;
        public Size2 Size;

        public string Text;
        public float FontSize;
        public Color TextColor;

        public float BorderThickness;
        public float CornerRadius;

        public Color ButtonColor;
        public Color ButtonHoverColor;
        public Color ButtonPressedColor;
        public Color BorderColor;
        public Color BorderHoverColor;
        public Color BorderPressedColor;
    }
}
