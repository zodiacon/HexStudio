using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Zodiacon.WPF;

namespace HexStudio.ViewModels {
	sealed class FindDialogViewModel : DialogViewModelBase {
		public FindDialogViewModel(Window dialog) : base(dialog) {

		}

		public string Title => "Find";
		public bool TitleCaps => false; 
	}
}
