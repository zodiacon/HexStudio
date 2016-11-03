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
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Zodiacon.WPF;

namespace HexStudio.ViewModels {
	[Export]
	class MainViewModel : BindableBase {
		ObservableCollection<OpenFileViewModel> _openFiles = new ObservableCollection<OpenFileViewModel>();
		ObservableCollection<string> _recentFiles = new ObservableCollection<string>();

#pragma warning disable 649
		[Import]
		public UIServicesDefaults UIServices;
#pragma warning restore 649

		public IList<string> RecentFiles => _recentFiles;

		public IFileDialogService FileDialogService => UIServices.FileDialogService;
		public IMessageBoxService MessageBoxService => UIServices.MessageBoxService;

		public bool QueryCloseAll() {
			if (!OpenFiles.Any(file => file.IsModified))
				return true;

			var result = MessageBoxService.ShowMessage("Save modified files before exit?",
				Constants.AppTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
			if (result == MessageBoxResult.Yes) {
				foreach (var file in OpenFiles)
					if (file.IsModified)
						file.SaveInternal();
				return true;
			}

			return result == MessageBoxResult.No;
		}

		public static ICommand EmptyCommand = new DelegateCommand(() => { }, () => false);

		public ICommand ExitCommand => new DelegateCommand(() => Application.Current.Shutdown());

		public ICommand NewFileCommand => new DelegateCommand(() => {
			var file = new OpenFileViewModel(this);
			OpenFiles.Add(file);
			SelectedFile = file;
		});

		public IList<OpenFileViewModel> OpenFiles => _openFiles;

		private OpenFileViewModel _selecetdFile;

		public OpenFileViewModel SelectedFile {
			get { return _selecetdFile; }
			set {
				if (SetProperty(ref _selecetdFile, value)) {
					OnPropertyChanged(nameof(IsSelectedFile));
				}
			}
		}

		public ICommand CloseAllCommand => new DelegateCommand(() => {
			var toClose = new List<OpenFileViewModel>(4);

			foreach (var file in OpenFiles)
				if (!file.IsModified || file.QueryCloseFile())
					toClose.Add(file);

			foreach (var file in toClose)
				OpenFiles.Remove(file);
		}, () => SelectedFile != null).
			ObservesProperty(() => SelectedFile);

		public ICommand OpenFileCommand => new DelegateCommand(() => {
			var filename = FileDialogService.GetFileForOpen();
			if (filename == null) return;

			if (!File.Exists(filename)) {
				MessageBoxService.ShowMessage("File not found.", Constants.AppTitle);
				return;
			}

			OpenFileInternal(filename);
		});

		public ICommand SaveFileCommand => new DelegateCommand<OpenFileViewModel>(file => file.SaveFile()).ObservesProperty(() => SelectedFile);

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
			var file = new OpenFileViewModel(this);
			file.Ready += delegate {
				Dispatcher.CurrentDispatcher.InvokeAsync(() => {
					try {
						file.OpenFile(filename);
					}
					catch (Exception ex) {
						OpenFiles.Remove(file);
						MessageBoxService.ShowMessage($"Error: {ex.Message}", Constants.AppTitle);
					}
				});
			};
			OpenFiles.Add(file);
			SelectedFile = file;
		}

		public bool IsSelectedFile => SelectedFile != null;
	}
}
