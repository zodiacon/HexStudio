using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	public interface IHexEdit {
		byte[] GetBytes(long offset, int count);
		long Size { get; }

		event Action<long, long> BufferSizeChanged;

		long CaretOffset { get; set; }

		long FindNext(long offset, byte[] data);
		void SetData(byte[] data);
	}
}
