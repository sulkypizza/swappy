using SharpDX.Direct2D1;

namespace save_switcher.Panels.Subpanels
{
    public delegate void SubpanelExitEvent<TExit>(TExit o);

    public delegate void SubpanelUpdateEvent<TUpdate>(TUpdate o);

    internal interface Subpanel<TUpdate, TExit>
    {
        event SubpanelExitEvent<TExit> OnExit;

        event SubpanelUpdateEvent<TUpdate> OnUpdate;

        void Activate();

        void Deactivate();

        void Draw();

        void Update();

        void Resize(DeviceContext deviceContext);
    }
}
