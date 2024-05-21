namespace Unosquare.FFME.Common;

using System;

/// <inheritdoc />
/// <summary>
/// An exception representing an error in Windows Multimedia Audio.
/// </summary>
[Serializable]
public sealed class LegacyAudioException : MediaContainerException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyAudioException"/> class.
    /// </summary>
    /// <param name="result">The result returned by the Windows API call.</param>
    /// <param name="functionName">The name of the Windows API that failed.</param>
    public LegacyAudioException(LegacyAudioResult result, string functionName)
        : base(ErrorMessage(result, functionName))
    {
        Result = result;
        FunctionName = functionName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyAudioException"/> class.
    /// </summary>
    public LegacyAudioException()
        : this(LegacyAudioResult.UnspecifiedError, $"{nameof(LegacyAudioException)}.ctor()")
    {
        // placeholder
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyAudioException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    public LegacyAudioException(string message)
        : this(LegacyAudioResult.UnspecifiedError, $"{nameof(LegacyAudioException)}.ctor(): {message}")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LegacyAudioException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public LegacyAudioException(string message, Exception innerException)
        : base(message, innerException)
    {
        Result = LegacyAudioResult.UnspecifiedError;
        FunctionName = $"{nameof(LegacyAudioException)}.ctor()";
    }

    /// <summary>
    /// Gets the name of the function that failed.
    /// </summary>
    public string FunctionName { get; }

    /// <summary>
    /// Gets the Windows API result code.
    /// </summary>
    public LegacyAudioResult Result { get; }

    /// <summary>
    /// Helper function to automatically raise an exception on failure.
    /// </summary>
    /// <param name="result">The result of the API call.</param>
    /// <param name="function">The API function name.</param>
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
    /// <returns>A descriptive error message.</returns>
    private static string ErrorMessage(LegacyAudioResult result, string function) => $"{result} calling {function}";
}
