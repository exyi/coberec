using System;

namespace NS
{
	public struct MyStruct
	{
		public Guid id;

		public int count;

		public MyStruct(Guid id, int count)
		{
			this.id = id;
			this.count = count;
		}
	}
}

