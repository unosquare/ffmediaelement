namespace Unosquare.FFME.Rendering.Wave
{
    using System;

    /// <summary>
    /// A wrapper class for MmException.
    /// </summary>
    [Serializable]
    internal class MmException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MmException"/> class.
        /// </summary>
        /// <param name="result">The result returned by the Windows API call</param>
        /// <param name="functionName">The name of the Windows API that failed</param>
        public MmException(MmResult result, string functionName)
            : base(ErrorMessage(result, functionName))
        {
            Result = result;
            FunctionName = functionName;
        }

        /// <summary>
        /// Gets the name of the function.
        /// </summary>
        public string FunctionName { get; }

        /// <summary>
        /// Returns the Windows API result
        /// </summary>
        public MmResult Result { get; }

        /// <summary>
        /// Helper function to automatically raise an exception on failure
        /// </summary>
        /// <param name="result">The result of the API call</param>
        /// <param name="function">The API function name</param>
        public static void Try(MmResult result, string function)
        {
            if (result != MmResult.NoError)
                throw new MmException(result, function);
        }

        /// <summary>
        /// Creates an error message base don an erro result.
        /// </summary>
        /// <param name="result">The result.</param>
        /// <param name="function">The function.</param>
        /// <returns>A descriptive rror message</returns>
        private static string ErrorMessage(MmResult result, string function)
        {
            return string.Format("{0} calling {1}", result, function);
        }
    }
}
