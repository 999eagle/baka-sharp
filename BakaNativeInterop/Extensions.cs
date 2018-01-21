using System;
using System.Collections.Generic;
using System.Text;

namespace BakaNativeInterop
{
	internal static class Extensions
	{
		public static bool DisposeOnException(this IDisposable disposable)
		{
			disposable.Dispose();
			return false;
		}

		public static bool ExceptionFilter(this Object @this, Action action)
		{
			action();
			return false;
		}
	}
}
