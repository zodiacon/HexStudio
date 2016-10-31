using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodiacon.HexEditControl {
	public struct Range<T> where T : struct, IComparable<T> {
		public static readonly Range<T> Empty = new Range<T>();

		public bool IsEmpty => this == Empty;

		public T Start { get; }
		public T End { get; }

		public Range(T start, T end)
			 : this() {
			Start = start;
			End = end;
		}

		/// <summary>
		/// Checks if object in inside range.
		/// </summary>
		public bool Contains(T obj) {
			var result = true;
			result &= Start.CompareTo(obj) <= 0;
			result &= End.CompareTo(obj) >= 0;

			return result;
		}

		public Range<T> GetIntersection(Range<T> other) {
			if (!Intersects(other))
				return Range<T>.Empty;

			T start, end;

			start = Start.CompareTo(other.Start) >= 0 ? Start : other.Start;

			end = End.CompareTo(other.End) < 0 ? End : other.End;

			return new Range<T>(start, end);
		}

		public bool Intersects(Range<T> other) {
			var hasClosedInterval = Start.CompareTo(other.End) <= 0 && other.End.CompareTo(End) <= 0;

			var hasOpenInterval =
				 (Start.CompareTo(other.End) <= 0) || (End.CompareTo(other.Start) >= 0) ||
				 (other.Start.CompareTo(End) <= 0) || (other.End.CompareTo(Start) >= 0);

			return hasClosedInterval || hasOpenInterval;
		}

		public override string ToString() {
			return $"{Start.ToString()}..{End.ToString()}";
		}

		public override int GetHashCode() {
			return Start.GetHashCode() ^ End.GetHashCode();
		}

		#region " Operators "

		public static bool operator ==(Range<T> a, Range<T> b) {
			return a.Start.Equals(b.Start) && a.End.Equals(b.End);
		}

		public static bool operator !=(Range<T> a, Range<T> b) {
			return !(a == b);
		}

		#endregion

		public override bool Equals(object obj) {
			if (!(obj is Range<T>))
				return false;

			return (this == (Range<T>)obj);
		}
	}
}
