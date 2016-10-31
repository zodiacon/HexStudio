using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	[DebuggerDisplay("{Offset}..{Offset+Size-1} (Byte)")]
	class ByteRange : IDataRange {
		public long Offset { get; }
		public long Size => Data.Length;
		public readonly byte[] Data;

		public ByteRange(long offset, byte[] data) {
			Offset = offset;
			Data = data;
		}

		public void GetData(byte[] bytes, int index, int count) {
			Buffer.BlockCopy(Data, index, bytes, 0, count); 
		}

		public IDataRange GetSubRange(long offset, long count) {
			return new ByteRange(offset, Data.Skip((int)(offset - Offset)).Take((int)count).ToArray());
		}
	}
}
