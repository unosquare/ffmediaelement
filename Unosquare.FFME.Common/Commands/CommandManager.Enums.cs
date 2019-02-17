namespace Unosquare.FFME.Commands
{
    internal partial class CommandManager
    {
        /// <summary>
        /// Enumerates the different seek modes for the seek command.
        /// </summary>
        private enum SeekMode
        {
            /// <summary>Normal seek mode</summary>
            Normal,

            /// <summary>Stop seek mode</summary>
            Stop,

            /// <summary>Frame step forward</summary>
            StepForward,

            /// <summary>Frame step backward</summary>
            StepBackward
        }

        /// <summary>
        /// Enumerates the different direct command types
        /// </summary>
        private enum DirectCommandType
        {
            /// <summary>
            /// No command Type.
            /// </summary>
            None,

            /// <summary>
            /// The open command
            /// </summary>
            Open,

            /// <summary>
            /// The close command
            /// </summary>
            Close,

            /// <summary>
            /// The change command
            /// </summary>
            Change
        }

        /// <summary>
        /// The priority command types
        /// </summary>
        private enum PriorityCommandType
        {
            /// <summary>
            /// The none command
            /// </summary>
            None,

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
            Stop
        }
    }
}
