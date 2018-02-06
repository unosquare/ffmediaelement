namespace Unosquare.FFME.Windows.Sample.Kernel
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Xml.Serialization;

    /// <summary>
    /// Represents the Config Root
    /// </summary>
    [Serializable]
    public class ConfigRoot
    {
        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        public string Version { get; set; } = typeof(ConfigRoot).Assembly.GetName().Version.ToString();

        /// <summary>
        /// Gets or sets the ffmpeg path.
        /// </summary>
        /// <value>
        /// The ffmpeg path.
        /// </value>
        public string FFmpegPath { get; set; } = @"C:\ffmpeg\";

        /// <summary>
        /// Gets or sets the history entries.
        /// </summary>
        /// <value>
        /// The history entries.
        /// </value>
        public List<string> HistoryEntries { get; set; } = new List<string>();

        private static string SavePath
        {
            get
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ffmeplay");
                if (Directory.Exists(folder) == false)
                    Directory.CreateDirectory(folder);

                var configFilePath = Path.Combine(folder, "config.xml");
                return configFilePath;
            }
        }

        /// <summary>
        /// Loads this instance.
        /// </summary>
        /// <returns>The ConfigRoot instance</returns>
        public static ConfigRoot Load()
        {
            if (File.Exists(SavePath) == false)
            {
                var config = new ConfigRoot();
                config.Save();
            }

            var serializer = new XmlSerializer(typeof(ConfigRoot));
            using (var readStream = File.OpenRead(SavePath))
            {
                var result = serializer.Deserialize(readStream) as ConfigRoot;
                return result;
            }
        }

        /// <summary>
        /// Saves this instance.
        /// </summary>
        public void Save()
        {
            var serializer = new XmlSerializer(typeof(ConfigRoot));
            using (var writeStream = File.Open(SavePath, FileMode.Create, FileAccess.Write))
            {
                serializer.Serialize(writeStream, this);
            }
        }
    }
}
