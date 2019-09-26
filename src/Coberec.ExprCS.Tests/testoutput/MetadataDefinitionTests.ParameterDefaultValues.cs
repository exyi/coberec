using System;

namespace MyNamespace
{
	public interface MyInterface2
	{
		int StringMethod(string myParameter = "default value");

		int ValueTypeMethod(Guid myParameter = default(Guid));
	}
}

