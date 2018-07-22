namespace Unosquare.FFME
{
    using Primitives;
    using Shared;

    public partial class MediaEngine
    {
        /// <summary>
        /// Gets a value indicating whether a worker interrupt has been requested by the command manager.
        /// This instructs potentially long loops in workers to immediately exit.
        /// </summary>
        private bool IsWorkerInterruptRequested
        {
            get
            {
                return Commands.IsSeeking ||
                    Commands.IsChanging ||
                    Commands.IsClosing ||
                    Commands.IsStopWorkersPending;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the reading worker can read packets at the current time.
        /// This is simply a bit-wise AND of negating <see cref="IsWorkerInterruptRequested"/> == false
        /// and <see cref="ShouldReadMorePackets"/> and <see cref="CanReadMorePackets"/>
        /// </summary>
        private bool CanWorkerReadPackets
        {
            get
            {
                return IsWorkerInterruptRequested == false &&
                    ShouldReadMorePackets &&
                    CanReadMorePackets;
            }
        }

        /// <summary>
        /// Runs the read task which keeps a packet buffer as full as possible.
        /// It reports on DownloadProgress by enqueueing an update to the property
        /// in order to avoid any kind of disruption to this thread caused by the UI thread.
        /// </summary>
        internal void RunPacketReadingWorker()
        {
            // Setup some state variables
            var delay = new DelayProvider(); // The delay provider prevents 100% core usage
            var packetsReadCount = 0; // Holds the packet count for each read cycle
            var t = MediaType.None; // State variables for media types

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
                    packetsReadCount = 0;
                    t = MediaType.None;

                    while (CanWorkerReadPackets)
                    {
                        // Perform a packet read. t will hold the packet type.
                        try { t = Container.Read(); }
                        catch (MediaContainerException) { break; }

                        // Packet skipped
                        if (t == MediaType.None)
                            continue;

                        packetsReadCount++;
                    }

                    // Introduce a delay if we did not read packets
                    if (packetsReadCount <= 0 && IsWorkerInterruptRequested == false)
                        delay.WaitOne();

                    // finish the reading cycle.
                    PacketReadingCycle.Complete();
                }
            }
            catch { throw; }
            finally
            {
                // Always exit notifying the reading cycle is done.
                PacketReadingCycle.Complete();
                delay.Dispose();
            }
        }
    }
}
