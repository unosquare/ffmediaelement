namespace Unosquare.FFME.Windows.Sample.Controls
{
    public partial class MessageConsole
    {
        /// <summary>
        /// Contains controller methods
        /// </summary>
        public class LocalController
        {
            private MessageConsole Parent;

            /// <summary>
            /// Initializes a new instance of the <see cref="LocalController"/> class.
            /// </summary>
            /// <param name="parent">The parent.</param>
            internal LocalController(MessageConsole parent)
            {
                Parent = parent;
            }
        }
    }
}
