using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexEditControl.Tests {
	static class Helpers {
		public static byte[] CreateByteArray(int size) {
			return Enumerable.Range(0, size).Select(i => (byte)(65 + i % 26)).ToArray();
		}
	}
}
