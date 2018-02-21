namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using Playlists;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// A custom playlist entry with notification properties backed by Attributes
    /// </summary>
    /// <seealso cref="PlaylistEntry" />
    public class CustomPlaylistEntry : PlaylistEntry
    {
        private static readonly Dictionary<string, string> PropertyMap = new Dictionary<string, string>()
        {
            { nameof(Thumbnail), "ffme-thumbnail" },
            { nameof(Format), "info-format" },
            { nameof(LastOpenedUtc), "ffme-lastopened" },
        };

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
            get => GetMappedAttibuteValue();
            set => SetMappedAttibuteValue(value);
        }

        /// <summary>
        /// Gets or sets the format.
        /// </summary>
        public string Format
        {
            get => GetMappedAttibuteValue();
            set => SetMappedAttibuteValue(value);
        }

        /// <summary>
        /// Gets or sets the last opened UTC.
        /// </summary>
        public DateTime? LastOpenedUtc
        {
            get
            {
                var currentValue = GetMappedAttibuteValue();
                if (string.IsNullOrWhiteSpace(currentValue))
                    return default;

                if (long.TryParse(currentValue, out long binaryValue))
                    return DateTime.FromBinary(binaryValue);

                return default;
            }
            set
            {
                if (value == null)
                {
                    SetMappedAttibuteValue(null);
                    return;
                }

                var binaryValue = value.Value.ToBinary().ToString(CultureInfo.InvariantCulture);
                SetMappedAttibuteValue(binaryValue);
            }
        }

        private string GetMappedAttibuteValue([CallerMemberName] string propertyName = null)
        {
            return Attributes.GetEntryValue(PropertyMap[propertyName]);
        }

        private void SetMappedAttibuteValue(string value, [CallerMemberName] string propertyName = null)
        {
            if (Attributes.SetEntryValue(PropertyMap[propertyName], value))
                OnPropertyChanged(propertyName);
        }
    }
}
