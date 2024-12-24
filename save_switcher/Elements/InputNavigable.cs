﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace save_switcher.Elements
{

    public enum NavigateDirection
    {
        Up,
        Down,
        Left,
        Right
    };

    abstract class InputNavigable
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
    }
}