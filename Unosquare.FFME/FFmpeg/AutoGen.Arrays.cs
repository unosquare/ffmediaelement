#pragma warning disable 169
#pragma warning disable 649
namespace FFmpeg.AutoGen
{
    using System;

    internal unsafe struct IntArrayOf3
    {
        public static readonly int Size = 3;
        fixed int _[3];

        public int this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (IntArrayOf3* p = &this) { return p->_[i]; } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (IntArrayOf3* p = &this) { p->_[i] = value; } }
        }
        public int[] ToArray()
        {
            fixed (IntArrayOf3* p = &this) { var a = new int[Size]; for (uint i = 0; i < Size; i++) a[i] = p->_[i]; return a; }
        }
        public void UpdateFrom(int[] array)
        {
            fixed (IntArrayOf3* p = &this) { uint i = 0; foreach (var value in array) { p->_[i++] = value; if (i >= Size) return; } }
        }
        public static implicit operator int[] (IntArrayOf3 @struct) => @struct.ToArray();
    }

    internal unsafe struct BytePointerArrayOf4
    {
        public static readonly int Size = 4;
        byte* _0; byte* _1; byte* _2; byte* _3;

        public byte* this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (byte** p0 = &_0) { return *(p0 + i); } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (byte** p0 = &_0) { *(p0 + i) = value; } }
        }
        public byte*[] ToArray()
        {
            fixed (byte** p0 = &_0) { var a = new byte*[Size]; for (uint i = 0; i < Size; i++) a[i] = *(p0 + i); return a; }
        }
        public void UpdateFrom(byte*[] array)
        {
            fixed (byte** p0 = &_0) { uint i = 0; foreach (var value in array) { *(p0 + i++) = value; if (i >= Size) return; } }
        }
        public static implicit operator byte*[] (BytePointerArrayOf4 @struct) => @struct.ToArray();
    }

    internal unsafe struct IntArrayOf4
    {
        public static readonly int Size = 4;
        fixed int _[4];

        public int this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (IntArrayOf4* p = &this) { return p->_[i]; } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (IntArrayOf4* p = &this) { p->_[i] = value; } }
        }
        public int[] ToArray()
        {
            fixed (IntArrayOf4* p = &this) { var a = new int[Size]; for (uint i = 0; i < Size; i++) a[i] = p->_[i]; return a; }
        }
        public void UpdateFrom(int[] array)
        {
            fixed (IntArrayOf4* p = &this) { uint i = 0; foreach (var value in array) { p->_[i++] = value; if (i >= Size) return; } }
        }
        public static implicit operator int[] (IntArrayOf4 @struct) => @struct.ToArray();
    }

    internal unsafe struct LongArrayOf4
    {
        public static readonly int Size = 4;
        fixed long _[4];

        public long this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (LongArrayOf4* p = &this) { return p->_[i]; } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (LongArrayOf4* p = &this) { p->_[i] = value; } }
        }
        public long[] ToArray()
        {
            fixed (LongArrayOf4* p = &this) { var a = new long[Size]; for (uint i = 0; i < Size; i++) a[i] = p->_[i]; return a; }
        }
        public void UpdateFrom(long[] array)
        {
            fixed (LongArrayOf4* p = &this) { uint i = 0; foreach (var value in array) { p->_[i++] = value; if (i >= Size) return; } }
        }
        public static implicit operator long[] (LongArrayOf4 @struct) => @struct.ToArray();
    }

    internal unsafe struct IntArrayOf5
    {
        public static readonly int Size = 5;
        fixed int _[5];

        public int this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (IntArrayOf5* p = &this) { return p->_[i]; } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (IntArrayOf5* p = &this) { p->_[i] = value; } }
        }
        public int[] ToArray()
        {
            fixed (IntArrayOf5* p = &this) { var a = new int[Size]; for (uint i = 0; i < Size; i++) a[i] = p->_[i]; return a; }
        }
        public void UpdateFrom(int[] array)
        {
            fixed (IntArrayOf5* p = &this) { uint i = 0; foreach (var value in array) { p->_[i++] = value; if (i >= Size) return; } }
        }
        public static implicit operator int[] (IntArrayOf5 @struct) => @struct.ToArray();
    }

    internal unsafe struct AVBufferRefPointerArrayOf8
    {
        public static readonly int Size = 8;
        AVBufferRef* _0; AVBufferRef* _1; AVBufferRef* _2; AVBufferRef* _3; AVBufferRef* _4; AVBufferRef* _5; AVBufferRef* _6; AVBufferRef* _7;

        public AVBufferRef* this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (AVBufferRef** p0 = &_0) { return *(p0 + i); } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (AVBufferRef** p0 = &_0) { *(p0 + i) = value; } }
        }
        public AVBufferRef*[] ToArray()
        {
            fixed (AVBufferRef** p0 = &_0) { var a = new AVBufferRef*[Size]; for (uint i = 0; i < Size; i++) a[i] = *(p0 + i); return a; }
        }
        public void UpdateFrom(AVBufferRef*[] array)
        {
            fixed (AVBufferRef** p0 = &_0) { uint i = 0; foreach (var value in array) { *(p0 + i++) = value; if (i >= Size) return; } }
        }
        public static implicit operator AVBufferRef*[] (AVBufferRefPointerArrayOf8 @struct) => @struct.ToArray();
    }

    internal unsafe struct BytePointerArrayOf8
    {
        public static readonly int Size = 8;
        byte* _0; byte* _1; byte* _2; byte* _3; byte* _4; byte* _5; byte* _6; byte* _7;

        public byte* this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (byte** p0 = &_0) { return *(p0 + i); } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (byte** p0 = &_0) { *(p0 + i) = value; } }
        }
        public byte*[] ToArray()
        {
            fixed (byte** p0 = &_0) { var a = new byte*[Size]; for (uint i = 0; i < Size; i++) a[i] = *(p0 + i); return a; }
        }
        public void UpdateFrom(byte*[] array)
        {
            fixed (byte** p0 = &_0) { uint i = 0; foreach (var value in array) { *(p0 + i++) = value; if (i >= Size) return; } }
        }
        public static implicit operator byte*[] (BytePointerArrayOf8 @struct) => @struct.ToArray();
    }

    internal unsafe struct IntArrayOf8
    {
        public static readonly int Size = 8;
        fixed int _[8];

        public int this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (IntArrayOf8* p = &this) { return p->_[i]; } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (IntArrayOf8* p = &this) { p->_[i] = value; } }
        }
        public int[] ToArray()
        {
            fixed (IntArrayOf8* p = &this) { var a = new int[Size]; for (uint i = 0; i < Size; i++) a[i] = p->_[i]; return a; }
        }
        public void UpdateFrom(int[] array)
        {
            fixed (IntArrayOf8* p = &this) { uint i = 0; foreach (var value in array) { p->_[i++] = value; if (i >= Size) return; } }
        }
        public static implicit operator int[] (IntArrayOf8 @struct) => @struct.ToArray();
    }

    internal unsafe struct UlongArrayOf8
    {
        public static readonly int Size = 8;
        fixed ulong _[8];

        public ulong this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (UlongArrayOf8* p = &this) { return p->_[i]; } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (UlongArrayOf8* p = &this) { p->_[i] = value; } }
        }
        public ulong[] ToArray()
        {
            fixed (UlongArrayOf8* p = &this) { var a = new ulong[Size]; for (uint i = 0; i < Size; i++) a[i] = p->_[i]; return a; }
        }
        public void UpdateFrom(ulong[] array)
        {
            fixed (UlongArrayOf8* p = &this) { uint i = 0; foreach (var value in array) { p->_[i++] = value; if (i >= Size) return; } }
        }
        public static implicit operator ulong[] (UlongArrayOf8 @struct) => @struct.ToArray();
    }

    internal unsafe struct ByteArrayOf17
    {
        public static readonly int Size = 17;
        fixed byte _[17];

        public byte this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (ByteArrayOf17* p = &this) { return p->_[i]; } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (ByteArrayOf17* p = &this) { p->_[i] = value; } }
        }
        public byte[] ToArray()
        {
            fixed (ByteArrayOf17* p = &this) { var a = new byte[Size]; for (uint i = 0; i < Size; i++) a[i] = p->_[i]; return a; }
        }
        public void UpdateFrom(byte[] array)
        {
            fixed (ByteArrayOf17* p = &this) { uint i = 0; foreach (var value in array) { p->_[i++] = value; if (i >= Size) return; } }
        }
        public static implicit operator byte[] (ByteArrayOf17 @struct) => @struct.ToArray();
    }

    internal unsafe struct LongArrayOf17
    {
        public static readonly int Size = 17;
        fixed long _[17];

        public long this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (LongArrayOf17* p = &this) { return p->_[i]; } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (LongArrayOf17* p = &this) { p->_[i] = value; } }
        }
        public long[] ToArray()
        {
            fixed (LongArrayOf17* p = &this) { var a = new long[Size]; for (uint i = 0; i < Size; i++) a[i] = p->_[i]; return a; }
        }
        public void UpdateFrom(long[] array)
        {
            fixed (LongArrayOf17* p = &this) { uint i = 0; foreach (var value in array) { p->_[i++] = value; if (i >= Size) return; } }
        }
        public static implicit operator long[] (LongArrayOf17 @struct) => @struct.ToArray();
    }

    internal unsafe struct ByteArrayOf32
    {
        public static readonly int Size = 32;
        fixed byte _[32];

        public byte this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (ByteArrayOf32* p = &this) { return p->_[i]; } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (ByteArrayOf32* p = &this) { p->_[i] = value; } }
        }
        public byte[] ToArray()
        {
            fixed (ByteArrayOf32* p = &this) { var a = new byte[Size]; for (uint i = 0; i < Size; i++) a[i] = p->_[i]; return a; }
        }
        public void UpdateFrom(byte[] array)
        {
            fixed (ByteArrayOf32* p = &this) { uint i = 0; foreach (var value in array) { p->_[i++] = value; if (i >= Size) return; } }
        }
        public static implicit operator byte[] (ByteArrayOf32 @struct) => @struct.ToArray();
    }

    internal unsafe struct DoubleArrayOf399
    {
        public static readonly int Size = 399;
        fixed double _[399];

        public double this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (DoubleArrayOf399* p = &this) { return p->_[i]; } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (DoubleArrayOf399* p = &this) { p->_[i] = value; } }
        }
        public double[] ToArray()
        {
            fixed (DoubleArrayOf399* p = &this) { var a = new double[Size]; for (uint i = 0; i < Size; i++) a[i] = p->_[i]; return a; }
        }
        public void UpdateFrom(double[] array)
        {
            fixed (DoubleArrayOf399* p = &this) { uint i = 0; foreach (var value in array) { p->_[i++] = value; if (i >= Size) return; } }
        }
        public static implicit operator double[] (DoubleArrayOf399 @struct) => @struct.ToArray();
    }

    internal unsafe struct DoubleArrayOfArrayOf798
    {
        public static readonly int Size = 798;
        DoubleArrayOf399 _0; DoubleArrayOf399 _1; DoubleArrayOf399 _2; DoubleArrayOf399 _3; DoubleArrayOf399 _4; DoubleArrayOf399 _5; DoubleArrayOf399 _6; DoubleArrayOf399 _7; DoubleArrayOf399 _8; DoubleArrayOf399 _9; DoubleArrayOf399 _10; DoubleArrayOf399 _11; DoubleArrayOf399 _12; DoubleArrayOf399 _13; DoubleArrayOf399 _14; DoubleArrayOf399 _15; DoubleArrayOf399 _16; DoubleArrayOf399 _17; DoubleArrayOf399 _18; DoubleArrayOf399 _19; DoubleArrayOf399 _20; DoubleArrayOf399 _21; DoubleArrayOf399 _22; DoubleArrayOf399 _23; DoubleArrayOf399 _24; DoubleArrayOf399 _25; DoubleArrayOf399 _26; DoubleArrayOf399 _27; DoubleArrayOf399 _28; DoubleArrayOf399 _29; DoubleArrayOf399 _30; DoubleArrayOf399 _31; DoubleArrayOf399 _32; DoubleArrayOf399 _33; DoubleArrayOf399 _34; DoubleArrayOf399 _35; DoubleArrayOf399 _36; DoubleArrayOf399 _37; DoubleArrayOf399 _38; DoubleArrayOf399 _39; DoubleArrayOf399 _40; DoubleArrayOf399 _41; DoubleArrayOf399 _42; DoubleArrayOf399 _43; DoubleArrayOf399 _44; DoubleArrayOf399 _45; DoubleArrayOf399 _46; DoubleArrayOf399 _47; DoubleArrayOf399 _48; DoubleArrayOf399 _49; DoubleArrayOf399 _50; DoubleArrayOf399 _51; DoubleArrayOf399 _52; DoubleArrayOf399 _53; DoubleArrayOf399 _54; DoubleArrayOf399 _55; DoubleArrayOf399 _56; DoubleArrayOf399 _57; DoubleArrayOf399 _58; DoubleArrayOf399 _59; DoubleArrayOf399 _60; DoubleArrayOf399 _61; DoubleArrayOf399 _62; DoubleArrayOf399 _63; DoubleArrayOf399 _64; DoubleArrayOf399 _65; DoubleArrayOf399 _66; DoubleArrayOf399 _67; DoubleArrayOf399 _68; DoubleArrayOf399 _69; DoubleArrayOf399 _70; DoubleArrayOf399 _71; DoubleArrayOf399 _72; DoubleArrayOf399 _73; DoubleArrayOf399 _74; DoubleArrayOf399 _75; DoubleArrayOf399 _76; DoubleArrayOf399 _77; DoubleArrayOf399 _78; DoubleArrayOf399 _79; DoubleArrayOf399 _80; DoubleArrayOf399 _81; DoubleArrayOf399 _82; DoubleArrayOf399 _83; DoubleArrayOf399 _84; DoubleArrayOf399 _85; DoubleArrayOf399 _86; DoubleArrayOf399 _87; DoubleArrayOf399 _88; DoubleArrayOf399 _89; DoubleArrayOf399 _90; DoubleArrayOf399 _91; DoubleArrayOf399 _92; DoubleArrayOf399 _93; DoubleArrayOf399 _94; DoubleArrayOf399 _95; DoubleArrayOf399 _96; DoubleArrayOf399 _97; DoubleArrayOf399 _98; DoubleArrayOf399 _99; DoubleArrayOf399 _100; DoubleArrayOf399 _101; DoubleArrayOf399 _102; DoubleArrayOf399 _103; DoubleArrayOf399 _104; DoubleArrayOf399 _105; DoubleArrayOf399 _106; DoubleArrayOf399 _107; DoubleArrayOf399 _108; DoubleArrayOf399 _109; DoubleArrayOf399 _110; DoubleArrayOf399 _111; DoubleArrayOf399 _112; DoubleArrayOf399 _113; DoubleArrayOf399 _114; DoubleArrayOf399 _115; DoubleArrayOf399 _116; DoubleArrayOf399 _117; DoubleArrayOf399 _118; DoubleArrayOf399 _119; DoubleArrayOf399 _120; DoubleArrayOf399 _121; DoubleArrayOf399 _122; DoubleArrayOf399 _123; DoubleArrayOf399 _124; DoubleArrayOf399 _125; DoubleArrayOf399 _126; DoubleArrayOf399 _127; DoubleArrayOf399 _128; DoubleArrayOf399 _129; DoubleArrayOf399 _130; DoubleArrayOf399 _131; DoubleArrayOf399 _132; DoubleArrayOf399 _133; DoubleArrayOf399 _134; DoubleArrayOf399 _135; DoubleArrayOf399 _136; DoubleArrayOf399 _137; DoubleArrayOf399 _138; DoubleArrayOf399 _139; DoubleArrayOf399 _140; DoubleArrayOf399 _141; DoubleArrayOf399 _142; DoubleArrayOf399 _143; DoubleArrayOf399 _144; DoubleArrayOf399 _145; DoubleArrayOf399 _146; DoubleArrayOf399 _147; DoubleArrayOf399 _148; DoubleArrayOf399 _149; DoubleArrayOf399 _150; DoubleArrayOf399 _151; DoubleArrayOf399 _152; DoubleArrayOf399 _153; DoubleArrayOf399 _154; DoubleArrayOf399 _155; DoubleArrayOf399 _156; DoubleArrayOf399 _157; DoubleArrayOf399 _158; DoubleArrayOf399 _159; DoubleArrayOf399 _160; DoubleArrayOf399 _161; DoubleArrayOf399 _162; DoubleArrayOf399 _163; DoubleArrayOf399 _164; DoubleArrayOf399 _165; DoubleArrayOf399 _166; DoubleArrayOf399 _167; DoubleArrayOf399 _168; DoubleArrayOf399 _169; DoubleArrayOf399 _170; DoubleArrayOf399 _171; DoubleArrayOf399 _172; DoubleArrayOf399 _173; DoubleArrayOf399 _174; DoubleArrayOf399 _175; DoubleArrayOf399 _176; DoubleArrayOf399 _177; DoubleArrayOf399 _178; DoubleArrayOf399 _179; DoubleArrayOf399 _180; DoubleArrayOf399 _181; DoubleArrayOf399 _182; DoubleArrayOf399 _183; DoubleArrayOf399 _184; DoubleArrayOf399 _185; DoubleArrayOf399 _186; DoubleArrayOf399 _187; DoubleArrayOf399 _188; DoubleArrayOf399 _189; DoubleArrayOf399 _190; DoubleArrayOf399 _191; DoubleArrayOf399 _192; DoubleArrayOf399 _193; DoubleArrayOf399 _194; DoubleArrayOf399 _195; DoubleArrayOf399 _196; DoubleArrayOf399 _197; DoubleArrayOf399 _198; DoubleArrayOf399 _199; DoubleArrayOf399 _200; DoubleArrayOf399 _201; DoubleArrayOf399 _202; DoubleArrayOf399 _203; DoubleArrayOf399 _204; DoubleArrayOf399 _205; DoubleArrayOf399 _206; DoubleArrayOf399 _207; DoubleArrayOf399 _208; DoubleArrayOf399 _209; DoubleArrayOf399 _210; DoubleArrayOf399 _211; DoubleArrayOf399 _212; DoubleArrayOf399 _213; DoubleArrayOf399 _214; DoubleArrayOf399 _215; DoubleArrayOf399 _216; DoubleArrayOf399 _217; DoubleArrayOf399 _218; DoubleArrayOf399 _219; DoubleArrayOf399 _220; DoubleArrayOf399 _221; DoubleArrayOf399 _222; DoubleArrayOf399 _223; DoubleArrayOf399 _224; DoubleArrayOf399 _225; DoubleArrayOf399 _226; DoubleArrayOf399 _227; DoubleArrayOf399 _228; DoubleArrayOf399 _229; DoubleArrayOf399 _230; DoubleArrayOf399 _231; DoubleArrayOf399 _232; DoubleArrayOf399 _233; DoubleArrayOf399 _234; DoubleArrayOf399 _235; DoubleArrayOf399 _236; DoubleArrayOf399 _237; DoubleArrayOf399 _238; DoubleArrayOf399 _239; DoubleArrayOf399 _240; DoubleArrayOf399 _241; DoubleArrayOf399 _242; DoubleArrayOf399 _243; DoubleArrayOf399 _244; DoubleArrayOf399 _245; DoubleArrayOf399 _246; DoubleArrayOf399 _247; DoubleArrayOf399 _248; DoubleArrayOf399 _249; DoubleArrayOf399 _250; DoubleArrayOf399 _251; DoubleArrayOf399 _252; DoubleArrayOf399 _253; DoubleArrayOf399 _254; DoubleArrayOf399 _255; DoubleArrayOf399 _256; DoubleArrayOf399 _257; DoubleArrayOf399 _258; DoubleArrayOf399 _259; DoubleArrayOf399 _260; DoubleArrayOf399 _261; DoubleArrayOf399 _262; DoubleArrayOf399 _263; DoubleArrayOf399 _264; DoubleArrayOf399 _265; DoubleArrayOf399 _266; DoubleArrayOf399 _267; DoubleArrayOf399 _268; DoubleArrayOf399 _269; DoubleArrayOf399 _270; DoubleArrayOf399 _271; DoubleArrayOf399 _272; DoubleArrayOf399 _273; DoubleArrayOf399 _274; DoubleArrayOf399 _275; DoubleArrayOf399 _276; DoubleArrayOf399 _277; DoubleArrayOf399 _278; DoubleArrayOf399 _279; DoubleArrayOf399 _280; DoubleArrayOf399 _281; DoubleArrayOf399 _282; DoubleArrayOf399 _283; DoubleArrayOf399 _284; DoubleArrayOf399 _285; DoubleArrayOf399 _286; DoubleArrayOf399 _287; DoubleArrayOf399 _288; DoubleArrayOf399 _289; DoubleArrayOf399 _290; DoubleArrayOf399 _291; DoubleArrayOf399 _292; DoubleArrayOf399 _293; DoubleArrayOf399 _294; DoubleArrayOf399 _295; DoubleArrayOf399 _296; DoubleArrayOf399 _297; DoubleArrayOf399 _298; DoubleArrayOf399 _299; DoubleArrayOf399 _300; DoubleArrayOf399 _301; DoubleArrayOf399 _302; DoubleArrayOf399 _303; DoubleArrayOf399 _304; DoubleArrayOf399 _305; DoubleArrayOf399 _306; DoubleArrayOf399 _307; DoubleArrayOf399 _308; DoubleArrayOf399 _309; DoubleArrayOf399 _310; DoubleArrayOf399 _311; DoubleArrayOf399 _312; DoubleArrayOf399 _313; DoubleArrayOf399 _314; DoubleArrayOf399 _315; DoubleArrayOf399 _316; DoubleArrayOf399 _317; DoubleArrayOf399 _318; DoubleArrayOf399 _319; DoubleArrayOf399 _320; DoubleArrayOf399 _321; DoubleArrayOf399 _322; DoubleArrayOf399 _323; DoubleArrayOf399 _324; DoubleArrayOf399 _325; DoubleArrayOf399 _326; DoubleArrayOf399 _327; DoubleArrayOf399 _328; DoubleArrayOf399 _329; DoubleArrayOf399 _330; DoubleArrayOf399 _331; DoubleArrayOf399 _332; DoubleArrayOf399 _333; DoubleArrayOf399 _334; DoubleArrayOf399 _335; DoubleArrayOf399 _336; DoubleArrayOf399 _337; DoubleArrayOf399 _338; DoubleArrayOf399 _339; DoubleArrayOf399 _340; DoubleArrayOf399 _341; DoubleArrayOf399 _342; DoubleArrayOf399 _343; DoubleArrayOf399 _344; DoubleArrayOf399 _345; DoubleArrayOf399 _346; DoubleArrayOf399 _347; DoubleArrayOf399 _348; DoubleArrayOf399 _349; DoubleArrayOf399 _350; DoubleArrayOf399 _351; DoubleArrayOf399 _352; DoubleArrayOf399 _353; DoubleArrayOf399 _354; DoubleArrayOf399 _355; DoubleArrayOf399 _356; DoubleArrayOf399 _357; DoubleArrayOf399 _358; DoubleArrayOf399 _359; DoubleArrayOf399 _360; DoubleArrayOf399 _361; DoubleArrayOf399 _362; DoubleArrayOf399 _363; DoubleArrayOf399 _364; DoubleArrayOf399 _365; DoubleArrayOf399 _366; DoubleArrayOf399 _367; DoubleArrayOf399 _368; DoubleArrayOf399 _369; DoubleArrayOf399 _370; DoubleArrayOf399 _371; DoubleArrayOf399 _372; DoubleArrayOf399 _373; DoubleArrayOf399 _374; DoubleArrayOf399 _375; DoubleArrayOf399 _376; DoubleArrayOf399 _377; DoubleArrayOf399 _378; DoubleArrayOf399 _379; DoubleArrayOf399 _380; DoubleArrayOf399 _381; DoubleArrayOf399 _382; DoubleArrayOf399 _383; DoubleArrayOf399 _384; DoubleArrayOf399 _385; DoubleArrayOf399 _386; DoubleArrayOf399 _387; DoubleArrayOf399 _388; DoubleArrayOf399 _389; DoubleArrayOf399 _390; DoubleArrayOf399 _391; DoubleArrayOf399 _392; DoubleArrayOf399 _393; DoubleArrayOf399 _394; DoubleArrayOf399 _395; DoubleArrayOf399 _396; DoubleArrayOf399 _397; DoubleArrayOf399 _398; DoubleArrayOf399 _399; DoubleArrayOf399 _400; DoubleArrayOf399 _401; DoubleArrayOf399 _402; DoubleArrayOf399 _403; DoubleArrayOf399 _404; DoubleArrayOf399 _405; DoubleArrayOf399 _406; DoubleArrayOf399 _407; DoubleArrayOf399 _408; DoubleArrayOf399 _409; DoubleArrayOf399 _410; DoubleArrayOf399 _411; DoubleArrayOf399 _412; DoubleArrayOf399 _413; DoubleArrayOf399 _414; DoubleArrayOf399 _415; DoubleArrayOf399 _416; DoubleArrayOf399 _417; DoubleArrayOf399 _418; DoubleArrayOf399 _419; DoubleArrayOf399 _420; DoubleArrayOf399 _421; DoubleArrayOf399 _422; DoubleArrayOf399 _423; DoubleArrayOf399 _424; DoubleArrayOf399 _425; DoubleArrayOf399 _426; DoubleArrayOf399 _427; DoubleArrayOf399 _428; DoubleArrayOf399 _429; DoubleArrayOf399 _430; DoubleArrayOf399 _431; DoubleArrayOf399 _432; DoubleArrayOf399 _433; DoubleArrayOf399 _434; DoubleArrayOf399 _435; DoubleArrayOf399 _436; DoubleArrayOf399 _437; DoubleArrayOf399 _438; DoubleArrayOf399 _439; DoubleArrayOf399 _440; DoubleArrayOf399 _441; DoubleArrayOf399 _442; DoubleArrayOf399 _443; DoubleArrayOf399 _444; DoubleArrayOf399 _445; DoubleArrayOf399 _446; DoubleArrayOf399 _447; DoubleArrayOf399 _448; DoubleArrayOf399 _449; DoubleArrayOf399 _450; DoubleArrayOf399 _451; DoubleArrayOf399 _452; DoubleArrayOf399 _453; DoubleArrayOf399 _454; DoubleArrayOf399 _455; DoubleArrayOf399 _456; DoubleArrayOf399 _457; DoubleArrayOf399 _458; DoubleArrayOf399 _459; DoubleArrayOf399 _460; DoubleArrayOf399 _461; DoubleArrayOf399 _462; DoubleArrayOf399 _463; DoubleArrayOf399 _464; DoubleArrayOf399 _465; DoubleArrayOf399 _466; DoubleArrayOf399 _467; DoubleArrayOf399 _468; DoubleArrayOf399 _469; DoubleArrayOf399 _470; DoubleArrayOf399 _471; DoubleArrayOf399 _472; DoubleArrayOf399 _473; DoubleArrayOf399 _474; DoubleArrayOf399 _475; DoubleArrayOf399 _476; DoubleArrayOf399 _477; DoubleArrayOf399 _478; DoubleArrayOf399 _479; DoubleArrayOf399 _480; DoubleArrayOf399 _481; DoubleArrayOf399 _482; DoubleArrayOf399 _483; DoubleArrayOf399 _484; DoubleArrayOf399 _485; DoubleArrayOf399 _486; DoubleArrayOf399 _487; DoubleArrayOf399 _488; DoubleArrayOf399 _489; DoubleArrayOf399 _490; DoubleArrayOf399 _491; DoubleArrayOf399 _492; DoubleArrayOf399 _493; DoubleArrayOf399 _494; DoubleArrayOf399 _495; DoubleArrayOf399 _496; DoubleArrayOf399 _497; DoubleArrayOf399 _498; DoubleArrayOf399 _499; DoubleArrayOf399 _500; DoubleArrayOf399 _501; DoubleArrayOf399 _502; DoubleArrayOf399 _503; DoubleArrayOf399 _504; DoubleArrayOf399 _505; DoubleArrayOf399 _506; DoubleArrayOf399 _507; DoubleArrayOf399 _508; DoubleArrayOf399 _509; DoubleArrayOf399 _510; DoubleArrayOf399 _511; DoubleArrayOf399 _512; DoubleArrayOf399 _513; DoubleArrayOf399 _514; DoubleArrayOf399 _515; DoubleArrayOf399 _516; DoubleArrayOf399 _517; DoubleArrayOf399 _518; DoubleArrayOf399 _519; DoubleArrayOf399 _520; DoubleArrayOf399 _521; DoubleArrayOf399 _522; DoubleArrayOf399 _523; DoubleArrayOf399 _524; DoubleArrayOf399 _525; DoubleArrayOf399 _526; DoubleArrayOf399 _527; DoubleArrayOf399 _528; DoubleArrayOf399 _529; DoubleArrayOf399 _530; DoubleArrayOf399 _531; DoubleArrayOf399 _532; DoubleArrayOf399 _533; DoubleArrayOf399 _534; DoubleArrayOf399 _535; DoubleArrayOf399 _536; DoubleArrayOf399 _537; DoubleArrayOf399 _538; DoubleArrayOf399 _539; DoubleArrayOf399 _540; DoubleArrayOf399 _541; DoubleArrayOf399 _542; DoubleArrayOf399 _543; DoubleArrayOf399 _544; DoubleArrayOf399 _545; DoubleArrayOf399 _546; DoubleArrayOf399 _547; DoubleArrayOf399 _548; DoubleArrayOf399 _549; DoubleArrayOf399 _550; DoubleArrayOf399 _551; DoubleArrayOf399 _552; DoubleArrayOf399 _553; DoubleArrayOf399 _554; DoubleArrayOf399 _555; DoubleArrayOf399 _556; DoubleArrayOf399 _557; DoubleArrayOf399 _558; DoubleArrayOf399 _559; DoubleArrayOf399 _560; DoubleArrayOf399 _561; DoubleArrayOf399 _562; DoubleArrayOf399 _563; DoubleArrayOf399 _564; DoubleArrayOf399 _565; DoubleArrayOf399 _566; DoubleArrayOf399 _567; DoubleArrayOf399 _568; DoubleArrayOf399 _569; DoubleArrayOf399 _570; DoubleArrayOf399 _571; DoubleArrayOf399 _572; DoubleArrayOf399 _573; DoubleArrayOf399 _574; DoubleArrayOf399 _575; DoubleArrayOf399 _576; DoubleArrayOf399 _577; DoubleArrayOf399 _578; DoubleArrayOf399 _579; DoubleArrayOf399 _580; DoubleArrayOf399 _581; DoubleArrayOf399 _582; DoubleArrayOf399 _583; DoubleArrayOf399 _584; DoubleArrayOf399 _585; DoubleArrayOf399 _586; DoubleArrayOf399 _587; DoubleArrayOf399 _588; DoubleArrayOf399 _589; DoubleArrayOf399 _590; DoubleArrayOf399 _591; DoubleArrayOf399 _592; DoubleArrayOf399 _593; DoubleArrayOf399 _594; DoubleArrayOf399 _595; DoubleArrayOf399 _596; DoubleArrayOf399 _597; DoubleArrayOf399 _598; DoubleArrayOf399 _599; DoubleArrayOf399 _600; DoubleArrayOf399 _601; DoubleArrayOf399 _602; DoubleArrayOf399 _603; DoubleArrayOf399 _604; DoubleArrayOf399 _605; DoubleArrayOf399 _606; DoubleArrayOf399 _607; DoubleArrayOf399 _608; DoubleArrayOf399 _609; DoubleArrayOf399 _610; DoubleArrayOf399 _611; DoubleArrayOf399 _612; DoubleArrayOf399 _613; DoubleArrayOf399 _614; DoubleArrayOf399 _615; DoubleArrayOf399 _616; DoubleArrayOf399 _617; DoubleArrayOf399 _618; DoubleArrayOf399 _619; DoubleArrayOf399 _620; DoubleArrayOf399 _621; DoubleArrayOf399 _622; DoubleArrayOf399 _623; DoubleArrayOf399 _624; DoubleArrayOf399 _625; DoubleArrayOf399 _626; DoubleArrayOf399 _627; DoubleArrayOf399 _628; DoubleArrayOf399 _629; DoubleArrayOf399 _630; DoubleArrayOf399 _631; DoubleArrayOf399 _632; DoubleArrayOf399 _633; DoubleArrayOf399 _634; DoubleArrayOf399 _635; DoubleArrayOf399 _636; DoubleArrayOf399 _637; DoubleArrayOf399 _638; DoubleArrayOf399 _639; DoubleArrayOf399 _640; DoubleArrayOf399 _641; DoubleArrayOf399 _642; DoubleArrayOf399 _643; DoubleArrayOf399 _644; DoubleArrayOf399 _645; DoubleArrayOf399 _646; DoubleArrayOf399 _647; DoubleArrayOf399 _648; DoubleArrayOf399 _649; DoubleArrayOf399 _650; DoubleArrayOf399 _651; DoubleArrayOf399 _652; DoubleArrayOf399 _653; DoubleArrayOf399 _654; DoubleArrayOf399 _655; DoubleArrayOf399 _656; DoubleArrayOf399 _657; DoubleArrayOf399 _658; DoubleArrayOf399 _659; DoubleArrayOf399 _660; DoubleArrayOf399 _661; DoubleArrayOf399 _662; DoubleArrayOf399 _663; DoubleArrayOf399 _664; DoubleArrayOf399 _665; DoubleArrayOf399 _666; DoubleArrayOf399 _667; DoubleArrayOf399 _668; DoubleArrayOf399 _669; DoubleArrayOf399 _670; DoubleArrayOf399 _671; DoubleArrayOf399 _672; DoubleArrayOf399 _673; DoubleArrayOf399 _674; DoubleArrayOf399 _675; DoubleArrayOf399 _676; DoubleArrayOf399 _677; DoubleArrayOf399 _678; DoubleArrayOf399 _679; DoubleArrayOf399 _680; DoubleArrayOf399 _681; DoubleArrayOf399 _682; DoubleArrayOf399 _683; DoubleArrayOf399 _684; DoubleArrayOf399 _685; DoubleArrayOf399 _686; DoubleArrayOf399 _687; DoubleArrayOf399 _688; DoubleArrayOf399 _689; DoubleArrayOf399 _690; DoubleArrayOf399 _691; DoubleArrayOf399 _692; DoubleArrayOf399 _693; DoubleArrayOf399 _694; DoubleArrayOf399 _695; DoubleArrayOf399 _696; DoubleArrayOf399 _697; DoubleArrayOf399 _698; DoubleArrayOf399 _699; DoubleArrayOf399 _700; DoubleArrayOf399 _701; DoubleArrayOf399 _702; DoubleArrayOf399 _703; DoubleArrayOf399 _704; DoubleArrayOf399 _705; DoubleArrayOf399 _706; DoubleArrayOf399 _707; DoubleArrayOf399 _708; DoubleArrayOf399 _709; DoubleArrayOf399 _710; DoubleArrayOf399 _711; DoubleArrayOf399 _712; DoubleArrayOf399 _713; DoubleArrayOf399 _714; DoubleArrayOf399 _715; DoubleArrayOf399 _716; DoubleArrayOf399 _717; DoubleArrayOf399 _718; DoubleArrayOf399 _719; DoubleArrayOf399 _720; DoubleArrayOf399 _721; DoubleArrayOf399 _722; DoubleArrayOf399 _723; DoubleArrayOf399 _724; DoubleArrayOf399 _725; DoubleArrayOf399 _726; DoubleArrayOf399 _727; DoubleArrayOf399 _728; DoubleArrayOf399 _729; DoubleArrayOf399 _730; DoubleArrayOf399 _731; DoubleArrayOf399 _732; DoubleArrayOf399 _733; DoubleArrayOf399 _734; DoubleArrayOf399 _735; DoubleArrayOf399 _736; DoubleArrayOf399 _737; DoubleArrayOf399 _738; DoubleArrayOf399 _739; DoubleArrayOf399 _740; DoubleArrayOf399 _741; DoubleArrayOf399 _742; DoubleArrayOf399 _743; DoubleArrayOf399 _744; DoubleArrayOf399 _745; DoubleArrayOf399 _746; DoubleArrayOf399 _747; DoubleArrayOf399 _748; DoubleArrayOf399 _749; DoubleArrayOf399 _750; DoubleArrayOf399 _751; DoubleArrayOf399 _752; DoubleArrayOf399 _753; DoubleArrayOf399 _754; DoubleArrayOf399 _755; DoubleArrayOf399 _756; DoubleArrayOf399 _757; DoubleArrayOf399 _758; DoubleArrayOf399 _759; DoubleArrayOf399 _760; DoubleArrayOf399 _761; DoubleArrayOf399 _762; DoubleArrayOf399 _763; DoubleArrayOf399 _764; DoubleArrayOf399 _765; DoubleArrayOf399 _766; DoubleArrayOf399 _767; DoubleArrayOf399 _768; DoubleArrayOf399 _769; DoubleArrayOf399 _770; DoubleArrayOf399 _771; DoubleArrayOf399 _772; DoubleArrayOf399 _773; DoubleArrayOf399 _774; DoubleArrayOf399 _775; DoubleArrayOf399 _776; DoubleArrayOf399 _777; DoubleArrayOf399 _778; DoubleArrayOf399 _779; DoubleArrayOf399 _780; DoubleArrayOf399 _781; DoubleArrayOf399 _782; DoubleArrayOf399 _783; DoubleArrayOf399 _784; DoubleArrayOf399 _785; DoubleArrayOf399 _786; DoubleArrayOf399 _787; DoubleArrayOf399 _788; DoubleArrayOf399 _789; DoubleArrayOf399 _790; DoubleArrayOf399 _791; DoubleArrayOf399 _792; DoubleArrayOf399 _793; DoubleArrayOf399 _794; DoubleArrayOf399 _795; DoubleArrayOf399 _796; DoubleArrayOf399 _797;

        public DoubleArrayOf399 this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (DoubleArrayOf399* p0 = &_0) { return *(p0 + i); } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (DoubleArrayOf399* p0 = &_0) { *(p0 + i) = value; } }
        }
        public DoubleArrayOf399[] ToArray()
        {
            fixed (DoubleArrayOf399* p0 = &_0) { var a = new DoubleArrayOf399[Size]; for (uint i = 0; i < Size; i++) a[i] = *(p0 + i); return a; }
        }
        public void UpdateFrom(DoubleArrayOf399[] array)
        {
            fixed (DoubleArrayOf399* p0 = &_0) { uint i = 0; foreach (var value in array) { *(p0 + i++) = value; if (i >= Size) return; } }
        }
        public static implicit operator DoubleArrayOf399[] (DoubleArrayOfArrayOf798 @struct) => @struct.ToArray();
    }

    internal unsafe struct ByteArrayOf1024
    {
        public static readonly int Size = 1024;
        fixed byte _[1024];

        public byte this[uint i]
        {
            get { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (ByteArrayOf1024* p = &this) { return p->_[i]; } }
            set { if (i > Size) throw new ArgumentOutOfRangeException(); fixed (ByteArrayOf1024* p = &this) { p->_[i] = value; } }
        }
        public byte[] ToArray()
        {
            fixed (ByteArrayOf1024* p = &this) { var a = new byte[Size]; for (uint i = 0; i < Size; i++) a[i] = p->_[i]; return a; }
        }
        public void UpdateFrom(byte[] array)
        {
            fixed (ByteArrayOf1024* p = &this) { uint i = 0; foreach (var value in array) { p->_[i++] = value; if (i >= Size) return; } }
        }
        public static implicit operator byte[] (ByteArrayOf1024 @struct) => @struct.ToArray();
    }

}

#pragma warning restore 649
#pragma warning restore 169