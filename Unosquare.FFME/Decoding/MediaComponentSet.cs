namespace Unosquare.FFME.Decoding
{
    using Core;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;

    /// <summary>
    /// Represents a set of Audio, Video and Subtitle components.
    /// This class is useful in order to group all components into 
    /// a single set. Sending packets is automatically handled by
    /// this class. This class is thread safe.
    /// </summary>
    internal sealed class MediaComponentSet : IDisposable
    {
        #region Private Declarations

        /// <summary>
        /// To detect redundant Dispose calls
        /// </summary>
        private bool IsDisposed = false;

        /// <summary>
        /// The internal Components
        /// </summary>
        private readonly Dictionary<MediaType, MediaComponent> Items = new Dictionary<MediaType, MediaComponent>();


        /// <summary>
        /// Provides a cached array to the components backing the All property.
        /// </summary>
        private ReadOnlyCollection<MediaComponent> CachedComponents = null;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaComponentSet"/> class.
        /// </summary>
        internal MediaComponentSet()
        {
            // prevent external initialization
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the available component media types.
        /// </summary>
        public MediaType[] MediaTypes
        {
            get { return Items.Keys.ToArray(); }
        }

        /// <summary>
        /// Gets all the components in a read-only collection.
        /// </summary>
        public ReadOnlyCollection<MediaComponent> All
        {
            get
            {
                if (CachedComponents == null || CachedComponents.Count != Items.Count)
                    CachedComponents = new ReadOnlyCollection<MediaComponent>(Items.Values.ToArray());

                return CachedComponents;
            }
        }

        /// <summary>
        /// Gets the main media component of the stream to which time is synchronized.
        /// By order of priority, first Audio, then Video
        /// </summary>
        public MediaComponent Main { get; private set; }

        /// <summary>
        /// Gets the video component.
        /// Returns null when there is no such stream component.
        /// </summary>
        public VideoComponent Video
        {
            get
            {
                return Items.ContainsKey(MediaType.Video) ? Items[MediaType.Video] as VideoComponent : null;
            }
        }

        /// <summary>
        /// Gets the audio component.
        /// Returns null when there is no such stream component.
        /// </summary>
        public AudioComponent Audio
        {
            get
            {
                return Items.ContainsKey(MediaType.Audio) ? Items[MediaType.Audio] as AudioComponent : null;
            }
        }

        /// <summary>
        /// Gets the subtitles component.
        /// Returns null when there is no such stream component.
        /// </summary>
        public SubtitleComponent Subtitles
        {
            get
            {
                return Items.ContainsKey(MediaType.Subtitle) ? Items[MediaType.Subtitle] as SubtitleComponent : null;
            }
        }

        /// <summary>
        /// Gets the sum of decoded frame count of all media components.
        /// </summary>
        public ulong DecodedFrameCount
        {
            get
            {
                return (ulong)All.Sum(c => (double)c.DecodedFrameCount);
            }
        }

        /// <summary>
        /// Gets the sum of received packets of all media components.
        /// </summary>
        public ulong ReceivedPacketCount
        {
            get
            {
                return (ulong)All.Sum(c => (double)c.ReceivedPacketCount);
            }
        }

        /// <summary>
        /// Gets the length in bytes of the packet buffer.
        /// These packets are the ones that have not been yet deecoded.
        /// </summary>
        public int PacketBufferLength
        {
            get
            {
                return All.Sum(c => c.PacketBufferLength);
            }
        }

        /// <summary>
        /// Gets the number of packets that have not been
        /// fed to the decoders.
        /// </summary>
        public int PacketBufferCount
        {
            get
            {
                return All.Sum(c => c.PacketBufferCount);
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance has a video component.
        /// </summary>
        public bool HasVideo { get { return Video != null; } }

        /// <summary>
        /// Gets a value indicating whether this instance has an audio component.
        /// </summary>
        public bool HasAudio { get { return Audio != null; } }

        /// <summary>
        /// Gets a value indicating whether this instance has a subtitles component.
        /// </summary>
        public bool HasSubtitles { get { return Subtitles != null; } }

        /// <summary>
        /// Gets or sets the <see cref="MediaComponent"/> with the specified media type.
        /// Setting a new component on an existing media type component will throw.
        /// Getting a non existing media component fro the given media type will return null.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentException"></exception>
        /// <exception cref="System.ArgumentNullException">MediaComponent</exception>
        public MediaComponent this[MediaType mediaType]
        {
            get
            {
                return Items.ContainsKey(mediaType) ? Items[mediaType] : null;
            }
            set
            {
                if (Items.ContainsKey(mediaType))
                    throw new ArgumentException($"A component for '{mediaType}' is already registered.");

                if (value == null)
                    throw new ArgumentNullException($"{nameof(MediaComponent)} {nameof(value)} must not be null.");

                Items[mediaType] = value;
                Main = HasAudio ? Audio as MediaComponent : Video as MediaComponent;
            }
        }

        /// <summary>
        /// Removes the component of specified media type (if registered).
        /// It calls the dispose method of the media component too.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        public void Remove(MediaType mediaType)
        {

            if (Items.ContainsKey(mediaType) == false) return;

            try
            {
                var component = Items[mediaType];
                Items.Remove(mediaType);
                component.Dispose();
            }
            catch
            { }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Sends the specified packet to the correct component by reading the stream index
        /// of the packet that is being sent. No packet is sent if the provided packet is set to null.
        /// Returns the media type of the component that accepted the packet.
        /// </summary>
        /// <param name="packet">The packet.</param>
        /// <returns></returns>
        internal unsafe MediaType SendPacket(AVPacket* packet)
        {
            if (packet == null)
                return MediaType.None;

            foreach (var component in All)
            {
                if (component.StreamIndex == packet->stream_index)
                {
                    component.SendPacket(packet);
                    return component.MediaType;
                }
            }

            return MediaType.None;
        }

        /// <summary>
        /// Sends an empty packet to all media components.
        /// When an EOF/EOS situation is encountered, this forces
        /// the decoders to enter drainig mode untill all frames are decoded.
        /// </summary>
        internal void SendEmptyPackets()
        {
            foreach (var component in All)
                component.SendEmptyPacket();
        }

        /// <summary>
        /// Clears the packet queues for all components.
        /// Additionally it flushes the codec buffered packets.
        /// This is useful after a seek operation is performed or a stream
        /// index is changed.
        /// </summary>
        internal void ClearPacketQueues()
        {
            foreach (var component in All)
                component.ClearPacketQueues();
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (!IsDisposed)
            {
                if (alsoManaged)
                {
                    var componentKeys = Items.Keys.ToArray();
                    foreach (var mediaType in componentKeys)
                        Remove(mediaType);
                }

                // free unmanaged resources (unmanaged objects) and override a finalizer below.
                // set large fields to null.

                IsDisposed = true;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
