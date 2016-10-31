using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zodiacon.HexEditControl;

namespace HexEditControl.Tests {
	[TestClass]
	public class EditChangeTests {
		[TestMethod]
		public void TestSplit() {
			var change = new EditChange(6, Helpers.CreateByteArray(10));
			var split = change.Split(new EditChange(9, 100, 101, 102));

			Assert.IsNotNull(split.Item1);
			Assert.IsNotNull(split.Item2);

			Assert.IsTrue(split.Item1.Size == 3);
			Assert.IsTrue(split.Item2.Size == 4);
		}
	}
}
