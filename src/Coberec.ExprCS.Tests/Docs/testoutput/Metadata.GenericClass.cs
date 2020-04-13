using System.Collections.Generic;

namespace NS
{
	public class MyContainer<T>
	{
		public T Item {
			get;
			set;
		}

		public List<T> ToList()
		{
			return new List<T>
			{
				Item
			};
		}

		public void CopyFrom(MyContainer<T> other)
		{
			Item = other.Item;
		}
	}
}

