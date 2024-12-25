using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using SharpDX.Direct2D1;

namespace save_switcher.Panels
{
    internal class Settings : Panel
    {
        private DeviceContext deviceContext;
        public Settings(DeviceContext deviceContext)
        {
            this.deviceContext = deviceContext;

            createSizeDependantResources();
        }

        private void createSizeDependantResources()
        {

        }

        public void Resize(DeviceContext deviceContext)
        {
            this.deviceContext = deviceContext;
            createSizeDependantResources();
        }

        public void Update()
        {

        }

        public void Draw(DeviceContext deviceContext)
        {

        }
    }
}
