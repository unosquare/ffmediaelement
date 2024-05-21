namespace FFmpeg;

using FFmpeg.AutoGen;
using System;
using System.Runtime.InteropServices;
using Unosquare.FFME;

internal unsafe sealed class FFBPrint : IDisposable
{
    private static readonly nint ReservedFieldOffset = sizeof(nint) + (3 * sizeof(uint));

    public FFBPrint()
    {
        Update(AllocateAutoAVBPrint());
    }

    public AVBPrint* Target { get; private set; }

    public string Contents
    {
        get
        {
            if (Target is null)
                return string.Empty;

            var bpStruct = Marshal.PtrToStructure<AVBPrintExtended>(new IntPtr(Target));
            return Utilities.PtrToStringUTF8(bpStruct.str) ?? string.Empty;
        }
    }

    public unsafe void Dispose()
    {
        var bpStruct = Marshal.PtrToStructure<AVBPrintExtended>((nint)Target);

        var isAllocated = Target + ReservedFieldOffset != bpStruct.str;

        if (isAllocated)
            ffmpeg.av_freep(&bpStruct.str);

        var target = Target;
        ffmpeg.av_freep(&target);
        Update(null);
    }

    private static unsafe AVBPrint* AllocateAutoAVBPrint()
    {
        // https://ffmpeg.org/doxygen/1.0/bprint_8h-source.html
        const int StructurePadding = 1024;
        var bpStructAddress = ffmpeg.av_mallocz(StructurePadding);
        var bStruct = default(AVBPrintExtended);

        bStruct.len = 0;
        bStruct.size = 1;
        bStruct.size_max = uint.MaxValue - 1;
        bStruct.reserved_internal_buffer = 0;

        // point at the address of the reserved_internal_buffer
        bStruct.str = (byte*)((nint)bpStructAddress + ReservedFieldOffset);

        Marshal.StructureToPtr(bStruct, (nint)bpStructAddress, true);
        return (AVBPrint*)bpStructAddress;
    }

    private unsafe void Update(AVBPrint* target)
    {
        Target = target;
    }

#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1310 // Field names should not contain underscore
    [StructLayout(LayoutKind.Sequential)]
    private struct AVBPrintExtended
    {
        public byte* str;
        public uint len;
        public uint size;
        public uint size_max;
        public byte reserved_internal_buffer;
    }
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
}