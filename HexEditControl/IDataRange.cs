using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	interface IDataRange {
		long Offset { get; }
		long Size { get; }

		void GetData(byte[] bytes, int index, int count);
		IDataRange GetSubRange(long offset, long count);
	}

	static class IDataRangeExtensions {
		public static Range<long> ToRange(this IDataRange dr) {
			return new Range<long>(dr.Offset, dr.Size + dr.Offset - 1);
		}
	}
}
