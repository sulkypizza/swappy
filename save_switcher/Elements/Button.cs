using SharpDX;
using SharpDX.Direct2D1;
using System;
using System.Diagnostics;

namespace save_switcher.Elements
{
    public delegate void ButtonEvent();

    internal abstract class Button : InputNavigable
    {
        static protected bool inputHandled;

        public Size2 Size;
        public Vector2 Position;

        public ButtonEvent OnHover;
        public ButtonEvent OnPressed;
        public ButtonEvent OnHeld;

        private bool cursorWithinBounds = false;

        protected DeviceContext deviceContext;

        static Button()
        {
            InputManager.OnUpInput += (t) => { inputHandled = false; };
            InputManager.OnDownInput += (t) => { inputHandled = false; };
            InputManager.OnLeftInput += (t) => { inputHandled = false; };
            InputManager.OnRightInput += (t) => { inputHandled = false; };
            InputManager.OnEnterInput += (t) => { inputHandled = false; };
        }

        public Button(DeviceContext deviceContext, Vector2 position, Size2 size)
        {
            this.deviceContext = deviceContext;
            this.Position = position;
            this.Size = size;

            InputManager.OnMousePosChanged += inputMousePosChanged;
            InputManager.OnLeftMouseInput += inputMouseLeft;

            InputManager.OnLeftInput += (t) => { inputDirection(t, NavigateDirection.Left); };
            InputManager.OnRightInput += (t) => { inputDirection(t, NavigateDirection.Right); };
            InputManager.OnUpInput += (t) => { inputDirection(t, NavigateDirection.Up); };
            InputManager.OnDownInput += (t) => { inputDirection(t, NavigateDirection.Down); };

            InputManager.OnEnterInput += (t) =>
            {
                if (CurrentSelectedObject == this)
                {
                    if (t == InputManager.ButtonTravel.Down)
                        OnPressed?.Invoke();
                }
            };
        }

        private void inputDirection(InputManager.ButtonTravel travel, NavigateDirection direction)
        {
            if (CurrentSelectedObject == this && travel == InputManager.ButtonTravel.Down 
                && inputHandled == false)
            {
                SelectNeighbor(direction);
                inputHandled = true;
            }
        }

        private void inputMouseLeft(InputManager.ButtonTravel travel)
        {
            if (CurrentSelectedObject == this)
            {
                if (travel == InputManager.ButtonTravel.Down && cursorWithinBounds)
                {
                    OnPressed.Invoke();
                }
                else if (travel == InputManager.ButtonTravel.Up)
                {
                    OnHover.Invoke();
                }
            }
        }

        private void inputMousePosChanged(System.Drawing.Point p)
        {
            Vector2 mousePos = new Vector2(p.X, p.Y);

            if (InputManager.CurrentInputType == InputManager.InputType.Mouse && mousePos.X >= Position.X &&
                mousePos.X <= Position.X + Size.Width && mousePos.Y >= Position.Y &&
                mousePos.Y <= Position.Y + Size.Height)
            {
                if (CurrentSelectedObject != this)
                {
                    Select();
                }

                cursorWithinBounds = true;
            }
            else
            {
                cursorWithinBounds = false;
            }
        }

        public override void Select()
        {
            base.Select();

            OnHover?.Invoke();
        }

        public abstract void Update();

        public abstract void Draw();

        public override void Dispose()
        {
            InputManager.RemoveEventsFromObject(this);

            OnHover = null;
            OnPressed = null;
            OnHeld = null;

            base.Dispose();
        }

        public virtual void Resize(DeviceContext deviceContext)
        {
            this.deviceContext = deviceContext;
        }
    }
}
