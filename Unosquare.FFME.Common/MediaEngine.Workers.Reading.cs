namespace Unosquare.FFME
{
    using Shared;
    using System;

    public partial class MediaEngine
    {
        /// <summary>
        /// Runs the read task which keeps a packet buffer as full as possible.
        /// It reports on DownloadProgress by enqueueing an update to the property
        /// in order to avoid any kind of disruption to this thread caused by the UI thread.
        /// </summary>
        internal void RunPacketReadingWorker()
        {
            var delay = TimeSpan.FromMilliseconds(1);

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

                    // Perform a packet read. t will hold the packet type.
                    if (ShouldWorkerReadPackets)
                    {
                        try { Container.Read(); }
                        catch (MediaContainerException) { break; }
                    }
                    else
                    {
                        // Give it a break until there are packet changes
                        // this prevent pegging the cpu core
                        BufferChangedEvent.Begin();
                        while (IsWorkerInterruptRequested == false)
                        {
                            if (ShouldWorkerReadPackets)
                                break;

                            if (BufferChangedEvent.Wait(delay))
                                break;
                        }
                    }

                    // finish the reading cycle.
                    PacketReadingCycle.Complete();
                }
            }
            catch { throw; }
            finally
            {
                // Always exit notifying the reading cycle is done.
                PacketReadingCycle.Complete();
            }
        }
    }
}
