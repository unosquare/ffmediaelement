namespace Unosquare.FFME.Windows.Sample
{
    using Engine;
    using Platform;
    using System;
    using System.Threading;
    using System.Windows;
    using ViewModels;

    /// <summary>
    /// Interaction logic for App.xaml
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
            MediaElement.FFmpegDirectory = @"c:\ffmpeg" + (Environment.Is64BitProcess ? @"\x64" : string.Empty);

            // You can pick which FFmpeg binaries are loaded. See issue #28
            // Full Features is already the default.
            MediaElement.FFmpegLoadModeFlags = FFmpegLoadMode.FullFeatures;

            // Multi-threaded video enables the creation of independent
            // dispatcher threads to render video frames.
            MediaElement.EnableWpfMultiThreadedVideo = GuiContext.Current.IsInDebugMode == false;
        }

        /// <summary>
        /// Provides access to the root-level, application-wide VM
        /// </summary>
        public static RootViewModel ViewModel => Current.Resources[nameof(ViewModel)] as RootViewModel;

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
                    MediaElement.LoadFFmpeg();
                }
                catch(Exception ex)
                {
                    GuiContext.Current?.EnqueueInvoke(() =>
                    {
                        MessageBox.Show(MainWindow,
                            $"Unable to Load FFmpeg Libraries from path:\r\n    {MediaElement.FFmpegDirectory}" +
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
