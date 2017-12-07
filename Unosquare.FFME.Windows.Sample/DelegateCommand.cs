namespace Unosquare.FFME.Windows.Sample
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Threading;

    /// <summary>
    /// Serves as a UI, XAML-bindable command defined using delegates
    /// </summary>
    public class DelegateCommand : ICommand
    {
        #region Property backing fields

        private readonly Action<object> m_Execute;
        private readonly Func<object, bool> m_CanExecute;
        private readonly Action<object> ExecuteAction;
        private int IsExecuting = 0;

        #endregion // Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateCommand"/> class.
        /// </summary>
        /// <param name="execute">The execute.</param>
        /// <param name="canExecute">The can execute.</param>
        /// <exception cref="System.ArgumentNullException">execute</exception>
        public DelegateCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            m_Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            m_CanExecute = canExecute;

            ExecuteAction = parameter =>
            {
                var canExecuteAction = m_CanExecute?.Invoke(parameter) ?? true;

                if (canExecuteAction)
                    m_Execute(parameter);
            };
        }

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
        public bool CanExecute(object parameter = null)
        {
            if (IsExecuting == 1) return false;
            return m_CanExecute == null || m_CanExecute(parameter);
        }

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command should execute.
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Defines the method to be called when the command is invoked.
        /// </summary>
        /// <param name="parameter">Data used by the command.  If the command does not require data to be passed, this object can be set to null.</param>
        public async void Execute(object parameter = null)
        {
            if (Volatile.Read(ref IsExecuting) == 1) return;

            try
            {
                Interlocked.Exchange(ref IsExecuting, 1);
                await Application.Current.Dispatcher.BeginInvoke(ExecuteAction, DispatcherPriority.Normal, parameter);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not execute command. {ex.Message}");
                throw ex;
            }
            finally
            {
                Interlocked.Exchange(ref IsExecuting, 0);
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Raises the can execute changed.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        #endregion
    }

    /// <summary>
    /// A delegate command that takes in a Typed argument value
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <seealso cref="System.Windows.Input.ICommand" />
    public class DelegateCommand<T> : DelegateCommand
        where T : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateCommand{T}"/> class.
        /// </summary>
        /// <param name="execute">The execute.</param>
        /// <param name="canExecute">The can execute.</param>
        public DelegateCommand(Action<T> execute, Func<T, bool> canExecute)
            : base(o => execute(o as T),
                   canExecute == null ? null : new Func<object, bool>(o => canExecute(o as T)))
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegateCommand{T}"/> class.
        /// </summary>
        /// <param name="execute">The execute.</param>
        public DelegateCommand(Action<T> execute) : this(execute, null) { }


        /// <summary>
        /// Executes the specified arguments.
        /// </summary>
        /// <param name="args">The arguments.</param>
        public void Execute(T args)
        {
            base.Execute(args);
        }
    }
}
