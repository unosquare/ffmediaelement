namespace Unosquare.FFME
{
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Effects;

    /// <summary>
    /// Defines attached properties for subtitle rendering.
    /// </summary>
    public sealed class Subtitles
    {
        /// <summary>
        /// The foreground text property.
        /// </summary>
        public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
            "Text", typeof(string), typeof(Subtitles));

        /// <summary>
        /// The foreground text property.
        /// </summary>
        public static readonly DependencyProperty ForegroundProperty = DependencyProperty.RegisterAttached(
            "Foreground", typeof(Brush), typeof(Subtitles));

        /// <summary>
        /// The text foreground effect dependency property.
        /// </summary>
        public static readonly DependencyProperty EffectProperty = DependencyProperty.RegisterAttached(
            "Effect", typeof(Effect), typeof(Subtitles));

        /// <summary>
        /// The text outline width dependency property.
        /// </summary>
        public static readonly DependencyProperty OutlineWidthProperty = DependencyProperty.RegisterAttached(
            "OutlineWidth", typeof(Thickness), typeof(Subtitles));

        /// <summary>
        /// The text outline brush dependency property.
        /// </summary>
        public static readonly DependencyProperty OutlineBrushProperty = DependencyProperty.RegisterAttached(
            "OutlineBrush", typeof(Brush), typeof(Subtitles));

        /// <summary>
        /// The font size property.
        /// </summary>
        public static readonly DependencyProperty FontSizeProperty = DependencyProperty.RegisterAttached(
            "FontSize", typeof(double), typeof(Subtitles));

        /// <summary>
        /// The font weight property.
        /// </summary>
        public static readonly DependencyProperty FontWeightProperty = DependencyProperty.RegisterAttached(
            "FontWeight", typeof(FontWeight), typeof(Subtitles));

        /// <summary>
        /// The font family property.
        /// </summary>
        public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.RegisterAttached(
            "FontFamily", typeof(FontFamily), typeof(Subtitles));

        /// <summary>
        /// Prevents a default instance of the <see cref="Subtitles"/> class from being created.
        /// </summary>
        private Subtitles()
        {
            // placeholder
        }

        /// <summary>
        /// Gets the text.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <returns>The value.</returns>
        public static string GetText(MediaElement element) => element?.GetValue(TextProperty) as string;

        /// <summary>
        /// Gets the size of the font.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <returns>The value.</returns>
        public static double GetFontSize(MediaElement element) => (double)(element?.GetValue(FontSizeProperty) ?? default(double));

        /// <summary>
        /// Gets the font weight.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <returns>The value.</returns>
        public static FontWeight GetFontWeight(MediaElement element) => (FontWeight)(element?.GetValue(FontWeightProperty) ?? FontWeights.Normal);

        /// <summary>
        /// Gets the font family.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <returns>The value.</returns>
        public static FontFamily GetFontFamily(MediaElement element) => element?.GetValue(FontFamilyProperty) as FontFamily;

        /// <summary>
        /// Gets the text foreground.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <returns>The value.</returns>
        public static Brush GetForeground(MediaElement element) => element?.GetValue(ForegroundProperty) as Brush;

        /// <summary>
        /// Gets the effect.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <returns>The value.</returns>
        public static Effect GetEffect(MediaElement element) => element?.GetValue(EffectProperty) as Effect;

        /// <summary>
        /// Gets the width of the outline.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <returns>The value.</returns>
        public static Thickness GetOutlineWidth(MediaElement element) => (Thickness)(element?.GetValue(OutlineWidthProperty) ?? default(Thickness));

        /// <summary>
        /// Gets the outline brush.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <returns>The value.</returns>
        public static Brush GetOutlineBrush(MediaElement element) => element?.GetValue(OutlineBrushProperty) as Brush;

        /// <summary>
        /// Sets the text.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetText(MediaElement element, string value) => element?.SetValue(TextProperty, value);

        /// <summary>
        /// Sets the size of the font.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetFontSize(MediaElement element, double value) => element?.SetValue(FontSizeProperty, value);

        /// <summary>
        /// Sets the font weight.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetFontWeight(MediaElement element, FontWeight value) => element?.SetValue(FontWeightProperty, value);

        /// <summary>
        /// Sets the font family.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetFontFamily(MediaElement element, FontFamily value) => element?.SetValue(FontFamilyProperty, value);

        /// <summary>
        /// Sets the text foreground.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetForeground(MediaElement element, Brush value) => element?.SetValue(ForegroundProperty, value);

        /// <summary>
        /// Sets the effect.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetEffect(MediaElement element, Effect value) => element?.SetValue(EffectProperty, value);

        /// <summary>
        /// Sets the width of the outline.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetOutlineWidth(MediaElement element, Thickness value) => element?.SetValue(OutlineWidthProperty, value);

        /// <summary>
        /// Sets the outline brush.
        /// </summary>
        /// <param name="element">The object.</param>
        /// <param name="value">The value.</param>
        public static void SetOutlineBrush(MediaElement element, Brush value) => element?.SetValue(OutlineBrushProperty, value);
    }
}
