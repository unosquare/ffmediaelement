namespace Unosquare.FFME.Rendering
{
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    internal sealed class HostedImage : HostedVisualElement<Image>
    {
        public static readonly DependencyProperty SourceProperty = Image.SourceProperty.AddOwner(typeof(HostedImage));
        public static readonly DependencyProperty StretchProperty = Image.StretchProperty.AddOwner(typeof(HostedImage));
        public static readonly DependencyProperty StretchDirectionProperty = Image.StretchDirectionProperty.AddOwner(typeof(HostedImage));
        public static new readonly DependencyProperty HorizontalAlignmentProperty = FrameworkElement.HorizontalAlignmentProperty.AddOwner(typeof(HostedImage));
        public static new readonly DependencyProperty VerticalAlignmentProperty = FrameworkElement.VerticalAlignmentProperty.AddOwner(typeof(HostedImage));
        public static new readonly DependencyProperty LayoutTransformProperty = FrameworkElement.LayoutTransformProperty.AddOwner(typeof(HostedImage));

        public HostedImage()
            : base()
        {
            // placeholder
        }

        public ImageSource Source
        {
            get => GetElementProperty<ImageSource>(SourceProperty);
            set => SetElementProperty(SourceProperty, value);
        }

        public Stretch Stretch
        {
            get => GetElementProperty<Stretch>(StretchProperty);
            set => SetElementProperty(StretchProperty, value);
        }

        public StretchDirection StretchDirection
        {
            get => GetElementProperty<StretchDirection>(StretchDirectionProperty);
            set => SetElementProperty(StretchDirectionProperty, value);
        }

        public new HorizontalAlignment HorizontalAlignment
        {
            get => GetElementProperty<HorizontalAlignment>(HorizontalAlignmentProperty);
            set => SetElementProperty(HorizontalAlignmentProperty, value);
        }

        public new VerticalAlignment VerticalAlignment
        {
            get => GetElementProperty<VerticalAlignment>(VerticalAlignmentProperty);
            set => SetElementProperty(VerticalAlignmentProperty, value);
        }

        public new Transform LayoutTransform
        {
            get => GetElementProperty<Transform>(LayoutTransformProperty);
            set => SetElementProperty(LayoutTransformProperty, value);
        }

        protected override Image CreateHostedElement()
        {
            var control = new Image();
            control.BeginInit();
            control.HorizontalAlignment = HorizontalAlignment.Stretch;
            control.EndInit();
            return control;
        }
    }
}
