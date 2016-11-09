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

		public double MaxWidth => 600;
		public string Title => "Find";
	}
}
