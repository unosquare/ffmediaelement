# FFME: *WPF MediaElement Alternative*
[![Analytics](https://ga-beacon.appspot.com/UA-8535255-2/unosquare/ffmediaelement/)](https://github.com/igrigorik/ga-beacon)
[![NuGet version](https://badge.fury.io/nu/FFME.Windows.svg)](https://badge.fury.io/nu/FFME.Windows)
[![NuGet](https://img.shields.io/nuget/dt/FFME.Windows.svg)](https://www.nuget.org/packages/FFME.Windows)
[![Build status](https://ci.appveyor.com/api/projects/status/ppqeayanucj1hadj?svg=true)](https://ci.appveyor.com/project/geoperez/ffmediaelement)

*:star:Please star this project if you find it useful!*

![ffmeplay](https://github.com/unosquare/ffmediaelement/raw/master/Support/ffmeplay.png)

## Announcements
- Current Status: (2018-02-03) - 2.0, codenamed Michelob is now in Release 1 (see the <a href="https://github.com/unosquare/ffmediaelement/releases">Releases</a>)
- NuGet Package available here: https://www.nuget.org/packages/FFME.Windows/
- FFmpeg Version: <a href="http://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.4.1-win32-shared.zip">3.4.1 (32-bit)</a>
- If you would like to support this project, you can show your appreciation via [PayPal.Me](https://www.paypal.me/mariodivece/50usd)

## Quick Usage Guide for WPF Apps

Here is a quick guide on how to get started.
1. Open Visual Studio (v2017 recommended), and create a new WPF Application. Target Framework must be 4.6.2 or above. (This will change to 4.6.1 in the final release)
2. Install the NuGet Package from your Package Manager Console: `PM> Install-Package FFME.Windows`
3. You need FFmpeg binaries now. Build your own or download a compatible build from [Zeranoe FFmpeg Builds site](http://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.4-win32-shared.zip).
4. Your FFmpeg build should have a `bin` folder with 3 exe files and 8 dll files. Copy all 11 files to a folder such as `c:\ffmpeg`
5. Within you application's startup code (`Main` method), set `Unosquare.FFME.MediaElement.FFmpegDirectory = @"c:\ffmpeg";`.
6. Use the FFME `MediaElement` control as any other WPF control.
For example: In your `MainForm.xaml`, add the namespace: `xmlns:ffme="clr-namespace:Unosquare.FFME;assembly=ffme.win"` and then add the FFME control your window's XAML: `<ffme:MediaElement x:Name="Media" Background="Gray" LoadedBehavior="Play" UnloadedBehavior="Manual" />` 
7. To play files or streams, simply set the `Source` property: `Media.Source = new Uri(@"c:\your-file-here");`

### Additional Usage Notes
- Remember: The `Unosquare.FFME.Windows.Sample` provides plenty of usage examples
- The generated API documentation is available [here](http://unosquare.github.io/ffmediaelement/api/Unosquare.FFME.html)

## Features Overview
FFME is a close (and I'd like to think better) drop-in replacement for <a href="https://msdn.microsoft.com/en-us/library/system.windows.controls.mediaelement(v=vs.110).aspx">Microsoft's WPF MediaElement Control</a>. While the standard MediaElement uses DirectX (DirectShow) for media playback, FFME uses <a href="http://ffmpeg.org/">FFmpeg</a> to read and decode audio and video. This means that for those of you who want to support stuff like HLS playback, or just don't want to go through the hassle of installing codecs on client machines, using FFME *might* just be the answer. 

FFME provides multiple improvements over the standard MediaElement such as:
- Fast media seeking and frame-by-frame seeking
- Properties such as Position, NaturalDuration, SpeedRatio, and Volume are all Dependency Properties!
- Additional and extended media events. Extracting (and modifying) video, audio and subtitle frames is very easy.
- Ability to easily apply FFmpeg video and audio filtergraphs.
- Ability to extract media metadata and tech specs of a media stream (title, album, bitrate, codecs, FPS, etc).
- Ability to apply volume, balance and speed ratio to media playback.
- MediaState actually works on this control. The standard WPF MediaElement severely lacks in this area.

*... all in a single MediaElement control*

### About how it works

First off, let's review a few concepts. A `packet` is a group of bytes read from the input. All `packets` are of a specific `MediaType` (Audio, Video, Subtitle, Data), and contain some timing information and most importantly compressed data. Packets are sent to a `Codec` and in turn, the codec produces `Frames`. Please note that producing 1 `frame` does not always take exactly 1 `packet`. A `packet` may contain many `frames` but also a `frame` may require several `packets` for the decoder to build it. `Frames` will contain timing informattion and the raw, uncompressed data. Now, you may think you can use `frames` and show pixels on the screen or send samples to the sound card. We are close, but we still need to do some additional processing. Turns out different `Codecs` will produce different uncompressed data formats. For example, some video codecs will output pixel data in ARGB, some others in RGB, and some other in YUV420. Therefore, we will need to `Convert` these `frames` into something all hardware can use natively. I call these converted frames, `MediaBlocks`. These `MediaBlocks` will contain uncompressed data in standard Audio and Video formats that all hardware is able to receive.

The process described above is implemented in 3 different layers:
- The `MediaContainer` wraps an input stream. This layer keeps track of a `MediaComponentSet` which is nothing more than a collecttion of `MediaComponent` objects. Each `MediaComponent` holds `packet` **caching**, `frame` **decoding**, and `block` **conversion** logic. It provides the following important functionality:
  - We call `Open` to open the input stream and detect the different stream components. This also determines the codecs to use.
  - We call `Read` to read the next available packet and store it in its corresponding component (audio, video, subtitle, data, etc)
  - We call `Decode` to read the following packet from the queue that each of the components hold, and return a set of frames.
  - Finally, we call `Convert` to turn a given `frame` into a `MediaBlock`.
- The `MediaEngine` wraps a `MediaContainer` and it is responsible for executing commands to control the input stream (Play, Pause, Stop, Seek, etc.) while keeping keeping 3 background workers.
  - The `PacketReadingWroker` is designed to continuously read packets from the `MediaContainer`. It will read packets when it needs them and it will pause if it does not need them. This is determined by how much data is in the cache. It will try to keep approximately 1 second of media packets at all times.
  - The `FrameDecodingWroker` gets the packets that the `PacketReadingWorker` writes and decodes them into frames. It then converts those frames into `blocks` and writes them to a `MediaBlockBuffer`. This block buffer can then be read by something else (the following worker described here) so its contents can be rendered.
  - Finally, the `BlockRenderingWorker` reads blocks form the `MediaBlockBuffer`s and sends those blocks to a plat-from specific `IMediaRenderer`.
- At the highest level, we have a `MediaElement`. It wraps a `MediaEngine` and it contains platform-specific implementation of methods to perform stuff like audio rendering, video rendering, subtitle rendering, and property synchronization between the `MediaEngine` and itself.

A high-level diagram is provided as additional reference below.
![arch-michelob-2.0](https://github.com/unosquare/ffmediaelement/raw/master/Support/arch-michelob-2.0.png)

### Known Limitations
*Your help is welcome!*

- I am planning the next version of this control, `Floyd`. See the **Issues** section.
- Working on Hardware acceleration. Maybe CUDA for highest compatibility.
- Starting version 3.4.210 FFME supports opening capture devices. See examples below and [issue #48](https://github.com/unosquare/ffmediaelement/issues/48)
```
device://dshow/?audio=Microphone (Vengeance 2100)
device://gdigrab?title=Command Prompt
device://gdigrab?desktop
```

## Windows: Compiling, Running and Testing

*Please note that I am unable to distribute FFmpeg's binaries because I don't know if I am allowed to do so. Follow the instructions below to compile, run and test FFME.*

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

### Windows: Troubleshooting

If you get the following error when compiling:
*The current .NET SDK does not support targeting .NET Standard 2.0. Either target .NET Standard 1.6 or lower, or use a version of the .NET SDK that supports .NET Standard 2.0.*

Simply download and install [.NET Core SDK v2](https://www.microsoft.com/net/download/windows) or later.

## MacOS: Sample Player (in preview, WIP)
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
