namespace MyNamespace
{
	public abstract class MyClass
	{
		public void InstanceMethod()
		{
		}

		public static void StaticMethod()
		{
		}

		public abstract void AbstractMethod();

		public virtual void VirtualMethod()
		{
		}

		public static T GenericMethod<T>()
		{
			return default(T);
		}

		public void MethodWithParams(string p1, ref double byReferenceParameter, int withDefaultValue = 0)
		{
		}
	}
}

