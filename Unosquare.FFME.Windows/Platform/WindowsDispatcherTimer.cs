namespace Unosquare.FFME.Platform
{
    using Shared;
    using System.Windows.Threading;

    /// <summary>
    /// WPF dispatcher that satisfies common code requirements.
    /// </summary>
    internal class WindowsDispatcherTimer : DispatcherTimer, IDispatcherTimer
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsDispatcherTimer"/> class.
        /// </summary>
        /// <param name="priority">The priority at which to invoke the timer.</param>
        public WindowsDispatcherTimer(DispatcherPriority priority)
            : base(priority)
        {
        }
    }
}
