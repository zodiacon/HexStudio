using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	public enum OperationType {
		InsertData,
		OverwriteData,
		DeleteData
	}

	public interface IEditOperation {
		string Name { get; }
		long Offset { get; }
		int Count { get; }
		IList<byte> Data { get; }
		OperationType Type { get; }
	}

	public static class IEditOperationExtensions {
		public static Range ToRange(this IEditOperation op) {
			return Range.FromStartAndCount(op.Offset, op.Count);
		}
	}
}
