using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HexStudio.Controls {
	class EditChange : IEqualityComparer<EditChange>, IComparable<EditChange> {
		public long Offset { get; set; }
		public ulong Value { get; set; }

		public int CompareTo(EditChange other) {
			return Offset.CompareTo(other.Offset);
		}

		public bool Equals(EditChange x, EditChange y) {
			return x.Offset == y.Offset;
		}

		public int GetHashCode(EditChange obj) {
			return obj.Offset.GetHashCode();
		}
	}

}
