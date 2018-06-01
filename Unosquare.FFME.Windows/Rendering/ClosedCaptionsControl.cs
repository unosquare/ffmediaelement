namespace Unosquare.FFME.Rendering
{
    using ClosedCaptions;
    using Platform;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    /// <summary>
    /// A control that renders Closed Captions.
    /// </summary>
    /// <seealso cref="Viewbox" />
    internal sealed class ClosedCaptionsControl : Viewbox
    {
        private const double BackgroundWidth = 45;
        private const double BackgroundHeight = 80;
        private const double DefaultOpacity = 0.80d;
        private const double DefaultFontSize = 65;

        private readonly ClosedCaptionsBuffer Buffer = new ClosedCaptionsBuffer();
        private readonly FontFamily FontFamily = new FontFamily("Lucida Console");
        private readonly Dictionary<int, Dictionary<int, TextBlock>> CharacterLookup
            = new Dictionary<int, Dictionary<int, TextBlock>>(ClosedCaptionsBuffer.RowCount);

        private Grid CaptionsGrid = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClosedCaptionsControl"/> class.
        /// </summary>
        public ClosedCaptionsControl()
            : base()
        {
            Width = 0;
            Height = 0;
            Visibility = Visibility.Collapsed;
            Focusable = false;
            IsHitTestVisible = false;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            InitializeComponent();
        }

        /// <summary>
        /// Sends the packets to the CC packet buffer for state management.
        /// </summary>
        /// <param name="currentBlock">The current block.</param>
        /// <param name="mediaCore">The media core.</param>
        public void SendPackets(VideoBlock currentBlock, MediaEngine mediaCore)
        {
            Buffer.Write(currentBlock, mediaCore);
        }

        /// <summary>
        /// Updates the CC Packet State Buffer and Renders it.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="clockPosition">The clock position.</param>
        public void Render(CaptionsChannel channel, TimeSpan clockPosition)
        {
            Buffer.UpdateState(channel, clockPosition);
            PaintBuffer();
        }

        /// <summary>
        /// Resets the CC Packet State Buffer and Renders it.
        /// </summary>
        public void Reset()
        {
            Buffer.Reset();
            PaintBuffer();
        }

        /// <summary>
        /// Takes the current state of the CC packet buffer and projects the visual properties
        /// on to the CC visual grid of characters
        /// </summary>
        private void PaintBuffer()
        {
            for (var r = 0; r < ClosedCaptionsBuffer.RowCount; r++)
            {
                for (var c = 0; c < ClosedCaptionsBuffer.ColumnCount; c++)
                {
                    CharacterLookup[r][c].Text = Buffer.State[r][c].Text;
                }
            }
        }

        /// <summary>
        /// Initializes the component.
        /// </summary>
        private void InitializeComponent()
        {
            // Create The Layout Controls
            CaptionsGrid = new Grid { UseLayoutRounding = true, SnapsToDevicePixels = true, Focusable = false };
            Child = CaptionsGrid;

            for (var columnIndex = 0; columnIndex < ClosedCaptionsBuffer.ColumnCount; columnIndex++)
                CaptionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ClosedCaptionsBuffer.ColumnCount, GridUnitType.Star) });

            for (var columnIndex = 0; columnIndex < ClosedCaptionsBuffer.RowCount; columnIndex++)
                CaptionsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ClosedCaptionsBuffer.RowCount, GridUnitType.Star) });

            var textProperty = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
            for (var rowIndex = 0; rowIndex < ClosedCaptionsBuffer.RowCount; rowIndex++)
            {
                for (var columnIndex = 0; columnIndex < ClosedCaptionsBuffer.ColumnCount; columnIndex++)
                {
                    var letterBorder = new Border
                    {
                        Focusable = false,
                        IsHitTestVisible = false,
                        Background = Brushes.Black,
                        Opacity = DefaultOpacity,
                        BorderThickness = new Thickness(0),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Visibility = Visibility.Hidden,
                        Width = BackgroundWidth,
                        Height = BackgroundHeight
                    };

                    var letterText = new TextBlock
                    {
                        Focusable = false,
                        IsHitTestVisible = false,
                        Text = string.Empty,
                        FontFamily = FontFamily,
                        TextAlignment = TextAlignment.Center,
                        Foreground = Brushes.WhiteSmoke,
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = DefaultFontSize,
                        FontWeight = FontWeights.Medium,
                        MaxWidth = letterBorder.Width,
                        MaxHeight = letterBorder.Height
                    };

                    letterBorder.Child = letterText;
                    CaptionsGrid.Children.Add(letterBorder);
                    Grid.SetRow(letterBorder, rowIndex);
                    Grid.SetColumn(letterBorder, columnIndex);
                    if (CharacterLookup.ContainsKey(rowIndex) == false)
                        CharacterLookup[rowIndex] = new Dictionary<int, TextBlock>(ClosedCaptionsBuffer.ColumnCount);

                    CharacterLookup[rowIndex][columnIndex] = letterText;
                    textProperty.AddValueChanged(letterText, (s, ea) =>
                    {
                        var border = letterText.Parent as Border;
                        border.Visibility = string.IsNullOrEmpty(letterText.Text) ?
                            Visibility.Hidden : Visibility.Visible;
                    });
                }
            }

            // Show some preview of the text
            if (GuiContext.Current.IsInDesignTime)
            {
                // Line 11 (index 10) preview
                var sampleText = "L11: Closed Captions (preview)";
                for (var charIndex = 0; charIndex < Math.Min(sampleText.Length, ClosedCaptionsBuffer.ColumnCount); charIndex++)
                {
                    CharacterLookup[10][charIndex].Text = sampleText.Substring(charIndex, 1);
                }

                // Line 12 (index 11) preview
                sampleText = "L12: Closed Captions (preview)";
                for (var charIndex = 0; charIndex < Math.Min(sampleText.Length, ClosedCaptionsBuffer.ColumnCount); charIndex++)
                {
                    CharacterLookup[11][charIndex].Text = sampleText.Substring(charIndex, 1);
                }
            }
        }
    }
}
