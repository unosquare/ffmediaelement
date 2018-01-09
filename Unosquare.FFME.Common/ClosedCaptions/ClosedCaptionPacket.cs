namespace Unosquare.FFME.ClosedCaptions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a 3-byte packet of closed-captioning data in EIA-608 format.
    /// See: http://jackyjung.tistory.com/attachment/499e14e28c347DB.pdf
    /// </summary>
    public class ClosedCaptionPacket : IComparable
    {
        #region Dictionaries

        private static readonly Dictionary<byte, string> SpecialNorthAmerican = new Dictionary<byte, string>()
        {
            { 0x30, "®"},
            { 0x31, "°"},
            { 0x32, "½"},
            { 0x33, "¿"},
            { 0x34, "™"},
            { 0x35, "¢"},
            { 0x36, "£"},
            { 0x37, "♪"},
            { 0x38, "à"},
            { 0x39, " "},
            { 0x3A, "è"},
            { 0x3B, "â"},
            { 0x3C, "ê"},
            { 0x3D, "î"},
            { 0x3E, "ô"},
            { 0x3F, "û"},
        };

        private static readonly Dictionary<byte, string> Spanish = new Dictionary<byte, string>()
        {
            { 0x20, "Á"},
            { 0x21, "É"},
            { 0x22, "Ó"},
            { 0x23, "Ú"},
            { 0x24, "Ü"},
            { 0x25, "ü"},
            { 0x26, "´"},
            { 0x27, "¡"},
            { 0x28, "*"},
            { 0x29, "'"},
            { 0x2A, "-"},
            { 0x2B, "©"},
            { 0x2C, "S"},
            { 0x2D, "·"},
            { 0x2E, "\""},
            { 0x2F, "\""},
        };

        private static readonly Dictionary<byte, string> Portuguese = new Dictionary<byte, string>()
        {
            { 0x20, "Á"},
            { 0x21, "ã"},
            { 0x22, "Í"},
            { 0x23, "Ì"},
            { 0x24, "ì"},
            { 0x25, "Ò"},
            { 0x26, "ò"},
            { 0x27, "Õ"},
            { 0x28, "õ"},
            { 0x29, "{"},
            { 0x2A, "}"},
            { 0x2B, "\\"},
            { 0x2C, "^"},
            { 0x2D, "_"},
            { 0x2E, "|"},
            { 0x2F, "~"},
        };

        private static readonly Dictionary<byte, string> French = new Dictionary<byte, string>()
        {
            { 0x30, "À"},
            { 0x31, "Â"},
            { 0x32, "Ç"},
            { 0x33, "È"},
            { 0x34, "Ê"},
            { 0x35, "Ë"},
            { 0x36, "ë"},
            { 0x37, "Î"},
            { 0x38, "Ï"},
            { 0x39, "ï"},
            { 0x3A, "Ô"},
            { 0x3B, "Ù"},
            { 0x3C, "ù"},
            { 0x3D, "Û"},
            { 0x3E, "«"},
            { 0x3F, "»"},
        };

        private static readonly Dictionary<byte, string> German = new Dictionary<byte, string>()
        {
            { 0x30, "Ä"},
            { 0x31, "ä"},
            { 0x32, "Ö"},
            { 0x33, "ö"},
            { 0x34, "ß"},
            { 0x35, "¥"},
            { 0x36, "¤"},
            { 0x37, "¦"},
            { 0x38, "Å"},
            { 0x39, "å"},
            { 0x3A, "Ø"},
            { 0x3B, "ø"},
            { 0x3C, "+"},
            { 0x3D, "+"},
            { 0x3E, "+"},
            { 0x3F, "+"},
        };

        private static readonly Dictionary<byte, int> OddPreambleRows = new Dictionary<byte, int>
        {
            { 0x11, 1 },
            { 0x19, 1 },
            { 0x12, 3 },
            { 0x1A, 3 },
            { 0x15, 5 },
            { 0x1D, 5 },
            { 0x16, 7 },
            { 0x1E, 7 },
            { 0x17, 9 },
            { 0x1F, 9 },
            { 0x10, 11 },
            { 0x18, 11 },
            { 0x13, 13 },
            { 0x1B, 13 },
            { 0x14, 15 },
            { 0x1C, 15 },
        };

        private static readonly Dictionary<byte, int> EvenPreambleRows = new Dictionary<byte, int>
        {
            { 0x11, 2 },
            { 0x19, 2 },
            { 0x12, 4 },
            { 0x1A, 4 },
            { 0x15, 6 },
            { 0x1D, 6 },
            { 0x16, 8 },
            { 0x1E, 8 },
            { 0x17, 10 },
            { 0x1F, 10 },
            { 0x13, 12 },
            { 0x1B, 12 },
            { 0x14, 14 },
            { 0x1C, 14 },
        };

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ClosedCaptionPacket"/> class.
        /// </summary>
        /// <param name="timestamp">The timestamp.</param>
        /// <param name="source">The source.</param>
        /// <param name="offset">The offset.</param>
        public ClosedCaptionPacket(TimeSpan timestamp, byte[] source, int offset)
            : this(timestamp, source[offset + 0], source[offset + 1], source[offset + 2])
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClosedCaptionPacket"/> class.
        /// </summary>
        /// <param name="timestamp">The timestamp.</param>
        /// <param name="header">The header.</param>
        /// <param name="d0">The d0.</param>
        /// <param name="d1">The d1.</param>
        public ClosedCaptionPacket(TimeSpan timestamp, byte header, byte d0, byte d1)
        {
            Data = new byte[] { header, d0, d1 };

            D0 = DropParityBit(d0);
            D1 = DropParityBit(d1);

            NtscField = GetHeaderFieldType(header);
            Channel = 0;
            Timestamp = timestamp;

            #region Header Checking

            if (HeaderHasMarkers(header) == false
                || IsHeaderValidFalgSet(header) == false
                || (NtscField == 0)
                || (D0 == 0x00 && D1 == 0x00))
            {
                PacketType = CCPacketType.NullPad;
                return;
            }

            PacketType = CCPacketType.Unrecognized;

            #endregion

            #region Xds Packet Detection

            // XDS Parsing
            if ((D0 & 0x0F) == D0 && D0 != 0)
            {
                PacketType = CCPacketType.XdsClass;
                XdsClass = (CCXdsClassType)D0;
                return;
            }

            #endregion

            #region Color Command Detection (Table 3)

            if ((D0 == 0x10 || D0 == 0x18) && (D1 >= 0x20 && D1 <= 0x2F))
            {
                Channel = (D0 == 0x10) ? 1 : 2;
                PacketType = CCPacketType.Color;
                Color = (CCColorType)D1;
                return;
            }

            if ((D0 == 0x17 || D0 == 0x1F) && (D1 >= 0x2D && D1 <= 0x2F))
            {
                Channel = (D0 == 0x17) ? 1 : 2;
                PacketType = CCPacketType.Color;
                var colorValue = D1 << 16;
                Color = (CCColorType)colorValue;

                return;
            }

            #endregion

            #region Charset Select Packet Detection (Table 4)

            if ((D0 == 0x17 || D0 == 0x1F) && (D1 >= 0x24 && D1 <= 0x2A))
            {
                Channel = (D0 == 0x17) ? 1 : 2;
                PacketType = CCPacketType.Charset;
                return;
            }

            #endregion

            #region MidRow Code Detection (Table 69)

            // Midrow Code Parsing
            if ((D0 == 0x11 || D0 == 0x19) && (D1 >= 0x20 && D1 <= 0x2F))
            {
                PacketType = CCPacketType.MidRow;
                Channel = D0 == 0x11 ? 1 : 2;
                MidRowStyle = (CCStyleType)D1;
                return;
            }

            #endregion

            #region Misc Command Detection (Table 70)

            // Screen command parsing
            if ((D0 == 0x14 || D0 == 0x1C) && (D1 >= 0x20 && D1 <= 0x2F))
            {
                PacketType = CCPacketType.MiscCommand;
                MiscCommand = (CCMiscCommandType)D1;
                Channel = (D0 == 0x14 || D0 == 0x1C) ? 1 : 2;
                return;
            }

            // Tab command Parsing
            if ((D0 == 0x17 || D0 == 0x1F) && (D1 >= 0x21 && D1 <= 0x23))
            {
                PacketType = CCPacketType.Tabs;
                Tabs = D1 & 0x03;
                Channel = D0 == 0x17 ? 1 : 2;
                return;
            }

            #endregion

            #region Preamble Command Detection (Table 71)

            // Preamble Parsing
            if ((D1 >= 0x40 && D1 <= 0x5F) || (D1 >= 0x60 && D1 <= 0x7F))
            {
                // Row 11 is different -- check for it
                if (D0 == 0x10 || D0 == 0x18)
                {
                    PacketType = CCPacketType.Preamble;
                    Channel = D0 == 0x10 ? 1 : 2;
                    PreambleRow = 11;
                    PreambleStyle = (CCStyleType)(D1 - 0x20);
                    return;
                }

                var wasSet = false;
                if (D0 >= 0x11 && D0 <= 0x17)
                {
                    PacketType = CCPacketType.Preamble;
                    Channel = 1;
                    wasSet = true;
                }

                if (D0 >= 0x19 && D0 <= 0x1F)
                {
                    PacketType = CCPacketType.Preamble;
                    Channel = 2;
                    wasSet = true;
                }

                if (wasSet)
                {
                    if (D1 >= 0x40 && D1 <= 0x5F)
                    {
                        PreambleRow = OddPreambleRows[D0];
                        PreambleStyle = (CCStyleType)(D1 - 0x20);
                    }
                    else
                    {
                        PreambleRow = EvenPreambleRows[D0];
                        PreambleStyle = (CCStyleType)(D1 - 0x40);
                    }

                    return;
                }
            }

            #endregion

            #region Text Parsing (Table 5, 6, 7, 8, 9, 10, and 68 for ASCII Chars)

            PacketType = CCPacketType.Text;

            // Special North American character set
            if ((D0 == 0x11 || D0 == 0x19) && D1 >= 0x30 && D1 <= 0x3F)
            {
                if (SpecialNorthAmerican.ContainsKey(D1))
                {
                    Channel = (D0 == 0x11) ? 1 : 2;
                    Text = SpecialNorthAmerican[D1];
                    return;
                }
            }

            if (D0 == 0x12 || D0 == 0x1A)
            {
                if (Spanish.ContainsKey(D1))
                {
                    Channel = 1;
                    Text = Spanish[D1];
                    return;
                }

                if (French.ContainsKey(D1))
                {
                    Channel = 2;
                    Text = French[D1];
                    return;
                }
            }

            if (D0 == 0x13 || D0 == 0x1B)
            {
                if (Portuguese.ContainsKey(D1))
                {
                    Channel = 1;
                    Text = Portuguese[D1];
                    return;
                }

                if (German.ContainsKey(D1))
                {
                    Channel = 2;
                    Text = German[D1];
                    return;
                }
            }

            // Basic North American character set (2 chars)
            if (D0 >= 0x20 && D0 <= 0x7F && (D1 == 0x00 || (D1 >= 0x20 && D1 <= 0x7F)))
            {
                Channel = 0;
                Text = D1 == 0x00 ? $"{ToEia608Char(D0)}" : $"{ToEia608Char(D0)}{ToEia608Char(D1)}";
                return;
            }

            #endregion

            PacketType = CCPacketType.Unrecognized;
            Channel = 0;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the original packet data.
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// Gets the first of the two-byte packet data
        /// </summary>
        public byte D0
        {
            get { return Data[1]; }
            private set { Data[1] = value; }
        }

        /// <summary>
        /// Gets the second of the two-byte packet data
        /// </summary>
        public byte D1
        {
            get { return Data[2]; }
            private set { Data[2] = value; }
        }

        /// <summary>
        /// Gets the timestamp this packet applies to.
        /// </summary>
        public TimeSpan Timestamp { get; }

        /// <summary>
        /// Gets the NTSC field (1 or 2).
        /// 0 for unknown/null packet
        /// </summary>
        public int NtscField { get; }

        /// <summary>
        /// Gets the channel. 0 for any, 1 or 2 for specific channel toggle.
        /// 0 just means to use what a prior packet had specified.
        /// </summary>
        public int Channel { get; }

        /// <summary>
        /// Gets the type of the packet.
        /// </summary>
        public CCPacketType PacketType { get; }

        /// <summary>
        /// Gets the number of tabs, if the packet type is of Tabs
        /// </summary>
        public int Tabs { get; }

        /// <summary>
        /// Gets the Misc Command, if the packet type is of Misc Command
        /// </summary>
        public CCMiscCommandType MiscCommand { get; }

        /// <summary>
        /// Gets the Color, if the packet type is of Color
        /// </summary>
        public CCColorType Color { get; }

        /// <summary>
        /// Gets the Style, if the packet type is of Mid Row Style
        /// </summary>
        public CCStyleType MidRowStyle { get; }

        /// <summary>
        /// Gets the XDS Class, if the packet type is of XDS
        /// </summary>
        public CCXdsClassType XdsClass { get; }

        /// <summary>
        /// Gets the Preamble Row Number (1 through 15), if the packet type is of Preamble
        /// </summary>
        public int PreambleRow { get; }

        /// <summary>
        /// Gets the Style, if the packet type is of Preamble
        /// </summary>
        public CCStyleType PreambleStyle { get; }

        /// <summary>
        /// Gets the text, if the packet type is of text.
        /// </summary>
        public string Text { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            var output = string.Empty;
            var ts = $"{Timestamp.TotalSeconds:0.000}";
            switch (PacketType)
            {
                case CCPacketType.Charset:
                    output = $"{ts} CHR F{NtscField} C{Channel} - 0x{D1:X}"; break;
                case CCPacketType.Color:
                    output = $"{ts} CLR F{NtscField} C{Channel} - {nameof(Color)}: {Color}"; break;
                case CCPacketType.MiscCommand:
                    output = $"{ts} CMD F{NtscField} C{Channel} - {nameof(MiscCommand)}: {MiscCommand}"; break;
                case CCPacketType.MidRow:
                    output = $"{ts} MDR F{NtscField} C{Channel} - {nameof(MidRowStyle)}: {MidRowStyle}"; break;
                case CCPacketType.NullPad:
                    output = $"{ts} NUL F{NtscField} C{Channel} - (NULL)"; break;
                case CCPacketType.Preamble:
                    output = $"{ts} PRE F{NtscField} C{Channel} - Row: {PreambleRow}, Style: {PreambleStyle}"; break;
                case CCPacketType.Tabs:
                    output = $"{ts} TAB F{NtscField} C{Channel} - {nameof(Tabs)}: {Tabs}"; break;
                case CCPacketType.Text:
                    output = $"{ts} TXT F{NtscField} C{(Channel == 0 ? "A" : Channel.ToString())} - '{Text}'"; break;
                case CCPacketType.XdsClass:
                    output = $"{ts} XDS F{NtscField} C{Channel} - {nameof(XdsClass)}: {XdsClass}"; break;
                default:
                    output = $"{ts} UNR F{NtscField} C{Channel} - 0x{D0:X} 0x{D1:X}"; break;
            }

            return output;
        }

        #endregion

        #region IComparable Support

        /// <summary>
        /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
        /// </summary>
        /// <param name="obj">An object to compare with this instance.</param>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared. The return value has these meanings: Value Meaning Less than zero This instance precedes <paramref name="obj" /> in the sort order. Zero This instance occurs in the same position in the sort order as <paramref name="obj" />. Greater than zero This instance follows <paramref name="obj" /> in the sort order.
        /// </returns>
        public int CompareTo(object obj)
        {
            if (obj is null || obj is ClosedCaptionPacket == false)
                throw new InvalidOperationException("Types must be compatible and non-null.");

            return Timestamp.Ticks.CompareTo((obj as ClosedCaptionPacket).Timestamp.Ticks);
        }

        #endregion

        #region Static Methods

        /// <summary>
        /// Checks that the header byte starts with 11111b (5 ones binary)
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns>If header has markers</returns>
        private static bool HeaderHasMarkers(byte data)
        {
            return (data & 0xF8) == 0xF8;
        }

        /// <summary>
        /// Determines whether the valid flag of the header byte is set.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns>
        ///   <c>true</c> if [is header valid falg set] [the specified data]; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsHeaderValidFalgSet(byte data)
        {
            return (data & 0x04) == 0x04;
        }

        /// <summary>
        /// Gets the NTSC field type (1 or 2).
        /// Returns 0 for unknown.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns>The field type</returns>
        private static int GetHeaderFieldType(byte data)
        {
            if ((data & 0x03) == 2) return 0;
            return (data & 0x03) == 0 ? 1 : 2;
        }

        /// <summary>
        /// Determines whether the data is null padding
        /// </summary>
        /// <param name="d0">The d0.</param>
        /// <param name="d1">The d1.</param>
        /// <returns>
        ///   <c>true</c> if [is empty channel data] [the specified d0]; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsEmptyChannelData(byte d0, byte d1)
        {
            return DropParityBit(d0) == 0 && DropParityBit(d1) == 0;
        }

        /// <summary>
        /// Drops the parity bit from the data byte.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>The byte without a parity bit.</returns>
        private static byte DropParityBit(byte input)
        {
            return (byte)(input & 0x7F);
        }

        /// <summary>
        /// Converst an ASCII character code to an EIA-608 char (in Unicode)
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>The charset char.</returns>
        private static char ToEia608Char(byte input)
        {
            // see: Annex A Character Set Differences, and Table 68
            if (input == 0x2A) return 'á';
            if (input == 0x5C) return 'é';
            if (input == 0x5E) return 'í';
            if (input == 0x5F) return 'ó';
            if (input == 0x60) return 'ú';
            if (input == 0x7B) return 'ç';
            if (input == 0x7C) return '÷';
            if (input == 0x7D) return 'Ñ';
            if (input == 0x7E) return 'ñ';
            if (input == 0x7F) return '█';

            return (char)input;
        }

        #endregion
    }
}
