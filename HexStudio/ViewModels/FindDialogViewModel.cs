using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Zodiacon.HexEditControl;
using Zodiacon.WPF;

namespace HexStudio.ViewModels {
	sealed class FindDialogViewModel : DialogViewModelBase {
		public FindDialogViewModel(Window dialog) : base(dialog) {
			CanExecuteOKCommand = () => IsStringSearch && !string.IsNullOrEmpty(SearchString) || IsBytesSearch && HexEdit?.Size > 0;
			OKCommand.ObservesProperty(() => IsStringSearch).ObservesProperty(() => IsBytesSearch).ObservesProperty(() => SearchString);
		}

		public double Width => 650.0;
		public SizeToContent SizeToContent => SizeToContent.WidthAndHeight;
		public string Title => "Find";
		public string Icon => "/icons/find.ico";
		public ResizeMode ResizeMode => ResizeMode.NoResize;

		private bool _isBytesSearch = true;

		public bool IsBytesSearch {
			get { return _isBytesSearch; }
			set { SetProperty(ref _isBytesSearch, value); }
		}

		private bool _isStringSearch;

		public bool IsStringSearch {
			get { return _isStringSearch; }
			set { SetProperty(ref _isStringSearch, value); }
		}

		private bool _isAscii = true;

		public bool IsAscii {
			get { return _isAscii; }
			set { SetProperty(ref _isAscii, value); }
		}

		private bool _isUtf8;

		public bool IsUTF8 {
			get { return _isUtf8; }
			set { SetProperty(ref _isUtf8, value); }
		}

		private bool _isUtf16;

		public bool IsUTF16 {
			get { return _isUtf16; }
			set { SetProperty(ref _isUtf16, value); }
		}

		private string _searchString;

		public string SearchString {
			get { return _searchString; }
			set { SetProperty(ref _searchString, value); }
		}

		IHexEdit _hexEdit;
		public IHexEdit HexEdit {
			get { return _hexEdit; }
			set {
				_hexEdit = value;
				_hexEdit.BufferSizeChanged += delegate { OKCommand.RaiseCanExecuteChanged(); };
				if (_data != null)
					_hexEdit.SetData(_data);
			}
		}

		private bool _isSearchSelection;

		public bool IsSearchSelection {
			get { return _isSearchSelection; }
			set { SetProperty(ref _isSearchSelection, value); }
		}

		private bool _isSearchFile = true;

		public bool IsSearchFile {
			get { return _isSearchFile; }
			set { SetProperty(ref _isSearchFile, value); }
		}

		private bool _isSearchAllFiles;

		public bool IsSearchAllFiles {
			get { return _isSearchAllFiles; }
			set { SetProperty(ref _isSearchAllFiles, value); }
		}

		byte[] _data;
		protected override void OnClose(bool? result) {
			_data = HexEdit.GetBytes(0, (int)HexEdit.Size);
		}
	}
}
