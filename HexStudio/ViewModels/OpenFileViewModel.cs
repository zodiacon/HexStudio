using Prism.Mvvm;
using System.IO;

namespace HexStudio.ViewModels {
	class OpenFileViewModel : BindableBase {
		public string FileName { get; }
		public OpenFileViewModel(string filename) {
			FileName = filename;
		}

		public string Title => Path.GetFileName(FileName);

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

	}
}