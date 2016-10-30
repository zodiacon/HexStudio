using Prism.Commands;
using Prism.Mvvm;
using System.IO;
using System;
using System.Windows;
using Zodiacon.HexEditControl;

namespace HexStudio.ViewModels {
	class OpenFileViewModel : BindableBase, IDisposable {
		public DelegateCommandBase SaveFileCommand { get; }
		public DelegateCommandBase SaveAsFileCommand { get; }
		public DelegateCommandBase RevertFileCommand { get; }
		public DelegateCommandBase CloseCommand { get; }

		MainViewModel _mainViewModel;

		public event EventHandler Ready;

		public OpenFileViewModel(MainViewModel mainViewModel) {
			_mainViewModel = mainViewModel;

			SaveFileCommand = new DelegateCommand(SaveInternal, () => IsModified).ObservesProperty(() => IsModified);
			SaveAsFileCommand = new DelegateCommand(SaveAsInternal);

			RevertFileCommand = new DelegateCommand(() => {
				_editor.DiscardChanges();
			}, () => IsModified).ObservesProperty(() => IsModified);

			CloseCommand = new DelegateCommand(() => {
				if (IsModified) {
					var reply = _mainViewModel.MessageBoxService.ShowMessage("File modified. Save before close?",
						Constants.AppTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
					if (reply == MessageBoxResult.Cancel)
						return;
					if (reply == MessageBoxResult.Yes)
						_editor.SaveChanges();
				}
				_mainViewModel.CloseFile(this);
			});
		}

		public void OpenFile(string filename) {
			_editor.OpenFile(filename);
			FileName = filename;
		}

		private string _filename;

		public string FileName {
			get { return _filename; }
			set {
				if (SetProperty(ref _filename, value)) {
					OnPropertyChanged(nameof(Title));
				}
			}
		}

		public void SaveInternal() {
			if (FileName == null)
				SaveAsInternal();
			else
				_editor.SaveChanges();
		}

		private void SaveAsInternal() {
			var filename = _mainViewModel.FileDialogService.GetFileForSave();
			if (filename == null) return;

			_editor.SaveChangesAs(filename);
			FileName = filename;
			OnPropertyChanged(nameof(Title));
		}

		public string Title => (FileName == null ? "Untitled" : Path.GetFileName(FileName)) + (IsModified ? " *" : string.Empty);

		private bool _isReadOnly;

		public bool IsReadOnly {
			get { return _isReadOnly; }
			set { SetProperty(ref _isReadOnly, value); }
		}

		private int _wordSize = 1;

		public int WordSize {
			get { return _wordSize; }
			set { SetProperty(ref _wordSize, value); }
		}

		private bool _isModified;

		public bool IsModified {
			get { return _isModified; }
			set {
				if (SetProperty(ref _isModified, value)) {
					OnPropertyChanged(nameof(Title));
				}
			}
		}

		HexEdit _editor;
		internal void SetHexEdit(HexEdit hexEdit) {
			_editor = hexEdit;
			Ready?.Invoke(this, EventArgs.Empty);
		}

		public void Dispose() {
			_editor.Dispose();
		}

		private bool _is1Byte = true;

		public bool Is1Byte {
			get { return _is1Byte; }
			set {
				if (SetProperty(ref _is1Byte, value) && value) {
					WordSize = 1;
				}
			}
		}

		private bool _is2Byte;

		public bool Is2Byte {
			get { return _is2Byte; }
			set {
				if (SetProperty(ref _is2Byte, value) && value) {
					WordSize = 2;
				}
			}
		}

		private bool _is4Byte;

		public bool Is4Byte {
			get { return _is4Byte; }
			set {
				if (SetProperty(ref _is4Byte, value) && value) {
					WordSize = 4;
				}
			}
		}

		private bool _is8Byte;

		public bool Is8Byte {
			get { return _is8Byte; }
			set {
				if (SetProperty(ref _is8Byte, value) && value) {
					WordSize = 8;
				}
			}
		}

		private bool _overwriteMode = true;

		public bool OverwriteMode {
			get { return _overwriteMode; }
			set { SetProperty(ref _overwriteMode, value); }
		}

	}
}