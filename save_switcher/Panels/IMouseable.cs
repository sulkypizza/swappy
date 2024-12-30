using System.Windows.Forms;

namespace save_switcher.Panels
{
    internal interface IMouseable
    {
        void OnMouseDown(MouseEventArgs e);

        void OnMouseUp(MouseEventArgs e);

        void OnMouseWheel(MouseEventArgs e);
    }
}
