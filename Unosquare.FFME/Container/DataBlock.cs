namespace Unosquare.FFME.Container
{
    using Unosquare.FFME.Common;

    /// <summary>
    /// A subtitle frame container. Simply contains text lines.
    /// </summary>
    internal sealed class DataBlock : MediaBlock
    {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DataBlock"/> class.
        /// </summary>
        internal DataBlock()
            : base(MediaType.Data)
        {
            // placeholder
        }
        #endregion

        #region Properties

        /// <summary>
        /// Byte array.
        /// </summary>
        public byte[] Bytes { get; set; }
        #endregion
    }
}