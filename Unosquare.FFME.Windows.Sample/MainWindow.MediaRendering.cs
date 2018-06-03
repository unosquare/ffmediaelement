namespace Unosquare.FFME.Windows.Sample
{
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;

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
            var overlayTextFont = new System.Drawing.Font("Arial", 14, System.Drawing.FontStyle.Bold);
            var overlayTextFontBrush = System.Drawing.Brushes.WhiteSmoke;
            var overlayTextOffset = new System.Drawing.PointF(12, 8);
            var overlayBackBuffer = IntPtr.Zero;

            var drawVuMeterLeftPen = new System.Drawing.Pen(System.Drawing.Color.OrangeRed, 12);
            var drawVuMeterRightPen = new System.Drawing.Pen(System.Drawing.Color.GreenYellow, 12);
            var drawVuMeterRmsLock = new object();
            var drawVuMeterLeftRms = new SortedDictionary<TimeSpan, double>();
            var drawVuMeterRightRms = new SortedDictionary<TimeSpan, double>();

            var drawVuMeterLeftValue = 0d;
            var drawVuMeterRightValue = 0d;
            const float drawVuMeterLeftOffset = 16;
            const float drawVuMeterTopOffset = 50;
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
                        drawVuMeterLeftRms.Clear();
                        drawVuMeterRightRms.Clear();
                    }

                    if (overlayGraphics != null) overlayGraphics.Dispose();
                    if (overlayBitmap != null) overlayBitmap.Dispose();

                    overlayBitmap = e.Bitmap.CreateDrawingBitmap();

                    overlayBackBuffer = e.Bitmap.Scan0;
                    overlayGraphics = System.Drawing.Graphics.FromImage(overlayBitmap);
                    overlayGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Default;
                }

                #endregion

                #region Read the instantaneous RMS of the audio

                lock (drawVuMeterRmsLock)
                {
                    var position = e.Clock;
                    drawVuMeterLeftValue = drawVuMeterLeftRms.Where(kvp => kvp.Key > position).Select(kvp => kvp.Value).FirstOrDefault();
                    drawVuMeterRightValue = drawVuMeterRightRms.Where(kvp => kvp.Key > position).Select(kvp => kvp.Value).FirstOrDefault();

                    // do some cleanup so the dictionary does not grow too big.
                    if (drawVuMeterLeftRms.Count > 256)
                    {
                        var keysToRemove = drawVuMeterLeftRms.Keys.Where(k => k < position).OrderBy(k => k).ToArray();
                        foreach (var k in keysToRemove)
                        {
                            drawVuMeterLeftRms.Remove(k);
                            drawVuMeterRightRms.Remove(k);

                            if (drawVuMeterLeftRms.Count < 256)
                                break;
                        }
                    }
                }

                #endregion

                #region Draw the text and the VU meter

                var differenceMillis = TimeSpan.FromTicks(e.Clock.Ticks - e.StartTime.Ticks).TotalMilliseconds;

                overlayGraphics.DrawString($"Clock: {e.StartTime.TotalSeconds:00.000} | Skew: {differenceMillis:00.000} | PN: {e.PictureNumber}",
                    overlayTextFont,
                    overlayTextFontBrush,
                    overlayTextOffset);

                // draw a simple VU meter
                overlayGraphics.DrawLine(drawVuMeterLeftPen,
                    drawVuMeterLeftOffset,
                    drawVuMeterTopOffset,
                    drawVuMeterLeftOffset + 5 + (Convert.ToSingle(drawVuMeterLeftValue) * drawVuMeterScaleFactor),
                    drawVuMeterTopOffset);

                overlayGraphics.DrawLine(drawVuMeterRightPen,
                    drawVuMeterLeftOffset,
                    drawVuMeterTopOffset + 20,
                    drawVuMeterLeftOffset + 5 + (Convert.ToSingle(drawVuMeterRightValue) * drawVuMeterScaleFactor),
                    drawVuMeterTopOffset + 20);

                #endregion
            };

            Media.RenderingAudio += (s, e) =>
            {
                // The buffer contains all the samples
                var buffer = new byte[e.BufferLength];
                Marshal.Copy(e.Buffer, buffer, 0, e.BufferLength);

                // We need to split the samples into left and right samples
                var leftSamples = new double[e.SamplesPerChannel];
                var rightSamples = new double[e.SamplesPerChannel];

                // Iterate through the buffer
                var isLeftSample = true;
                var sampleIndex = 0;
                var samplePercent = default(double);

                for (var i = 0; i < e.BufferLength; i += e.BitsPerSample / 8)
                {
                    samplePercent = 100d * buffer.GetAudioSampleLevel(i);

                    if (isLeftSample)
                        leftSamples[sampleIndex] = samplePercent;
                    else
                        rightSamples[sampleIndex] = samplePercent;

                    sampleIndex += !isLeftSample ? 1 : 0;
                    isLeftSample = !isLeftSample;
                }

                // Compute the RMS of the samples and save it for the given point in time.
                lock (drawVuMeterRmsLock)
                {
                    // The VU meter should show the audio RMS, we compute it and save it in a dictionary.
                    drawVuMeterLeftRms[e.StartTime] = Math.Sqrt((1d / leftSamples.Length) * leftSamples.Sum(n => n));
                    drawVuMeterRightRms[e.StartTime] = Math.Sqrt((1d / rightSamples.Length) * rightSamples.Sum(n => n));
                }
            };

            Media.RenderingSubtitles += (s, e) =>
            {
                // a simple example of suffixing subtitles
                // if (e.Text != null && e.Text.Count > 0 && e.Text[e.Text.Count - 1] != "(subtitles)")
                //    e.Text.Add("(subtitles)");
            };

            #endregion
        }
    }
}
