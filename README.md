# FFME: *WPF MediaElement Alternative*
[![Analytics](https://ga-beacon.appspot.com/UA-8535255-2/unosquare/ffmediaelement/)](https://github.com/igrigorik/ga-beacon)
[![NuGet version](https://badge.fury.io/nu/FFME.Windows.svg)](https://badge.fury.io/nu/FFME.Windows)
[![Build status](https://ci.appveyor.com/api/projects/status/ppqeayanucj1hadj?svg=true)](https://ci.appveyor.com/project/geoperez/ffmediaelement)

*:star:Please star this project if you find it useful!*

![ffmeplay](https://github.com/unosquare/ffmediaelement/raw/master/ffmeplay.png)

- Current Status: (2018-01-06) - 2.0, codenamed Michelob is now in beta 7 (see the <a href="https://github.com/unosquare/ffmediaelement/releases">Releases</a>)
- NuGet Package now available: https://www.nuget.org/packages/FFME.Windows/
- FFmpeg Version: <a href="http://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.4-win32-shared.zip">3.4.0 (32-bit)</a>
- For a history of commits see the repo: https://github.com/unosquare/ffplaydotnet

## Features Overview
FFME is a close (and I'd like to think better) drop-in replacement for <a href="https://msdn.microsoft.com/en-us/library/system.windows.controls.mediaelement(v=vs.110).aspx">Microsoft's WPF MediaElement Control</a>. While the standard MediaElement uses DirectX (DirectShow) for media playback, FFME uses <a href="http://ffmpeg.org/">FFmpeg</a> to read and decode audio and video. This means that for those of you who want to support stuff like HLS playback, or just don't want to go through the hassle of installing codecs on client machines, using FFME *might* just be the answer. 

FFME provides multiple improvements over the standard MediaElement such as:
- Fast media seeking and frame-by-frame seeking
- Properties such as Position, NaturalDuration, SpeedRatio, and Volume are all Dependency Properties!
- Additional and extended media events. Extracting (and modifying) video, audio and subtitle frames is very easy.
- Ability to easily apply FFmpeg video and audio filtergraphs.
- Ability to extract media metadata and tech specs of a media stream (title, album, bitrate, codecs, FPS, etc).
- Ability to apply volume, balance and speed ratio to media playback.

*... all in a single MediaElement control*

### Known Limitations
*Your help is welcome!*

- I still have some items I need to address. See the issues section.
- Working on Hardware acceleration D3D 9 and D3D 11
- There currently is no support for opening capture devices such as webcams or TV cards. While this is not too hard to do, it is not (yet) implemented in this library. See issue #48

## Windows: Compiling, Running and Testing

*Please note that I am unable to distribute FFmpeg's binaries because I don't know if I am allowed to do so. Follow the instructions below to compile, run and test FFME. I will look into releasing a NuGet package. See issue #1*

1. Clone this repository.
2. Download the FFmpeg win32-shared binaries from <a href="http://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.4-win32-shared.zip">Zeranoe FFmpeg Builds</a>.
3. Extract the contents of the <code>zip</code> file you just downloaded and go to the <code>bin</code> folder that got extracted. You should see 3 <code>exe</code> files and 8 <code>dll</code> files. Select and copy all of them.
4. Now paste all 11 files from the prior step onto a well-known folder. Take note of the full path. (I used c:\ffmpeg\)
5. Open the solution and set the <code>Unosquare.FFME.Windows.Sample</code> project as the startup project. You can do this by right clicking on the project and selecting <code>Set as startup project</code>
6. Under the <code>Unosquare.FFME.Windows.Sample</code> project, locate the line <code>public string FFmpegPath { get; set; } = @"C:\ffmpeg\";</code> and replace the path so that it points to the folder where you extracted your FFmpeg binaries (dll files).
7. Click on <code>Start</code> to run the project.
8. You should see a sample media player. Click on the <code>Open</code> icon located at the bottom right and enter a URL or path to a media file.
9. The file or URL should play immediately, and all the properties should display to the right of the media display by clicking on the <code>Info</code> icon.
10. You can use the resulting compiled assemblies in your project without further dependencies. Look for both ```ffme.common.dll``` and ```ffme.win.dll```.

### Windows: NuGet Installation
```
PM> Install-Package FFME.Windows
```

### Windows: Troubleshooting

If you get the following error:
*The current .NET SDK does not support targeting .NET Standard 2.0. Either target .NET Standard 1.6 or lower, or use a version of the .NET SDK that supports .NET Standard 2.0.*

Simply download and install [.NET Core SDK v2](https://www.microsoft.com/net/download/windows) or later.

### MacOS: Sample Player (in preview)
Compile FFmpeg for Mac (instructions can be found on [FFmpeg.AutoGen](https://github.com/Ruslan-B/FFmpeg.AutoGen)) and copy the following libraries from `/opt/local/lib` 's to `/Users/{USER}/ffmpeg` (`~/ffmpeg`):
 - avcodec.57.dylib
 - avdevice.57.dylib
 - avfilter.6.dylib
 - avformat.57.dylib
 - avutil.55.dylib
 - swresample.2.dylib
 - swscale.4.dylib

*Note: when building FFmpeg locally, compiled libraries are named differently than in the list above. E.g. `avcodec.57.dylib` is actually named `libavcodec.57.89.100.dylib`. To properly load libraries, copy and rename each library to match the format in the list above.*

In the sample MacOS player, the FFmpeg folder is configured to point to `~/ffmpeg` in the following line of code:

```csharp
Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ffmpeg");
```

Note that this can be customized to point to any other folder.

When distributing the player and the associated libraries with your application, dll files should be added to the project as `BundleResource` items. Also, each library should be copied to the output directory on build. Afterwards, change the above configuration to use `Environment.CurrentDirectory` to search for FFmpeg libraries.

### MacOS: Troubleshooting
Make sure you have Xamarin for Visual Studio 2017 installed if you want to open the MacOS projects.

## Windows: Using FFME in your WPF Project
*Remember: The Unosquare.FFME.Windows.Sample provides a reference implementation of usage*

1. Create a new WPF application
2. Add a reference to <code>ffme.win.dll</code> or install `FFME.Windows` via NuGet
3. In your <code>MainForm.xaml</code>, add the namespace: <code>xmlns:ffme="clr-namespace:Unosquare.FFME;assembly=ffme.win"</code>
4. Finally, create an instance of the FFME control in your <code>MainForm.xaml</code> as follows: `<ffme:MediaElement x:Name="MediaEl" Background="Gray" LoadedBehavior="Play" UnloadedBehavior="Manual" />`

## Thanks
*In no particular order*

- To the <a href="http://ffmpeg.org/">FFmpeg team</a> for making the Swiss Army Knife of media. I encourage you to donate to them.
- To Kyle Schwarz for creating and making <a href="http://ffmpeg.zeranoe.com/builds/">Zeranoe FFmpeg builds available to everyone</a>.
- To the <a href="https://github.com/naudio/NAudio">NAudio</a> team for making the best audio library out there for .NET -- one day I will contribute some improvements I have noticed they need.
- To Ruslan Balanukhin for his FFmpeg interop bindings generator tool: <a href="https://github.com/Ruslan-B/FFmpeg.AutoGen">FFmpeg.AutoGen</a>.
- To Martin Bohme for his <a href="http://dranger.com/ffmpeg/">tutorial</a> on creating a video player with FFmpeg.
- To Barry Mieny for his beautiful <a href="http://barrymieny.deviantart.com/art/isabi4-for-Windows-105473723">FFmpeg logo</a>

## Similar Projects
- <a href="https://github.com/higankanshi/Meta.Vlc">Meta Vlc</a>
- <a href="https://github.com/Microsoft/FFmpegInterop">Microsoft FFmpeg Interop</a>
- <a href="https://github.com/Sascha-L/WPF-MediaKit">WPF-MediaKit</a>
- <a href="https://libvlcnet.codeplex.com/">LibVLC.NET</a>
- <a href="http://playerframework.codeplex.com/">Microsoft Player Framework</a>

## License
- Please refer to the <a href="https://github.com/unosquare/ffmediaelement/blob/master/LICENSE">LICENSE</a> file for more information.
