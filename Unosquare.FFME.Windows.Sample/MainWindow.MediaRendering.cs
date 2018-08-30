namespace Unosquare.FFME.Windows.Sample
{
    using Shared;
    using System;
    using System.Linq;

    public partial class MainWindow
    {
        /// <summary>
        /// Provides examples for rendering events
        /// </summary>
        private void BindMediaRenderingEvents()
        {
            if (System.Diagnostics.Debugger.IsAttached == false)
                return;

            #region Audio and Video Frame Rendering Variables

            // Setup GDI+ graphics
            System.Drawing.Bitmap overlayBitmap = null;
            System.Drawing.Graphics overlayGraphics = null;
            var overlayTextFont = new System.Drawing.Font("Courier New", 14, System.Drawing.FontStyle.Bold);
            var overlayTextFontBrush = System.Drawing.Brushes.WhiteSmoke;
            var overlayTextOffset = new System.Drawing.PointF(12, 8);
            var overlayBackBuffer = IntPtr.Zero;

            var drawVuMeterLeftPen = new System.Drawing.Pen(System.Drawing.Color.OrangeRed, 12);
            var drawVuMeterRightPen = new System.Drawing.Pen(System.Drawing.Color.GreenYellow, 12);
            var drawVuMeterClock = TimeSpan.Zero;
            var drawVuMeterRmsLock = new object();

            var drawVuMeterLeftValue = 0d;
            var drawVuMeterRightValue = 0d;
            double[] drawVuMeterLeftSamples = null;
            double[] drawVuMeterRightSamples = null;

            const float drawVuMeterLeftOffset = 36;
            const float drawVuMeterTopSpacing = 21;
            const float drawVuMeterTopOffset = 81;
            const float drawVuMeterMinWidth = 5;
            const float drawVuMeterScaleFactor = 20; // RMS * pixel factor = the length of the VU meter lines

            #endregion

            #region Rendering Event Examples

            Media.RenderingVideo += (s, e) =>
            {
                #region Create the overlay buffer to work with

                if (overlayBackBuffer != e.Bitmap.Scan0)
                {
                    lock (drawVuMeterRmsLock)
                    {
                        drawVuMeterLeftValue = 0;
                        drawVuMeterRightValue = 0;
                    }

                    overlayGraphics?.Dispose();
                    overlayBitmap?.Dispose();

                    overlayBitmap = e.Bitmap.CreateDrawingBitmap();

                    overlayBackBuffer = e.Bitmap.Scan0;
                    overlayGraphics = System.Drawing.Graphics.FromImage(overlayBitmap);
                    overlayGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Default;
                }

                #endregion

                #region Draw the text and the VU meter

                var differenceMillis = 0d;
                var leftChannelWidth = 0f;
                var rightChannelWidth = 0f;

                if (e.EngineState.HasAudio)
                {
                    lock (drawVuMeterRmsLock)
                    {
                        differenceMillis = Math.Round(TimeSpan.FromTicks(drawVuMeterClock.Ticks - e.StartTime.Ticks).TotalMilliseconds, 0);
                        leftChannelWidth = drawVuMeterMinWidth + (Convert.ToSingle(drawVuMeterLeftValue) * drawVuMeterScaleFactor);
                        rightChannelWidth = drawVuMeterMinWidth + (Convert.ToSingle(drawVuMeterRightValue) * drawVuMeterScaleFactor);
                    }
                }

                overlayGraphics?.DrawString($"Clock: {e.Clock.TotalSeconds:00.00}\r\nPN   : {e.PictureNumber}\r\nA/V  : {differenceMillis:+000;-000}\r\nL \r\nR",
                    overlayTextFont,
                    overlayTextFontBrush,
                    overlayTextOffset);

                // draw a simple VU meter
                overlayGraphics?.DrawLine(drawVuMeterLeftPen,
                    drawVuMeterLeftOffset,
                    drawVuMeterTopOffset * overlayGraphics.DpiY / 96f,
                    drawVuMeterLeftOffset + leftChannelWidth,
                    drawVuMeterTopOffset * overlayGraphics.DpiY / 96f);

                overlayGraphics?.DrawLine(drawVuMeterRightPen,
                    drawVuMeterLeftOffset,
                    (drawVuMeterTopOffset + drawVuMeterTopSpacing) * overlayGraphics.DpiY / 96f,
                    drawVuMeterLeftOffset + rightChannelWidth,
                    (drawVuMeterTopOffset + drawVuMeterTopSpacing) * overlayGraphics.DpiY / 96f);

                #endregion
            };

            Media.RenderingAudio += (s, e) =>
            {
                // If we don't have video, we don't need to draw a thing.
                if (e.EngineState.HasVideo == false) return;

                // We need to split the samples into left and right sample channels
                if (drawVuMeterLeftSamples == null || drawVuMeterLeftSamples.Length != e.SamplesPerChannel)
                    drawVuMeterLeftSamples = new double[e.SamplesPerChannel];

                if (drawVuMeterRightSamples == null || drawVuMeterRightSamples.Length != e.SamplesPerChannel)
                    drawVuMeterRightSamples = new double[e.SamplesPerChannel];

                // Iterate through the buffer
                var isLeftSample = true;
                var sampleIndex = 0;
                var samplePercent = 0d;

                for (var i = 0; i < e.BufferLength; i += e.BitsPerSample / 8)
                {
                    samplePercent = 100d * e.Buffer.GetAudioSampleLevel(i);

                    if (isLeftSample)
                        drawVuMeterLeftSamples[sampleIndex] = samplePercent;
                    else
                        drawVuMeterRightSamples[sampleIndex] = samplePercent;

                    sampleIndex += !isLeftSample ? 1 : 0;
                    isLeftSample = !isLeftSample;
                }

                // Compute the RMS of the samples and save it for the given point in time.
                lock (drawVuMeterRmsLock)
                {
                    // The VU meter should show the audio RMS, we compute it and save it in a dictionary.
                    drawVuMeterClock = TimeSpan.FromTicks(e.StartTime.Ticks + (e.Duration.Ticks / 2));
                    drawVuMeterLeftValue = Math.Sqrt((1d / drawVuMeterLeftSamples.Length) * drawVuMeterLeftSamples.Sum(n => n));
                    drawVuMeterRightValue = Math.Sqrt((1d / drawVuMeterRightSamples.Length) * drawVuMeterRightSamples.Sum(n => n));
                }
            };

            Media.RenderingSubtitles += (s, e) =>
            {
                // a simple example of suffixing subtitles:
                // if (e.Text != null && e.Text.Count > 0 && e.Text[e.Text.Count - 1] != "(subtitles)")
                //    e.Text.Add("(subtitles)");
            };

            Media.AudioDeviceStopped += async (s, e) =>
            {
                // If we detect that the audio device has stopped, simply
                // call the ChangeMedia command so the default audio device gets selected
                // and reopened. See issue #93
                await Media.ChangeMedia();
            };

            #endregion
        }
    }
}
