using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexEditControl.Tests {
	static class Extensions {
		public static int Hash(this byte[] bytes, int index = 0, int size = 0) {
			if (size == 0)
				size = bytes.Length;

			int hash = size;
			for (int i = index; i < size + index; i++)
				hash = unchecked(hash * 17 + bytes[i]);
			return hash;
		}

		public static byte[] InsertBytes(this byte[] bytes, int index, params byte[] data) {
			Debug.Assert(data != null);
			Array.Resize(ref bytes, bytes.Length + data.Length);
			Array.Copy(bytes, index, bytes, index + data.Length, bytes.Length - index - data.Length);
			Array.Copy(data, 0, bytes, index, data.Length);

			return bytes;
		}

		public static byte[] ReplaceBytes(this byte[] bytes, int index, params byte[] data) {
			Debug.Assert(data != null);

			Array.Copy(data, 0, bytes, index, data.Length);

			return bytes;
		}
	}
}
