namespace MyNamespace
{
	public abstract class MyClass
	{
		public int InstanceG => default(int);

		public int InstanceGS {
			get
			{
				return default(int);
			}
			set
			{
			}
		}

		public int InstanceS {
			set
			{
			}
		}

		public static int StaticG => default(int);

		public static int StaticGS {
			get
			{
				return default(int);
			}
			internal set
			{
			}
		}

		public abstract int AbstractG {
			get;
		}
	}
}

