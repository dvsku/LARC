using System.Windows.Controls;
using LARC.GUI.ViewModels;

namespace LARC.GUI.Views {
    public partial class MainView : UserControl {
        public MainView() {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
