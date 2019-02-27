namespace Unosquare.FFME.Windows.Sample
{
    using Platform;
    using Shared;
    using System;
    using System.Diagnostics.CodeAnalysis;
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
        /// Gets the current application.
        /// </summary>
        [SuppressMessage("ReSharper", "ArrangeModifiersOrder", Justification = "StyleCop rule mandates specified order of modifiers")]
        public static new App Current => Application.Current as App;

        /// <summary>
        /// Gets the main window of the application.
        /// </summary>
        public new MainWindow MainWindow => Application.Current.MainWindow as MainWindow;

        /// <summary>
        /// Gets the media element hosted by the main window.
        /// </summary>
        public MediaElement MediaElement => MainWindow?.Media;

        /// <summary>
        /// Provides access to the root-level, application-wide VM
        /// </summary>
        public RootViewModel ViewModel => Application.Current.Resources[nameof(ViewModel)] as RootViewModel;

        /// <summary>
        /// Provides access to application-wide commands
        /// </summary>
        public AppCommands Commands { get; } = new AppCommands();

        /// <inheritdoc />
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Application.Current.MainWindow = new MainWindow();
            Application.Current.MainWindow.Loaded += (snd, eva) => ViewModel.OnApplicationLoaded();
            Application.Current.MainWindow.Show();

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

                        Application.Current?.Shutdown();
                    });
                }
            });
        }
    }
}
