How to use FFME

In order to use the FFME MediaElement control, you will need to setup a folder with FFmpeg binaries and point to it from your application code.
Here are the steps:

1. You can build your own FFmpeg shared binaries or download a compatible build from the wonderful Zeranoe FFmpeg Builds site: (https://ffmpeg.zeranoe.com/).
2. Your FFmpeg build (see the bin folder) should have 3 exe files and a number of dll files and must match your app's architecture (32-bit or 64-bit). Copy all of them to a folder such as (c:\ffmpeg)
3. Within you application's startup code (Main method), set Unosquare.FFME.Library.FFmpegDirectory = @"path to ffmpeg binaries from the previous step";.
4. Use the FFME MediaElement control as any other WPF control!
   For example: In your MainForm.xaml, add the namespace: xmlns:ffme="clr-namespace:Unosquare.FFME;assembly=ffme.win"
   And then add the FFME control your window: <ffme:MediaElement x:Name="Media" Background="Gray" LoadedBehavior="Play" UnloadedBehavior="Manual" />
   To play files or streams, call the Open method: await Media.Open(new Uri(@"c:\your-file-here"));

Happy coding!
*Mario, Unosquare and the FFME contributors.*