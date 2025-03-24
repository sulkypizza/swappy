using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SharpDX.Mathematics.Interop;
using System;
using System.Windows.Forms;

namespace save_switcher.Elements
{
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

    internal class SimpleButton : Button, IDisposable
    {
        private SimpleButtonProperties properties;

        private SolidColorBrush colorBrush;

        private TextLayout buttonText;

        private (Color button, Color border) currentColors;


        public SimpleButton(DeviceContext deviceContext, SimpleButtonProperties properties, Action buttonAction) 
            : base (deviceContext, properties.Position, properties.Size)
        {
            this.properties = properties;

            currentColors = (properties.ButtonColor, properties.BorderColor);

            colorBrush = new SolidColorBrush(deviceContext, Color.White);

            createSizeDependantResources();

            OnHover += () => { currentColors = (properties.ButtonHoverColor, properties.BorderHoverColor); };
            OnPressed += () => { 
                currentColors = (properties.ButtonPressedColor, properties.BorderPressedColor);
                buttonAction.Invoke();
            };
        }

        public override void Deselect()
        {
            currentColors = (properties.ButtonColor, properties.BorderColor);
        }

        public new void Dispose()
        {
            buttonText.Dispose();
            colorBrush.Dispose();

            base.Dispose();
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

        public override void Update() { }

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
    }
}
