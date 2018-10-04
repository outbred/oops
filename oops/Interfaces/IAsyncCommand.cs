using System.Threading.Tasks;
using System.Windows.Input;

namespace oops.Interfaces {
    public interface IAsyncCommand : ICommand
    {
        Task ExecuteAsync(object parameter = null);

        void RaiseCanExecuteChanged();
    }

    public interface IAsyncCommand<in TArg> : ICommand
    {
        Task ExecuteAsync(TArg parameter);

        void RaiseCanExecuteChanged();
    }
}