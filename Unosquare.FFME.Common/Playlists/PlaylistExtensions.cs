namespace Unosquare.FFME.Playlists
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Heklper methods for parsing m3u8 playlists
    /// </summary>
    internal static class PlaylistExtensions
    {
        /// <summary>
        /// Gets all index positions of the given substring
        /// </summary>
        /// <param name="str">The string.</param>
        /// <param name="value">The value.</param>
        /// <returns>A list of index positions</returns>
        public static List<int> AllIndexesOf(this string str, string value)
        {
            var indexes = new List<int>();

            if (string.IsNullOrEmpty(value))
                return indexes;

            for (int index = 0; ; index += value.Length)
            {
                index = str.IndexOf(value, index);
                if (index == -1)
                    return indexes;
                indexes.Add(index);
            }
        }

        /// <summary>
        /// Parses the header line.
        /// </summary>
        /// <typeparam name="T">The type of playlist items</typeparam>
        /// <param name="target">The target.</param>
        /// <param name="line">The line.</param>
        public static void ParseHeaderLine<T>(this Playlist<T> target, string line)
            where T : PlaylistEntry, new()
        {
            var headerData = line.Substring($"{Playlist<T>.HeaderPrefix} ".Length).Trim();
            var attributes = headerData.ParseAttributes();

            foreach (var attribute in attributes)
            {
                headerData = headerData.Replace(attribute.Substring, string.Empty);
                target.Attributes[attribute.Key] = attribute.Value.Trim().Trim('"').Replace("\"\"", "\"");
            }

            headerData = headerData.Trim();
            target.Name = headerData;
        }

        /// <summary>
        /// Parses the extended information line into a Playlist entry.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="line">The line.</param>
        public static void BeginExtendedInfoLine(this PlaylistEntry target, string line)
        {
            var result = new PlaylistEntry();
            var headerData = line.Substring($"{Playlist.EntryPrefix}:".Length).Trim();
            var attributes = headerData.ParseAttributes();

            foreach (var attribute in attributes)
            {
                headerData = headerData.Replace(attribute.Substring, string.Empty);
                target.Attributes[attribute.Key] = attribute.Value.Trim().Trim('"').Replace("\"\"", "\"");
            }

            headerData = headerData.Trim();
            var headerFields = headerData.Split(',');
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
            var c = default(char);
            var nc = default(char);
            var startIndex = lastAttribute == null ? 0 : lastAttribute.EndIndex;
            var attributePivotIndex = headerData.IndexOf("=\"", startIndex);
            var attributeStartIndex = -1;
            var attributeEndIndex = -1;
            for (var i = attributePivotIndex - 1; i >= 0; i--)
            {
                c = headerData[i];
                if (char.IsWhiteSpace(c))
                {
                    attributeStartIndex = i + 1;
                    break;
                }
            }

            if (attributeStartIndex >= 0)
            {
                for (var i = attributePivotIndex + 2; i < headerData.Length; i++)
                {
                    c = headerData[i];
                    nc = i + 1 < headerData.Length ? headerData[i + 1] : default(char);
                    if (c == '"' && nc != '"')
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
                Key = headerData.Substring(attributeStartIndex, attributePivotIndex - attributeStartIndex),
                Value = headerData.Substring(attributePivotIndex + 1, attributeEndIndex - attributePivotIndex),
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
