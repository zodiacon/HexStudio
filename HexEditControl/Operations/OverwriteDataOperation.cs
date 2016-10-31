using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl.Operations {
	public sealed class OverwriteDataOperation : IEditOperation {
		public long Offset { get; }
		public IList<byte> Data { get; }
		public OperationType Type => OperationType.OverwriteData;

		public OverwriteDataOperation(long offset, IEnumerable<byte> data) {
			Offset = offset;
			Data = new List<byte>(data);
		}

		public string Name => "Overwrite Data";
		public int Count => Data.Count;
	}
}
