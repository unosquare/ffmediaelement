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
            var result = TimeSpan.FromTicks((long)Math.Round(TimeSpan.TicksPerSecond * (double)value, 0));
            // Do the conversion from visibility to bool
            if (targetType == typeof(TimeSpan)) return result;
            if (targetType == typeof(Duration)) return new Duration(result);

            return Activator.CreateInstance(targetType);
        }
    }
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool booleanValue = (bool)value;
            return !booleanValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool booleanValue = (bool)value;
            return !booleanValue;
        }
    }
        public class TimeSpanFormatter : IValueConverter
    {
        public object Convert(object position, Type targetType, object duration, CultureInfo culture)
        {
            if (duration != null)
                duration = (App.Current.MainWindow as MainWindow)?.Media?.NaturalDuration;

            var p = TimeSpan.Zero;
            var d = TimeSpan.Zero;

            if (position is TimeSpan) p = (TimeSpan)position;
            if (position is Duration) p = ((Duration)position).HasTimeSpan ? ((Duration)position).TimeSpan : TimeSpan.Zero;

            if (duration != null)
            {
                if (duration is TimeSpan) d = (TimeSpan)duration;
                if (duration is Duration) d = ((Duration)duration).HasTimeSpan ? ((Duration)duration).TimeSpan : TimeSpan.Zero;

                if (d == TimeSpan.Zero) return string.Empty;
                p = TimeSpan.FromTicks(d.Ticks - p.Ticks);
                
            }

            return $"{(int)(p.TotalHours):00}:{p.Minutes:00}:{p.Seconds:00}.{p.Milliseconds:000}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotImplementedException(); }
    }

    public class PercentageFormatter : IValueConverter
    {
        public object Convert(object value, Type targetType, object format, CultureInfo culture)
        {
            var percentage = 0d;
            if (value is double) percentage = (double)value;

            percentage = Math.Round(percentage * 100d, 0);

            if (format == null || percentage == 0d)
                return $"{percentage,3:0}%";

            else
                return $"{((percentage > 0d) ? "R " : "L ")} {Math.Abs(percentage),3:0}%";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotImplementedException(); }
    }
}
