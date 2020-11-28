Here is a quick guide on how to get started.
1. Open Visual Studio (v2019 recommended), and create a new WPF Application. Target Framework must be .Net 5.0 or above.
2. Install the NuGet Package from your Package Manager Console: `PM> Install-Package FFME.Windows`
3. You need FFmpeg **shared** binaries (64 or 32 bit, depending on your app's target architecture). Build your own or download a compatible build from [FFmpeg Windows Downloads](https://ffmpeg.org/download.html).
4. Your FFmpeg build should have a `bin` folder with 3 exe files and some dll files. Copy all those files to a folder such as `c:\ffmpeg`
5. Within you application's startup code (`Main` method), set `Unosquare.FFME.Library.FFmpegDirectory = @"c:\ffmpeg";`.
6. Use the FFME `MediaElement` control as any other WPF control.
For example: In your `MainForm.xaml`, add the namespace: `xmlns:ffme="clr-namespace:Unosquare.FFME;assembly=ffme.win"` and then add the FFME control your window's XAML: `<ffme:MediaElement x:Name="Media" Background="Gray" LoadedBehavior="Play" UnloadedBehavior="Manual" />` 
7. To play files or streams, simply call the asynchronous `Open` method: `await Media.Open(new Uri(@"c:\your-file-here"));`.
