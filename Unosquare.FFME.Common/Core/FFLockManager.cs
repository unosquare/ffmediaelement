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
        /* because Zeranoe FFmpeg Builds don't have --enable-pthreads,
         * https://ffmpeg.zeranoe.com/builds/readme/win64/static/ffmpeg-20170620-ae6f6d4-win64-static-readme.txt
         * and because by default FFmpeg is not thread-safe,
         * https://stackoverflow.com/questions/13888915/thread-safety-of-libav-ffmpeg
         * we need to register a lock manager with av_lockmgr_register
         * Just like in https://raw.githubusercontent.com/FFmpeg/FFmpeg/release/3.4/ffplay.c
         */

        /// <summary>
        /// The register lock
        /// </summary>
        private static readonly object RegisterLock = new object();

        /// <summary>
        /// Keeps track of the unmanaged and managed locking structures for the FFmpeg libraries to use.
        /// </summary>
        private static readonly Dictionary<IntPtr, ManualResetEvent> FFmpegOpDone = new Dictionary<IntPtr, ManualResetEvent>();

        /// <summary>
        /// The registration state
        /// </summary>
        private static bool m_HasRegistered = false;

        /// <summary>
        /// Gets a value indicating whether the lock manager has registered.
        /// </summary>
        public static bool HasRegistered
        {
            get
            {
                lock (RegisterLock)
                {
                    return m_HasRegistered;
                }
            }
        }

        /// <summary>
        /// Gets the FFmpeg lock manager callback.
        /// Example: ffmpeg.av_lockmgr_register(FFLockManager.LockOpCallback);
        /// </summary>
        private static unsafe av_lockmgr_register_cb LockOpCallback { get; } = OnFFmpegLockOp;

        /// <summary>
        /// Registers the lock manager. If it has been registered it does not do it again.
        /// Thi method is thread-safe.
        /// </summary>
        public static void Register()
        {
            lock (RegisterLock)
            {
                if (m_HasRegistered) return;
                ffmpeg.av_lockmgr_register(LockOpCallback);
                m_HasRegistered = true;
            }
        }

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
