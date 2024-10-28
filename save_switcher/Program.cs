using SharpDX.Direct2D1;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Windows;
using SharpDX;
using System;
using System.Windows.Forms;

using save_switcher.Panels;

using AlphaMode = SharpDX.Direct2D1.AlphaMode;
using Device = SharpDX.Direct3D11.Device;
using PixelFormat = SharpDX.Direct2D1.PixelFormat;
using Bitmap = SharpDX.Direct2D1.Bitmap;

namespace save_switcher
{
    class Program
    {
        public static int? GameID;

        private static Panel currentPanel;
        private static SharpDX.Direct2D1.DeviceContext deviceContext;

        [STAThread]
        static void Main(string[] args)
        {

            //Args parsing:
            for(int i = 0; i < args.Length; i++)
            {
                string input = args[i].ToLower().TrimStart('/');
                if (input == "gameid")
                {
                    if(i + 1 < args.Length)
                    {
                        int parsed;
                        if(int.TryParse(args[i + 1], out parsed))
                            GameID = parsed;
                    }
                }
                else if(input == "help" || input == "?" || args[i] == "help")
                {
                    Console.WriteLine("Usage: <exe>.exe /gameid <int>:\n");
                    Console.WriteLine("/gameid <int>\t\tThe gameid of the executable you want to launch.");
                    Console.WriteLine("/help\t\tShow this help screen.");
                }
            }

            //Configuration.EnableObjectTracking = true;

            RenderForm form = new RenderForm("Swappy");
            
            form.ClientSize = new System.Drawing.Size(1920, 1080);
            form.MinimumSize = new System.Drawing.Size(800, 400);
            form.Icon = new System.Drawing.Icon("Media/swappy_icon.ico");

#if !DEBUG
            form.FormBorderStyle = FormBorderStyle.None;
            form.WindowState = FormWindowState.Maximized;
#endif
            form.Show();
            

            Device d3dDevice = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);

            SharpDX.DXGI.Device dxgiDevice = d3dDevice.QueryInterface<SharpDX.DXGI.Device>();

            SharpDX.DXGI.Factory dxgiFactory = dxgiDevice.Adapter.GetParent<SharpDX.DXGI.Factory>();

            SharpDX.Direct2D1.Factory1 d2dFactory = new SharpDX.Direct2D1.Factory1();
            SharpDX.Direct2D1.Device d2dDevice = new SharpDX.Direct2D1.Device(d2dFactory, dxgiDevice);
            deviceContext = new SharpDX.Direct2D1.DeviceContext(d2dDevice, DeviceContextOptions.None);
            deviceContext.AntialiasMode = AntialiasMode.PerPrimitive;

            SwapChainDescription desc = new SwapChainDescription()
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription(form.ClientSize.Width, form.ClientSize.Height, new Rational(60, 1), Format.B8G8R8A8_UNorm),
                IsWindowed = true,
                OutputHandle = form.Handle,
                SampleDescription = new SampleDescription(1, d3dDevice.CheckMultisampleQualityLevels(Format.B8G8R8A8_UNorm, 4) - 1),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput

            };
            desc.ModeDescription.Scaling = DisplayModeScaling.Stretched;

            SwapChain swapChain = new SwapChain(dxgiFactory, d3dDevice, desc);

            Surface dxgiBackBuffer = swapChain.GetBackBuffer<Surface>(0);

            Bitmap backBuffer = new Bitmap(deviceContext, dxgiBackBuffer, new BitmapProperties(new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied)));

            deviceContext.Target = backBuffer;

            //set up brushes
            SolidColorBrush colorBrush = new SolidColorBrush(deviceContext, Color.AliceBlue);

            //startup panel
            currentPanel = new ProfileSelector(deviceContext);

            form.Resize += (object sender, EventArgs a) => { Draw(); };

            form.Move += (object sender, EventArgs a) => { Draw(); };

            form.ResizeEnd += (object sender, EventArgs a) => { Resize(); };

            form.UserResized += (object sender, EventArgs a) => { Resize(); };

            form.MouseDown += new System.Windows.Forms.MouseEventHandler(OnMouseDown);

            form.MouseUp += new System.Windows.Forms.MouseEventHandler(OnMouseUp);

            form.KeyDown += new System.Windows.Forms.KeyEventHandler(OnKeyDown);

            form.KeyPress += new System.Windows.Forms.KeyPressEventHandler(OnKeyPress);

            form.KeyUp += new System.Windows.Forms.KeyEventHandler(OnKeyUp);

            form.MouseWheel += new System.Windows.Forms.MouseEventHandler(OnMouseWheel);



            RenderLoop.Run(form, () => { Update(); Draw(); });

            void Update()
            {
                currentPanel.Update();
            }

            void Draw()
            {
                deviceContext.Transform = Matrix3x2.Identity;

                //panel rendering
                currentPanel.Draw(deviceContext);

                swapChain.Present(0, PresentFlags.None);
            }

            void Resize()
            {

                deviceContext.Dispose();
                backBuffer.Dispose();
                dxgiBackBuffer.Dispose();

                swapChain.ResizeBuffers(1, 0, 0, Format.Unknown, SwapChainFlags.None);

                dxgiBackBuffer = swapChain.GetBackBuffer<Surface>(0);

                deviceContext = new SharpDX.Direct2D1.DeviceContext(d2dDevice, DeviceContextOptions.None);
                deviceContext.AntialiasMode = AntialiasMode.PerPrimitive;

                backBuffer = new Bitmap(deviceContext, dxgiBackBuffer, new BitmapProperties(new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied)));

                deviceContext.Target = backBuffer;

                currentPanel.Resize(deviceContext);
            };

            void OnMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
            {
                IMouseable mouseablePanel = currentPanel as IMouseable;
                if(mouseablePanel != null)
                {
                    mouseablePanel.OnMouseDown(e);
                }
                
            }

            void OnMouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
            {
                IMouseable mouseablePanel = currentPanel as IMouseable;
                if (mouseablePanel != null)
                {
                    mouseablePanel.OnMouseUp(e);
                }
            }

            void OnMouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
            {
                IMouseable mouseablePanel = currentPanel as IMouseable;
                if(mouseablePanel != null)
                {
                    mouseablePanel.OnMouseWheel(e);
                }
            }

            void OnKeyDown(object sender, System.Windows.Forms.KeyEventArgs e)
            {
                IKeyboardable keyboardablePanel = currentPanel as IKeyboardable;
                if(keyboardablePanel != null)
                {
                    keyboardablePanel.OnKeyDown(e);
                }
            }

            void OnKeyUp(object sender, System.Windows.Forms.KeyEventArgs e)
            {
                IKeyboardable keyboardablePanel = currentPanel as IKeyboardable;
                if (keyboardablePanel != null)
                {
                    keyboardablePanel.OnKeyUp(e);
                }
            }

            void OnKeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
            {
                IKeyboardable keyboardablePanel = currentPanel as IKeyboardable;
                if (keyboardablePanel != null)
                {
                    keyboardablePanel.OnKeyPress(e);
                }
            }

            backBuffer.Dispose();
            dxgiBackBuffer.Dispose();
            deviceContext.Dispose();
            d2dDevice.Dispose();
            d3dDevice.ImmediateContext.ClearState();
            d3dDevice.ImmediateContext.Flush();
            d3dDevice.Dispose();
            swapChain.Dispose();
            dxgiFactory.Dispose();
            dxgiDevice.Dispose();
        }

        public static void ChangePanel(Panel newPanel)
        {
            currentPanel = newPanel;
        }

        public static SharpDX.Direct2D1.DeviceContext GetDeviceContext()
        {
            return deviceContext;
        }
    }
}
