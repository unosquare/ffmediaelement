namespace Unosquare.FFME
{
    using System;
    using System.Runtime.CompilerServices;

    public static partial class Utilities
    {
        /// <summary>
        /// Gets the audio sample amplitude (absolute value of the sample).
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The sample amplitude.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short GetAudioSampleAmplitude(this byte[] buffer, int offset)
        {
            var value = buffer.GetAudioSample(offset);
            return value == short.MinValue ? short.MaxValue : Math.Abs(value);
        }

        /// <summary>
        /// Gets the audio sample level for 0 to 1.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The amplitude level.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetAudioSampleLevel(this byte[] buffer, int offset)
        {
            return buffer.GetAudioSampleAmplitude(offset) / Convert.ToDouble(short.MaxValue);
        }

        /// <summary>
        /// Puts a short value in the target buffer as bytes.
        /// </summary>
        /// <param name="buffer">The target.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="value">The value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void PutAudioSample(this byte[] buffer, int offset, short value)
        {
            if (BitConverter.IsLittleEndian)
            {
                buffer[offset] = (byte)(value & 0x00ff); // set the LSB
                buffer[offset + 1] = (byte)(value >> 8); // set the MSB
                return;
            }

            buffer[offset] = (byte)(value >> 8); // set the MSB
            buffer[offset + 1] = (byte)(value & 0x00ff); // set the LSB
        }

        /// <summary>
        /// Gets the a signed 16 bit integer at the given offset.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="offset">The offset.</param>
        /// <returns>The signed integer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static short GetAudioSample(this byte[] buffer, int offset) =>
            BitConverter.ToInt16(buffer, offset);
    }
}
