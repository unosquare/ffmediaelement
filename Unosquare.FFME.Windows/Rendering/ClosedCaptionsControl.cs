namespace Unosquare.FFME.Rendering
{
    using ClosedCaptions;
    using Platform;
    using Primitives;
    using Shared;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;

    /// <summary>
    /// A control that renders Closed Captions
    /// </summary>
    /// <seealso cref="Viewbox" />
    internal class ClosedCaptionsControl : Viewbox
    {
        private const int ColumnCount = 32;
        private const int RowCount = 15;
        private const double BackgroundWidth = 45;
        private const double BackgroundHeight = 80;
        private const double DefaultOpacity = 0.80d;
        private const double DefaultFontSize = 65;

        private readonly Dictionary<int, Dictionary<int, TextBlock>> CharacterLookup = new Dictionary<int, Dictionary<int, TextBlock>>(RowCount);
        private readonly FontFamily FontFamily = new FontFamily("Lucida Console");
        private ISyncLocker StateLock = SyncLockerFactory.Create(useSlim: true);
        private Grid CaptionsGrid = null;

        private int m_RowIndex = 0;
        private int m_ColumnIndex = 0;
        private ClosedCaptionChannel m_Channel = ClosedCaptionChannel.CC1; // TODO: maybe change channel to a dependency property in the MediaElement?
        private ClosedCaptionPacket LastRenderedPacket = null;

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
            Channel = ClosedCaptionChannel.CC1;
            Loaded += (s, e) => InitializeComponent();
        }

        /// <summary>
        /// Gets the current cursor row index (from 0 to 14).
        /// </summary>
        public int RowIndex
        {
            get
            {
                using (StateLock.AcquireReaderLock()) { return m_RowIndex; }
            }

            private set
            {
                using (StateLock.AcquireWriterLock())
                {
                    if (value >= RowCount) value = RowCount - 1;
                    if (value < 0) value = 0;
                    m_RowIndex = value;
                }
            }
        }

        /// <summary>
        /// Gets the current cursor column (from 0 to 31).
        /// </summary>
        public int ColumnIndex
        {
            get
            {
                using (StateLock.AcquireReaderLock()) { return m_ColumnIndex; }
            }

            private set
            {
                using (StateLock.AcquireWriterLock())
                {
                    if (value >= ColumnCount) value = ColumnCount - 1;
                    if (value < 0) value = 0;
                    m_ColumnIndex = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the CC channel to render.
        /// </summary>
        public ClosedCaptionChannel Channel
        {
            get
            {
                using (StateLock.AcquireReaderLock())
                {
                    return m_Channel;
                }
            }

            set
            {
                using (StateLock.AcquireWriterLock())
                {
                    m_Channel = value;
                    LastRenderedPacket = null;
                }
            }
        }

        /// <summary>
        /// Resets the state.
        /// </summary>
        public void ResetState()
        {
            RowIndex = 0;
            ColumnIndex = 0;
            for (var r = 0; r < RowCount; r++)
            {
                for (var c = 0; c < ColumnCount; c++)
                {
                    SetChar(r, c, string.Empty);
                }
            }

            LastRenderedPacket = null;
            Visibility = Visibility.Collapsed;

        }

        /// <summary>
        /// Renders the packets.
        /// </summary>
        /// <param name="currentBlock">The current block.</param>
        /// <param name="mediaCore">The media core.</param>
        public void RenderPacket(VideoBlock currentBlock, MediaEngine mediaCore)
        {
            var channelPackets = mediaCore.ClosedCaptions.Dequeue(currentBlock.StartTime, Channel);

            if (channelPackets.Count > 0 && Visibility != Visibility.Visible)
                Visibility = Visibility.Visible;

            using (StateLock.AcquireWriterLock())
            {
                // TODO: complete implementation
                foreach (var cc in channelPackets)
                {
                    if (LastRenderedPacket != null)
                    {
                        // According to section D.2 Transmission of control code pairs, control codes are transmitted twice
                        // but we don't need to run them twice.
                        if (cc.D0 == LastRenderedPacket.D0 && cc.D0 >= 0x10 && cc.D0 <= 0x1F)
                            continue;
                    }

                    switch (cc.PacketType)
                    {
                        case CCPacketType.Text:
                            {
                                RenderTextPacket(cc);
                                break;
                            }

                        case CCPacketType.Preamble:
                            {
                                RowIndex = cc.PreambleRow - 1;
                                ColumnIndex = 0;
                                break;
                            }

                        case CCPacketType.Tabs:
                            {
                                for (var t = 1; t <= cc.Tabs; t++)
                                {
                                    SetCurrentChar(" ");
                                    ColumnIndex++;
                                }

                                break;
                            }

                        case CCPacketType.MiscCommand:
                            {
                                RenderMiscCommandPacket(cc);
                                break;
                            }

                        default:
                            {
                                break;
                            }
                    }

                    LastRenderedPacket = cc;
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

            for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
                CaptionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColumnCount, GridUnitType.Star) });

            for (var columnIndex = 0; columnIndex < RowCount; columnIndex++)
                CaptionsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowCount, GridUnitType.Star) });

            var textProperty = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
            for (var rowIndex = 0; rowIndex < RowCount; rowIndex++)
            {
                for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
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
                        CharacterLookup[rowIndex] = new Dictionary<int, TextBlock>(ColumnCount);

                    CharacterLookup[rowIndex][columnIndex] = letterText;
                    textProperty.AddValueChanged(letterText, (s, ea) =>
                    {
                        var border = letterText.Parent as Border;
                        border.Visibility = string.IsNullOrEmpty(letterText.Text) ?
                            Visibility.Hidden : Visibility.Visible;
                    });
                }
            }

            if (GuiContext.Current.IsInDesignTime)
            {
                var sampleText = "Hey there, how are you?";
                for (var charIndex = 0; charIndex < sampleText.Length; charIndex++)
                {
                    SetChar(12, charIndex, sampleText.Substring(charIndex, 1));
                }

                sampleText = "This is a second line of text";
                for (var charIndex = 0; charIndex < sampleText.Length; charIndex++)
                {
                    SetChar(13, charIndex, sampleText.Substring(charIndex, 1));
                }
            }
        }

        private void RenderTextPacket(ClosedCaptionPacket c)
        {
            foreach (var ch in c.Text)
            {
                SetCurrentChar(new string(ch, 1));
                ColumnIndex++;
            }
        }

        private void RenderMiscCommandPacket(ClosedCaptionPacket c)
        {
            switch (c.MiscCommand)
            {
                case CCMiscCommandType.RollUp2:
                case CCMiscCommandType.RollUp3:
                case CCMiscCommandType.RollUp4:
                    {
                        ShiftTextUp();
                        RowIndex -= 1;
                        break;
                    }

                case CCMiscCommandType.NewLine:
                    {
                        ColumnIndex = 0;
                        RowIndex += 1;
                        break;
                    }

                case CCMiscCommandType.Backspace:
                    {
                        ColumnIndex -= 1;
                        ClearCurrentChar();
                        break;
                    }

                case CCMiscCommandType.ClearLine:
                    {
                        for (ColumnIndex = 0; ColumnIndex < ColumnCount; ColumnIndex++)
                            ClearCurrentChar();

                        ColumnIndex = 0;
                        break;
                    }

                case CCMiscCommandType.ClearBuffer:
                case CCMiscCommandType.ClearScreen:
                    {
                        for (var ri = 0; ri < RowCount; ri++)
                        {
                            for (var ci = 0; ci < ColumnCount; ci++)
                            {
                                SetChar(ri, ci, string.Empty);
                            }
                        }

                        break;
                    }

                default:
                    {
                        break;
                    }
            }
        }

        private void ShiftTextUp()
        {
            var firstRowTextBlocks = CharacterLookup[0].Values.ToArray();

            for (var rowIndex = 1; rowIndex < RowCount; rowIndex++)
            {
                for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
                {
                    var border = CharacterLookup[rowIndex][columnIndex].Parent as Border;
                    Grid.SetRow(border, rowIndex - 1);
                    CharacterLookup[rowIndex - 1][columnIndex] = CharacterLookup[rowIndex][columnIndex];
                }
            }

            for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
            {
                Grid.SetRow(firstRowTextBlocks[columnIndex].Parent as Border, RowCount - 1);
                CharacterLookup[RowCount - 1][columnIndex] = firstRowTextBlocks[columnIndex];
                firstRowTextBlocks[columnIndex].Text = string.Empty;
            }

            // Simultating Rollup2 style (rollup 2 means 2 rows of text will be active)
            for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
            {
                CharacterLookup[RowIndex - 2][columnIndex].Text = string.Empty;
            }
        }

        /// <summary>
        /// Sets the character at the given position.
        /// </summary>
        /// <param name="rowIndex">Index of the row.</param>
        /// <param name="columnIndex">Index of the column.</param>
        /// <param name="text">The text.</param>
        private void SetChar(int rowIndex, int columnIndex, string text)
        {
            if (rowIndex >= RowCount) rowIndex = RowCount - 1;
            if (rowIndex < 0) rowIndex = 0;

            if (columnIndex >= ColumnCount) columnIndex = ColumnCount - 1;
            if (columnIndex < 0) columnIndex = 0;

            var textChar = string.IsNullOrEmpty(text) ? string.Empty : text;
            textChar = text.Length > 1 ? text.Substring(0, 1) : text;
            CharacterLookup[rowIndex][columnIndex].Text = textChar;
        }

        private void SetCurrentChar(string text)
        {
            if (string.IsNullOrEmpty(text))
                ClearCurrentChar();
            else
                CharacterLookup[RowIndex][ColumnIndex].Text = text.Substring(0, 1);
        }

        private void ClearCurrentChar()
        {
            CharacterLookup[RowIndex][ColumnIndex].Text = string.Empty;
        }
    }
}
