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

namespace HexStudio.Views {
	/// <summary>
	/// Interaction logic for OpenFileView.xaml
	/// </summary>
	public partial class OpenFileView : UserControl {
		public OpenFileView() {
			InitializeComponent();

			DataContextChanged += OpenFileView_DataContextChanged;
		}

		private void OpenFileView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
			((OpenFileViewModel)DataContext).SetHexEdit(_hexEdit);
		}
	}
}
