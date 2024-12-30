using SharpDX;
using SharpDX.Direct2D1;

namespace save_switcher.Elements
{
    public delegate void HoverEvent();
    public delegate void PressedEvent();
    public delegate void HeldEvent();

    internal abstract class Button : InputNavigable
    {
        public Size2 Size;
        public Vector2 Position;

        public HoverEvent OnHover;
        public PressedEvent OnPressed;
        public HeldEvent OnHeld;

        protected DeviceContext deviceContext;

        public Button(DeviceContext deviceContext, Vector2 position, Size2 size)
        {
            this.deviceContext = deviceContext;
            this.Position = position;
            this.Size = size;
        }

        public override void Select()
        {
            base.Select();

            OnHover?.Invoke();
        }

        public abstract void Update();

        public abstract void Draw();

        public virtual void Resize(DeviceContext deviceContext)
        {
            this.deviceContext = deviceContext;
        }
    }
}
