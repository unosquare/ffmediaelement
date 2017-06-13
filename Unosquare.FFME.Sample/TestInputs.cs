namespace Unosquare.FFME.Sample
{
    using System;
    using System.IO;

    static class TestInputs
    {
        private static string InputBasePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "videos");

        #region Local Files

        /// <summary>
        /// The matroska test. It contains various subtitle an audio tracks
        /// Files can be obtained here: https://sourceforge.net/projects/matroska/files/test_files/matroska_test_w1_1.zip/download
        /// </summary>
        public static string MatroskaLocalFile = $"{InputBasePath}\\matroska.mkv";

        public static string FinlandiaMp3LocalFile = $"{InputBasePath}\\finlandia.mp3";

        public static string FinlandiaOggLocalFile = $"{InputBasePath}\\finlandia.ogg";

        public static string ElysiumLocalFile = $"{InputBasePath}\\elysium.mkv";

        /// <summary>
        /// The transport stream file
        /// From: https://github.com/unosquare/ffmediaelement/issues/16#issuecomment-299183167
        /// </summary>
        public static string TransportLocalFile = $"{InputBasePath}\\transport.ts";

        public static string Transport1LocalFile = $"{InputBasePath}\\transport1.ts";

        public static string Transport2LocalFile = $"{InputBasePath}\\transport2.ts";

        /// <summary>
        /// The small MP4 local file
        /// http://techslides.com/sample-webm-ogg-and-mp4-video-files-for-html5
        /// </summary>
        public static string SmallMp4LocalFile = $"{InputBasePath}\\small.mp4";

        /// <summary>
        /// The small web m local file
        /// http://techslides.com/sample-webm-ogg-and-mp4-video-files-for-html5
        /// </summary>
        public static string SmallWebMLocalFile = $"{InputBasePath}\\small.webm";

        /// <summary>
        /// Downloaded From: https://www.dropbox.com/sh/vggf640iniwxwyu/AABSeLJfAZeApEoJAY3N34Y2a?dl=0
        /// </summary>
        public static string BigBuckBunnyLocal = $"{InputBasePath}\\bigbuckbunny.mp4";

        public static string YoutubeLocalFile = $"{InputBasePath}\\youtube.mp4";

        /// <summary>
        /// The mpg file form issue https://github.com/unosquare/ffmediaelement/issues/22
        /// </summary>
        public static string MpegPart2LocalFile = $"{InputBasePath}\\mpegpart2.mpg";

        public static string PngTestLocalFile = $"{InputBasePath}\\pngtest.png";

        public static string MusicLocalFile = $"{InputBasePath}\\music.mp3";

        #endregion

        #region Network Files

        public static string UdpStream = @"udp://@225.1.1.181:5181/";

        public static string UdpStream2 = @"udp://@225.1.1.3:5003/";

        public static string HlsStream = @"http://qthttp.apple.com.edgesuite.net/1010qwoeiuryfg/sl.m3u8";

        public static string HlsStream2 = @"https://devimages.apple.com.edgekey.net/streaming/examples/bipbop_16x9/bipbop_16x9_variant.m3u8";

        public static string HlsStream3 = @"https://devimages.apple.com.edgekey.net/streaming/examples/bipbop_4x3/bipbop_4x3_variant.m3u8";

        /// <summary>
        /// The RTSP stream from http://g33ktricks.blogspot.mx/p/the-rtsp-real-time-streaming-protocol.html
        /// </summary>
        public static string RtspStream = "rtsp://184.72.239.149/vod/mp4:BigBuckBunny_175k.mov";

        public static string NetworkShareStream = @"\\STARBIRD\Dropbox\MEXICO 20120415 TOLUCA 0-3 CRUZ AZUL.mp4";

        public static string NetworkShareStream2 = @"\\STARBIRD\Public\Movies\Ender's Game (2013).mp4";

        #endregion
    }
}
