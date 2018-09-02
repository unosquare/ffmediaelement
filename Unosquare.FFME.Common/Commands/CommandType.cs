namespace Unosquare.FFME.Commands
{
    /// <summary>
    /// Enumerates the different available Media Command Types
    /// </summary>
    internal enum CommandType
    {
        /// <summary>
        /// Represents no command
        /// </summary>
        None,

        /// <summary>
        /// The open command id
        /// </summary>
        Open,

        /// <summary>
        /// The seek command id
        /// </summary>
        Seek,

        /// <summary>
        /// The play command id
        /// </summary>
        Play,

        /// <summary>
        /// The pause command id
        /// </summary>
        Pause,

        /// <summary>
        /// The stop command id
        /// </summary>
        Stop,

        /// <summary>
        /// The close command id
        /// </summary>
        Close,

        /// <summary>
        /// The change media command id
        /// </summary>
        ChangeMedia
    }
}
