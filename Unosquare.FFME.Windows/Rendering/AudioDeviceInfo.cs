namespace Unosquare.FFME.Rendering
{
    /// <summary>
    /// Represents a device identifier
    /// </summary>
    /// <typeparam name="T">The type of the device identifier</typeparam>
    public class AudioDeviceInfo<T>
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AudioDeviceInfo{T}" /> class.
        /// </summary>
        /// <param name="deviceId">The device identifier.</param>
        /// <param name="name">The name.</param>
        /// <param name="provider">The provider.</param>
        /// <param name="isDefault">if set to <c>true</c> [is default].</param>
        /// <param name="tag">The tag.</param>
        internal AudioDeviceInfo(T deviceId, string name, string provider, bool isDefault, string tag)
        {
            DeviceId = deviceId;
            Name = name;
            Provider = provider;
            Tag = tag;
            IsDefault = isDefault;
        }

        /// <summary>
        /// Gets the device identifier.
        /// </summary>
        public T DeviceId { get; }

        /// <summary>
        /// Gets the name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the provider.
        /// </summary>
        public string Provider { get; }

        /// <summary>
        /// Gets the tag.
        /// </summary>
        public string Tag { get; }

        /// <summary>
        /// Gets a value indicating whether this device is the default.
        /// </summary>
        public bool IsDefault { get; }

        /// <summary>
        /// Returns a <see cref="string" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="string" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"{Provider}: {Name}";
        }
    }
}
