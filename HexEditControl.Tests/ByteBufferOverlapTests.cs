using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zodiacon.HexEditControl;
using System.Linq;
using System.Collections.Generic;

namespace HexEditControl.Tests {
	[TestClass]
	public class ByteBufferOverlapTests {
		[TestMethod]
		public void TestOverwriteChangesByteArray() {
			var bytes = Helpers.CreateByteArray(1024);

			var buffer = new ByteBuffer(bytes);
			var data1 = new byte[] { 100, 101, 200, 65, 70 };
			buffer.AddChange(456, data1, true);

			var data2 = new byte[] { 44, 56, 99, 123, 44, 90, 150 };
			buffer.AddChange(801, data2, true);

			var result = new byte[bytes.Length];
			var changes = new List<OffsetRange>();

			var len = buffer.GetBytes(0, result.Length, result, 0, changes);

			Assert.IsTrue(len == result.Length);
			Assert.IsTrue(changes.Count == 2);

			var hash = result.Hash();

			// construct the array manually
			bytes.ReplaceBytes(456, data1).ReplaceBytes(801, data2);
			var hash2 = bytes.Hash();

			Assert.IsTrue(hash == hash2);
		}

		[TestMethod]
		public void TestOverwriteChangesByteArrayWithOffset() {
			var bytes = Helpers.CreateByteArray(1024);

			var buffer = new ByteBuffer(bytes);
			var data1 = new byte[] { 100, 101, 200, 65, 70 };
			buffer.AddChange(356, data1, true);

			var data2 = new byte[] { 44, 56, 99, 123, 44, 90, 150 };
			buffer.AddChange(601, data2, true);

			int start = 100, size = 600;
			var result = new byte[1024];
			var changes = new List<OffsetRange>();

			var len = buffer.GetBytes(start, size, result, start, changes);

			Assert.IsTrue(len == size);
			Assert.IsTrue(changes.Count == 2);

			var hash = result.Hash(start, size);

			// construct the array manually
			Array.Copy(data1, 0, bytes, 356, data1.Length);
			Array.Copy(data2, 0, bytes, 601, data2.Length);

			var hash2 = bytes.Hash(start, size);

			Assert.IsTrue(hash == hash2);
		}

		[TestMethod]
		public void TestOverwriteChangesByteArrayWithOffsetAndEarlyChanges() {
			var bytes = Helpers.CreateByteArray(1024);

			var buffer = new ByteBuffer(bytes);
			var data1 = new byte[] { 100, 101, 200, 65, 70 };
			buffer.AddChange(98, data1, true);

			var data2 = new byte[] { 44, 56, 99, 123, 44, 90, 150, 101, 2, 6, 13, 33, 77 };
			buffer.AddChange(596, data2, true);

			int start = 200, size = 500;
			var result = new byte[1024];
			var changes = new List<OffsetRange>();

			var len = buffer.GetBytes(start, size, result, start, changes);

			Assert.IsTrue(len == size);
			Assert.IsTrue(changes.Count == 1);

			var hash = result.Hash(start, size);

			// construct the array manually
			Array.Copy(data1, 0, bytes, 98, data1.Length);
			Array.Copy(data2, 0, bytes, 596, data2.Length);

			var hash2 = bytes.Hash(start, size);

			Assert.IsTrue(hash == hash2);
		}

		[TestMethod]
		public void TestOverwriteChangesByteArrayWithOffsetWithOverlap() {
			var bytes = Helpers.CreateByteArray(1024);

			var buffer = new ByteBuffer(bytes);
			var data1 = new byte[] { 100, 101, 200, 65, 70, 200, 202, 204 };
			buffer.AddChange(98, data1, true);

			var data2 = new byte[] { 44, 56, 99, 123, 44, 90, 150, 101, 2, 6, 13, 33, 77 };
			buffer.AddChange(596, data2, true);

			int start = 100, size = 500;
			var result = new byte[1024];
			var changes = new List<OffsetRange>();

			var len = buffer.GetBytes(start, size, result, start, changes);

			Assert.IsTrue(len == size);
			Assert.IsTrue(changes.Count == 2);

			var hash = result.Hash(start, size);

			// construct the array manually
			bytes.ReplaceBytes(98, data1).ReplaceBytes(596, data2);

			var hash2 = bytes.Hash(start, size);

			Assert.IsTrue(hash == hash2);
		}

		[TestMethod]
		public void TestOverwriteChangesByteArrayNoMatch() {
			var bytes = Helpers.CreateByteArray(1024);

			var buffer = new ByteBuffer(bytes);
			var data1 = new byte[] { 100, 101, 200, 65, 70 };
			buffer.AddChange(456, data1, true);

			var data2 = new byte[] { 44, 56, 99, 123, 44, 90, 150 };
			buffer.AddChange(801, data2, true);

			var result = new byte[bytes.Length];
			var changes = new List<OffsetRange>();

			var len = buffer.GetBytes(0, result.Length, result, 0, changes);

			Assert.IsTrue(len == result.Length);
			Assert.IsTrue(changes.Count == 2);

			var hash = result.Hash();

			// construct the array manually
			Array.Copy(data1, 0, bytes, 456, data1.Length);
			Array.Copy(data2, 0, bytes, 801, data2.Length - 1);

			var hash2 = bytes.Hash();

			Assert.IsFalse(hash == hash2);
		}

	}
}
