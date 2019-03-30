namespace Unosquare.FFME
{
    using System;

    internal static partial class Constants
    {
        /// <summary>
        /// Gets the period at which media state properties are updated.
        /// </summary>
        public static TimeSpan PropertyUpdatesInterval { get; } = TimeSpan.FromMilliseconds(30);
    }
}
