namespace Unosquare.FFME.Rendering
{
    using Engine;
    using System;
    using System.Runtime.Serialization;
    using System.Security.Permissions;

    /// <inheritdoc />
    /// <summary>
    /// An exception representing an error in Windows Multimedia Audio
    /// </summary>
    [Serializable]
    public sealed class LegacyAudioException : MediaContainerException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LegacyAudioException"/> class.
        /// </summary>
        /// <param name="result">The result returned by the Windows API call</param>
        /// <param name="functionName">The name of the Windows API that failed</param>
        internal LegacyAudioException(LegacyAudioResult result, string functionName)
            : base(ErrorMessage(result, functionName))
        {
            Result = result;
            FunctionName = functionName;
        }

        /// <summary>
        /// Gets the name of the function that failed.
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// Gets the Windows API result code.
        /// </summary>
        public LegacyAudioResult Result { get; }

        /// <inheritdoc/>
        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null) throw new ArgumentNullException(nameof(info));
            info.AddValue(nameof(FunctionName), FunctionName);
            info.AddValue(nameof(Result), Result);

            base.GetObjectData(info, context);
        }

        /// <summary>
        /// Helper function to automatically raise an exception on failure
        /// </summary>
        /// <param name="result">The result of the API call</param>
        /// <param name="function">The API function name</param>
        internal static void Try(LegacyAudioResult result, string function)
        {
            if (result != LegacyAudioResult.NoError)
                throw new LegacyAudioException(result, function);
        }

        /// <summary>
        /// Creates an error message base don an error result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="function">The function.</param>
        /// <returns>A descriptive error message</returns>
        private static string ErrorMessage(LegacyAudioResult result, string function) => $"{result} calling {function}";
    }
}
