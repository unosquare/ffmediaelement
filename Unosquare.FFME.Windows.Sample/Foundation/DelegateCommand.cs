namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using Primitives;
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Threading;

    /// <summary>
    /// Serves as a UI, XAML-bindable command defined using delegates
    /// </summary>
    public class DelegateCommand : ICommand
    {
        #region Property backing fields

        private readonly Func<object, bool> m_CanExecute;
        private readonly Action<object> ExecuteAction;
        private readonly AtomicBoolean IsExecuting = new AtomicBoolean(false);

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateCommand"/> class.
        /// </summary>
        /// <param name="execute">The execute callback.</param>
        /// <param name="canExecute">The can execute checker callback.</param>
        /// <exception cref="ArgumentNullException">execute</exception>
        public DelegateCommand(Action<object> execute, Func<object, bool> canExecute)
        {
            var callback = execute ?? throw new ArgumentNullException(nameof(execute));
            m_CanExecute = canExecute;

            ExecuteAction = parameter =>
            {
                var canExecuteAction = m_CanExecute?.Invoke(parameter) ?? true;

                if (canExecuteAction)
                    callback(parameter);
            };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateCommand"/> class.
        /// </summary>
        /// <param name="execute">The execute callback.</param>
        /// <exception cref="ArgumentNullException">execute</exception>
        public DelegateCommand(Action<object> execute)
            : this(execute, null)
        {
            // placeholder
        }

        #endregion

        #region Events

        /// <inheritdoc />
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        #endregion

        #endregion

        #region ICommand Members

        /// <summary>
        /// Defines the method that determines whether the command can execute in its current state.
        /// </summary>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        /// <returns>
        /// true if this command can be executed; otherwise, false.
        /// </returns>
        [DebuggerStepThrough]
        public bool CanExecute(object parameter)
        {
            if (IsExecuting.Value) return false;
            return m_CanExecute == null || m_CanExecute(parameter);
        }

        /// <summary>
        /// Determines whether this instance can execute.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if this instance can execute; otherwise, <c>false</c>.
        /// </returns>
        public bool CanExecute() => CanExecute(null);

        /// <inheritdoc />
        public void Execute(object parameter) => ExecuteAsync(parameter);

        /// <summary>
        /// Executes the command but does not wait for it to complete
        /// </summary>
        public void Execute() => ExecuteAsync(null);

        /// <summary>
        /// Executes the command. This call can be awaited.
        /// </summary>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        /// <returns>The awaitable task</returns>
        public ConfiguredTaskAwaitable ExecuteAsync(object parameter)
        {
            return Task.Run(async () =>
            {
                if (IsExecuting.Value)
                    return;

                try
                {
                    IsExecuting.Value = true;
                    await Application.Current.Dispatcher.BeginInvoke(ExecuteAction, DispatcherPriority.Normal, parameter);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not execute command. {ex.Message}");
                    throw;
                }
                finally
                {
                    IsExecuting.Value = false;
                    CommandManager.InvalidateRequerySuggested();
                }
            }).ConfigureAwait(true);
        }

        /// <summary>
        /// Executes the command. This call can be awaited.
        /// </summary>
        /// <returns>The awaitable task</returns>
        public ConfiguredTaskAwaitable ExecuteAsync() => ExecuteAsync(null);

        #endregion
    }
}
