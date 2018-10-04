using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using oops;
using oops.Demo;
using oops.Interfaces;

namespace URF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel();
            this.Loaded += (s, e) => ((IViewStateAware) DataContext).Loaded();
            this.Unloaded += (s, e) => ((IViewStateAware) DataContext).Unloaded();
            var vm = DataContext as MainWindowViewModel;

            // this bit is ugly and I hate it, but...it's a demo and it gets the job done
            (vm.Manager.Undoables as INotifyCollectionChanged).CollectionChanged += (s, e) =>
            {
                UndoMenu.Items.Clear();

                var idx = 1;
                foreach (var item in (vm.Manager.Undoables as IStack<Accumulator>).GetEnumerable())
                {
                    var menu = new MenuItem()
                        {
                            DataContext = item,
                            Command = vm.Manager.Undo,
                            CommandParameter = item,
                            Header = $"({idx++}) Undo '{item.Name}' with {item.Records.Count} changes"
                        };
                    UndoMenu.Items.Add(menu);
                }
            };

            (vm.Manager.Redoables as INotifyCollectionChanged).CollectionChanged += (s, e) =>
            {

                RedoMenu.Items.Clear();
                var idx = 1;
                foreach (var item in (vm.Manager.Redoables as IStack<Accumulator>).GetEnumerable())
                {
                    var menu = new MenuItem()
                        {
                            DataContext = item,
                            Command = vm.Manager.Redo,
                            CommandParameter = item,
                            Header = $"({idx++}) Redo '{item.Name}' with {item.Records.Count} changes"
                        };
                    RedoMenu.Items.Add(menu);
                }
            };
        }

        private void OnSimpleClick(object sender, RoutedEventArgs e)
        {
            Intro.Visibility = Visibility.Collapsed;
            Simple.Visibility = Visibility.Visible;
        }

        private void OnThreadedClick(object sender, RoutedEventArgs e)
        {
            // todo
            Intro.Visibility = Visibility.Collapsed;
            Simple.Visibility = Visibility.Visible;
        }

        private void OnBackClick(object sender, RoutedEventArgs e)
        {
            Intro.Visibility = Visibility.Visible;
            Simple.Visibility = Visibility.Collapsed;
            var vm = DataContext as MainWindowViewModel;
            vm.ThreadedDemo = false;
        }
    }
}