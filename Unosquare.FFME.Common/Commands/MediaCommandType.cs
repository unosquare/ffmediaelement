namespace Unosquare.FFME.Commands
{
    /// <summary>
    /// Enumerates the different available Media Command Types
    /// </summary>
    internal enum MediaCommandType
    {
        /// <summary>
        /// The open command
        /// </summary>
        Open,

        /// <summary>
        /// The seek command
        /// </summary>
        Seek,

        /// <summary>
        /// The play command
        /// </summary>
        Play,

        /// <summary>
        /// The pause command
        /// </summary>
        Pause,

        /// <summary>
        /// The stop command
        /// </summary>
        Stop,

        /// <summary>
        /// The close command
        /// </summary>
        Close,

        /// <summary>
        /// The set speed ratio command
        /// </summary>
        SetSpeedRatio
    }
}
