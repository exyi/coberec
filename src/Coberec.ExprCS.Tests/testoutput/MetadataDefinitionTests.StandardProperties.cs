using System.Runtime.CompilerServices;

namespace MyNamespace
{
	public class MyType
	{
		public string A {
			[CompilerGenerated]
			get
			{
				return "abcd";
			}
			[CompilerGenerated]
			protected set
			{
				while (Equals(null))
				{
				}
			}
		}
	}
}

