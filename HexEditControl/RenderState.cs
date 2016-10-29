using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	sealed class RenderState {
		public long StartOffset { get; set; }
		public long EndOffset { get; set; }
		public long FileSize { get; set; }
		public byte[] Buffer { get; set; }
	}
}
