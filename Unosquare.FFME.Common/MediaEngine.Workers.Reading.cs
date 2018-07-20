namespace Unosquare.FFME
{
    using Primitives;
    using Shared;

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

            // Signal the start of a buffering operation
            State.SignalBufferingStarted();

            #endregion

            #region Worker Loop

            try
            {
                // Worker logic begins here
                while (Commands.IsStopWorkersPending == false)
                {
                    // Determine what to do on a priority command
                    if (Commands.IsExecutingDirectCommand)
                    {
                        if (Commands.IsClosing) break;
                        if (Commands.IsChanging) Commands.WaitForDirectCommand();
                    }

                    // Wait for seeking or changing to be done.
                    Commands.WaitForActiveSeekCommand();

                    // Enter a packet reading cycle
                    PacketReadingCycle.Begin();

                    // Initialize Packets read to 0 for each component and state variables
                    foreach (var k in Container.Components.MediaTypes)
                        packetsRead[k] = 0;

                    // Start to perform the read loop
                    // NOTE: Disrupting the packet reader causes errors in UPD streams. Disrupt as little as possible
                    while (ShouldReadMorePackets
                        && CanReadMorePackets
                        && Commands.IsActivelySeeking == false)
                    {
                        // Perform a packet read. t will hold the packet type.
                        try { t = Container.Read(); }
                        catch (MediaContainerException) { continue; }

                        // We could not read any packets.
                        // We'll check again on the next reading cycle.
                        if (t == MediaType.None)
                            break;

                        // Discard packets that we don't need (i.e. MediaType == None)
                        if (Container.Components.MediaTypes.Contains(t) == false)
                            continue;

                        // Update the packet count for the components
                        packetsRead[t] += 1;

                        // Ensure we have read at least some packets from main and auxiliary streams.
                        if (packetsRead.ContainsMoreThan(0))
                            break;
                    }

                    // finish the reading cycle.
                    PacketReadingCycle.Complete();

                    // Don't evaluate a pause/delay condition if we are seeking
                    if (Commands.IsActivelySeeking)
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
            catch { throw; }
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
