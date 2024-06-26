# FFME: *The Advanced WPF MediaElement Alternative*

[![Join the chat at https://gitter.im/ffmediaelement/Lobby](https://badges.gitter.im/ffmediaelement/Lobby.svg)](https://gitter.im/ffmediaelement/Lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)
[![Analytics](https://ga-beacon.appspot.com/UA-8535255-2/unosquare/ffmediaelement/)](https://github.com/igrigorik/ga-beacon)
[![NuGet version](https://badge.fury.io/nu/FFME.Windows.svg)](https://badge.fury.io/nu/FFME.Windows)
[![NuGet](https://img.shields.io/nuget/dt/FFME.Windows.svg)](https://www.nuget.org/packages/FFME.Windows)
[![Build status](https://ci.appveyor.com/api/projects/status/ppqeayanucj1hadj?svg=true)](https://ci.appveyor.com/project/geoperez/ffmediaelement)
[![Codacy Badge](https://api.codacy.com/project/badge/Grade/c439ad57c68e43f796401467bca06e9e)](https://www.codacy.com/app/UnosquareLabs/ffmediaelement?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=unosquare/ffmediaelement&amp;utm_campaign=Badge_Grade)

:star: *Please star this project if you like it and show your appreciation via* **[PayPal.Me](https://www.paypal.me/mariodivece/50usd)**

![ffmeplay](https://github.com/unosquare/ffmediaelement/raw/master/Support/ffmeplay.png)

## Status Updates
- If you would like to support this project, you can show your appreciation via [PayPal.Me](https://www.paypal.me/mariodivece/50usd)
- Current Status: (2024-06-26) - BETA 1 Release 7.0.361.1 is now available, (see the <a href="https://github.com/unosquare/ffmediaelement/releases">Releases</a>)
- NuGet Package available here: https://www.nuget.org/packages/FFME.Windows/
- FFmpeg Version: <a href="https://ffmpeg.org/download.html">7.0</a> -- Make sure you download one built as a SHARED library and for your right architecture (typically x64)
- BREAKING CHANGE: Starting realease 4.1.320 the `Source` dependency property has been downgraded to a notification property. Please use the asynchronous `Open` and `Close` methods instead.
- I have been learning a ton while writing this project. You can find my latest video and rendering experiments <a href="https://github.com/mariodivece/ffplaysharp">here (if you are curious)</a>

*Please note the current NuGet realease might require a different version of the FFmpeg binaries than the ones of the current state of the source code.*

## Quick Usage Guide for WPF Apps

### Get Started

1. Open Visual Studio and create a new WPF Application.
   
   **Target Framework must be set to .net 5.0 or above**
   
2. Install the NuGet Package from your Package Manager Console: 
   ```bash
   PM> Install-Package FFME.Windows
3. Acquire the FFmpeg shared binaries (either 64 or 32 bit, depending on your app's target architecture)
   
   *by either*
   
* Building your own
  
    I recommend the [Media Autobuild Suite](https://github.com/jb-alvarado/media-autobuild_suite)  _please don't ask for help on it here._

  *or*
* Downloading a compatible build 

  For a x64 build 
  * the **dlls** are located here, [7.0 x64](https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-full-shared.7z),
   combine the contents of the `bin` folder of both downloaded folders into a separate folder e.g `c:\ffmpeg\x64`.
 
   *The resulting contents of the folder e.g `c:\ffmpeg\x64` should be so*
     - avcodec-59.dll
     - avdevice-59.dll
     - avfilter-8.dll
     - avformat-59.dll
     - avutil-58.dll
     - ffmpeg.exe
     - ffplay.exe
     - ffprobe.exe
     - swresample-4.dll
     - swscale-6.dll
     
4. Within your application's startup code (Main method)
   
   set the _Unosquare.FFME.Library.FFmpegDirectory_ variable to the path of the folder where the DLLs and EXEs are located, e.g.

  ```Unosquare.FFME.Library.FFmpegDirectory = @"c:\ffmpeg";```
  
  And use the FFME MediaElement control as you would any other WPF control.

### Example 
in your main window (e.g MainWindow.xaml)

* Add the namespace:
```xmlns:ffme="clr-namespace:Unosquare.FFME;assembly=ffme.win"```

* Add the FFME control:
```<ffme:MediaElement x:Name="Media" Background="Gray" LoadedBehavior="Play" UnloadedBehavior="Manual" />```

* Play files or streams, by calling the asynchronous method, Open:
```await Media.Open(new Uri(@"c:\your-file-here"));```

* Close the media, by calling:
```await Media.Close();```


#### Additional Usage Notes
- Remember: The `Unosquare.FFME.Windows.Sample` provides usage examples for plenty of features. Use it as your main reference.
- The generated API documentation is available [here](http://unosquare.github.io/ffmediaelement/api/Unosquare.FFME.html)

## Features Overview
FFME is an advanced and close drop-in replacement for <a href="https://msdn.microsoft.com/en-us/library/system.windows.controls.mediaelement(v=vs.110).aspx">Microsoft's WPF MediaElement Control</a>. While the standard MediaElement uses DirectX (DirectShow) for media playback, FFME uses <a href="http://ffmpeg.org/">FFmpeg</a> to read and decode audio and video. This means that for those of you who want to support stuff like HLS playback, or just don't want to go through the hassle of installing codecs on client machines, using FFME *might* just be the answer. 

FFME provides multiple improvements over the standard MediaElement such as:
- Fast media seeking and frame-by-frame seeking.
- Properties such as Position, Balance, SpeedRatio, IsMuted, and Volume are all Dependency Properties.
- Additional and extended media events. Extracting (and modifying) video, audio and subtitle frames is very easy.
- Easily apply FFmpeg video and audio filtergraphs.
- Extract media metadata and specs of a media stream (title, album, bit rate, codecs, FPS, etc).
- Apply volume, balance and speed ratio to media playback.
- MediaState actually works on this control. The standard WPF MediaElement is severely lacking in this area.
- Ability to pick media streams contained in a file or a URL.
- Specify input and codec parameters.
- Opt-in hardware decoding acceleration via devices or via codecs.
- Capture stream packets, audio, video and subtitle frames.
- Change raw video, audio and subtitle data upon rendering.
- Perform custom stream reading and stream recording.

*... all in a single MediaElement control*

FFME also supports opening capture devices. See example URLs below and [issue #48](https://github.com/unosquare/ffmediaelement/issues/48)
```
device://dshow/?audio=Microphone (Vengeance 2100):video=MS Webcam 4000
device://gdigrab?title=Command Prompt
device://gdigrab?desktop
```

If you'd like audio to not change pitch while changing the SpeedRatio property, you'll need the `SoundTouch.dll` library v2.1.1 available on the same directory as the FFmpeg binaries. You can get the [SoundTouch library here](https://www.surina.net/soundtouch/).

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

### Some Work In Progress
*Your help is welcome!*

- I am planning the next version of this control, `Floyd`. See the **Issues** section.

## Windows: Compiling, Running and Testing

*Please note that I am unable to distribute FFmpeg's binaries because I don't know if I am allowed to do so. Follow the instructions below to compile, run and test FFME.*

1. Clone this repository and make sure you have <a href="https://dotnet.microsoft.com/download/dotnet-core/3.1">.Net Core 3.1 or above</a> installed.
2. Download the FFmpeg **shared** binaries for your target architecture: <a href="https://ffmpeg.org/download.html">FFmpeg Windows Downloads</a>.
3. Extract the contents of the <code>zip</code> file you just downloaded and go to the <code>bin</code> folder that got extracted. You should see 3 <code>exe</code> files and multiple <code>dll</code> files. Select and copy all of them.
4. Now paste all files from the prior step onto a well-known folder. Take note of the full path. (I used `c:\ffmpeg\`)
5. Open the solution and set the <code>Unosquare.FFME.Windows.Sample</code> project as the startup project. You can do this by right clicking on the project and selecting <code>Set as startup project</code>. Please note that you will need Visual Studio 2019 with dotnet 5.0 SDK for your target architecture installed.
6. Under the <code>Unosquare.FFME.Windows.Sample</code> project, find the file `App.xaml.cs` and under the constructor, locate the line <code>Library.FFmpegDirectory = @"c:\ffmpeg";</code> and replace the path so that it points to the folder where you extracted your FFmpeg binaries (dll files).
7. Click on <code>Start</code> to run the project.
8. You should see a sample media player. Click on the <code>Open</code> icon located at the bottom right and enter a URL or path to a media file.
9. The file or URL should play immediately, and all the properties should display to the right of the media display by clicking on the <code>Info</code> icon.
10. You can use the resulting compiled assemblies in your project without further dependencies. Look for ```ffme.win.dll```.

### ffmeplay.exe Sample Application

The source code for this project contains a very capable media player (`FFME.Windows.Sample`) covering most of the use cases for the `FFME` control. If you are just checking things out, here is a quick set of shortcut keys that `ffmeplay` accepts.

| Shortcut Key | Function Description |
| --- | --- |
| G | Example of toggling subtitle color |
| Left | Seek 1 frame to the left |
| Right | Seek 1 frame to the right |
| + / Volume Up | Increase Audio Volume |
| - / Volume Down | Decrease Audio Volume |
| M / Volume Mute | Mute Audio |
| Up | Increase playback Speed |
| Down | Decrease playback speed |
| A | Cycle Through Audio Streams |
| S | Cycle Through Subtitle Streams |
| Q | Cycle Through Video Streams |
| C | Cycle Through Closed Caption Channels |
| R | Reset Changes |
| Y / H | Contrast: Increase / Decrease |
| U / J | Brightness: Increase / Decrease |
| I / K | Saturation: Increase / Decrease |
| E | Example of cycling through audio filters |
| T | Capture Screenshot to `desktop/ffplay` folder |
| W | Start/Stop recording packets (no transcoding) into a transport stream to `desktop/ffplay` folder. |
| Double-click | Enter fullscreen |
| Escape | Exit fullscreen |
| Mouse Wheel Up / Down | Zoom: In / Out |

## Thanks
*In no particular order*

- To the <a href="http://ffmpeg.org/">FFmpeg team</a> for making the Swiss Army Knife of media. I encourage you to donate to them.
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
