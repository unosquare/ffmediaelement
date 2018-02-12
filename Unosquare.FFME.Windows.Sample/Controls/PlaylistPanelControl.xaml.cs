namespace Unosquare.FFME.Windows.Sample.Controls
{
    using System.Windows.Controls;
    using System.Windows.Input;

    /// <summary>
    /// Interaction logic for PlaylistPanelControl.xaml
    /// </summary>
    public partial class PlaylistPanelControl : UserControl
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
                await App.Current.Commands.OpenCommand.ExecuteAsync(OpenFileTextBox.Text);
                e.Handled = true;
            };
        }
    }
}
