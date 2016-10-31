using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	[DebuggerDisplay("{Offset}..{Offset+Size-1} (File)")]
	class FileRange : IDataRange {
		MemoryMappedViewAccessor _accessor;

		public long Offset { get; }

		public long Size { get; }

		public long FileOffset { get; }

		public FileRange(long offset, long fileOffset, long size, MemoryMappedViewAccessor accessor) {
			Offset = offset;
			Size = size;
			FileOffset = fileOffset;
			_accessor = accessor;
		}

		public void GetData(byte[] bytes, int index, int count) {
			_accessor.ReadArray(FileOffset + index, bytes, 0, count);
		}

		public IDataRange GetSubRange(long offset, long count) {
			return new FileRange(offset, FileOffset + (offset - Offset), count, _accessor);
		}
	}
}
