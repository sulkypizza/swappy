using SharpDX;
using SharpDX.Direct2D1;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace save_switcher.Elements
{
    public struct SimpleButtonProperties
    {
        public Vector2 Position;
        public Size2 Size;
        public string Text;
        public float BorderThickness;
        public Color ButtonColor;
        public Color ButtonHoverColor;
        public Color ButtonPressedColor;
        public Color BorderColor;
        public Color BorderHoverColor;
        public Color BorderPressedColor;
    }

    internal class SimpleButton : Button
    {
        public SimpleButton(DeviceContext deviceContext, SimpleButtonProperties properties) 
            : base (deviceContext, properties.Position, properties.Size)
        {

        }

        public override void Update()
        {
            
        }

        public override void Deselect()
        {
            
        }

        public override void Draw()
        {

        }
    }
}
