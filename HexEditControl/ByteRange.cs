using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	[DebuggerDisplay("{Range} (Byte)")]
	public class ByteRange : DataRange {
		byte[] Data;

		public ByteRange(long offset, params byte[] data) : base(Range.FromStartAndCount(offset, data.Length)) {
			Data = data;
		}

		public ByteRange(long offset) : base(Range.FromStartAndCount(offset, 0)) {
			Data = new byte[8];
		}

		public void SetData(int index, byte data) {
			Data[index] = data;
		}

		public void AddData(byte data) {
			if (Count >= Data.Length)
				Array.Resize(ref Data, (int)Count * 2);
			Data[Count] = data;
			Range = Range.FromStartAndCount(Start, Count + 1);
		}

		public override DataRange GetSubRange(Range range) {
			if (range.IsEmpty)
				return EmptyDataRange.Instance;

			var isec = range.GetIntersection(Range);
			if (isec.IsEmpty)
				return EmptyDataRange.Instance;

			return new ByteRange(range.Start, Data.Skip((int)(isec.Start - Range.Start)).Take((int)isec.Count).ToArray());
		}

		public override string ToString() {
			return $"{{{Range}}} ({Count}) (Byte)";
		}

		public override void Shift(long offset) {
			Range = Range.Offset(offset);
		}

		public override void GetData(int srcIndex, byte[] buffer, int dstIndex, int count) {
			Buffer.BlockCopy(Data, srcIndex, buffer, dstIndex, count);
		}

		public override void WriteData(long position, MemoryMappedViewAccessor accessor) {
			accessor.WriteArray(Start, Data, 0, (int)Count);
		}

		public void SwapLastBytes(int n) {
			switch (n) {
				case 8:
					SwapBytes();
					SwapWords();
					ulong value = ((ulong)BitConverter.ToUInt32(Data, (int)Count - 8) << 32) | BitConverter.ToUInt32(Data, (int)Count - 4);
					Array.Copy(BitConverter.GetBytes(value), 0, Data, Count - 8, 8);
					break;

				case 4:
					SwapBytes();
					SwapWords();
					break;

				case 2:
					SwapBytes();
					break;
			}
		}

		private void SwapWords() {
			uint value = ((uint)BitConverter.ToUInt16(Data, (int)Count - 4) << 16) | BitConverter.ToUInt16(Data, (int)Count - 2);
			Array.Copy(BitConverter.GetBytes(value), 0, Data, Count - 4, 4);
		}

		private void SwapBytes() {
			var temp = Data[Count - 1];
			Data[Count - 1] = Data[Count - 2];
			Data[Count - 2] = temp;
		}
	}
}
