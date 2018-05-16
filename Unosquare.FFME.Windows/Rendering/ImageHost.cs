namespace Unosquare.FFME.Rendering
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    /// <summary>
    /// Implements an Image control that is hosted on its own independent dispatcher
    /// but maintains composability with the main UI.
    /// </summary>
    internal sealed class ImageHost : ElementHostBase<Image>
    {
        #region Dependency Property Registrations

        /// <summary>
        /// The source property
        /// </summary>
        public static readonly DependencyProperty SourceProperty = Image.SourceProperty.AddOwner(typeof(ImageHost));

        /// <summary>
        /// The stretch property
        /// </summary>
        public static readonly DependencyProperty StretchProperty = Image.StretchProperty.AddOwner(typeof(ImageHost));

        /// <summary>
        /// The stretch direction property
        /// </summary>
        public static readonly DependencyProperty StretchDirectionProperty = Image.StretchDirectionProperty.AddOwner(typeof(ImageHost));

        /// <summary>
        /// The horizontal alignment property
        /// </summary>
        public static new readonly DependencyProperty HorizontalAlignmentProperty = FrameworkElement.HorizontalAlignmentProperty.AddOwner(typeof(ImageHost));

        /// <summary>
        /// The vertical alignment property
        /// </summary>
        public static new readonly DependencyProperty VerticalAlignmentProperty = FrameworkElement.VerticalAlignmentProperty.AddOwner(typeof(ImageHost));

        /// <summary>
        /// The layout transform property
        /// </summary>
        public static new readonly DependencyProperty LayoutTransformProperty = FrameworkElement.LayoutTransformProperty.AddOwner(typeof(ImageHost));

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageHost"/> class.
        /// </summary>
        public ImageHost()
            : base(true)
        {
            // placeholder
        }

        /// <summary>
        /// Gets or sets the source.
        /// </summary>
        public ImageSource Source
        {
            get => GetElementProperty<ImageSource>(SourceProperty);
            set => SetElementProperty(SourceProperty, value);
        }

        /// <summary>
        /// Gets or sets the stretch.
        /// </summary>
        public Stretch Stretch
        {
            get => GetElementProperty<Stretch>(StretchProperty);
            set => SetElementProperty(StretchProperty, value);
        }

        /// <summary>
        /// Gets or sets the stretch direction.
        /// </summary>
        public StretchDirection StretchDirection
        {
            get => GetElementProperty<StretchDirection>(StretchDirectionProperty);
            set => SetElementProperty(StretchDirectionProperty, value);
        }

        /// <summary>
        /// Gets or sets the horizontal alignment characteristics applied to this element when it is composed within a parent element, such as a panel or items control.
        /// </summary>
        public new HorizontalAlignment HorizontalAlignment
        {
            get => GetElementProperty<HorizontalAlignment>(HorizontalAlignmentProperty);
            set => SetElementProperty(HorizontalAlignmentProperty, value);
        }

        /// <summary>
        /// Gets or sets the vertical alignment characteristics applied to this element when it is composed within a parent element such as a panel or items control.
        /// </summary>
        public new VerticalAlignment VerticalAlignment
        {
            get => GetElementProperty<VerticalAlignment>(VerticalAlignmentProperty);
            set => SetElementProperty(VerticalAlignmentProperty, value);
        }

        /// <summary>
        /// Gets or sets a graphics transformation that should apply to this element when  layout is performed.
        /// </summary>
        public new Transform LayoutTransform
        {
            get => GetElementProperty<Transform>(LayoutTransformProperty);
            set => SetElementProperty(LayoutTransformProperty, value);
        }

        /// <summary>
        /// Creates the element contained by this host
        /// </summary>
        /// <returns>
        /// An instance of the framework element to be hosted
        /// </returns>
        protected override Image CreateHostedElement()
        {
            var control = new Image();
            control.BeginInit();
            control.HorizontalAlignment = HorizontalAlignment.Stretch;
            control.VerticalAlignment = VerticalAlignment.Stretch;
            control.EndInit();
            return control;
        }
    }
}
