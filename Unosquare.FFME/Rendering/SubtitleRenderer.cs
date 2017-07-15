namespace Unosquare.FFME.Rendering
{
    using System;
    using Decoding;
    using System.Windows;
    using Unosquare.FFME.Core;
    using System.Collections.Generic;

    /// <summary>
    /// Subtitle Renderer - Does nothing at this point.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Rendering.IRenderer" />
    internal class SubtitleRenderer : IRenderer
    {

        /// <summary>
        /// The synchronize lock
        /// </summary>
        private readonly object SyncLock = new object();

        private TimeSpan? StartTime = new TimeSpan?();
        private TimeSpan? EndTime = new TimeSpan?();
        private string CurrentText = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubtitleRenderer"/> class.
        /// </summary>
        /// <param name="mediaElement">The media element.</param>
        public SubtitleRenderer(MediaElement mediaElement)
        {
            MediaElement = mediaElement;
        }

        /// <summary>
        /// Executed when the Close method is called on the parent MediaElement
        /// </summary>
        public void Close()
        {
            SetText(string.Empty);
        }

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        public void Pause()
        {
            //placeholder
        }

        /// <summary>
        /// Executed when the Play method is called on the parent MediaElement
        /// </summary>
        public void Play()
        {
            //placeholder
        }

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        public void Stop()
        {
            SetText(string.Empty);
        }

        /// <summary>
        /// Executed after a Seek operation is performed on the parent MediaElement
        /// </summary>
        public void Seek()
        {
            // placeholder
        }

        /// <summary>
        /// Gets or creates the tex blocks that make up the subtitle text and outline.
        /// </summary>
        /// <returns></returns>
        private List<System.Windows.Controls.TextBlock> GetTextBlocks()
        {
            const string SubtitleElementNamePrefix = "SubtitlesTextBlock_";
            const double OutlineWidth = 1d;

            var contentChildren = MediaElement.ContentGrid.Children;
            var textBlocks = new List<System.Windows.Controls.TextBlock>();
            for (var i = 0; i < contentChildren.Count; i++)
            {
                var currentChild = contentChildren[i] as System.Windows.Controls.TextBlock;
                if (currentChild == null)
                    continue;

                if (currentChild.Name.StartsWith(SubtitleElementNamePrefix))
                    textBlocks.Add(currentChild);
            }

            if (textBlocks.Count > 0) return textBlocks;

            var m = new System.Windows.Thickness(40, 0, 40, 200);

            for (var i = 0; i <= 4; i++)
            {
                var textBlock = new System.Windows.Controls.TextBlock()
                {
                    Name = $"{SubtitleElementNamePrefix}{i}",
                    TextWrapping = System.Windows.TextWrapping.Wrap,
                    FontSize = 40,
                    FontWeight = FontWeights.DemiBold,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Black),
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Bottom,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    Margin = m
                };

                textBlocks.Add(textBlock);
            }

            textBlocks[1].Margin = new System.Windows.Thickness(m.Left + OutlineWidth, m.Top, m.Right - OutlineWidth, m.Bottom);
            textBlocks[2].Margin = new System.Windows.Thickness(m.Left - OutlineWidth, m.Top, m.Right + OutlineWidth, m.Bottom);
            textBlocks[3].Margin = new System.Windows.Thickness(m.Left, m.Top + OutlineWidth, m.Right, m.Bottom - OutlineWidth);
            textBlocks[4].Margin = new System.Windows.Thickness(m.Left, m.Top - OutlineWidth, m.Right, m.Bottom + OutlineWidth);

            textBlocks[0].Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);

            textBlocks[0].Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 4,
                Color = System.Windows.Media.Colors.Black,
                Direction = 315,
                Opacity = 0.75,
                RenderingBias = System.Windows.Media.Effects.RenderingBias.Performance,
                ShadowDepth = 4
            };

            for (var i = 4; i >= 0; i--)
                MediaElement.ContentGrid.Children.Add(textBlocks[i]);

            return textBlocks;
        }

        /// <summary>
        /// Sets the text to be rendered on the text blocks.
        /// </summary>
        /// <param name="text">The text.</param>
        private void SetText(string text)
        {
            Utils.UIEnqueueInvoke(System.Windows.Threading.DispatcherPriority.DataBind, new Action<string>((s) =>
            {
                lock (SyncLock)
                {
                    CurrentText = text;
                    var textBlocks = GetTextBlocks();
                    foreach (var tb in textBlocks)
                        tb.Text = s;
                }
            }), text);
        }

        /// <summary>
        /// Renders the specified media block.
        /// </summary>
        /// <param name="mediaBlock">The media block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <param name="renderIndex">Index of the render.</param>
        public void Render(MediaBlock mediaBlock, TimeSpan clockPosition, int renderIndex)
        {
            var subtitleBlock = mediaBlock as SubtitleBlock;
            if (subtitleBlock == null) return;

            StartTime = subtitleBlock.StartTime;
            EndTime = subtitleBlock.EndTime;

            var textToRender = string.Join("\r\n", subtitleBlock.Text);

            SetText(textToRender);
        }

        /// <summary>
        /// Called when a media block must stop being rendered.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="clockPosition">The clock position.</param>
        public void Update(TimeSpan clockPosition)
        {
            if (string.IsNullOrWhiteSpace(CurrentText))
                return;

            if (StartTime.HasValue == false || EndTime.HasValue == false)
            {
                SetText(string.Empty);
                return;
            }

            if (clockPosition > EndTime.Value || clockPosition < StartTime.Value)
            {
                SetText(string.Empty);
                return;
            }

        }

        /// <summary>
        /// Gets the parent media element.
        /// </summary>
        public MediaElement MediaElement { get; private set; }
    }
}
