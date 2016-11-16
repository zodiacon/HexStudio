using HexStudio.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zodiacon.HexEditControl;

namespace HexStudio {
	class FindResultViewModel {
		public OpenFileViewModel OpenFile { get; }
		public long Offset { get; set; }
		public IHexEdit Editor { get; set; }
		public FindResultViewModel(OpenFileViewModel openFile) {
			OpenFile = openFile;
		}
	}

	enum ByteFinderOptions {
		FromStart = 0,
		FromCurrentPosition = 1,
	}

	class ByteFinder {
		IEnumerable<OpenFileViewModel> _files;
		byte[] _data;
		ByteFinderOptions _options;

		public ByteFinder(IEnumerable<OpenFileViewModel> files, byte[] data, ByteFinderOptions options) {
			_files = files;
			_data = data;
			_options = options;
		}

		public IEnumerable<FindResultViewModel> Find() {
			foreach (var file in _files) {
				var editor = file.HexEditor;
				var start = _options.HasFlag(ByteFinderOptions.FromCurrentPosition) ? editor.CaretOffset : 0;
				do {
					var find = editor.FindNext(start, _data);
					if (find < 0)
						break;

					yield return new FindResultViewModel(file) {
						Editor = editor,
						Offset = find
					};
					start = find + _data.Length;
				} while (true);
			}
		}
	}
}
