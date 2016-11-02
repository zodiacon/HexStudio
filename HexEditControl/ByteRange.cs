using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	[DebuggerDisplay("{Range} (Byte)")]
	public class ByteRange : DataRange {
		public readonly byte[] Data;

		public ByteRange(long offset, byte[] data) : base(Range.FromStartAndCount(offset, data.Length)) {
			Data = data;
		}

		public override DataRange GetSubRange(Range range) {
			var isec = range.GetIntersection(Range);
			if (isec.IsEmpty)
				return null;

			return new ByteRange(range.Start, Data.Skip((int)(isec.Start - Range.Start)).Take((int)isec.Count).ToArray());
		}

		public override void GetData(byte[] bytes, int index, int count) {
			Buffer.BlockCopy(Data, index, bytes, 0, count);
		}

		public override string ToString() {
			return $"{{{Range}}} ({Count}) (Byte)";
		}
	}
}
