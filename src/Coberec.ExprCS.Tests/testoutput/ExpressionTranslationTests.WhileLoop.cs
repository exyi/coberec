using System.Threading;

namespace NS
{
	public class C
	{
		public static void M()
		{
			while (Thread.CurrentThread.IsBackground)
			{
				int.Parse("123456789");
			}
		}
	}
}

