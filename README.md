# FFME: *WPF MediaElement Alternative*
[![Analytics](https://ga-beacon.appspot.com/UA-8535255-2/unosquare/ffmediaelement/)](https://github.com/igrigorik/ga-beacon)

*:star:Please star this project if you find it useful!*

- MediaElement Status: Still Work in progress (see the Releases section for a code-complete version)
- Estimated Beta Release Date: 7/2/2017
- MediaElement Codename: Michelob (because it uses very little CPU and RAM!)
- FFmpeg Version: <a href="https://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.2.4-win32-shared.zip">3.2.4</a>
- For a history of commits see the repo: https://github.com/unosquare/ffplaydotnet

## Features Overview
FFME is a close (and I'd like to think better) drop-in replacement for <a href="https://msdn.microsoft.com/en-us/library/system.windows.controls.mediaelement(v=vs.110).aspx">Microsoft's WPF MediaElement Control</a>. While the standard MediaElement uses DirectX (DirectShow) for media playback, FFME uses <a href="http://ffmpeg.org/">FFmpeg</a> to read and decode audio and video. This means that for those of you who want to support stuff like HLS playback, or just don't want to go through the hassle of installing codecs on client machines, using FFME *might* be the answer. 

FFME provides multiple improvements over the standard MediaElement such as:
- Asynchronous and synchronous frame scrubbing
- Fast media seeking and frame-by-frame seeking
- Properties such as Position, NaturalDuration, SpeedRatio, and Volume are all Dependency Properties!
- Additional and extended media events.
- Ability to easily apply filtergraphs.
- Ability to extract media metadata and tech specs of a media stream (title, album, bitrate, FPS, etc).

*... all in a single MediaElement control*

### Known Limitations
*Your help is welcome!*

- I still have a lot of TODOs in this new version -- but I am getting through them :)
- SpeedRatio other than 1.0 could adjust audio pitch. Currently, if SpeedRatio is less than 1.0, there is no audio playback, and if SpeedRatio is greater than 1.0, samples just play faster. This situation can be greatly improved by manually manipulating the samples passed to the Audio Renderer.
- (*Nice to have*) It would be nice to implement a method on the control that is able to extract a copy of the current video frame.
- There currently is no support for opening capture devices such as webcams or TV cards. While this is not too hard to do, it is not (yet) implemented in this library.

## Compiling, Running and Testing
*Please note that I am unable to distribute FFmpeg's binaries because I don't know if I am allowed to do so. Follow the instructions below to compile, run and test FFME*

1. Clone this repository.
2. Download the FFmpeg win32-shared binaries from <a href="https://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.2.4-win32-shared.zip">Zeranoe FFmpeg Builds</a>.
3. Extract the contents of the <code>zip</code> file you just downloaded and go to the <code>bin</code> folder that got extracted. You should see 3 <code>exe</code> files and 8 <code>dll</code> files. Select and copy all of them.
4. Now paste all 11 files from the prior step onto a well-known foleder. Take note of the full path.
5. Open the solution and set the <code>Unosquare.FFME.Sample</code> project as the startup project. You can do this by right clicking on the project and selecting <code>Set as startup project</code>
6. Under the <code>Unosquare.FFME.Sample</code> project, locate the line <code>Unosquare.FFME.MediaElement.FFmpegDirectory = @"C:\ffmpeg";</code> and replace the path so that it points to the folder where you extracted your FFmpeg binaries (dll files).
7. Click on <code>Start</code> to run the project.
8. You should see a very simplistic media player. Enter a URL or a path to a file in the textbox at the bottom of the window and then click on <code>Open</code>.
9. The file or URL should play immediately, and all the properties should display to the right of the media display.
10. You can use the resulting compiled assembly in your project without further dependencies as FFME is entirely self-contained. The locations of the compiled FFME assembly, depending on your build configuration are either <code>...\ffmediaelement\Unosquare.FFME\bin\Debug\Unosquare.FFME.dll</code> or <code>...\ffmediaelement\Unosquare.FFME\bin\Release\Unosquare.FFME.dll</code>

## Using FFME in your Project
*The Unosquare.FFME.Sample provides a reference implementation of usage*

1. Create a new WPF application
2. Add a reference to <code>Unosquare.FFME.dll</code>
3. In your <code>MainForm.xaml</code>, add the namespace: <code>xmlns:ffme="clr-namespace:Unosquare.FFME;assembly=Unosquare.FFME"</code>
4. Finally, create an instance of the FFME control in your <code>MainForm.xaml</code> as follows: `<ffme:MediaElement x:Name="MediaEl" Background="Gray" LoadedBehavior="Play" UnloadedBehavior="Manual" />`

## Thanks
*In no particular order*

- To the <a href="http://ffmpeg.org/">FFmpeg team</a> for making the Swiss Army Knife of media. I encourage to donate to them.
- To Kyle Schwarz for creating and making <a href="http://ffmpeg.zeranoe.com/builds/">Zeranoe FFmpeg builds available to everyone</a>.
- To the <a href="https://github.com/naudio/NAudio">NAudio</a> team for making the best audio library out there for .NET -- one day I will contribut improvements that they really need :P
- To Ruslan Balanukhin for his FFmpeg interop bindings generator tool: <a href="https://github.com/Ruslan-B/FFmpeg.AutoGen">FFmpeg.AutoGen</a>.
- To Martin Bohme for his <a href="http://dranger.com/ffmpeg/">excellent tutorial</a> on creating a video player with FFmpeg.
- To Barry Mieny for his beautiful <a href="http://barrymieny.deviantart.com/art/isabi4-for-Windows-105473723">FFmpeg logo</a>

## Similar Projects
- <a href="https://github.com/Microsoft/FFmpegInterop">Microsoft FFmpeg Interop</a>
- <a href="https://github.com/Sascha-L/WPF-MediaKit">WPF-MediaKit</a>
- <a href="https://libvlcnet.codeplex.com/">LibVLC.NET</a>
- <a href="http://playerframework.codeplex.com/">Microsoft Player Framework</a>

## License
- The NAudio portion of this library <code>Unosquare.FFME\NAudio</code> is distributed under Ms-PL. Please see <a href="https://github.com/unosquare/ffmediaelement/blob/master/Unosquare.FFME/NAudio/LICENSE.txt">LICENSE.txt</a>.
- Code generated by the FFmpeg.Autogen tool <code>Unosquare.FFME\FFmpeg.Autogen</code> does not specify a license by the tool's author.
- The source code of the FFME project excluding the items above is distributed under the Ms-PL.
