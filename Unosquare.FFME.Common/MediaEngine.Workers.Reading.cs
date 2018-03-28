namespace Unosquare.FFME
{
    using Primitives;
    using Shared;
    using System.Threading;

    public partial class MediaEngine
    {
        /// <summary>
        /// Runs the read task which keeps a packet buffer as full as possible.
        /// It reports on DownloadProgress by enqueueing an update to the property
        /// in order to avoid any kind of disruption to this thread caused by the UI thread.
        /// </summary>
        internal void RunPacketReadingWorker()
        {
            #region Worker State Setup

            // The delay provider prevents 100% core usage
            var delay = new DelayProvider();

            // Holds the packet count for each read cycle
            var packetsRead = new MediaTypeDictionary<int>();

            // State variables for media types
            var t = MediaType.None;

            // Store Container in local variable to prevent NullReferenceException
            // when dispose occurs sametime with read cycle
            var mediaContainer = Container;

            var main = mediaContainer.Components.Main.MediaType;
            var auxs = mediaContainer.Components.MediaTypes.FundamentalAuxsFor(main);
            var all = main.JoinMediaTypes(auxs);

            #endregion

            #region Worker Loop

            try
            {
                // Worker logic begins here
                while (IsTaskCancellationPending == false)
                {
                    // Wait for seeking to be done.
                    SeekingDone.Wait();

                    // Enter a packet reading cycle
                    PacketReadingCycle.Begin();

                    // Initialize Packets read to 0 for each component and state variables
                    foreach (var k in mediaContainer.Components.MediaTypes)
                        packetsRead[k] = 0;

                    // Start to perform the read loop
                    // NOTE: Disrupting the packet reader causes errors in UPD streams. Disrupt as little as possible
                    while (CanReadMorePackets && ShouldReadMorePackets && IsTaskCancellationPending == false)
                    {
                        // Perform a packet read. t will hold the packet type.
                        try
                        {
                           t = mediaContainer.Read();
                        } 
                        catch (MediaContainerException)
                        {
                           continue;
                        }

                        // Discard packets that we don't need (i.e. MediaType == None)
                        if (mediaContainer.Components.MediaTypes.HasMediaType(t) == false)
                            continue;

                        // Update the packet count for the components
                        packetsRead[t] += 1;

                        // Ensure we have read at least some packets from main and auxiliary streams.
                        if (packetsRead.FundamentalsGreaterThan(0))
                            break;
                    }

                    // finish the reading cycle.
                    PacketReadingCycle.Complete();

                    // Don't evaluate a pause condition if we are seeking
                    if (SeekingDone.IsInProgress)
                        continue;

                    // Wait some if we have a full packet buffer or we are unable to read more packets (i.e. EOF).
                    if (ShouldReadMorePackets == false
                        || CanReadMorePackets == false
                        || packetsRead.GetSum() <= 0)
                    {
                        delay.WaitOne();
                    }
                }
            }
            catch (ThreadAbortException) { /* swallow */ }
            catch { if (!IsDisposed) throw; }
            finally
            {
                // Always exit notifying the reading cycle is done.
                PacketReadingCycle.Complete();
                delay.Dispose();
            }

            #endregion
        }
    }
}
