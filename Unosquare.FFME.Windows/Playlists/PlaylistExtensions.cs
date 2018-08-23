namespace Unosquare.FFME.Playlists
{
    using System;
    using System.Collections.Generic;
    using System.Web;

    /// <summary>
    /// Heklper methods for parsing m3u8 playlists
    /// </summary>
    internal static class PlaylistExtensions
    {
        /// <summary>
        /// Parses the header line.
        /// </summary>
        /// <typeparam name="T">The type of playlist items</typeparam>
        /// <param name="target">The target.</param>
        /// <param name="line">The line.</param>
        public static void ParseHeaderLine<T>(this Playlist<T> target, string line)
            where T : PlaylistEntry, new()
        {
            var lineData = line.Substring($"{Playlist<T>.HeaderPrefix} ".Length).Trim();
            var attributes = lineData.ParseAttributes();

            foreach (var attribute in attributes)
            {
                lineData = lineData.Replace(attribute.Substring, string.Empty);
                target.Attributes[attribute.Key] = attribute.Value;
            }

            lineData = lineData.Trim();
            target.Name = lineData;
        }

        /// <summary>
        /// Parses the extended information line into a Playlist entry.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="line">The line.</param>
        public static void BeginExtendedInfoLine(this PlaylistEntry target, string line)
        {
            var result = new PlaylistEntry();
            var lineData = line.Substring($"{Playlist.EntryPrefix}:".Length).Trim();
            var attributes = lineData.ParseAttributes();

            foreach (var attribute in attributes)
            {
                lineData = lineData.Replace(attribute.Substring, string.Empty);
                target.Attributes[attribute.Key] = attribute.Value;
            }

            lineData = lineData.Trim();
            var headerFields = lineData.Split(',');
            if (headerFields.Length >= 1 && long.TryParse(headerFields[0].Trim(), out long duration))
                target.Duration = TimeSpan.FromSeconds(Convert.ToDouble(duration));

            if (headerFields.Length >= 2)
                target.Title = headerFields[1].Trim();
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
            var startIndex = lastAttribute == null ? 0 : lastAttribute.EndIndex;
            var attributePivotIndex = headerData.IndexOf("=\"", startIndex, StringComparison.InvariantCulture);
            var attributeStartIndex = -1;
            var attributeEndIndex = -1;

            // Find the attribute name
            for (var i = attributePivotIndex - 1; i >= 0; i--)
            {
                c = headerData[i];
                if (char.IsWhiteSpace(c))
                {
                    attributeStartIndex = i + 1;
                    break;
                }
            }

            // find the attribute value
            if (attributeStartIndex >= 0)
            {
                for (var i = attributePivotIndex + 2; i < headerData.Length; i++)
                {
                    c = headerData[i];
                    if (c == '"')
                    {
                        attributeEndIndex = i;
                        break;
                    }
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
            public string Key { get; set; }
            public string Value { get; set; }
            public string Substring { get; set; }
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
        }
    }
}
