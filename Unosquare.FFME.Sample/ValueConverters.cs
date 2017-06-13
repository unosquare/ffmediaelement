using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace Unosquare.FFME.Sample
{
    public class TimeSpanToSecondsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            if (value is TimeSpan) return ((TimeSpan)value).TotalSeconds;
            if (value is Duration) return ((Duration)value).HasTimeSpan ? ((Duration)value).TimeSpan.TotalSeconds : 0d;

            return 0d;
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            var result = TimeSpan.FromTicks((long)(TimeSpan.TicksPerSecond * (double)value));
            // Do the conversion from visibility to bool
            if (targetType == typeof(TimeSpan)) return result;
            if (targetType == typeof(Duration)) return new Duration(result);

            return Activator.CreateInstance(targetType);
        }
    }
}
