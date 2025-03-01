using SharpDX.Direct2D1;

namespace save_switcher
{
    internal interface IPanel
    {
        void Resize(DeviceContext deviceContext);

        void Draw(DeviceContext deviceContext);

        void Update();
    }
}
