using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Ink;

namespace save_switcher.Elements
{

    public enum NavigateDirection
    {
        Up = 0x00,
        Down = 0x11,
        Left = 0x10,
        Right = 0x01,
    };

    abstract class InputNavigable : IDisposable
    {

        protected static InputNavigable CurrentSelectedObject;

        private Dictionary<NavigateDirection, InputNavigable> neighbors;

        public InputNavigable()
        {
            neighbors = new Dictionary<NavigateDirection, InputNavigable>();
        }

        public virtual void AddNeighbor(NavigateDirection direction, InputNavigable neighbor)
        {
            neighbors.Add(direction, neighbor);
        }

        public virtual bool SelectNeighbor(NavigateDirection direction)
        {
            InputNavigable neighbor;
            if (neighbors.TryGetValue(direction, out neighbor))
            {
                if (!Equals(neighbor, null))
                {
                    neighbor.Select();
                    return true;
                }
                else return false;
            }
            else return false;
        }

        public virtual void Select() 
        {
            CurrentSelectedObject?.Deselect();
            CurrentSelectedObject = this;
        }

        public abstract void Deselect();

        public void Dispose()
        {
            if (CurrentSelectedObject == this)
            {
                CurrentSelectedObject = null;
            }
        }

        public static void ConnectNeighbors(InputNavigable a, InputNavigable b, NavigateDirection directionAtoB)
        {
            a.AddNeighbor(directionAtoB, b);
            b.AddNeighbor((NavigateDirection)((byte)directionAtoB ^ 0x11), a);
        }
    }
}
