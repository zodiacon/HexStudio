using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	public abstract class DataRange {
		public Range Range { get; protected set; }

		public long Start => Range.Start;
		public long End => Range.End;
		public long Count => Range.Count;

		public abstract void GetData(int srcIndex, byte[] buffer, int dstIndex, int count);
		public abstract DataRange GetSubRange(Range range);

		public abstract void Shift(long offset);

		public abstract void WriteData(long position, MemoryMappedViewAccessor accessor);

		protected DataRange(Range range) {
			Range = range;
		}
	}

}
