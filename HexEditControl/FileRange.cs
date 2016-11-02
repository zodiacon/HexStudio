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

		public override DataRange GetSubRange(Range range) {
			if (range.IsEmpty)
				return EmptyDataRange.Instance;

			return new FileRange(range, FileOffset + (range.Start - Range.Start), _accessor);
		}

		public override string ToString() {
			return $"{{{Range}}} ({Count}) (File offset={FileOffset})";
		}

		public override void Shift(long offset) {
			Range = Range.Offset(offset);
		}

		public override void GetData(int srcIndex, byte[] buffer, int dstIndex, int count) {
			_accessor.ReadArray(FileOffset + srcIndex, buffer, dstIndex, count);
		}

		static byte[] _moveBuffer;

		public override void WriteData(long position, MemoryMappedViewAccessor accessor) {
			if (position != FileOffset) {
				var count = Count;
				const int buffseSize = 1 << 21; 
				if (_moveBuffer == null)
					_moveBuffer = new byte[buffseSize];
				var start = FileOffset;
				while (count > 0) {
					int read = accessor.ReadArray(start, _moveBuffer, 0, (int)Math.Min(count, buffseSize));
					accessor.WriteArray(position, _moveBuffer, 0, read);
					count -= read;
					position += read;
					start += read;
				}
			}
		}
	}
}
