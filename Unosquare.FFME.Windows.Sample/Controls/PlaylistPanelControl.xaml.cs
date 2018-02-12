namespace Unosquare.FFME.Windows.Sample.Controls
{
    using Foundation;
    using System.Windows.Controls;
    using System.Windows.Input;
    using ViewModels;

    /// <summary>
    /// Interaction logic for PlaylistPanelControl.xaml
    /// </summary>
    public partial class PlaylistPanelControl : UserControl, IViewModelUserControl<PlaylistViewModel>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistPanelControl"/> class.
        /// </summary>
        public PlaylistPanelControl()
        {
            InitializeComponent();

            // Bind the Enter key to the command
            OpenFileTextBox.KeyDown += async (s, e) =>
            {
                if (e.Key != Key.Enter) return;
                await App.Current.Commands.OpenCommand.ExecuteAsync();
                e.Handled = true;
            };
        }

        /// <summary>
        /// Gets the view model.
        /// This should be a proxy, strongly-typed accessor to the DataContext
        /// </summary>
        public PlaylistViewModel ViewModel => DataContext as PlaylistViewModel;
    }
}
