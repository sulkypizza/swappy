using save_switcher.Panels.Subpanels;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;

namespace save_switcher.Elements
{
    internal class KeyboardButtonCharacter : KeyboardButton
    {
        public SolidColorBrush colorBrush;

        private readonly OnScreenKeyboard parent;

        private TextLayout characterLayoutLowercase;
        private TextLayout characterLayoutUppercase;

        private char inputChar;
        private float scalePercentage;


        public KeyboardButtonCharacter(OnScreenKeyboard parent, DeviceContext deviceContext, Vector2 position, Size2 size, char inputSequence ) 
            :base (parent, deviceContext, position, size, inputSequence.ToString())
        {
            this.parent = parent;
            inputChar = inputSequence;

            createSizeDependantResources();
        }

        public override void Resize(DeviceContext deviceContext)
        {
            base.Resize(deviceContext);

            createSizeDependantResources();
        }

        public override void Draw()
        {
            base.Draw();

            TextLayout characterLayout = parent.ShiftStates.activated ? characterLayoutUppercase : characterLayoutLowercase;

            colorBrush.Color = Color.White;
            deviceContext.DrawTextLayout(new SharpDX.Mathematics.Interop.RawVector2(Position.X + (Size.Width / 2 - characterLayout.MaxWidth / 2) * scalePercentage, Position.Y + (Size.Height / 2 - characterLayout.MaxHeight / 2) * scalePercentage), characterLayout, colorBrush);

        }

        private void createSizeDependantResources()
        {
            colorBrush?.Dispose();
            characterLayoutLowercase?.Dispose();
            characterLayoutUppercase?.Dispose();

            scalePercentage = deviceContext.Size.Width > deviceContext.Size.Height ? deviceContext.Size.Width / 1920 : deviceContext.Size.Height / 1080;

            colorBrush = new SolidColorBrush(deviceContext, Color.White);

            var writeFactory = Program.GetDirectWriteFactory();
            var fontCollection = Program.GetFontCollection();
            var textFormat = new TextFormat(writeFactory, "Gabarito", fontCollection, SharpDX.DirectWrite.FontWeight.Normal, SharpDX.DirectWrite.FontStyle.Normal, SharpDX.DirectWrite.FontStretch.Normal, 50f * scalePercentage);

            characterLayoutLowercase = new TextLayout(writeFactory, inputChar.ToString().ToLower(), textFormat, 50f * scalePercentage, 50f * scalePercentage);
            characterLayoutLowercase.ParagraphAlignment = ParagraphAlignment.Center;
            characterLayoutLowercase.TextAlignment = TextAlignment.Center;

            characterLayoutUppercase = new TextLayout(writeFactory, inputChar.ToString().ToUpper(), textFormat, 50f * scalePercentage, 50f * scalePercentage);
            characterLayoutUppercase.ParagraphAlignment = ParagraphAlignment.Center;
            characterLayoutUppercase.TextAlignment = TextAlignment.Center;

            textFormat.Dispose();
        }
    }
}
