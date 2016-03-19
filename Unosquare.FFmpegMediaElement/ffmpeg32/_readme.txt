FFmpeg binaries not dirstributed with source code.
Please obtain the binaries from:
https://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.0-win32-shared.7z

This folder must contain the following files:
ffmpeg.exe
ffplay.exe
ffprobe.exe
avcodec-57.dll
avdevice-57.dll
avfilter-6.dll
avformat-57.dll
avutil-55.dll
postproc-54.dll
swresample-2.dll
swscale-4.dll

Make sure you include them in the solution and under Properties set:
Build Action = Embedded Resource
Copy to Output Directory = Do not copy