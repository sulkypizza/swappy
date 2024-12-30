using SharpDX.Direct2D1;

namespace save_switcher
{
    internal interface Panel
    {
        void Resize(DeviceContext deviceContext);

        void Draw(DeviceContext deviceContext);

        void Update();
    }
}
