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

        public bool IsEmpty => this == EmptyDataRange.Instance;

		protected DataRange(Range range) {
			Range = range;
		}
	}

	sealed class EmptyDataRange : DataRange {
		public static readonly DataRange Instance = new EmptyDataRange();

		private EmptyDataRange() : base(Range.FromStartToEnd(-1, -2)) {
		}

		public override void GetData(int srcIndex, byte[] buffer, int dstIndex, int count) {
			throw new NotImplementedException();
		}

		public override DataRange GetSubRange(Range range) {
			throw new NotImplementedException();
		}

		public override void Shift(long offset) {
			throw new NotImplementedException();
		}

		public override void WriteData(long position, MemoryMappedViewAccessor accessor) {
			throw new NotImplementedException();
		}
	}

}
