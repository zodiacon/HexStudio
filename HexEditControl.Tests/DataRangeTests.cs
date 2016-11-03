using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zodiacon.HexEditControl;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace HexEditControl.Tests {
	[TestClass]
	public class DataRangeTests {
		[TestMethod]
		public void TestOverwrite() {
			var bytes = Helpers.CreateByteArray(100);
			var filename = Path.GetTempPath() + "test.dat";
			File.WriteAllBytes(filename, bytes);
			using (var buffer = new ByteBuffer(filename)) {

				var dr1 = new ByteRange(20, new byte[] { 65, 66, 67, 68 });
				buffer.Overwrite(dr1);

				var dr2 = new ByteRange(18, new byte[] { 65, 66, 67, 68, 69, 3, 4, 5, 6, 7, 8 });
				buffer.Overwrite(dr2);

				var dr3 = new ByteRange(50, new byte[] { 65, 66, 67, 68 });
				buffer.Overwrite(dr3);

				var dr4 = new ByteRange(48, new byte[] { 65, 66, 67, 68 });
				buffer.Overwrite(dr4);

				var dr5 = new ByteRange(50, new byte[] { 11, 33, 37, 5, 5, 6, 7, 8, 9 });
				buffer.Overwrite(dr5);

				foreach (var dr in buffer.DataRanges)
					Debug.WriteLine(dr);

				Assert.IsTrue(buffer.DataRanges.Count() == 6);
				Assert.IsTrue(buffer.Size == 100);
			}
		}

		[TestMethod]
		public void TestInsert() {
			var bytes = Helpers.CreateByteArray(100);
			var filename = Path.GetTempPath() + "test.dat";
			File.WriteAllBytes(filename, bytes);
			using (var buffer = new ByteBuffer(filename)) {

				var dr1 = new ByteRange(20, new byte[] { 65, 66, 67, 68 });
				buffer.Insert(dr1);

				var dr2 = new ByteRange(50, new byte[] { 65, 66, 67, 68, 80 });
				buffer.Insert(dr2);

				var dr3 = new ByteRange(48, new byte[] { 65, 66, 67, 68, 77, 33, 55 });
				buffer.Insert(dr3);

				Assert.IsTrue(buffer.Size == 116);
			}
		}
	}

}