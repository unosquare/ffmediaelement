namespace Unosquare.FFME.Rendering.Wave
{
    /// <summary>
    /// Windows multimedia error codes from mmsystem.h.
    /// </summary>
    internal enum MmResult
    {
        /// <summary>no error</summary>
        NoError = 0,

        /// <summary>unspecified error</summary>
        UnspecifiedError = 1,

        /// <summary>device ID out of range</summary>
        BadDeviceId = 2,

        /// <summary>driver failed enable</summary>
        NotEnabled = 3,

        /// <summary>device already allocated</summary>
        AlreadyAllocated = 4,

        /// <summary>device handle is invalid</summary>
        InvalidHandle = 5,

        /// <summary>no device driver present</summary>
        NoDriver = 6,

        /// <summary>memory allocation error</summary>
        MemoryAllocationError = 7,

        /// <summary>function isn't supported</summary>
        NotSupported = 8,

        /// <summary>error value out of range</summary>
        BadErrorNumber = 9,

        /// <summary>invalid flag passed</summary>
        InvalidFlag = 10,

        /// <summary>invalid parameter passed</summary>
        InvalidParameter = 11,

        /// <summary>handle being used simultaneously on another thread (eg callback)</summary>
        HandleBusy = 12,

        /// <summary>specified alias not found</summary>
        InvalidAlias = 13,

        /// <summary>bad registry database</summary>
        BadRegistryDatabase = 14,

        /// <summary>registry key not found</summary>
        RegistryKeyNotFound = 15,

        /// <summary>registry read error</summary>
        RegistryReadError = 16,

        /// <summary>registry write error</summary>
        RegistryWriteError = 17,

        /// <summary>registry delete error</summary>
        RegistryDeleteError = 18,

        /// <summary>registry value not found</summary>
        RegistryValueNotFound = 19,

        /// <summary>driver does not call DriverCallback</summary>
        NoDriverCallback = 20,

        /// <summary>more data to be returned</summary>
        MoreData = 21,

        /// <summary>unsupported wave format</summary>
        WaveBadFormat = 32,

        /// <summary>still something playing</summary>
        WaveStillPlaying = 33,

        /// <summary>header not prepared</summary>
        WaveHeaderUnprepared = 34,

        /// <summary>device is synchronous</summary>
        WaveSync = 35,

        // ACM error codes, found in msacm.h

        /// <summary>Conversion not possible</summary>
        AcmNotPossible = 512,

        /// <summary>Busy</summary>
        AcmBusy = 513,

        /// <summary>Header Unprepared</summary>
        AcmHeaderUnprepared = 514,

        /// <summary>Cancelled</summary>
        AcmCancelled = 515,

        // Mixer error codes, found in mmresult.h

        /// <summary>invalid line</summary>
        MixerInvalidLine = 1024,

        /// <summary>invalid control</summary>
        MixerInvalidControl = 1025,

        /// <summary>invalid value</summary>
        MixerInvalidValue = 1026,
    }
}
