namespace Unosquare.FFME.Windows.Sample.Controls
{
    using Foundation;
    using System;
    using System.Windows.Controls;
    using System.Windows.Input;
    using ViewModels;

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

            SearchTextBox.IsEnabledChanged += (s, e) =>
            {
                if ((bool)e.OldValue == false && (bool)e.NewValue == true)
                    FocusSearchBox();

                if ((bool)e.OldValue == true && (bool)e.NewValue == false)
                    FocusFileBox();
            };

            IsVisibleChanged += (s, e) =>
            {
                if (SearchTextBox.IsEnabled)
                    FocusSearchBox();
                else
                    FocusFileBox();
            };
        }

        #region Properties

        /// <summary>
        /// A proxy, strongly-typed property to the underlying DataContext
        /// </summary>
        public RootViewModel ViewModel => DataContext as RootViewModel;

        #endregion

        private void FocusTextBox(TextBox textBox)
        {
            DeferredAction deferredAction = null;
            deferredAction = DeferredAction.Create(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
                FocusManager.SetFocusedElement(App.Current.MainWindow, textBox);
                Keyboard.Focus(textBox);

                if (textBox.IsVisible == false || textBox.IsKeyboardFocused)
                    deferredAction.Dispose();
                else
                    deferredAction.Defer(TimeSpan.FromSeconds(0.25));
            });

            deferredAction.Defer(TimeSpan.FromSeconds(0.25));
        }

        /// <summary>
        /// Focuses the search box.
        /// </summary>
        private void FocusSearchBox()
        {
            FocusTextBox(SearchTextBox);
        }

        /// <summary>
        /// Focuses the file box.
        /// </summary>
        private void FocusFileBox()
        {
            FocusTextBox(OpenFileTextBox);
        }
    }
}
