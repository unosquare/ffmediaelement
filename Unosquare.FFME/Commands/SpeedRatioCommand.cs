namespace Unosquare.FFME.Commands
{
    using Core;
    using System.Windows.Threading;

    /// <summary>
    /// A command to change speed ratio asynchronously
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Commands.MediaCommand" />
    internal sealed class SpeedRatioCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpeedRatioCommand"/> class.
        /// </summary>
        /// <param name="manager">The manager.</param>
        /// <param name="speedRatio">The speed ratio.</param>
        public SpeedRatioCommand(MediaCommandManager manager, double speedRatio) 
            : base(manager, MediaCommandType.SetSpeedRatio)
        {
            SpeedRatio = speedRatio;
        }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal override void Execute()
        {
            if (Manager.MediaElement.Clock.SpeedRatio != SpeedRatio)
                Manager.MediaElement.Clock.SpeedRatio = SpeedRatio;

            Utils.UIInvoke(DispatcherPriority.DataBind, () => {
                Manager.MediaElement.SpeedRatio = SpeedRatio;
            });
            
        }

        /// <summary>
        /// The target speed ratio
        /// </summary>
        public double SpeedRatio = 1.0d;
    }
}
