using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	[DebuggerDisplay("{Range} (File)")]
	public class FileRange : DataRange {
		MemoryMappedViewAccessor _accessor;

		public long Offset { get; }

		public long Size { get; }

		public long FileOffset { get; }

		public FileRange(Range range, long fileOffset, MemoryMappedViewAccessor accessor) : base(range) {
			FileOffset = fileOffset;
			_accessor = accessor;
		}

		public override void GetData(byte[] bytes, int index, int count) {
			_accessor.ReadArray(FileOffset + index, bytes, 0, count);
		}

		public override DataRange GetSubRange(Range range) {
			return new FileRange(range, FileOffset + (range.Start - Range.Start), _accessor);
		}

		public override string ToString() {
			return $"{{{Range}}} ({Count}) (File offset={FileOffset}) H={Height}";
		}
	}
}
