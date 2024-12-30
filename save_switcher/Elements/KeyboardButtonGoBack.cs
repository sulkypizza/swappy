using save_switcher.Panels.Subpanels;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;

namespace save_switcher.Elements
{
    internal class KeyboardButtonGoBack : KeyboardButton
    {
        private BitmapImage image;
        private Vector2 padding = new Vector2(30f, 25f);

        public KeyboardButtonGoBack(OnScreenKeyboard parent, DeviceContext deviceContext, Vector2 position, Size2 size, string inputSequence)
            : base(parent, deviceContext, position, size, inputSequence)
        {
            var imageFactory = new ImagingFactory();
            image = new BitmapImage("Media/down_chevron.png", deviceContext, imageFactory);

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
