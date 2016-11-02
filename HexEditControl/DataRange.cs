using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	public abstract class DataRange {
		public Range Range { get; }

		public long Start => Range.Start;
		public long End => Range.End;
		public long Count => Range.Count;

		public abstract void GetData(byte[] bytes, int index, int count);
		public abstract DataRange GetSubRange(Range range);

		protected DataRange(Range range) {
			Range = range;
		}
	}

}
