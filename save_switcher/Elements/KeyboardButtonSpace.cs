using save_switcher.Panels.Subpanels;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;
using SharpDX.WIC;

namespace save_switcher.Elements
{
    internal class KeyboardButtonSpace : KeyboardButton
    {
        private BitmapImage image;
        private Vector2 padding;

        private InputNavigable lastSelectedButton;

        public KeyboardButtonSpace(OnScreenKeyboard parent, DeviceContext deviceContext, Vector2 position, Size2 size, string inputSequence)
            : base(parent, deviceContext, position, size, inputSequence)
        {
            padding = new Vector2(size.Width * 0.4f, size.Height * 0.3f);

            var imageFactory = new ImagingFactory();
            image = new BitmapImage("Media/space.png", deviceContext, imageFactory);

            imageFactory.Dispose();
        }

        public override void Select()
        {
            if (CurrentSelectedObject != this)
                lastSelectedButton = CurrentSelectedObject;

            base.Select();
        }

        public override bool SelectNeighbor(NavigateDirection direction)
        {
            if (direction == NavigateDirection.Up)
            {
                lastSelectedButton.Select();
                return true;
            }
            else return base.SelectNeighbor(direction);
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
            lastSelectedButton = null;
        }
    }
}
