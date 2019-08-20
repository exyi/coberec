using System.Threading;

namespace NS
{
	public class C
	{
		public static void M()
		{
			int.Parse("123456789");
			if (Thread.CurrentThread.IsBackground)
			{
				return;
			}
			int.Parse("123456789");
		}
	}
}

