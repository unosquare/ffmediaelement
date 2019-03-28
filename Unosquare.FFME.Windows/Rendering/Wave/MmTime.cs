namespace Unosquare.FFME.Rendering.Wave
{
    using System;
    using System.Runtime.InteropServices;

    /// <summary>
    /// http://msdn.microsoft.com/en-us/library/dd757347(v=VS.85).aspx.
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct MmTime : IEquatable<MmTime>
    {
        public const int TimeMs = 0x0001;
        public const int TimeSamples = 0x0002;
        public const int TimeBytes = 0x0004;

        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(4)]
        public uint Ms;

        [FieldOffset(4)]
        public uint Sample;

        [FieldOffset(4)]
        public uint CB;

        [FieldOffset(4)]
        public uint Ticks;

        [FieldOffset(4)]
        public byte SmpteHour;

        [FieldOffset(5)]
        public byte SmpteMin;

        [FieldOffset(6)]
        public byte SmpteSec;

        [FieldOffset(7)]
        public byte SmpteFrame;

        [FieldOffset(8)]
        public byte SmpteFps;

        [FieldOffset(9)]
        public byte SmpteDummy;

        [FieldOffset(10)]
        public byte SmptePad0;

        [FieldOffset(11)]
        public byte SmptePad1;

        [FieldOffset(4)]
        public uint MidiSongPtrPos;

        /// <inheritdoc />
        public bool Equals(MmTime other)
        {
            return Type == other.Type &&
                Ms == other.Ms &&
                Sample == other.Sample &&
                CB == other.CB &&
                Ticks == other.Ticks;
        }

        /// <inheritdoc />
        public override bool Equals(object obj) =>
            obj is MmTime && Equals((MmTime)obj);

        /// <inheritdoc />
        public override int GetHashCode() =>
            throw new NotSupportedException($"{nameof(MmTime)} does not support hashing.");
    }
}
