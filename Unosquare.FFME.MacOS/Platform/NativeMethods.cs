namespace Unosquare.FFME.MacOS.Platform
{
	using System;
    using FFmpeg.AutoGen;

    public static class NativeMethods
    {
        public static bool SetDllDirectory(string lpPathName)
        {
            ffmpeg.RootPath = lpPathName;
            return true;
        }

        public static void CopyMemory(IntPtr destination, IntPtr source, uint length)
        {
        }

        public static void FillMemory(IntPtr destination, uint length, byte fill)
        {
        }
    }
}
