namespace Unosquare.FFME.Rendering.Wave
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// WaveOutCapabilities structure (based on WAVEOUTCAPS2 from mmsystem.h)
    /// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/multimed/htm/_win32_waveoutcaps_str.asp
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct WaveOutCapabilities
    {
        private const int MaxProductNameLength = 32;

        /// <summary>
        /// wMid
        /// </summary>
        private short manufacturerId;

        /// <summary>
        /// wPid
        /// </summary>
        private short productId;

        /// <summary>
        /// vDriverVersion
        /// </summary>
        private int driverVersion;

        /// <summary>
        /// Product Name (szPname)
        /// </summary>
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxProductNameLength)]
        private string productName;

        /// <summary>
        /// Supported formats (bit flags) dwFormats 
        /// </summary>
        private SupportedWaveFormat supportedFormats;

        /// <summary>
        /// Supported channels (1 for mono 2 for stereo) (wChannels)
        /// Seems to be set to -1 on a lot of devices
        /// </summary>
        private short channels;

        /// <summary>
        /// wReserved1
        /// </summary>
        private short reserved;

        /// <summary>
        /// Optional functionality supported by the device
        /// </summary>
        private WaveOutSupport support; // = new WaveOutSupport();

        // extra WAVEOUTCAPS2 members
        private Guid manufacturerGuid;
        private Guid productGuid;
        private Guid nameGuid;

        /// <summary>
        /// Number of channels supported
        /// </summary>
        public int Channels
        {
            get
            {
                return channels;
            }
        }

        /// <summary>
        /// Whether playback rate control is supported
        /// </summary>
        public bool SupportsPlaybackRateControl
        {
            get { return support.HasFlag(WaveOutSupport.PlaybackRate); }
        }

        /// <summary>
        /// Whether volume control is supported
        /// </summary>
        public bool SupportsVolumeControl
        {
            get { return support.HasFlag(WaveOutSupport.Volume); }
        }

        /// <summary>
        /// Gets a value indicating whether this device supports independent channel volume control.
        /// </summary>
        public bool SupportsChannelVolumeControl
        {
            get { return support.HasFlag(WaveOutSupport.LRVolume); }
        }

        /// <summary>
        /// Gets a value indicating whether this device supports pitch control.
        /// </summary>
        public bool SupportsPitchControl
        {
            get { return support.HasFlag(WaveOutSupport.Pitch); }
        }

        /// <summary>
        /// Gets a value indicating whether the device returns sample-accurate position information.
        /// </summary>
        public bool SupportsSampleAccuratePosition
        {
            get { return support.HasFlag(WaveOutSupport.SampleAccurate); }
        }

        /// <summary>
        /// Gets a value indicating whether the driver is synchronous and will block while playing a buffer.
        /// </summary>
        public bool IsSynchronousOutput
        {
            get { return support.HasFlag(WaveOutSupport.Sync); }
        }

        /// <summary>
        /// The product name
        /// </summary>
        public string ProductName
        {
            get
            {
                return productName;
            }
        }

        /// <summary>
        /// The device name Guid (if provided)
        /// </summary>
        public Guid NameGuid
        {
            get { return nameGuid; }
        }

        /// <summary>
        /// The product name Guid (if provided)
        /// </summary>
        public Guid ProductGuid
        {
            get { return productGuid; }
        }

        /// <summary>
        /// The manufacturer guid (if provided)
        /// </summary>
        public Guid ManufacturerGuid
        {
            get { return manufacturerGuid; }
        }

        /// <summary>
        /// Checks to see if a given SupportedWaveFormat is supported
        /// </summary>
        /// <param name="waveFormat">The SupportedWaveFormat</param>
        /// <returns>true if supported</returns>
        public bool SupportsWaveFormat(SupportedWaveFormat waveFormat)
        {
            return (supportedFormats & waveFormat) == waveFormat;
        }
    }
}
