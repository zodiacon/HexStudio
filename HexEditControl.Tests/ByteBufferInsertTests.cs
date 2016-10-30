using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zodiacon.HexEditControl;
using System.Collections.Generic;

namespace HexEditControl.Tests {
	[TestClass]
	public class ByteBufferInsertTests {
		[TestMethod]
		public void TestInsertChangesByteArray() {
			var bytes = Helpers.CreateByteArray(30);

			var buffer = new ByteBuffer(bytes);
			var data1 = new byte[] { 100, 101, 200, 65, 70 };
			buffer.AddChange(6, data1, false);

			var data2 = new byte[] { 44, 56, 99, 123, 44, 90, 150 };
			buffer.AddChange(20, data2, false);

			var result = new byte[bytes.Length + data1.Length + data2.Length];
			var changes = new List<OffsetRange>();

			var len = buffer.GetBytes(0, result.Length, result, 0, changes);

			Assert.IsTrue(len == result.Length);
			Assert.IsTrue(changes.Count == 2);

			var hash = result.Hash();

			// construct the array manually
			var clone = (byte[])bytes.Clone();
			clone = clone.InsertBytes(6, data1).InsertBytes(20, data2);

			var hash2 = clone.Hash();

			Assert.IsTrue(hash == hash2);
		}

		[TestMethod]
		public void TestInsertChangesByteArrayWithOverlap() {
			var bytes = Helpers.CreateByteArray(30);

			var buffer = new ByteBuffer(bytes);
			var data1 = new byte[] { 48, 49, 50, 51, 52 };
			buffer.AddChange(10, data1, false);

			var data2 = new byte[] { 53, 54, 55, 56, 57, 58, 59 };
			buffer.AddChange(20, data2, false);

			var result = new byte[bytes.Length + data1.Length + data2.Length];
			var changes = new List<OffsetRange>();

			int start = 7, size = 24;
			var len = buffer.GetBytes(start, size, result, start, changes);

			Assert.IsTrue(len == size);
			Assert.IsTrue(changes.Count == 2);

			var hash = result.Hash(start, size);

			// construct the array manually
			var clone = (byte[])bytes.Clone();
			clone = clone.InsertBytes(10, data1).InsertBytes(20, data2);

			var hash2 = clone.Hash(start, size);

			Assert.IsTrue(hash == hash2);
		}

		[TestMethod]
		public void TestMixedChangesByteArray() {
			var bytes = Helpers.CreateByteArray(30);

			var buffer = new ByteBuffer(bytes);
			var data1 = new byte[] { 100, 101, 200, 65, 70 };
			buffer.AddChange(6, data1, false);

			var data2 = new byte[] { 44, 56, 99, 123, 44, 90, 150 };
			buffer.AddChange(20, data2, true);

			var result = new byte[bytes.Length + data1.Length];
			var changes = new List<OffsetRange>();

			var len = buffer.GetBytes(0, result.Length, result, 0, changes);

			Assert.IsTrue(len == result.Length);
			Assert.IsTrue(changes.Count == 2);

			var hash = result.Hash();

			// construct the array manually
			var clone = (byte[])bytes.Clone();
			clone = clone.InsertBytes(6, data1).ReplaceBytes(20, data2);

			var hash2 = clone.Hash();

			Assert.IsTrue(hash == hash2);
		}

		[TestMethod]
		public void TestMixedChangesByteArray2() {
			var bytes = Helpers.CreateByteArray(50);

			var buffer = new ByteBuffer(bytes);
			var data1 = new byte[] { 100, 101, 200, 65, 70 };
			buffer.AddChange(20, data1, true);

			var data2 = new byte[] { 44, 56, 99, 123, 44, 90, 150 };
			buffer.AddChange(7, data2, false);

			var data3 = new byte[] { 66, 49, 50, 55 };
			buffer.AddChange(40, data3, true);

			var result = new byte[bytes.Length + data2.Length];
			var changes = new List<OffsetRange>();

			var len = buffer.GetBytes(0, result.Length, result, 0, changes);

			Assert.IsTrue(len == result.Length);
			Assert.IsTrue(changes.Count == 3);

			var hash = result.Hash();

			// construct the array manually
			var clone = (byte[])bytes.Clone();
			clone = clone.InsertBytes(7, data2).ReplaceBytes(20 + data2.Length, data1).ReplaceBytes(40, data3);

			var hash2 = clone.Hash();

			Assert.IsTrue(hash == hash2);
		}

		[TestMethod]
		public void TestMixedChangesByteArrayWithOffset() {
			var bytes = Helpers.CreateByteArray(30);

			var buffer = new ByteBuffer(bytes);
			var data1 = new byte[] { 100, 101, 200, 65, 70 };
			buffer.AddChange(6, data1, false);

			var data2 = new byte[] { 44, 56, 99, 123, 44, 90, 150 };
			buffer.AddChange(20, data2, true);

			var result = new byte[bytes.Length + data1.Length];
			var changes = new List<OffsetRange>();

			int start = 4, size = 20;
			var len = buffer.GetBytes(start, size, result, start, changes);

			Assert.IsTrue(len == size);
			Assert.IsTrue(changes.Count == 2);

			var hash = result.Hash(start, size);

			// construct the array manually
			var clone = (byte[])bytes.Clone();
			clone = clone.InsertBytes(6, data1).ReplaceBytes(20, data2);

			var hash2 = clone.Hash(start, size);

			Assert.IsTrue(hash == hash2);
		}

	}
}
