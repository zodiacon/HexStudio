using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zodiacon.HexEditControl;

namespace HexStudio {
	class FindResult {
		public long Offset { get; set; }
		public IHexEdit Editor { get; set; }
	}

	enum ByteFinderOptions {
		FromStart = 0,
		FromCurrentPosition = 1,
	}

	class ByteFinder {
		IEnumerable<IHexEdit> _editors;
		byte[] _data;
		ByteFinderOptions _options;

		public ByteFinder(IEnumerable<IHexEdit> editors, byte[] data, ByteFinderOptions options) {
			_editors = editors;
			_data = data;
			_options = options;
		}

		public IEnumerable<FindResult> Find() {
			foreach (var editor in _editors) {
				var start = _options.HasFlag(ByteFinderOptions.FromCurrentPosition) ? editor.CaretOffset : 0;
				do {
					var find = editor.FindNext(start, _data);
					if (find < 0)
						break;

					yield return new FindResult {
						Editor = editor,
						Offset = find
					};
					start = find + _data.Length;
				} while (true);
			}
		}
	}
}
