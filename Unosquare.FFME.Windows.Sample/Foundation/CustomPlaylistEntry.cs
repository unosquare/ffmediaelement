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
    public sealed class CustomPlaylistEntry : PlaylistEntry
    {
        private static readonly Dictionary<string, string> PropertyMap = new Dictionary<string, string>
        {
            { nameof(Thumbnail), "ffme-thumbnail" },
            { nameof(Format), "info-format" },
            { nameof(LastOpenedUtc), "ffme-lastopened" }
        };

        /// <summary>
        /// Gets or sets the thumbnail.
        /// </summary>
        public string Thumbnail
        {
            get => GetMappedAttributeValue();
            set => SetMappedAttributeValue(value);
        }

        /// <summary>
        /// Gets or sets the format.
        /// </summary>
        public string Format
        {
            get => GetMappedAttributeValue();
            set => SetMappedAttributeValue(value);
        }

        /// <summary>
        /// Gets or sets the last opened UTC.
        /// </summary>
        public DateTime? LastOpenedUtc
        {
            get
            {
                var currentValue = GetMappedAttributeValue();
                if (string.IsNullOrWhiteSpace(currentValue))
                    return default;

                return long.TryParse(currentValue, out var binaryValue) ?
                    DateTime.FromBinary(binaryValue) :
                    default(DateTime?);
            }
            set
            {
                if (value == null)
                {
                    SetMappedAttributeValue(null);
                    return;
                }

                var binaryValue = value.Value.ToBinary().ToString(CultureInfo.InvariantCulture);
                SetMappedAttributeValue(binaryValue);
            }
        }

        private string GetMappedAttributeValue([CallerMemberName] string propertyName = null)
        {
            return Attributes.GetEntryValue(PropertyMap[propertyName ?? throw new ArgumentNullException(nameof(propertyName))]);
        }

        private void SetMappedAttributeValue(string value, [CallerMemberName] string propertyName = null)
        {
            if (Attributes.SetEntryValue(PropertyMap[propertyName ?? throw new ArgumentNullException(nameof(propertyName))], value))
                OnPropertyChanged(propertyName);
        }
    }
}
