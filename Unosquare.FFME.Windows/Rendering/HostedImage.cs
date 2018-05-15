namespace Unosquare.FFME.Rendering
{
    using System;
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

        public HostedImage()
            : base()
        {
            // placeholder
        }

        public ImageSource Source
        {
            get => GetPropertyValue<ImageSource>(SourceProperty);
            set => SetPropertyValue(SourceProperty, value);
        }

        public Stretch Stretch
        {
            get => GetPropertyValue<Stretch>(StretchProperty);
            set => SetPropertyValue(StretchProperty, value);
        }

        public StretchDirection StretchDirection
        {
            get => GetPropertyValue<StretchDirection>(StretchDirectionProperty);
            set => SetPropertyValue(StretchDirectionProperty, value);
        }

        public new HorizontalAlignment HorizontalAlignment
        {
            get => GetPropertyValue<HorizontalAlignment>(HorizontalAlignmentProperty);
            set => SetPropertyValue(HorizontalAlignmentProperty, value);
        }

        public new VerticalAlignment VerticalAlignment
        {
            get => GetPropertyValue<VerticalAlignment>(VerticalAlignmentProperty);
            set => SetPropertyValue(VerticalAlignmentProperty, value);
        }

        protected override Image CreateHostedElement()
        {
            var control = new Image();
            control.BeginInit();
            control.HorizontalAlignment = HorizontalAlignment.Stretch;
            control.EndInit();
            return control;
        }

        private T GetPropertyValue<T>(DependencyProperty property)
        {
            var result = default(T);
            HostDispatcher.BeginInvoke(new Action(() =>
            {
                result = (T)Element.GetValue(property);
            })).Wait();
            return result;
        }

        private void SetPropertyValue<T>(DependencyProperty property, T value)
        {
            HostDispatcher.BeginInvoke(new Action(() =>
            {
                Element.SetValue(property, value);
            }));
        }
    }
}
