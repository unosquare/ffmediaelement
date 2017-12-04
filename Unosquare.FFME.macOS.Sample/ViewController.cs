namespace Unosquare.FFME.macOS.Sample
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using AppKit;
    using FFmpeg.AutoGen;
    using Foundation;

    public partial class ViewController : NSViewController
    {
        private NSImageView imageView;

        public ViewController(IntPtr handle) : base(handle)
        {

        }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            // Do any additional setup after loading the view.

            // First initialize FFmpeg dependencies

            var dir = Environment.CurrentDirectory;
            dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ffmpeg");

            var loadedLibraries = new Dictionary<string, IntPtr>();

            MediaElement.FFmpegDirectory = dir;

            // Create image view that will show each frame

            imageView = new NSImageView(new CoreGraphics.CGRect(
                (View.Bounds.Width - 640) / 2,
                (View.Bounds.Height - 480) / 2,
                640, 480));

            imageView.Image = new NSImage(new NSUrl("https://github.com/unosquare/ffmediaelement/raw/master/ffme.png"));
            View.AddSubview(imageView);

            imageView.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;

            // Create a player and start playing sample video

            var mediaElement = new MediaElement(imageView);
            var uri = @"http://www.quirksmode.org/html5/videos/big_buck_bunny.mp4";
            mediaElement.Open(new Uri(uri));
        }

        public override NSObject RepresentedObject
        {
            get => base.RepresentedObject;
            set
            {
                base.RepresentedObject = value;
                // Update the view, if already loaded.
            }
        }
    }
}
