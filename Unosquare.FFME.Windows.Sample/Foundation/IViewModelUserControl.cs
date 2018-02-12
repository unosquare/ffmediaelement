namespace Unosquare.FFME.Windows.Sample.Foundation
{
    /// <summary>
    /// Defines the components of a standard, View-Model backed user control
    /// </summary>
    /// <typeparam name="T">The type of the ViewModel</typeparam>
    public interface IViewModelUserControl<T>
        where T : ViewModelBase
    {
        /// <summary>
        /// Gets the view model.
        /// This should be a proxy, strongly-typed accessor to the DataContext
        /// </summary>
        T ViewModel { get; }
    }
}
