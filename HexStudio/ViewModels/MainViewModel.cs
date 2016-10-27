using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexStudio.ViewModels {
	class MainViewModel : BindableBase {
		ObservableCollection<OpenFileViewModel> _openFiles = new ObservableCollection<OpenFileViewModel>();

		public IList<OpenFileViewModel> OpenFiles => _openFiles;
	}
}
