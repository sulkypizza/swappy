using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
