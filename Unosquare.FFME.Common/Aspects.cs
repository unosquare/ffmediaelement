namespace Unosquare.FFME
{
    /// <summary>
    /// Provides constants for logging aspect identifiers
    /// </summary>
    internal static class Aspects
    {
        public static string None => "Log.Text";

        public static string FFmpegLog => "FFmpeg.Log";

        public static string EngineCommand => "Engine.Commands";

        public static string ReadingWorker => "Engine.Reading";

        public static string DecodingWorker => "Engine.Decoding";

        public static string RenderingWorker => "Engine.Rendering";

        public static string Connector => "Engine.Connector";

        public static string Container => "Container";

        public static string Component => "Container.Component";

        public static string ReferenceCounter => "ReferenceCounter";
    }
}
