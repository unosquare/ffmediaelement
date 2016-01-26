FFmpeg binaries not dirstributed with source code.
Please obtain the binaries from:
http://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-2.8.2-win32-shared.7z

This folder must contain the following files:
ffmpeg.exe
ffplay.exe
ffprobe.exe
avcodec-56.dll
avdevice-56.dll
avfilter-5.dll
avformat-56.dll
avutil-54.dll
postproc-53.dll
swresample-1.dll
swscale-3.dll

Make sure you include them in the solution and under Properties set:
Build Action = Embedded Resource
Copy to Output Directory = Do not copy