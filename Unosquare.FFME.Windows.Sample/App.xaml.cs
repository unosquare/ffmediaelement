namespace Unosquare.FFME.Windows.Sample
{
    using System.Windows;
    using ViewModels;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Gets the current application.
        /// </summary>
        public static new App Current => Application.Current as App;

        /// <summary>
        /// Gets the root view model.
        /// </summary>
        public RootViewModel ViewModel => FindResource(nameof(RootViewModel)) as RootViewModel;
    }
}
