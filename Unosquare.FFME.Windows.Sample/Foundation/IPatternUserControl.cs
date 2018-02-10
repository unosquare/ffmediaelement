namespace Unosquare.FFME.Windows.Sample.Foundation
{
    /// <summary>
    /// Defines the components of a stander pattern user control
    /// </summary>
    /// <typeparam name="M">The type of the View-Model</typeparam>
    /// <typeparam name="C">The type of the Controller</typeparam>
    public interface IPatternUserControl<M, C>
        where M : ViewModelBase
        where C : class
    {
        /// <summary>
        /// Gets the view model.
        /// </summary>
        M ViewModel { get; set; }

        /// <summary>
        /// Gets the controller.
        /// </summary>
        C Controller { get; }
    }
}
