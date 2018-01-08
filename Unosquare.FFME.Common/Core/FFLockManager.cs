namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// A lock manager for FFmpeg libraries
    /// </summary>
    internal static class FFLockManager
    {
        /// <summary>
        /// Keeps track of the unmanaged and managed locking structures for the FFmpeg libraries to use.
        /// </summary>
        private static readonly Dictionary<IntPtr, ManualResetEvent> FFmpegOpDone = new Dictionary<IntPtr, ManualResetEvent>();

        /// <summary>
        /// Gets the FFmpeg lock manager callback.
        /// Example: ffmpeg.av_lockmgr_register(FFLockManager.LockOpCallback);
        /// </summary>
        public static unsafe av_lockmgr_register_cb LockOpCallback { get; } = OnFFmpegLockOp;

        /// <summary>
        /// Manages FFmpeg Multithreaded locking
        /// </summary>
        /// <param name="mutex">The mutex.</param>
        /// <param name="lockingOperation">The op.</param>
        /// <returns>
        /// 0 for success, 1 for error
        /// </returns>
        private static unsafe int OnFFmpegLockOp(void** mutex, AVLockOp lockingOperation)
        {
            switch (lockingOperation)
            {
                case AVLockOp.AV_LOCK_CREATE:
                    {
                        var m = new ManualResetEvent(true);
                        var mutexPointer = m.SafeWaitHandle.DangerousGetHandle();
                        *mutex = (void*)mutexPointer;
                        FFmpegOpDone[mutexPointer] = m;
                        return 0;
                    }

                case AVLockOp.AV_LOCK_OBTAIN:
                    {
                        var mutexPointer = new IntPtr(*mutex);
                        FFmpegOpDone[mutexPointer].WaitOne();
                        FFmpegOpDone[mutexPointer].Reset();
                        return 0;
                    }

                case AVLockOp.AV_LOCK_RELEASE:
                    {
                        var mutexPointer = new IntPtr(*mutex);
                        FFmpegOpDone[mutexPointer].Set();
                        return 0;
                    }

                case AVLockOp.AV_LOCK_DESTROY:
                    {
                        var mutexPointer = new IntPtr(*mutex);
                        var m = FFmpegOpDone[mutexPointer];
                        FFmpegOpDone.Remove(mutexPointer);
                        m.Set();
                        m.Dispose();
                        return 0;
                    }
            }

            return 1;
        }
    }
}
