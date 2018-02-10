namespace Unosquare.FFME.Windows.Sample.Controls
{
    using System.Windows.Controls;
    using Unosquare.FFME.Windows.Sample.Foundation;
    using static Unosquare.FFME.Windows.Sample.Controls.MessageConsole;

    /// <summary>
    /// Interaction logic for MessageConsole.xaml
    /// </summary>
    public partial class MessageConsole : UserControl, IPatternUserControl<LocalViewModel, LocalController>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageConsole"/> class.
        /// </summary>
        public MessageConsole()
        {
            InitializeComponent();
            Controller = new LocalController(this);
        }

        /// <summary>
        /// Gets the view model.
        /// This is a proxy CLR accessor to the DataContextProperty
        /// </summary>
        public LocalViewModel ViewModel
        {
            get => GetValue(DataContextProperty) as LocalViewModel;
            set => SetValue(DataContextProperty, value);
        }

        /// <summary>
        /// Gets the controller.
        /// </summary>
        public LocalController Controller { get; }
    }
}
