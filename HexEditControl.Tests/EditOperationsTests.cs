using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zodiacon.HexEditControl;
using Zodiacon.HexEditControl.Operations;

namespace HexEditControl.Tests {
	[TestClass]
	public class EditOperationsTests {
		[TestMethod]
		public void TestBasicOperations() {
			var buffer = new ByteBuffer(@"c:\\temp\\hello.txt");
			var size = buffer.Size;
			buffer.AddOperation(new OverwriteDataOperation(10, new byte[] { 1, 2, 3, 4, 5 }));
			buffer.AddOperation(new OverwriteDataOperation(60, new byte[] { 11, 12, 13, 14, 15, 22, 30, 90 }));

			Assert.IsTrue(buffer.Size == size);

			buffer.AddOperation(new InsertDataOperation(100, new byte[] { 4, 5, 6, 7 }));

			Assert.IsTrue(buffer.Size == size + 4);

			buffer.AddOperation(new InsertDataOperation(80, new byte[] { 4, 5, 6, 7 }));

			Assert.IsTrue(buffer.Size == size + 8);
		}
	}
}
