namespace Unosquare.FFME.Rendering
{
    using ClosedCaptions;
    using Platform;
    using Primitives;
    using System.Collections.Generic;
    using System.ComponentModel;
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
        private readonly FontFamily FontFamily = new FontFamily("Courier New");
        private ISyncLocker StateLock = SyncLockerFactory.Create(useSlim: true);

        private int m_CurrentRow = 0;
        private int m_CurrentColumn = 0;
        private CaptionsChannel m_Channel = 0; // TODO: maybe change channel to a dependency property?

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
            Channel = CaptionsChannel.CC1;
            Loaded += (s, e) => InitializeComponent();
        }

        /// <summary>
        /// Enumerates the 4 different CC channels
        /// </summary>
        public enum CaptionsChannel
        {
            /// <summary>
            /// Field 1, Channel 1
            /// </summary>
            CC1 = 1,

            /// <summary>
            /// Field 1, Cahnnel 2
            /// </summary>
            CC2 = 2,

            /// <summary>
            /// Field 2, Channel 1
            /// </summary>
            CC3 = 3,

            /// <summary>
            /// Field 2, Channel 2
            /// </summary>
            CC4 = 4,
        }

        /// <summary>
        /// Gets the current cursor row.
        /// </summary>
        public int Row
        {
            get { using (StateLock.AcquireReaderLock()) { return m_CurrentRow; } }
            private set { using (StateLock.AcquireWriterLock()) { m_CurrentRow = value; } }
        }

        /// <summary>
        /// Gets the current cursor column.
        /// </summary>
        public int Column
        {
            get { using (StateLock.AcquireReaderLock()) { return m_CurrentColumn; } }
            private set { using (StateLock.AcquireWriterLock()) { m_CurrentColumn = value; } }
        }

        /// <summary>
        /// Gets or sets the CC channel to render.
        /// </summary>
        public CaptionsChannel Channel
        {
            get { using (StateLock.AcquireReaderLock()) { return m_Channel; } }
            set { using (StateLock.AcquireWriterLock()) { m_Channel = value; } }
        }

        /// <summary>
        /// Resets the state.
        /// </summary>
        public void ResetState()
        {
            Row = 0;
            Column = 0;
            for (var r = 0; r < RowCount; r++)
            {
                for (var c = 0; c < ColumnCount; c++)
                {
                    SetChar(r, c, string.Empty);
                }
            }

            Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Renders the closed caption packets coming in the frame.
        /// </summary>
        /// <param name="packets">The packets.</param>
        public void RenderPackets(ClosedCaptionCollection packets)
        {
            if (packets.All.Count > 0 && Visibility != Visibility.Visible)
                Visibility = Visibility.Visible;

            using (StateLock.AcquireWriterLock())
            {
                List<ClosedCaptionPacket> channelPackets = null;
                switch (Channel)
                {
                    case CaptionsChannel.CC1:
                        channelPackets = packets.CC1;
                        break;
                    case CaptionsChannel.CC2:
                        channelPackets = packets.CC2;
                        break;
                    case CaptionsChannel.CC3:
                        channelPackets = packets.CC3;
                        break;
                    case CaptionsChannel.CC4:
                        channelPackets = packets.CC4;
                        break;
                    default:
                        channelPackets = packets.CC1;
                        break;
                }

                // TODO: complete implementation
            }
        }

        /// <summary>
        /// Initializes the component.
        /// </summary>
        private void InitializeComponent()
        {
            // Create The Layout Controls
            var captionsGrid = new Grid { UseLayoutRounding = true, SnapsToDevicePixels = true, Focusable = false };
            Child = captionsGrid;

            for (var columnIndex = 0; columnIndex < ColumnCount; columnIndex++)
                captionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColumnCount, GridUnitType.Star) });

            for (var columnIndex = 0; columnIndex < RowCount; columnIndex++)
                captionsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RowCount, GridUnitType.Star) });

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
                    captionsGrid.Children.Add(letterBorder);
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

        /// <summary>
        /// Sets the character at the given position.
        /// </summary>
        /// <param name="rowIndex">Index of the row.</param>
        /// <param name="columnIndex">Index of the column.</param>
        /// <param name="text">The text.</param>
        private void SetChar(int rowIndex, int columnIndex, string text)
        {
            CharacterLookup[rowIndex][columnIndex].Text = string.IsNullOrEmpty(text) ? string.Empty : text.Substring(0, 1);
        }
    }
}
