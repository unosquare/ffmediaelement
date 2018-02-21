namespace Unosquare.FFME.Decoding
{
    using FFmpeg.AutoGen;
    using Primitives;
    using Shared;
    using System;
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
        /// The internal Components
        /// </summary>
        private readonly MediaTypeDictionary<MediaComponent> Items = new MediaTypeDictionary<MediaComponent>();

        /// <summary>
        /// The synchronize lock
        /// </summary>
        private readonly object SyncLock = new object();

        /// <summary>
        /// Provides a cached array to the components backing the All property.
        /// </summary>
        private ReadOnlyCollection<MediaComponent> CachedComponents = null;

        /// <summary>
        /// To detect redundant Dispose calls
        /// </summary>
        private bool IsDisposed = false;

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
            get { lock (SyncLock) return Items.Keys.ToArray(); }
        }

        /// <summary>
        /// Gets all the components in a read-only collection.
        /// </summary>
        public ReadOnlyCollection<MediaComponent> All
        {
            get
            {
                lock (SyncLock)
                {
                    if (CachedComponents == null || CachedComponents.Count != Items.Count)
                        CachedComponents = new ReadOnlyCollection<MediaComponent>(Items.Values.ToArray());

                    return CachedComponents;
                }
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
            get { lock (SyncLock) return Items[MediaType.Video] as VideoComponent; }
        }

        /// <summary>
        /// Gets the audio component.
        /// Returns null when there is no such stream component.
        /// </summary>
        public AudioComponent Audio
        {
            get { lock (SyncLock) return Items[MediaType.Audio] as AudioComponent; }
        }

        /// <summary>
        /// Gets the subtitles component.
        /// Returns null when there is no such stream component.
        /// </summary>
        public SubtitleComponent Subtitles
        {
            get { lock (SyncLock) return Items[MediaType.Subtitle] as SubtitleComponent; }
        }

        /// <summary>
        /// Gets the first packet DTS.
        /// </summary>
        public long? FirstPacketDts { get; private set; } = default;

        /// <summary>
        /// Gets the stream index of the first packet received.
        /// </summary>
        public int? FisrtPacketStreamIndex { get; private set; } = default;

        /// <summary>
        /// Gets the first packet byte position.
        /// </summary>
        public long? FirstPacketPosition { get; private set; } = default;

        /// <summary>
        /// Gets the current length in bytes of the packet buffer.
        /// These packets are the ones that have not been yet deecoded.
        /// </summary>
        public int PacketBufferLength
        {
            get { lock (SyncLock) return All.Sum(c => c.PacketBufferLength); }
        }

        /// <summary>
        /// Gets the number of packets that have not been
        /// fed to the decoders.
        /// </summary>
        public int PacketBufferCount
        {
            get { lock (SyncLock) return All.Sum(c => c.PacketBufferCount); }
        }

        /// <summary>
        /// Gets the total bytes read by all components in the lifetime of this object.
        /// </summary>
        public ulong LifetimeBytesRead
        {
            get
            {
                lock (SyncLock)
                {
                    ulong result = 0;
                    foreach (var c in All)
                        result = unchecked(result + c.LifetimeBytesRead);

                    return result;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance has a video component.
        /// </summary>
        public bool HasVideo
        {
            get { lock (SyncLock) return Items.ContainsKey(MediaType.Video); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance has an audio component.
        /// </summary>
        public bool HasAudio
        {
            get { lock (SyncLock) return Items.ContainsKey(MediaType.Audio); }
        }

        /// <summary>
        /// Gets a value indicating whether this instance has a subtitles component.
        /// </summary>
        public bool HasSubtitles
        {
            get { lock (SyncLock) return Items.ContainsKey(MediaType.Subtitle); }
        }

        /// <summary>
        /// Gets or sets the <see cref="MediaComponent"/> with the specified media type.
        /// Setting a new component on an existing media type component will throw.
        /// Getting a non existing media component fro the given media type will return null.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        /// <returns>The media component</returns>
        /// <exception cref="ArgumentException">When the media type is invalid</exception>
        /// <exception cref="ArgumentNullException">MediaComponent</exception>
        public MediaComponent this[MediaType mediaType]
        {
            get
            {
                lock (SyncLock) return Items.ContainsKey(mediaType) ? Items[mediaType] : null;
            }
            set
            {
                lock (SyncLock)
                {
                    if (Items.ContainsKey(mediaType))
                        throw new ArgumentException($"A component for '{mediaType}' is already registered.");
                    Items[mediaType] = value ?? throw new ArgumentNullException($"{nameof(MediaComponent)} {nameof(value)} must not be null.");

                    // Try for the main component to be the video (if it's not stuff like audio album art, that is)
                    if (HasVideo && HasAudio &&
                        (Video.StreamInfo.Disposition & ffmpeg.AV_DISPOSITION_ATTACHED_PIC) != ffmpeg.AV_DISPOSITION_ATTACHED_PIC)
                    {
                        Main = Video;
                        return;
                    }

                    // If it was not vide, then it has to be audio (if it has audio)
                    if (HasAudio)
                    {
                        Main = Audio;
                        return;
                    }

                    // Set it to video even if it's attached pic stuff
                    if (HasVideo)
                    {
                        Main = Video;
                        return;
                    }

                    // As a last resort, set the main component to be the subtitles
                    if (HasSubtitles)
                    {
                        Main = Subtitles;
                        return;
                    }

                    // We whould never really hit this line
                    if (Items.Count > 0)
                        Main = Items.First().Value;
                }
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

        #region Methods

        /// <summary>
        /// Sends the specified packet to the correct component by reading the stream index
        /// of the packet that is being sent. No packet is sent if the provided packet is set to null.
        /// Returns the media type of the component that accepted the packet.
        /// </summary>
        /// <param name="packet">The packet.</param>
        /// <returns>The media type</returns>
        public unsafe MediaType SendPacket(AVPacket* packet)
        {
            lock (SyncLock)
            {
                if (packet == null)
                    return MediaType.None;

                if (FirstPacketDts == null && packet->dts != ffmpeg.AV_NOPTS_VALUE)
                {
                    FirstPacketDts = packet->dts;
                    FisrtPacketStreamIndex = packet->stream_index;
                    FirstPacketPosition = packet->pos;
                }

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
        }

        /// <summary>
        /// Sends an empty packet to all media components.
        /// When an EOF/EOS situation is encountered, this forces
        /// the decoders to enter drainig mode until all frames are decoded.
        /// </summary>
        public void SendEmptyPackets()
        {
            lock (SyncLock)
                foreach (var component in All)
                    component.SendEmptyPacket();
        }

        /// <summary>
        /// Clears the packet queues for all components.
        /// Additionally it flushes the codec buffered packets.
        /// This is useful after a seek operation is performed or a stream
        /// index is changed.
        /// </summary>
        public void ClearPacketQueues()
        {
            lock (SyncLock)
                foreach (var component in All)
                    component.ClearPacketQueues();
        }

        #endregion

        #region IDisposable Support

        /// <summary>
        /// Removes the component of specified media type (if registered).
        /// It calls the dispose method of the media component too.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        private void RemoveComponent(MediaType mediaType)
        {
            lock (SyncLock)
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
        }

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
                        RemoveComponent(mediaType);
                }

                // free unmanaged resources (unmanaged objects) and override a finalizer below.
                // and set large fields to null.
                IsDisposed = true;
            }
        }

        #endregion
    }
}
