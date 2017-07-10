using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unosquare.FFME.Core
{

    public enum ScreenCommandType
    {
        None = 0,
        Resume = 0x20,
        Backspace = 0x21,
        AlarmOff = 0x22,
        AlarmOn = 0x23,
        ClearLine = 0x24,
        RollUp2 = 0x25,
        RollUp3 = 0x26,
        RollUp4 = 0x27,
        StartCaption = 0x29,
        StarNonCaption = 0x2A,
        ResumeNonCaption = 0x2B,
        ClearScreen = 0x2C,
        NewLine = 0x2D,
        ClearBuffer = 0x2E,
        EndCaption = 0x2F
    }

    public enum CommandType
    {
        None,
        Style,
        Cursor,
        Screen,
        Tab,
    }

    internal class Eia608Data
    { 

        private readonly byte[] D = new byte[2];

        #region Dictionaries

        static private readonly Dictionary<byte, ScreenCommandType> ScreenCommandTypes = 
            Enum.GetValues(typeof(ScreenCommandType)).Cast<ScreenCommandType>()
                .Where(k => k != ScreenCommandType.None)
                .ToDictionary(k => (byte)k, v => v);

        static private readonly Dictionary<byte, string> SpecialNorthAmerican = new Dictionary<byte, string>()
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

        static private readonly Dictionary<byte, string> Spanish = new Dictionary<byte, string>()
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

        static private readonly Dictionary<byte, string> Portuguese = new Dictionary<byte, string>()
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

        static private readonly Dictionary<byte, string> French = new Dictionary<byte, string>()
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

        static private readonly Dictionary<byte, string> German = new Dictionary<byte, string>()
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

        #endregion

        public Eia608Data(int field, byte d0, byte d1)
        {
            D0 = DropParityBit(d0);
            D1 = DropParityBit(d1);

            Field = field;
            Channel = -1;

            #region Command Parsing

            // Screen command parsing
            if ((D0 == 0x14 || D0 == 0x1C || D0 == 0x15 || D0 == 0x1D) && ScreenCommandTypes.ContainsKey(D1))
            {
                IsCommand = true;
                CommandType = CommandType.Screen;
                ScreenCommandType = ScreenCommandTypes[D1];
                Channel = (D0 == 0x14 || D0 == 0x1C) ? 0 : 1;
                return;
            }

            // Tab command Parsing
            if ((D0 == 0x17 || D0 == 0x1F) && (D1 >= 0x21 && D1 <= 0x23)) 
            {
                IsCommand = true;
                CommandType = CommandType.Tab;
                CommandTabs = D1 & 0x08;
                Channel = D0 == 0x17 ? 0 : 1;
                return;
            }

            // Style and cursor commands
            if ((D0 & 0x10) == 0x10 && (D1 & 0x40) == 0x40)
            {
                // TODO this needs work.
                IsCommand = true;
                CommandType = CommandType.Cursor;
                Channel = (D0 & 0x08) == 0 ? 0 : 1; // bit 11 is always the channel bit
                return;
            }

            #endregion

            // Basic North American character set
            if ((D0 & 0x40) != 0 || (D0 & 0x20) != 0)
            {
                Channel = 0;
                // Drop the parity bit
                D0 = AdjustAscii(D0);
                D1 = AdjustAscii(D1);

                Text = Encoding.ASCII.GetString(D);
            }
            // Special North American character set
            else if ((D0 == 0x11 || D0 == 0x19) && D1 >= 0x30 && D1 <= 0x3F)
            {
                Channel = (D0 == 0x11) ? 0 : 1;
                if (SpecialNorthAmerican.ContainsKey(D1)) Text = SpecialNorthAmerican[D1];
            }
            else if (D0 == 0x12 || D0 == 0x1A)
            {
                if (Spanish.ContainsKey(D1)) { Channel = 0; Text = Spanish[D1]; }
                if (French.ContainsKey(D1)) { Channel = 1; Text = French[D1]; }
            }
            else if (D0 == 0x13 || D0 == 0x1B)
            {
                if (Portuguese.ContainsKey(D1)) { Channel = 0; Text = Portuguese[D1]; }
                if (German.ContainsKey(D1)) { Channel = 1; Text = German[D1]; }
            }

        }

        public bool IsCommand { get; }

        public CommandType CommandType { get; }

        public int CommandTabs { get; }

        public ScreenCommandType ScreenCommandType { get; }

        public int CommandRow { get; }

        private byte DropParityBit(byte input)
        {
            return (byte)(input & 0x7F);
        }

        private byte AdjustAscii(byte input)
        {
            if (input < 123) return input; // regular, unadjusted
            if (input > 127) return 63; // ?

            switch (input)
            {
                case 123: return 231;
                case 124: return 247;
                case 125: return 209;
                case 126: return 241;
                case 127: return 219;
            }

            return input;
        }

        public byte D0 { get { return D[0]; } private set { D[0] = value; } }
        public byte D1 { get { return D[1]; } private set { D[1] = value; } }

        public int Channel { get; }

        public bool IsXds { get { return (D0 & 0x0F) == D0 && D0 != 0 && (D1 & 0x0F) == D1 && D1 != 0; } }

        public int XdsClass { get { return IsXds ? D0 : -1; } }

        public int XdsType { get { return IsXds ? D1 : -1; } }

        public bool IsText { get { return Text != null; } }

        public string Text { get; }

        public int Field { get; }

        public override string ToString()
        {
            if (IsXds)
                return $"XDS Class: {XdsClass}, Type: {XdsType}";

            if (IsText)
                return $"TXT C{Channel}, Text: '{Text}'";

            if (IsCommand)
            {
                switch (CommandType)
                {
                    case CommandType.Screen: return $"CMD C{Channel}, Type: {CommandType}, Op: {ScreenCommandType}";
                    case CommandType.Tab: return $"CMD C{Channel}, Type: {CommandType}, Tabs: {CommandTabs}";
                    case CommandType.Style: return $"CMD C{Channel}, Type: {CommandType}"; // TODO: add style info
                    case CommandType.Cursor: return $"CMD C{Channel}, Type: {CommandType}"; // TODO: add cusrsor info
                    default: return $"CMD C{Channel}, Type: {CommandType}";
                }

            }

            return $"NA {BitConverter.ToString(D).Replace("-", " ")}";
        }
    }
}
