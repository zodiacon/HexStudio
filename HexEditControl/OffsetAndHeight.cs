using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	struct OffsetAndHeight : IComparable<OffsetAndHeight> {
		public readonly long Offset;
		public readonly int Height;

		public OffsetAndHeight(long offset, int height) {
			Offset = offset;
			Height = height;
		}

		public int CompareTo(OffsetAndHeight other) {
			int compareOffsets = Offset.CompareTo(other.Offset);
			if (compareOffsets != 0)
				return compareOffsets;
			return -Height.CompareTo(other.Height);
		}
	}
}
