using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	public struct OffsetRange {
		public readonly long Start;
		public readonly int Count;

		public OffsetRange(long start, int count) {
			Start = start;
			Count = count;
		}
	}
}
