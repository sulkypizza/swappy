using save_switcher.Panels.Subpanels;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX.Direct2D1;
using SharpDX.WIC;
using SharpDX.Mathematics.Interop;
using System.Diagnostics;

namespace save_switcher.Elements
{

    internal class KeyboardButtonShift : KeyboardButton
    {
        public event PressedEvent OnShiftLockPressed;
        public event PressedEvent OnShiftPressed;

        private OnScreenKeyboard parent;

        private Vector2 padding;

        private (BitmapImage unpressed, BitmapImage pressed, BitmapImage locked) images;
        private Stopwatch stopwatch;
        private long lastPress;
        private const double doublePressMs = 250;

        public KeyboardButtonShift(OnScreenKeyboard parent, DeviceContext deviceContext, Vector2 position, Size2 size, string inputSequence)
            : base(parent, deviceContext, position, size, inputSequence)
        {
            this.parent = parent;

            stopwatch = new Stopwatch();
            stopwatch.Start();
            lastPress = stopwatch.ElapsedMilliseconds;

            var imageFactory = new ImagingFactory();
            images.pressed = new BitmapImage("Media/shift_pressed.png", deviceContext, imageFactory);
            images.unpressed = new BitmapImage("Media/shift_unpressed.png", deviceContext, imageFactory);
            images.locked = new BitmapImage("Media/shift_locked.png", deviceContext, imageFactory);

            imageFactory.Dispose();

            padding = new Vector2(35f, 20f);

            OnPressed += () =>
            {
                if (!parent.ShiftStates.locked)
                {

                    if (stopwatch.ElapsedMilliseconds - lastPress < doublePressMs)
                    {
                        //double press
                        OnShiftLockPressed();
                    }
                    else OnShiftPressed();

                }
                else
                {
                    OnShiftLockPressed();

                    if (parent.ShiftStates.activated)
                        OnShiftPressed();
                }

                lastPress = stopwatch.ElapsedMilliseconds;
            };
        }

        public override void Draw()
        {
            base.Draw();

            BitmapImage shiftImage = parent.ShiftStates.locked ? images.locked : parent.ShiftStates.activated ? images.pressed : images.unpressed;

            deviceContext.DrawBitmap(shiftImage.Image, new RawRectangleF(Position.X + padding.X, Position.Y + padding.Y,
                         Position.X + Size.Width - padding.X, Position.Y + Size.Height - padding.Y), 1f, SharpDX.Direct2D1.BitmapInterpolationMode.Linear);

        }

        public override void Dispose()
        {
            base.Dispose();

            images.pressed.Image.Dispose();
            images.unpressed.Image.Dispose();
            images.locked.Image.Dispose();

            stopwatch.Stop();

            parent = null;
        }
    }
}
