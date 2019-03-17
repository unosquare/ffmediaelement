namespace Unosquare.FFME.Playlists
{
    using Engine;
    using System;
    using System.Collections.Generic;
    using System.Web;

    /// <summary>
    /// Helper methods for parsing m3u8 playlists
    /// </summary>
    internal static class PlaylistExtensions
    {
        /// <summary>
        /// Parses the header line of a playlist.
        /// </summary>
        /// <typeparam name="T">The type of playlist items</typeparam>
        /// <param name="playlist">The playlist to parse data attributes into.</param>
        /// <param name="line">The line.</param>
        public static void ParseHeaderLine<T>(this PlaylistEntryCollection<T> playlist, string line)
            where T : PlaylistEntry, new()
        {
            // Get the line of text removing the start of the line data
            var headerAttributesText = line.Substring($"{PlaylistEntryCollection<T>.HeaderPrefix} ".Length).Trim();
            var headerAttributes = headerAttributesText.ParseAttributes();

            foreach (var attribute in headerAttributes)
                playlist.Attributes[attribute.Key] = attribute.Value;

            playlist.Name = headerAttributesText;
        }

        /// <summary>
        /// Parses the extended information line into a Playlist entry.
        /// </summary>
        /// <param name="entry">The playlist entry item to parse data into.</param>
        /// <param name="line">The line of text.</param>
        public static void BeginExtendedInfoLine(this PlaylistEntry entry, string line)
        {
            // Get the line of text removing the start of the line data
            var entryAttributesText = line.Substring($"{PlaylistEntryCollection.EntryPrefix}:".Length).Trim();
            var entryAttributes = entryAttributesText.ParseAttributes();

            foreach (var attribute in entryAttributes)
            {
                entryAttributesText = entryAttributesText.ReplaceOrdinal(attribute.Substring, string.Empty);
                entry.Attributes[attribute.Key] = attribute.Value;
            }

            // We need to further parse the data beyond the attributes
            // so we parse the left-over text.
            entryAttributesText = entryAttributesText.Trim();

            // The extra fields are comma-separated
            var headerFields = entryAttributesText.Split(',');

            // The first field is the duration
            if (headerFields.Length >= 1 && long.TryParse(headerFields[0].Trim(), out var duration))
                entry.Duration = TimeSpan.FromSeconds(Convert.ToDouble(duration));

            // The next field is the title
            if (headerFields.Length >= 2)
                entry.Title = headerFields[1].Trim();
        }

        /// <summary>
        /// Parses the attributes.
        /// </summary>
        /// <param name="headerData">The header data.</param>
        /// <returns>a list of attributes</returns>
        private static List<ParsedAttribute> ParseAttributes(this string headerData)
        {
            var result = new List<ParsedAttribute>(64);
            var attribute = default(ParsedAttribute);
            do
            {
                attribute = ParseNextAttribute(headerData, attribute);
                if (attribute != null)
                    result.Add(attribute);
            }
            while (attribute != null);

            return result;
        }

        /// <summary>
        /// Parses the next attribute.
        /// </summary>
        /// <param name="headerData">The header data.</param>
        /// <param name="lastAttribute">The last attribute.</param>
        /// <returns>THe parsed attribute</returns>
        private static ParsedAttribute ParseNextAttribute(string headerData, ParsedAttribute lastAttribute)
        {
            char c;
            var startIndex = lastAttribute?.EndIndex ?? 0;
            var attributePivotIndex = headerData.IndexOf("=\"", startIndex, StringComparison.Ordinal);
            var attributeStartIndex = -1;
            var attributeEndIndex = -1;

            // Find the attribute name
            for (var i = attributePivotIndex - 1; i >= 0; i--)
            {
                c = headerData[i];
                if (!char.IsWhiteSpace(c)) continue;
                attributeStartIndex = i + 1;
                break;
            }

            // find the attribute value
            if (attributeStartIndex >= 0)
            {
                for (var i = attributePivotIndex + 2; i < headerData.Length; i++)
                {
                    c = headerData[i];
                    if (c != '"') continue;
                    attributeEndIndex = i;
                    break;
                }

                if (attributeEndIndex == -1)
                    attributeEndIndex = headerData.Length - 1;
            }

            if (attributeStartIndex == -1)
                return null;

            return new ParsedAttribute
            {
                Key = HttpUtility.UrlDecode(headerData.Substring(attributeStartIndex, attributePivotIndex - attributeStartIndex)),
                Value = HttpUtility.UrlDecode(headerData.Substring(attributePivotIndex + 2, attributeEndIndex - attributePivotIndex - 2)),
                EndIndex = attributeEndIndex,
                StartIndex = attributeStartIndex,
                Substring = headerData.Substring(attributeStartIndex, attributeEndIndex - attributeStartIndex + 1)
            };
        }

        /// <summary>
        /// A POCO class to hold parsed attributes
        /// </summary>
        private class ParsedAttribute
        {
            // ReSharper disable UnusedAutoPropertyAccessor.Local
            public string Key { get; set; }
            public string Value { get; set; }
            public string Substring { get; set; }
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }

            // ReSharper restore UnusedAutoPropertyAccessor.Local
        }
    }
}
