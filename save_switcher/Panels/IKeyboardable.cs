namespace save_switcher.Panels
{
    internal interface IKeyboardable
    {
        void OnKeyUp(System.Windows.Forms.KeyEventArgs e);

        void OnKeyDown(System.Windows.Forms.KeyEventArgs e);

        void OnKeyPress(System.Windows.Forms.KeyPressEventArgs e);
    }
}
