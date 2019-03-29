namespace Unosquare.FFME.Windows.Sample.Controls
{
    using Foundation;
    using System;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using ViewModels;

    /// <summary>
    /// Interaction logic for PlaylistPanelControl.xaml.
    /// </summary>
    public partial class PlaylistPanelControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistPanelControl"/> class.
        /// </summary>
        public PlaylistPanelControl()
        {
            InitializeComponent();

            // Prevent binding to the events
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            // Bind the Enter key to the command
            OpenFileTextBox.KeyDown += async (s, e) =>
            {
                if (e.Key != Key.Enter) return;
                await App.ViewModel.Commands.OpenCommand.ExecuteAsync(OpenFileTextBox.Text);
                e.Handled = true;
            };

            SearchTextBox.IsEnabledChanged += (s, e) =>
            {
                if ((bool)e.OldValue == false && (bool)e.NewValue)
                    FocusSearchBox();

                if ((bool)e.OldValue && (bool)e.NewValue == false)
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
        /// A proxy, strongly-typed property to the underlying DataContext.
        /// </summary>
        public RootViewModel ViewModel => DataContext as RootViewModel;

        #endregion

        private static void FocusTextBox(TextBoxBase textBox)
        {
            DeferredAction.Create(context =>
            {
                if (textBox == null || Application.Current == null || Application.Current.MainWindow == null)
                    return;

                textBox.Focus();
                textBox.SelectAll();
                FocusManager.SetFocusedElement(Application.Current.MainWindow, textBox);
                Keyboard.Focus(textBox);

                if (textBox.IsVisible == false || textBox.IsKeyboardFocused)
                    context?.Dispose();
                else
                    context?.Defer(TimeSpan.FromSeconds(0.25));
            }).Defer(TimeSpan.FromSeconds(0.25));
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
