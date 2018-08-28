#pragma warning disable SA1649 // File name must match first type name
namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using ClosedCaptions;
    using System;
    using System.Globalization;
    using System.Windows;
    using System.Windows.Data;
    using System.Windows.Media;

    /// <inheritdoc />
    internal class TimeSpanToSecondsConverter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case TimeSpan span:
                    return span.TotalSeconds;
                case Duration duration:
                    return duration.HasTimeSpan ? duration.TimeSpan.TotalSeconds : 0d;
                default:
                    return 0d;
            }
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double == false) return 0d;
            var result = TimeSpan.FromTicks(System.Convert.ToInt64(TimeSpan.TicksPerSecond * (double)value));

            // Do the conversion from visibility to bool
            if (targetType == typeof(TimeSpan)) return result;
            if (targetType == typeof(Duration)) return new Duration(result);

            return Activator.CreateInstance(targetType);
        }
    }

    /// <inheritdoc />
    internal class TimeSpanFormatter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            TimeSpan? p = default;

            switch (value)
            {
                case TimeSpan position:
                    p = position;
                    break;
                case Duration duration:
                    p = duration.HasTimeSpan ? duration.TimeSpan : default;
                    break;
            }

            return p.HasValue ?
                $"{(int)p.Value.TotalHours:00}:{p.Value.Minutes:00}:{p.Value.Seconds:00}.{p.Value.Milliseconds:000}" :
                string.Empty;
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <inheritdoc />
    internal class ByteFormatter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object format, CultureInfo culture)
        {
            const double minKiloByte = 1024;
            const double minMegaByte = 1024 * 1024;
            const double minGigaByte = 1024 * 1024 * 1024;

            var byteCount = System.Convert.ToDouble(value);

            var suffix = "b";
            var output = 0d;

            if (byteCount >= minKiloByte)
            {
                suffix = "kB";
                output = Math.Round(byteCount / minKiloByte, 2);
            }

            if (byteCount >= minMegaByte)
            {
                suffix = "MB";
                output = Math.Round(byteCount / minMegaByte, 2);
            }

            if (byteCount >= minGigaByte)
            {
                suffix = "GB";
                output = Math.Round(byteCount / minGigaByte, 2);
            }

            return suffix == "b" ?
                $"{output:0} {suffix}" :
                $"{output:0.00} {suffix}";
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <inheritdoc />
    internal class BitFormatter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object format, CultureInfo culture)
        {
            const double minKiloBit = 1000;
            const double minMegaBit = 1000 * 1000;
            const double minGigaBit = 1000 * 1000 * 1000;

            var byteCount = System.Convert.ToDouble(value);

            var suffix = "bits/s";
            var output = 0d;

            if (byteCount >= minKiloBit)
            {
                suffix = "kbits/s";
                output = Math.Round(byteCount / minKiloBit, 2);
            }

            if (byteCount >= minMegaBit)
            {
                suffix = "Mbits/s";
                output = Math.Round(byteCount / minMegaBit, 2);
            }

            if (byteCount >= minGigaBit)
            {
                suffix = "Gbits/s";
                output = Math.Round(byteCount / minGigaBit, 2);
            }

            return suffix == "b" ?
                $"{output:0} {suffix}" :
                $"{output:0.00} {suffix}";
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <inheritdoc />
    internal class PercentageFormatter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object format, CultureInfo culture)
        {
            var percentage = 0d;
            if (value is double d) percentage = d;

            percentage = Math.Round(percentage * 100d, 0);

            if (format == null || Math.Abs(percentage) <= double.Epsilon)
                return $"{percentage,3:0} %".Trim();

            return $"{((percentage > 0d) ? "R " : "L ")} {Math.Abs(percentage),3:0} %".Trim();
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <inheritdoc />
    internal class PlaylistEntryThumbnailConverter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object format, CultureInfo culture)
        {
            var thumbnailFilename = value as string;
            if (thumbnailFilename == null) return default(ImageSource);
            if (Platform.GuiContext.Current.IsInDesignTime) return default(ImageSource);

            return ThumbnailGenerator.GetThumbnail(App.Current.ViewModel.Playlist.ThumbsDirectory, thumbnailFilename);
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }

    /// <inheritdoc />
    internal class PlaylistDurationFormatter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var duration = value is TimeSpan span ? span : TimeSpan.FromSeconds(-1);

            if (duration.TotalSeconds <= 0)
                return "∞";

            return duration.TotalMinutes >= 100 ?
                $"{System.Convert.ToInt64(duration.TotalHours)}h {System.Convert.ToInt64(duration.Minutes)}m" :
                $"{System.Convert.ToInt64(duration.Minutes):00}:{System.Convert.ToInt64(duration.Seconds):00}";
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <inheritdoc />
    internal class UtcDateToLocalTimeString : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return "unknown";
            var utcDate = (DateTime)value;
            return utcDate.ToLocalTime().ToString("f");
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <inheritdoc />
    [ValueConversion(typeof(bool), typeof(bool))]
    internal class InverseBooleanConverter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (targetType != typeof(bool))
                throw new InvalidOperationException("The target must be a boolean");

            return value != null && !(bool)value;
        }

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    /// <inheritdoc />
    [ValueConversion(typeof(bool), typeof(bool))]
    internal class ClosedCaptionsChannelConverter : IValueConverter
    {
        /// <inheritdoc />
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value != null && (CaptionsChannel)value != CaptionsChannel.CCP;

        /// <inheritdoc />
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value != null && (bool)value ? CaptionsChannel.CC1 : CaptionsChannel.CCP;
    }
}
#pragma warning restore SA1649 // File name must match first type name