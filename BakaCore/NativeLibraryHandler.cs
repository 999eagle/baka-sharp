using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace BakaCore
{
	class NativeLibraryHandler
	{
		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool SetDllDirectory(string pathName);

		public static void SetLibraryPath()
		{
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				var libPath = Path.Combine(assemblyPath, "lib", Environment.Is64BitProcess ? "x64" : "Win32");
				SetDllDirectory(libPath);
				if (Marshal.GetLastWin32Error() != 0)
				{
					Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
				}
			}
		}
	}
}
