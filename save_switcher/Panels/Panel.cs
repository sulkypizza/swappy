using SharpDX.Direct2D1;

namespace save_switcher
{
    internal abstract class Panel
    {
        public virtual void Resize(DeviceContext deviceContext) { }

        public virtual void Draw(DeviceContext deviceContext) { }

        public virtual void Update() { }

        public virtual void Initialize(DeviceContext deviceContext, params object[] args) { }
    }
}
