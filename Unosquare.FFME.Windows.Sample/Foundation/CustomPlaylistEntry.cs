namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using Playlists;
    using System;
    using System.Globalization;

    /// <summary>
    /// A custom playlist entry with notification properties backed by Attributes
    /// </summary>
    /// <seealso cref="PlaylistEntry" />
    public class CustomPlaylistEntry : PlaylistEntry
    {
        /// <summary>
        /// Initializes static members of the <see cref="CustomPlaylistEntry"/> class.
        /// </summary>
        static CustomPlaylistEntry()
        {
            IAttributeContainerExtensions.RegisterPropertyMapping(
                typeof(CustomPlaylistEntry), nameof(Thumbnail), "ffme-thumbnail");
            IAttributeContainerExtensions.RegisterPropertyMapping(
                typeof(CustomPlaylistEntry), nameof(Format), "info-format");
            IAttributeContainerExtensions.RegisterPropertyMapping(
                typeof(CustomPlaylistEntry), nameof(LastOpenedUtc), "ffme-lastopened");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomPlaylistEntry"/> class.
        /// </summary>
        public CustomPlaylistEntry()
            : base()
        {
            // placeholder
        }

        /// <summary>
        /// Gets or sets the thumbnail.
        /// </summary>
        public string Thumbnail
        {
            get => this.GetAttributeValue();
            set => this.SetAttributeValue(value);
        }

        /// <summary>
        /// Gets or sets the format.
        /// </summary>
        public string Format
        {
            get => this.GetAttributeValue();
            set => this.SetAttributeValue(value);
        }

        /// <summary>
        /// Gets or sets the last opened UTC.
        /// </summary>
        public DateTime? LastOpenedUtc
        {
            get
            {
                if (this.ContainsAttributeFor() == false)
                    return default(DateTime?);

                if (long.TryParse(this.GetAttributeValue(), out long binaryValue))
                    return DateTime.FromBinary(binaryValue);

                return default(DateTime?);
            }
            set
            {
                if (value == null)
                {
                    this.SetAttributeValue(null);
                    return;
                }

                var binaryValue = value.Value.ToBinary().ToString(CultureInfo.InvariantCulture);
                this.SetAttributeValue(binaryValue);
            }
        }
    }
}
