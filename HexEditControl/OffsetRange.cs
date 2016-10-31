using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	public struct OffsetRange {
		public readonly long Offset;
		public readonly int Count;

		public OffsetRange(long start, int count) {
			Offset = start;
			Count = count;
		}
	}
}
