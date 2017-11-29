namespace Unosquare.FFME.Core
{
    using System.Windows.Threading;

    /// <summary>
    /// WPF dispatcher that satisfies common code requirements.
    /// </summary>
    internal class CustomDispatcherTimer : DispatcherTimer, IDispatcherTimer
    {
        public CustomDispatcherTimer(DispatcherPriority priority)
            : base(priority)
        {
        }
    }
}
