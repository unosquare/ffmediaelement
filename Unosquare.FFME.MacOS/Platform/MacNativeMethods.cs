namespace Unosquare.FFME.MacOS.Platform
{
    using System;
    using FFmpeg.AutoGen;
    using Unosquare.FFME.Shared;

    public class MacNativeMethods : INativeMethods
    {
        public bool SetDllDirectory(string lpPathName)
        {
            if (lpPathName != null)
                ffmpeg.RootPath = lpPathName ?? string.Empty;
            return true;
        }

        public void CopyMemory(IntPtr destination, IntPtr source, uint length)
        {
            throw new NotImplementedException();
        }

        public void FillMemory(IntPtr destination, uint length, byte fill)
        {
            throw new NotImplementedException();
        }
    }
}
