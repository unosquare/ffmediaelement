namespace Unosquare.FFME.Commands
{
    /// <summary>
    /// Defines the different command categories and how to handle their execution.
    /// </summary>
    internal enum CommandCategory
    {
        /// <summary>
        /// Direct commands are handled immediately before any other commands.
        /// They are not queued or processed one by one, but rather executed
        /// exclusive to one another.
        /// </summary>
        Direct,

        /// <summary>
        /// These commands are queued but clear the command queue qhen they are
        /// queued for execution so that pending commands sre not executed
        /// and new commands can be queued.
        /// </summary>
        Priority,

        /// <summary>
        /// These commands are queued for later execution. Commands can only
        /// be queued if the current media state allows them to do so.
        /// </summary>
        Delayed
    }
}
