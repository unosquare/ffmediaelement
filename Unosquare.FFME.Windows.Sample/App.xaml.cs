﻿namespace Unosquare.FFME.Windows.Sample
{
    using Shared;
    using System.Windows;
    using ViewModels;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="App"/> class.
        /// </summary>
        public App()
            : base()
        {
            // Change the default location of the ffmpeg binaries
            // You can get the binaries here: https://ffmpeg.zeranoe.com/builds/win32/shared/ffmpeg-3.4.2-win32-shared.zip
            MediaElement.FFmpegDirectory = @"c:\ffmpeg";

            // You can pick which FFmpeg binaries are loaded. See issue #28
            // Full Features is already the default.
            MediaElement.FFmpegLoadModeFlags = FFmpegLoadMode.FullFeatures;
        }

        /// <summary>
        /// Gets the current application.
        /// </summary>
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
        /// Provides access to tthe root-level, application-wide VM
        /// </summary>
        public RootViewModel ViewModel => Application.Current.Resources[nameof(ViewModel)] as RootViewModel;

        /// <summary>
        /// Provides access to application-wide commands
        /// </summary>
        public AppCommands Commands { get; } = new AppCommands();

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Application.Startup" /> event.
        /// </summary>
        /// <param name="e">A <see cref="T:System.Windows.StartupEventArgs" /> that contains the event data.</param>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Application.Current.MainWindow = new MainWindow();
            Application.Current.MainWindow.Show();
            ViewModel.OnApplicationLoaded();
        }
    }
}
