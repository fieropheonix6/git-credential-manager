using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Git.CredentialManager
{
    /// <summary>
    /// Represents the execution environment for a Git credential helper command.
    /// </summary>
    public interface ICommandContext
    {
        /// <summary>
        /// The standard input text stream from the calling process, typically Git.
        /// </summary>
        TextReader StdIn { get; }

        /// <summary>
        /// The standard output text stream connected back to the calling process, typically Git.
        /// </summary>
        TextWriter StdOut { get; }

        /// <summary>
        /// The standard error text stream connected back to the calling process, typically Git.
        /// </summary>
        TextWriter StdError { get; }

        /// <summary>
        /// Application tracing system.
        /// </summary>
        ITrace Trace { get; }

        /// <summary>
        /// File system abstraction (exists mainly for testing).
        /// </summary>
        IFileSystem FileSystem { get; }

        /// <summary>
        /// Access the environment variables for the current GCM process.
        /// </summary>
        /// <returns>Set of all current environment variables.</returns>
        IReadOnlyDictionary<string, string> GetEnvironmentVariables();
    }

    /// <summary>
    /// Real command execution environment using the actual <see cref="Console"/>, file system calls and environment.
    /// </summary>
    public class CommandContext : ICommandContext
    {
        private const string LineFeed  = "\n";

        private static readonly Encoding Utf8NoBomEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private TextReader _stdIn;
        private TextWriter _stdOut;
        private TextWriter _stdErr;

        #region ICommandContext

        public TextReader StdIn
        {
            get
            {
                if (_stdIn == null)
                {
                    _stdIn = new StreamReader(Console.OpenStandardInput(), Utf8NoBomEncoding);
                }

                return _stdIn;
            }
        }

        public TextWriter StdOut
        {
            get
            {
                if (_stdOut == null)
                {
                    _stdOut = new StreamWriter(Console.OpenStandardOutput(), Utf8NoBomEncoding)
                    {
                        AutoFlush = true,
                        NewLine = LineFeed,
                    };
                }

                return _stdOut;
            }
        }

        public TextWriter StdError
        {
            get
            {
                if (_stdErr == null)
                {
                    _stdErr = new StreamWriter(Console.OpenStandardError(), Utf8NoBomEncoding)
                    {
                        AutoFlush = true,
                        NewLine = LineFeed,
                    };
                }

                return _stdErr;
            }
        }

        public ITrace Trace { get; } = new Trace();

        public IFileSystem FileSystem { get; } = new FileSystem();

        public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            IDictionary variables = Environment.GetEnvironmentVariables();

            // On Windows it is technically possible to get env vars which differ only by case
            // even though the general assumption is that they are case insensitive on Windows.
            // For example, some of the standard .NET types like System.Diagnostics.Process
            // will fail to start a process on Windows if given duplicate environment variables.
            // See this issue for more information: https://github.com/dotnet/corefx/issues/13146

            // If we're on the Windows platform we should de-duplicate by setting the string
            // comparer to OrdinalIgnoreCase.
            var comparer = PlatformUtils.IsWindows()
                         ? StringComparer.OrdinalIgnoreCase
                         : StringComparer.Ordinal;

            var result = new Dictionary<string, string>(comparer);

            foreach (var key in variables.Keys)
            {
                if (key is string name && variables[key] is string value)
                {
                    result[name] = value;
                }
            }

            return result;
        }

        #endregion
    }

    public static class CommandContextExtensions
    {
        /// <summary>
        /// Try to get the current value of the specified environment variable.
        /// </summary>
        /// <param name="context"><see cref="ICommandContext"/></param>
        /// <param name="key">The name of environment variable.</param>
        /// <param name="value">The current value of the environment variable.</param>
        /// <returns>True if the environment variable was set and has a value, false otherwise.</returns>
        public static bool TryGetEnvironmentVariable(this ICommandContext context, string key, out string value)
        {
            return context.GetEnvironmentVariables().TryGetValue(key, out value);
        }

        /// <summary>
        /// Test if the specified environment variable is 'truthy' (considered to be equivalent to a 'true' value
        /// by <see cref="StringExtensions.IsTruthy"/>).
        /// </summary>
        /// <param name="context"><see cref="ICommandContext"/></param>
        /// <param name="key">The name of environment variable.</param>
        /// <param name="defaultValue">
        /// The assumed default value of the environment variable if it does not
        /// exist/has not been set.
        /// </param>
        /// <returns>True if the environment variable was set and has a 'truthy' value, <see cref="defaultValue"/> otherwise.</returns>
        public static bool IsEnvironmentVariableTruthy(this ICommandContext context, string key, bool defaultValue)
        {
            if (context.TryGetEnvironmentVariable(key, out string valueStr) && valueStr.IsTruthy())
            {
                return true;
            }

            return defaultValue;
        }
    }
}
