﻿namespace Unosquare.FFME.Windows.Sample
{
    using FFmpeg.AutoGen;
    using Platform;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Windows;
    using ViewModels;

    /// <summary>
    /// Interaction logic for App.xaml.
    /// </summary>
    public partial class App
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="App" /> class.
        /// </summary>
        public App()
        {
            // Change the default location of the ffmpeg binaries
            // You can get the binaries here: https://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-4.1-win32-shared.zip
            Library.FFmpegDirectory = @"c:\ffmpeg" + (Environment.Is64BitProcess ? @"\x64" : string.Empty);

            // You can pick which FFmpeg binaries are loaded. See issue #28
            // Full Features is already the default.
            Library.FFmpegLoadModeFlags = FFmpegLoadMode.FullFeatures;

            // Multi-threaded video enables the creation of independent
            // dispatcher threads to render video frames. This is an experimental feature.
            Library.EnableWpfMultiThreadedVideo = !Debugger.IsAttached;
        }

        /// <summary>
        /// Provides access to the root-level, application-wide VM.
        /// </summary>
        public static RootViewModel ViewModel => Current.Resources[nameof(ViewModel)] as RootViewModel;

        /// <summary>
        /// Gets a full file path for a screen capture or stream recording.
        /// </summary>
        /// <param name="mediaPrefix">The media prefix. Use Screenshot or Capture for example.</param>
        /// <param name="extension">The file extension without a dot.</param>
        /// <returns>A full file path where the media file will be written to.</returns>
        public static string GetCaptureFilePath(string mediaPrefix, string extension)
        {
            var date = DateTime.UtcNow;
            var dateString = $"{date.Year:0000}-{date.Month:00}-{date.Day:00} {date.Hour:00}-{date.Minute:00}-{date.Second:00}.{date.Millisecond:000}";
            var targetFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "ffmeplay");

            if (Directory.Exists(targetFolder) == false)
                Directory.CreateDirectory(targetFolder);

            var targetFilePath = Path.Combine(targetFolder, $"{mediaPrefix} {dateString}.{extension}");
            if (File.Exists(targetFilePath))
                File.Delete(targetFilePath);

            return targetFilePath;
        }

        /// <inheritdoc />
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Current.MainWindow = new MainWindow();
            Current.MainWindow.Loaded += (snd, eva) => ViewModel.OnApplicationLoaded();
            Current.MainWindow.Show();

            // Pre-load FFmpeg libraries in the background. This is optional.
            // FFmpeg will be automatically loaded if not already loaded when you try to open
            // a new stream or file. See issue #242
            ThreadPool.QueueUserWorkItem(s =>
            {
                try
                {
                    // Force loading
                    Library.LoadFFmpeg();
                }
                catch(Exception ex)
                {
                    GuiContext.Current?.EnqueueInvoke(() =>
                    {
                        MessageBox.Show(MainWindow,
                            $"Unable to Load FFmpeg Libraries from path:\r\n    {Library.FFmpegDirectory}" +
                            $"\r\nMake sure the above folder contains FFmpeg shared binaries (dll files) for the " +
                            $"applicantion's architecture ({(Environment.Is64BitProcess ? "64-bit" : "32-bit")})" +
                            $"\r\nTIP: You can download builds from https://ffmpeg.zeranoe.com/builds/" +
                            $"\r\n{ex.GetType().Name}: {ex.Message}\r\n\r\nApplication will exit.",
                            "FFmpeg Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        Current?.Shutdown();
                    });
                }
            });
        }
    }
}
