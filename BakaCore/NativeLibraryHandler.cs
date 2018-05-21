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
			var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			var libPath = Path.Combine(assemblyPath, "lib");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				libPath = Path.Combine(libPath, Environment.Is64BitProcess ? "x64" : "Win32");
				if (!SetDllDirectory(libPath) && Marshal.GetLastWin32Error() != 0)
				{
					Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error());
				}
			}
			else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				libPath = Path.Combine(libPath, Environment.Is64BitProcess ? "linux_x64" : "linux_x86");
			}
			BakaNativeInterop.FFmpeg.FFmpeg.SetLibraryPath(libPath);
		}
	}
}
