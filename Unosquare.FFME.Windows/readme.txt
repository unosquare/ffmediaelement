How to use FFME
In order to use the FFME MediaElement control, you will need to setup a folder with FFmpeg binaries. Here are the steps:

1. You can build your own FFmpeg or download a compatible build from the wonderful Zeranoe FFmpeg Builds site: (http://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.4-win32-shared.zip).
2. Your FFmpeg build (see the bin folder) should have 3 exe files and 8 dll files. Copy all 11 files to a folder such as c:\ffmpeg
3. Within you application's startup code (Main method), set the following static property Unosquare.FFME.MediaElement.FFmpegDirectory = "c:\ffmpeg". The path you set it to must be that where you will keep the FFmpeg binaries.
4. Use the FFME MediaElement control as any other WPF control!

Happy coding!
- Mario, Unosquare and the FFME contributors.
