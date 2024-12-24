using save_switcher.Panels.Subpanels;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;
using System.Diagnostics;

namespace save_switcher.Elements
{
    internal class KeyboardButtonBackspace : KeyboardButton
    {
        BitmapImage image;
        Vector2 padding = new Vector2(30f, 20f);

        public KeyboardButtonBackspace(OnScreenKeyboard parent, DeviceContext deviceContext, Vector2 position, Size2 size, string inputSequence)
            : base(parent, deviceContext, position, size, inputSequence)
        {
            var imageFactory = new ImagingFactory();
            image = new BitmapImage("Media/backspace.png", deviceContext, imageFactory);

            imageFactory.Dispose();
        }

        public override void Draw()
        {
            base.Draw();

            deviceContext.DrawBitmap(image.Image, new RawRectangleF(Position.X + padding.X, Position.Y + padding.Y,
                        Position.X + Size.Width - padding.X, Position.Y + Size.Height - padding.Y), 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);
        }

        public override void Dispose()
        {
            base.Dispose();

            image.Image.Dispose();
        }
    }
}
