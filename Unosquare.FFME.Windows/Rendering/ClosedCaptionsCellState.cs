namespace Unosquare.FFME.Rendering
{
    using System.Globalization;
    using System.Windows.Media;

    /// <summary>
    /// Contains single-character text and its attributes
    /// </summary>
    internal sealed class ClosedCaptionsCellState
    {
        /// <summary>
        /// Gets the character as a string.
        /// </summary>
        public string Text => Character == default ?
                        string.Empty : Character.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// Gets or sets the character.
        /// </summary>
        public char Character { get; set; } = default;

        /// <summary>
        /// Gets or sets the opacity (from 0.0 to 1.0 opaque).
        /// </summary>
        public double Opacity { get; set; } = 0.80;

        /// <summary>
        /// Gets or sets the foreground text color.
        /// </summary>
        public Brush Foreground { get; set; } = Brushes.White;

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        public Brush Background { get; set; } = Brushes.Black;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is underline.
        /// </summary>
        public bool IsUnderlined { get; set; } = default;

        /// <summary>
        /// Gets or sets a value indicating whether this instance is italics.
        /// </summary>
        public bool IsItalics { get; set; } = default;

        /// <summary>
        /// Copies text and attributes from another cell state content.
        /// </summary>
        /// <param name="cell">The cell.</param>
        public void CopyFrom(ClosedCaptionsCellState cell)
        {
            Character = cell.Character;
            Opacity = cell.Opacity;
            Foreground = cell.Foreground;
            Background = cell.Background;
            IsUnderlined = cell.IsUnderlined;
            IsItalics = cell.IsItalics;
        }

        /// <summary>
        /// Clears the text and its attributes.
        /// </summary>
        public void Clear()
        {
            Character = default;
            Opacity = 0.80;
            Foreground = Brushes.White;
            Background = Brushes.Black;
            IsUnderlined = default;
            IsItalics = default;
        }
    }
}
