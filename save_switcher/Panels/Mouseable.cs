using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace save_switcher.Panels
{
    internal interface Mouseable
    {
        void OnMouseDown(MouseEventArgs e);

        void OnMouseUp(MouseEventArgs e);

        void OnMouseWheel(MouseEventArgs e);
    }
}
