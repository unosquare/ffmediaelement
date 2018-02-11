namespace Unosquare.FFME.Windows.Sample.Controls
{
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for MediaPropertyPanelControl.xaml
    /// </summary>
    public partial class MediaPropertyPanelControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaPropertyPanelControl"/> class.
        /// </summary>
        public MediaPropertyPanelControl()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets or sets the view model.
        /// This is aproxy property to the DataContext
        /// </summary>
        public MediaElement ViewModel
        {
            get => GetValue(DataContextProperty) as MediaElement;
            set => SetValue(DataContextProperty, value);
        }
    }
}
