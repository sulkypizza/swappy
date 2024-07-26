using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace save_switcher.Panels
{
    internal interface Keyboardable
    {
        void OnKeyUp(System.Windows.Forms.KeyEventArgs e);

        void OnKeyDown(System.Windows.Forms.KeyEventArgs e);

        void OnKeyPress(System.Windows.Forms.KeyPressEventArgs e);
    }
}
