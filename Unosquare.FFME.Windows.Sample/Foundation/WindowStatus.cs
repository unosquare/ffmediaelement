namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using System.Windows;

    /// <summary>
    /// Represents the general state of a Window
    /// </summary>
    public sealed class WindowStatus
    {
        /// <summary>
        /// Gets or sets the state of the window.
        /// </summary>
        /// <value>
        /// The state of the window.
        /// </value>
        public WindowState WindowState { get; set; }

        /// <summary>
        /// Gets or sets the top.
        /// </summary>
        /// <value>
        /// The top.
        /// </value>
        public double Top { get; set; }

        /// <summary>
        /// Gets or sets the left.
        /// </summary>
        /// <value>
        /// The left.
        /// </value>
        public double Left { get; set; }

        /// <summary>
        /// Gets or sets the window style.
        /// </summary>
        /// <value>
        /// The window style.
        /// </value>
        public WindowStyle WindowStyle { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="WindowStatus"/> is topmost.
        /// </summary>
        /// <value>
        ///   <c>true</c> if topmost; otherwise, <c>false</c>.
        /// </value>
        public bool Topmost { get; set; }

        /// <summary>
        /// Gets or sets the resize mode.
        /// </summary>
        /// <value>
        /// The resize mode.
        /// </value>
        public ResizeMode ResizeMode { get; set; }

        /// <summary>
        /// Captures the specified window state.
        /// </summary>
        /// <param name="w">The w.</param>
        public void Capture(Window w)
        {
            WindowState = w.WindowState;
            Top = w.Top;
            Left = w.Left;
            WindowStyle = w.WindowStyle;
            Topmost = w.Topmost;
            ResizeMode = w.ResizeMode;
        }

        /// <summary>
        /// Applies the specified window state.
        /// </summary>
        /// <param name="w">The w.</param>
        public void Apply(Window w)
        {
            w.WindowState = WindowState;
            w.Top = Top;
            w.Left = Left;
            w.WindowStyle = WindowStyle;
            w.Topmost = Topmost;
            w.ResizeMode = ResizeMode;
        }
    }
}
