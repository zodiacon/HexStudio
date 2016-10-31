using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl.Operations {
	public sealed class InsertDataOperation : IEditOperation {
		public long Offset { get; }
		public IList<byte> Data { get; }
		public OperationType Type => OperationType.InsertData;

		public InsertDataOperation(long offset, IEnumerable<byte> data) {
			Offset = offset;
			Data = new List<byte>(data);
		}

		public string Name => "Insert Data";

		public int Count => Data.Count;
	}
}
