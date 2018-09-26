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
using DURF.Demo;
using DURF.Interfaces;

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
            (vm.Manager.Undoables as INotifyCollectionChanged).CollectionChanged += (s, e) =>
            {
                UndoMenu.Items.Clear();

                var idx = 1;
                foreach (var item in vm.Manager.Undoables)
                {
                    var menu = new MenuItem()
                        {
                            DataContext = item,
                            Command = vm.Manager.Undo,
                            CommandParameter = item,
                            Header = $"({idx++}) Undo '{item.Name}' with {item.TrackedChanges.Count} changes"
                        };
                    UndoMenu.Items.Add(menu);
                }
            };

            (vm.Manager.Redoables as INotifyCollectionChanged).CollectionChanged += (s, e) =>
            {
                RedoMenu.Items.Clear();
                var idx = 1;
                foreach (var item in vm.Manager.Redoables.GetEnumerable())
                {
                    var menu = new MenuItem()
                        {
                            DataContext = item,
                            Command = vm.Manager.Redo,
                            CommandParameter = item,
                            Header = $"({idx++}) Redo '{item.Name}' with {item.TrackedChanges.Count} changes"
                        };
                    RedoMenu.Items.Add(menu);
                }
            };

            //vm.Manager.Undoables.
            // bug workarounds
            //UndoMenu.ItemsSource = (DataContext as MainWindowViewModel).Manager.Undoables;
            //RedoMenu.ItemsSource = (DataContext as MainWindowViewModel).Manager.Redoables;
        }

        private void UndoMenu_OnClick(object sender, RoutedEventArgs e)
        {
            UndoMenu.IsSubmenuOpen = true;
        }
    }
}