using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using save_switcher.Elements;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Mathematics.Interop;

namespace save_switcher.Panels
{
    internal class Settings : Panel, IDisposable
    {
        private DeviceContext deviceContext;

        private LinearGradientBrush backgroundBrush;

        private SimpleButton addGameButton;
        private SimpleButton addSyncButton;

        public Settings(DeviceContext deviceContext)
        {
            this.deviceContext = deviceContext;

            createSizeDependantResources();

            Program.GetProgramForm().KeyDown += OnKeyDown;
        }

        public void Dispose()
        {
            Program.GetProgramForm().KeyDown -= OnKeyDown;

            addGameButton.Dispose();
            addSyncButton.Dispose();
        }

        public void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                Program.ChangePanel(new ProfileSelector(deviceContext));
        }

        private void createSizeDependantResources()
        {
            LinearGradientBrushProperties backGroundGradientProperties = new LinearGradientBrushProperties
            {
                StartPoint = new RawVector2(0, 0),
                EndPoint = new RawVector2(deviceContext.Size.Width, deviceContext.Size.Height),
            };
            GradientStop[] backgroundGradientStops = new GradientStop[]
            {
                new GradientStop()
                {
                    //purple
                    Color = new Color(137, 41, 73),
                    Position = 0,
                },

                new GradientStop()
                {
                    //regular blue
                    Color = new Color(67, 106, 172),
                    Position = 0.7f,
                },

                new GradientStop()
                {
                    //light blue
                    Color = new Color(67, 183, 184),
                    Position = 0.9f,
                }
            };
            backgroundBrush = new LinearGradientBrush(deviceContext, backGroundGradientProperties, new GradientStopCollection(deviceContext, backgroundGradientStops, ExtendMode.Wrap));

            var baseButtonSize = new Size2(500, 100);
            var addGameProperties = new SimpleButtonProperties()
            {
                Size = baseButtonSize,
                Position = new Vector2(deviceContext.Size.Width / 2 - baseButtonSize.Width / 2, 200),
                
                Text = "Add Game",
                TextColor = Color.White,
                FontSize = 30f,

                BorderThickness = 5f,
                CornerRadius = 10f,

                ButtonColor = new Color(61, 82, 191),
                ButtonHoverColor = new Color(74, 93, 186),
                ButtonPressedColor = new Color(54, 74, 175),

                BorderColor = new Color(76, 98, 211),
                BorderHoverColor = new Color(95, 114, 211),
                BorderPressedColor = new Color(63, 85, 193),
            };

            var addSyncProperties = new SimpleButtonProperties()
            {
                Size = baseButtonSize,
                Position = new Vector2(deviceContext.Size.Width / 2 - baseButtonSize.Width / 2, 400),

                Text = "Add Sync",
                TextColor = Color.White,
                FontSize = 30f,

                BorderThickness = 5f,
                CornerRadius = 10f,

                ButtonColor = new Color(61, 82, 191),
                ButtonHoverColor = new Color(74, 93, 186),
                ButtonPressedColor = new Color(54, 74, 175),

                BorderColor = new Color(76, 98, 211),
                BorderHoverColor = new Color(95, 114, 211),
                BorderPressedColor = new Color(63, 85, 193),
            };

            addGameButton = new SimpleButton(deviceContext, addGameProperties, () => { /* TODO */ });
            addSyncButton = new SimpleButton(deviceContext, addSyncProperties, () => {  /* TODO */ });

            InputNavigable.ConnectNeighbors(NavigateDirection.Down, addGameButton, addSyncButton);

            addGameButton.Select();
        }

        public void Resize(DeviceContext deviceContext)
        {
            this.deviceContext = deviceContext;
            createSizeDependantResources();
        }

        public void Update()
        {
            addGameButton.Update();
            addSyncButton.Update();
        }

        public void Draw(DeviceContext deviceContext)
        {
            deviceContext.BeginDraw();
            deviceContext.Clear(Color.Black);
            deviceContext.FillRectangle(new RawRectangleF(0, 0, deviceContext.Size.Width, deviceContext.Size.Height), backgroundBrush);

            addGameButton.Draw();
            addSyncButton.Draw();

            deviceContext.EndDraw();
        }
    }
}
