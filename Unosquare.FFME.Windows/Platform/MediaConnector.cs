namespace Unosquare.FFME.Platform
{
    using Common;
    using Engine;
    using Rendering;
    using System;

    internal partial class MediaConnector
    {
        /// <inheritdoc />
        public IMediaRenderer CreateRenderer(MediaType mediaType, MediaEngine mediaCore)
        {
            switch (mediaType)
            {
                case MediaType.Audio:
                    return new AudioRenderer(mediaCore);
                case MediaType.Video:
                    return !D3DVideoRenderer.IsAvailable
                        ? new D3DVideoRenderer(mediaCore) as IMediaRenderer
                        : new VideoRenderer(mediaCore);
                case MediaType.Subtitle:
                    return new SubtitleRenderer(mediaCore);
                default:
                    throw new NotSupportedException($"No suitable renderer for Media Type '{mediaType}'");
            }
        }
    }
}
