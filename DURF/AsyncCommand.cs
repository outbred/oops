using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace DURF
{
    public class AsyncCommand : AsyncCommand<object>
    {
        public AsyncCommand(Func<Task> execute, Func<bool> canExecute = null) : base((o) => execute(), (o) => canExecute?.Invoke() ?? true)
        {
        }
    }

    public class AsyncCommand<T> : ICommand
    {
        /// <summary>The function executed by the command.</summary>
        protected Func<T, Task> ExecuteFunc;
        /// <summary>
        /// The function that determines if command can be executed.
        /// </summary>
        protected Func<T, bool> CanExecuteFunc;

        /// <summary>
        /// Occurs when changes occur that affect whether or not the command can be executed.
        /// </summary>
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="T:WpfAsyncPack.Command.AsyncCommand`1" /> class.
        /// </summary>
        /// <param name="execute">
        /// The asynchronous method with parameter and cancellation support. It is executed by the command.
        /// </param>
        /// <param name="canExecute">
        /// The method that determines whether the command can be executed in its current state or not.
        /// </param>
        public AsyncCommand(Func<T, Task> execute, Func<T, bool> canExecute = null)
        {
            this.ExecuteFunc = execute;
            this.CanExecuteFunc = canExecute;
        }

        /// <summary>
        /// Executes the command. Internally the execute will be executed asynchronously.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        public async void Execute(T parameter)
        {
            await this.ExecuteAsync(parameter);
        }

        /// <summary>
        /// Executes the command. Internally the execute will be executed asynchronously.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
        public async void Execute(object parameter = null)
        {
            await this.ExecuteAsync(parameter == null ? default(T) : (T)parameter);
        }

        private bool _running = false;
        /// <summary>Asynchronously executes the command.</summary>
        /// <param name="parameter">Data used by the command.</param>
        public async Task ExecuteAsync(T parameter)
        {
            var task = this.ExecuteFunc(parameter);
            _running = true;
            RaiseCanExecuteChanged();
            if(task != null)
                await task;
            _running = false;
            RaiseCanExecuteChanged();
        }

        /// <summary>The method that determines whether the command can be executed in its current state or not.</summary>
        /// <param name="parameter">Data used by the command.</param>
        /// <returns><c>true</c> if the command can be executed; otherwise, <c>false</c>.</returns>
        public bool CanExecute(T parameter)
        {
            if (this.CanExecuteFunc != null)
                return !_running && this.CanExecuteFunc(parameter);
            return !_running;
        }

        public bool CanExecute(object parameter)
        {
            return this.CanExecute((T)parameter);
        }

        /// <summary>
        /// Raises the <see cref="E:WpfAsyncPack.Command.AsyncCommand`1.CanExecuteChanged" /> event notifying the command state was changed.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, new EventArgs());
        }
    }
}
