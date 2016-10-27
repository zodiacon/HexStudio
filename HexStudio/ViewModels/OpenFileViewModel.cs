using Prism.Mvvm;
using System.IO;

namespace HexStudio.ViewModels {
	class OpenFileViewModel : BindableBase {
		public string FileName { get; }
		public OpenFileViewModel(string filename) {
			FileName = filename;
		}

		public string Title => Path.GetFileName(FileName);
	}
}