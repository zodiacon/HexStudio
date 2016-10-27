using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Zodiacon.WPF;

namespace HexStudio.ViewModels {
	[Export]
	class MainViewModel : BindableBase {
		ObservableCollection<OpenFileViewModel> _openFiles = new ObservableCollection<OpenFileViewModel>();

#pragma warning disable 649
		[Import]
		public UIServicesDefaults UIServices;
#pragma warning restore 649

		public IFileDialogService FileDialogService => UIServices.FileDialogService;
		public IMessageBoxService MessageBoxService => UIServices.MessageBoxService;

		public static ICommand EmptyCommand = new DelegateCommand(() => { }, () => false);

		public IList<OpenFileViewModel> OpenFiles => _openFiles;

		private OpenFileViewModel _selecetdFile;

		public OpenFileViewModel SelectedFile {
			get { return _selecetdFile; }
			set { SetProperty(ref _selecetdFile, value); }
		}

		public ICommand OpenFileCommand => new DelegateCommand(() => {
			var filename = FileDialogService.GetFileForOpen();
			if (filename == null) return;

			if (!File.Exists(filename)) {
				MessageBoxService.ShowMessage("File not found.", Constants.AppTitle);
				return;
			}

			OpenFileInternal(filename);
		});

		public void CloseFile(OpenFileViewModel file) {
			int index = OpenFiles.IndexOf(file);
			Debug.Assert(index >= 0);
			file.Dispose();
			OpenFiles.RemoveAt(index);
			if (--index < 0)
				index = 0;
			if (OpenFiles.Count > 0)
				SelectedFile = OpenFiles[index];
		}

		private void OpenFileInternal(string filename) {
			var file = new OpenFileViewModel(this, filename);
			OpenFiles.Add(file);
			SelectedFile = file;
		}
	}
}
