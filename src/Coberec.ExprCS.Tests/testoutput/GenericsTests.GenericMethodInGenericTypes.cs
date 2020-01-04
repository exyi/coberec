using System;

namespace MyNamespace
{
	public class MyType<T1>
	{
		public class MyNestedType<T2>
		{
			public MyNestedType<T3> M<T3>()
			{
				return new MyNestedType<T3>();
			}
		}

		public T1 A {
			get;
			set;
		}

		public MyType<TResult> Map<TResult>(Func<T1, TResult> func)
		{
			return new MyType<TResult>
			{
				A = func(A)
			};
		}
	}
}

