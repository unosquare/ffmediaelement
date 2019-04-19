Here is a quick guide on how to get started.
1. Open Visual Studio (v2019 recommended), and create a new WPF Application. Target Framework must be 4.6.1 or above.
2. Install the NuGet Package from your Package Manager Console: `PM> Install-Package FFME.Windows`
3. You need FFmpeg **shared** binaries (64 or 32 bit, depending on your app's target architecture). Build your own or download a compatible build from [Zeranoe FFmpeg Builds site](https://ffmpeg.zeranoe.com/builds/).
4. Your FFmpeg build should have a `bin` folder with 3 exe files and some dll files. Copy all those files to a folder such as `c:\ffmpeg`
5. Within you application's startup code (`Main` method), set `Unosquare.FFME.Library.FFmpegDirectory = @"c:\ffmpeg";`.
6. Use the FFME `MediaElement` control as any other WPF control.
For example: In your `MainForm.xaml`, add the namespace: `xmlns:ffme="clr-namespace:Unosquare.FFME;assembly=ffme.win"` and then add the FFME control your window's XAML: `<ffme:MediaElement x:Name="Media" Background="Gray" LoadedBehavior="Play" UnloadedBehavior="Manual" />` 
7. To play files or streams, simply set the `Source` property: `Media.Source = new Uri(@"c:\your-file-here");`. Since `Source` is a dependency property, it needs to be set from the GUI thread.
