using System;


namespace Zodiacon.HexEditControl.DataStructures {
	public partial class BinarySearchTree<TKey, TValue> {
		/// <summary>
		/// Node of Binary Search Tree
		/// </summary>
		/// <typeparam name="T">Data type</typeparam>
		[Serializable]
		protected class Node<TKeyNode, TValueNode>
			 where TKeyNode : IComparable<TKeyNode> {
			private TKeyNode key;
			private TValueNode val;

			public virtual TKeyNode Key {
				get { return key; }
				set {
					key = value;
				}

			}
			public virtual TValueNode Value {
				get { return val; }
				set {
					val = value;
				}
			}
			public virtual int Height { get; set; }
			public virtual Node<TKeyNode, TValueNode> Parent { get; set; }
			public virtual Node<TKeyNode, TValueNode> Left { get; set; }
			public virtual Node<TKeyNode, TValueNode> Right { get; set; }

			protected Node() {
				//Do Nothing
			}

			public Node(TKeyNode key, TValueNode val, Node<TKeyNode, TValueNode> parent) {
				this.key = key;
				this.val = val;
				Parent = parent;
				Left = null;
				Right = null;
			}

			public virtual bool Equals(Node<TKeyNode, TValueNode> otherNode) {
				if (otherNode == null || otherNode.Parent == null)        //Only NullNode have Parent to be null
				{
					return false;
				}
				return val.Equals(otherNode.Value);
			}

			public override bool Equals(object obj) {
				Node<TKeyNode, TValueNode> otherNode = obj as Node<TKeyNode, TValueNode>;
				if (otherNode == null || otherNode.Parent == null)        //Only NullNode have Parent to be null
				{
					return false;
				}
				return val.Equals(otherNode.Value);
			}

			public override int GetHashCode() {
				unchecked // Overflow is fine, just wrap
				{
					int hash = 17;
					// Suitable nullity checks etc, of course :)
					hash = hash * 23 + key.GetHashCode();
					return hash;
				}
			}
		}

		[Serializable]
		private sealed class NullNode<TKeyNode, TValueNode> : Node<TKeyNode, TValueNode>
			 where TKeyNode : IComparable<TKeyNode> {

			private static readonly NullNode<TKeyNode, TValueNode> instance = new NullNode<TKeyNode, TValueNode>();

			// Explicit static constructor to tell C# compiler
			// not to mark type as beforefieldinit
			static NullNode() {
				//Do Nothing
			}

			private NullNode() {
				//Do Nothing
			}

			public static NullNode<TKeyNode, TValueNode> Instance => instance;

			public override TKeyNode Key {
				get { return default(TKeyNode); }

			}
			public override TValueNode Value {
				get { return default(TValueNode); }
			}
			public override int Height { get { return 0; } }
			public override Node<TKeyNode, TValueNode> Parent { get { return null; } }
			public override Node<TKeyNode, TValueNode> Left { get { return null; } }
			public override Node<TKeyNode, TValueNode> Right { get { return null; } }

			public override bool Equals(Node<TKeyNode, TValueNode> otherNode) {
				if (otherNode == null) {
					return false;
				}
				if (otherNode.Parent == null)        //Only NullNode have Parent to be null
				{
					return true;
				}
				return false;
			}

			public override bool Equals(object obj) {
				Node<TKeyNode, TValueNode> otherNode = obj as Node<TKeyNode, TValueNode>;
				return this.Equals(otherNode);
			}

			public override int GetHashCode() {
				return 0;
			}
		}

	}
}
