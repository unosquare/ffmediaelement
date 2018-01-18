namespace Unosquare.FFME.Rendering
{
    using Platform;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Media.Effects;

    /// <summary>
    /// A control suitable for displaying subtitles.
    /// Layout is: UserControl:Viewbox:Grid:TextBlocks
    /// </summary>
    /// <seealso cref="UserControl" />
    public class SubtitleTextBlock : UserControl
    {
        #region Dependency Property Registrations

        /// <summary>
        /// The text dependency property
        /// </summary>
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(SubtitleTextBlock),
            new FrameworkPropertyMetadata(string.Empty, AffectsMeasureAndRender, OnTextPropertyChanged));

        /// <summary>
        /// The text foreground dependency property
        /// </summary>
        public static readonly DependencyProperty TextForegroundProperty = DependencyProperty.Register(
            nameof(TextForeground),
            typeof(Brush),
            typeof(SubtitleTextBlock),
            new FrameworkPropertyMetadata(DefaultTextForegound, AffectsMeasureAndRender, OnTextForegroundPropertyChanged));

        /// <summary>
        /// The text foreground effect dependency property
        /// </summary>
        public static readonly DependencyProperty TextForegroundEffectProperty = DependencyProperty.Register(
            nameof(TextForegroundEffect),
            typeof(Effect),
            typeof(SubtitleTextBlock),
            new FrameworkPropertyMetadata(DefaultTextForegroundEffect, AffectsMeasureAndRender, OnTextForegroundEffectPropertyChanged));

        /// <summary>
        /// The text outline width dependency property
        /// </summary>
        public static readonly DependencyProperty TextOutlineWidthProperty = DependencyProperty.Register(
            nameof(TextOutlineWidth),
            typeof(Thickness),
            typeof(SubtitleTextBlock),
            new FrameworkPropertyMetadata(DefaultTextOutlineWidth, AffectsMeasureAndRender, OnTextOutlineWidthPropertyChanged));

        /// <summary>
        /// The text outline dependency property
        /// </summary>
        public static readonly DependencyProperty TextOutlineProperty = DependencyProperty.Register(
            nameof(TextOutline),
            typeof(Brush),
            typeof(SubtitleTextBlock),
            new FrameworkPropertyMetadata(DefaultTextOutline, AffectsMeasureAndRender, OnTextOutlinePropertyChanged));

        #endregion

        #region Private State Backing

        private const double DefaultFontSize = 48;
        private const FrameworkPropertyMetadataOptions AffectsMeasureAndRender
            = FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender;
        private static Brush DefaultTextForegound = Brushes.WhiteSmoke;
        private static Brush DefaultTextOutline = Brushes.Black;
        private static Thickness DefaultTextOutlineWidth = new Thickness(1);
        private static Effect DefaultTextForegroundEffect = new DropShadowEffect
        {
            BlurRadius = 4,
            Color = Colors.Black,
            Direction = 315,
            Opacity = 0.75,
            RenderingBias = RenderingBias.Performance,
            ShadowDepth = 4
        };

        /// <summary>
        /// Holds the text blocks that together create an outlined subtitle text display.
        /// </summary>
        private readonly Dictionary<Block, TextBlock> TextBlocks = new Dictionary<Block, TextBlock>(5);

        /// <summary>
        /// The container for the outlined text blocks
        /// </summary>
        private readonly Viewbox Container = new Viewbox();

        /// <summary>
        /// A Layout transform to condense text.
        /// </summary>
        private readonly ScaleTransform CondenseTransform = new ScaleTransform { ScaleX = 0.82 };

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="SubtitleTextBlock"/> class.
        /// </summary>
        public SubtitleTextBlock()
            : base()
        {
            var layoutElement = new Grid();
            Container.Child = layoutElement;

            for (var i = (int)Block.Bottom; i >= (int)Block.Foreground; i--)
            {
                var textBlock = new TextBlock()
                {
                    Name = $"{nameof(SubtitleTextBlock)}_{(Block)i}",
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Center,
                    // LayoutTransform = CondenseTransform
                };

                TextBlocks[(Block)i] = textBlock;

                var blockType = (Block)i;
                if (blockType == Block.Foreground)
                {
                    textBlock.Effect = DefaultTextForegroundEffect;
                    textBlock.Foreground = DefaultTextForegound;
                    textBlock.Margin = new Thickness(0);
                }
                else
                {
                    textBlock.Foreground = DefaultTextOutline;
                    textBlock.Margin = ComputeMargin(blockType, DefaultTextOutlineWidth);
                }

                layoutElement.Children.Add(textBlock);
            }

            // Add the container as the content of the control.
            Container.Stretch = Stretch.Uniform;
            Container.StretchDirection = StretchDirection.DownOnly;
            Content = Container;
            Height = 0;

            // set font defaults
            FontSize = DefaultFontSize;
            FontWeight = FontWeights.DemiBold;
            VerticalAlignment = VerticalAlignment.Bottom;

            if (WindowsPlatform.Instance.IsInDesignTime)
            {
                Text = "Subtitle TextBlock\r\n(Design-Time Preview)";
            }
        }

        private enum Block
        {
            Foreground = 0,
            Left = 1,
            Right = 2,
            Top = 3,
            Bottom = 4
        }

        #region Dependency Property CLR Accessors

        /// <summary>
        /// Gets or sets the text contents of this text block.
        /// </summary>
        [Category(nameof(SubtitleTextBlock))]
        [Description("Gets or sets the text contents of this text block.")]
        public string Text
        {
            get { return GetValue(TextProperty) as string; }
            set { SetValue(TextProperty, value); }
        }

        /// <summary>
        /// Gets or sets the text foreground.
        /// </summary>
        [Category(nameof(SubtitleTextBlock))]
        [Description("Gets or sets the text contents of this text block.")]
        public Brush TextForeground
        {
            get { return GetValue(TextForegroundProperty) as Brush; }
            set { SetValue(TextForegroundProperty, value); }
        }

        /// <summary>
        /// Gets or sets the text outline.
        /// </summary>
        [Category(nameof(SubtitleTextBlock))]
        [Description("Gets or sets the text outline brush.")]
        public Brush TextOutline
        {
            get { return GetValue(TextOutlineProperty) as Brush; }
            set { SetValue(TextOutlineProperty, value); }
        }

        /// <summary>
        /// Gets or sets the text outline width.
        /// </summary>
        [Category(nameof(SubtitleTextBlock))]
        [Description("Gets or sets the text outline width.")]
        public Thickness TextOutlineWidth
        {
            get { return (Thickness)GetValue(TextOutlineWidthProperty); }
            set { SetValue(TextOutlineWidthProperty, value); }
        }

        /// <summary>
        /// Gets or sets the text foreground effect.
        /// </summary>
        [Category(nameof(SubtitleTextBlock))]
        [Description("Gets or sets the text foreground effect. It's a smooth drop shadow by default.")]
        public Effect TextForegroundEffect
        {
            get { return GetValue(TextForegroundEffectProperty) as Effect; }
            set { SetValue(TextForegroundEffectProperty, value); }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Invoked whenever the effective value of any dependency property on this <see cref="T:System.Windows.FrameworkElement" /> has been updated. The specific dependency property that changed is reported in the arguments parameter. Overrides <see cref="M:System.Windows.DependencyObject.OnPropertyChanged(System.Windows.DependencyPropertyChangedEventArgs)" />.
        /// </summary>
        /// <param name="e">The event data that describes the property that changed, as well as old and new values.</param>
        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            if (e.Property.Name == nameof(FontSize))
            {
                var value = (double)e.NewValue;
                foreach (var t in TextBlocks)
                    t.Value.FontSize = value;
            }
            else if (e.Property.Name == nameof(FontStretch))
            {
                var value = (FontStretch)e.NewValue;
                foreach (var t in TextBlocks)
                    t.Value.FontStretch = value;
            }
            else if (e.Property.Name == nameof(FontWeight))
            {
                var value = (FontWeight)e.NewValue;
                foreach (var t in TextBlocks)
                    t.Value.FontWeight = value;
            }

            base.OnPropertyChanged(e);
        }

        /// <summary>
        /// Computes the margin according to the block type.
        /// </summary>
        /// <param name="blockType">Type of the block.</param>
        /// <param name="outlineWidth">Width of the outline.</param>
        /// <returns>A thickness depending on the block type</returns>
        private static Thickness ComputeMargin(Block blockType, Thickness outlineWidth)
        {
            if (blockType == Block.Foreground) return default(Thickness);

            var topMargin = 0d;
            var leftMargin = 0d;

            if (blockType == Block.Top)
                topMargin = -outlineWidth.Top;
            else if (blockType == Block.Bottom)
                topMargin = outlineWidth.Bottom;
            else if (blockType == Block.Left)
                leftMargin = -outlineWidth.Left;
            else if (blockType == Block.Right)
                leftMargin = outlineWidth.Right;

            return new Thickness(leftMargin, topMargin, 0, 0);
        }

        #endregion

        #region Dependency Property Change Handlers

        private static void OnTextPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as SubtitleTextBlock;
            if (element == null) return;

            var value = e.NewValue as string;
            if (string.IsNullOrWhiteSpace(value)) value = $" \r\n ";
            if (value.Contains("\n") == false) value = $"{value}\r\n ";
            foreach (var t in element.TextBlocks)
                t.Value.Text = value;
        }

        private static void OnTextForegroundPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as SubtitleTextBlock;
            if (element == null) return;

            var value = e.NewValue as Brush;
            element.TextBlocks[Block.Foreground].Foreground = value;
        }

        private static void OnTextOutlinePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as SubtitleTextBlock;
            if (element == null) return;

            var value = e.NewValue as Brush;
            foreach (var t in element.TextBlocks)
            {
                if (t.Key != Block.Foreground)
                    t.Value.Foreground = value;
            }
        }

        private static void OnTextOutlineWidthPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as SubtitleTextBlock;
            if (element == null) return;

            var value = (Thickness)e.NewValue;
            foreach (var t in element.TextBlocks)
                t.Value.Margin = ComputeMargin(t.Key, value);
        }

        private static void OnTextForegroundEffectPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as SubtitleTextBlock;
            if (element == null) return;

            var value = e.NewValue as Effect;
            element.TextBlocks[Block.Foreground].Effect = value;
        }

        #endregion
    }
}
