namespace Unosquare.FFME
{
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Effects;

    /// <summary>
    /// Defines attached properties for subtitle rendering
    /// </summary>
    public sealed class Subtitles
    {
        /// <summary>
        /// The foreground text property
        /// </summary>
        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
            "Text", typeof(string), typeof(Subtitles));

        /// <summary>
        /// The foreground text property
        /// </summary>
        public static readonly DependencyProperty ForegroundProperty = DependencyProperty.RegisterAttached(
            "Foreground", typeof(Brush), typeof(Subtitles));

        /// <summary>
        /// The text foreground effect dependency property
        /// </summary>
        public static readonly DependencyProperty EffectProperty = DependencyProperty.RegisterAttached(
            "Effect", typeof(Effect), typeof(Subtitles));

        /// <summary>
        /// The text outline width dependency property
        /// </summary>
        public static readonly DependencyProperty OutlineWidthProperty = DependencyProperty.RegisterAttached(
            "OutlineWidth", typeof(Thickness), typeof(Subtitles));

        /// <summary>
        /// The text outline brush dependency property
        /// </summary>
        public static readonly DependencyProperty OutlineBrushProperty = DependencyProperty.RegisterAttached(
            "OutlineBrush", typeof(Brush), typeof(Subtitles));

        /// <summary>
        /// The font size property
        /// </summary>
        public static readonly DependencyProperty FontSizeProperty = DependencyProperty.RegisterAttached(
            "FontSize", typeof(double), typeof(Subtitles));

        /// <summary>
        /// The font weight property
        /// </summary>
        public static readonly DependencyProperty FontWeightProperty = DependencyProperty.RegisterAttached(
            "FontWeight", typeof(FontWeight), typeof(Subtitles));

        /// <summary>
        /// The font family property
        /// </summary>
        public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.RegisterAttached(
            "FontFamily", typeof(FontFamily), typeof(Subtitles));

        /// <summary>
        /// Gets the text.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>The value</returns>
        public static string GetText(MediaElement obj) { return obj.GetValue(TextProperty) as string; }

        /// <summary>
        /// Gets the size of the font.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>The value</returns>
        public static double GetFontSize(MediaElement obj) { return (double)obj.GetValue(FontSizeProperty); }

        /// <summary>
        /// Gets the font weight.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>The value</returns>
        public static FontWeight GetFontWeight(MediaElement obj) { return (FontWeight)obj.GetValue(FontWeightProperty); }

        /// <summary>
        /// Gets the font family.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>The value</returns>
        public static FontFamily GetFontFamily(MediaElement obj) { return obj.GetValue(FontFamilyProperty) as FontFamily; }

        /// <summary>
        /// Gets the text foreground.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>The value</returns>
        public static Brush GetForeground(MediaElement obj) { return obj.GetValue(ForegroundProperty) as Brush; }

        /// <summary>
        /// Gets the effect.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>The value</returns>
        public static Effect GetEffect(MediaElement obj) { return obj.GetValue(EffectProperty) as Effect; }

        /// <summary>
        /// Gets the width of the outline.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>The value.</returns>
        public static Thickness GetOutlineWidth(MediaElement obj) { return (Thickness)obj.GetValue(OutlineWidthProperty); }

        /// <summary>
        /// Gets the outline brush.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <returns>The value.</returns>
        public static Brush GetOutlineBrush(MediaElement obj) { return obj.GetValue(OutlineBrushProperty) as Brush; }

        /// <summary>
        /// Sets the text.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetText(MediaElement obj, string value) { obj.SetValue(TextProperty, value); }

        /// <summary>
        /// Sets the size of the font.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetFontSize(MediaElement obj, double value) { obj.SetValue(FontSizeProperty, value); }

        /// <summary>
        /// Sets the font weight.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetFontWeight(MediaElement obj, FontWeight value) { obj.SetValue(FontWeightProperty, value); }

        /// <summary>
        /// Sets the font family.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetFontFamily(MediaElement obj, FontFamily value) { obj.SetValue(FontFamilyProperty, value); }

        /// <summary>
        /// Sets the text foreground.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetForeground(MediaElement obj, Brush value) { obj.SetValue(ForegroundProperty, value); }

        /// <summary>
        /// Sets the effect.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetEffect(MediaElement obj, Effect value) { obj.SetValue(EffectProperty, value); }

        /// <summary>
        /// Sets the width of the outline.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetOutlineWidth(MediaElement obj, Thickness value) { obj.SetValue(OutlineWidthProperty, value); }

        /// <summary>
        /// Sets the outline brush.
        /// </summary>
        /// <param name="obj">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetOutlineBrush(MediaElement obj, Brush value) { obj.SetValue(OutlineBrushProperty, value); }
    }
}
