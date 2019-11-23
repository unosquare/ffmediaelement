namespace Unosquare.FFME.Container
{
    using FFmpeg.AutoGen;

    internal static class AVPictureTypeExtensions
    {
        /// <summary>
        /// Get the display string for the picture type.
        /// </summary>
        /// <param name="pictureType">The picture type.</param>
        /// <returns>A string indicating the picture type or an empty string if it is unkown.</returns>
        public static string GetDisplayString(this AVPictureType pictureType)
        {
            switch (pictureType)
            {
                case AVPictureType.AV_PICTURE_TYPE_I: return "I";
                case AVPictureType.AV_PICTURE_TYPE_P: return "P";
                case AVPictureType.AV_PICTURE_TYPE_B: return "B";
                case AVPictureType.AV_PICTURE_TYPE_S: return "S";
                case AVPictureType.AV_PICTURE_TYPE_SI: return "SI";
                case AVPictureType.AV_PICTURE_TYPE_SP: return "SP";
                case AVPictureType.AV_PICTURE_TYPE_BI: return "BI";
                default: return string.Empty;
            }
        }
    }
}
