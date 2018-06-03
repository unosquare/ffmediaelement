namespace Unosquare.FFME.Rendering
{
    /// <summary>
    /// Represents a grid cell state containing a Display and a back-buffer
    /// of a character and its properties.
    /// </summary>
    internal sealed class ClosedCaptionsCell
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClosedCaptionsCell" /> class.
        /// </summary>
        /// <param name="rowIndex">Index of the row.</param>
        /// <param name="columnIndex">Index of the column.</param>
        public ClosedCaptionsCell(int rowIndex, int columnIndex)
        {
            RowIndex = rowIndex;
            ColumnIndex = columnIndex;
        }

        /// <summary>
        /// Gets the index of the row this cell belongs to.
        /// </summary>
        public int RowIndex { get; }

        /// <summary>
        /// Gets the index of the column this cell belongs to.
        /// </summary>
        public int ColumnIndex { get; }

        /// <summary>
        /// Gets or sets the character.
        /// </summary>
        public ClosedCaptionsCellState Display { get; } = new ClosedCaptionsCellState();

        /// <summary>
        /// Gets or sets the buffered character.
        /// </summary>
        public ClosedCaptionsCellState Buffer { get; } = new ClosedCaptionsCellState();

        /// <summary>
        /// Copies the bufferc ontent on to the dsiplay content
        /// and clears the buffer content.
        /// </summary>
        public void DisplayBuffer()
        {
            Display.CopyFrom(Buffer);
            Buffer.Clear();
        }

        /// <summary>
        /// Resets the entire state and contents of this cell
        /// </summary>
        public void Reset()
        {
            Display.Clear();
            Buffer.Clear();
        }
    }
}
