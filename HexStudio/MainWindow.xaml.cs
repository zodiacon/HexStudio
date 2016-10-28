using HexStudio.ViewModels;
using System;
using System.Collections.Generic;
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

namespace HexStudio {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow {
		public MainWindow() {
			InitializeComponent();
		}

		private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			if (!((MainViewModel)DataContext).QueryCloseAll())
				e.Cancel = true;
		}
	}
}
