Instructions

In order to use the FFME you need to provide a folder with ffmpeg files.

1 - Download the FFmpeg win32-shared binaries from Zeranoe FFmpeg Builds (http://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.4-win32-shared.zip).
2 - Extract the contents of the zip file you just downloaded and go to the bin folder that got extracted. You should see 3 exe files and 8 dll files. Select and copy all of them.
3 - Now paste all 11 files from the prior step onto a well-known folder. Take note of the full path. (I used c:\ffmpeg)
4 - At your program bootstrap (Main method), call to method Unosquare.FFME.Core.Utils.RegisterFFmpeg(string overridePath) using the previous path.