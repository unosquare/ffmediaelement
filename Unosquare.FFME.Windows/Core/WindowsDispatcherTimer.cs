namespace Unosquare.FFME.Core
{
    using Shared;
    using System.Windows.Threading;

    /// <summary>
    /// WPF dispatcher that satisfies common code requirements.
    /// </summary>
    internal class WindowsDispatcherTimer : DispatcherTimer, IDispatcherTimer
    {
        public WindowsDispatcherTimer(DispatcherPriority priority)
            : base(priority)
        {
        }
    }
}
