using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedLarkLib.Utilities
{
    /// <summary>
    /// Provides simple utility methods used by the RedLark library.
    /// </summary>
    public static class SimpleUtils
    {
        /// <summary>
        /// Random number generator for creating unique values.
        /// </summary>
        private static readonly Random random = new Random();

        /// <summary>
        /// Generates a unique random string value suitable for use as a lock token.
        /// </summary>
        /// <returns>A 20-character alphanumeric string.</returns>
        /// <remarks>
        /// <para>
        /// The generated value is used to uniquely identify a lock acquisition.
        /// This ensures that:
        /// </para>
        /// <list type="bullet">
        ///   <item><description>Only the original acquirer can release the lock</description></item>
        ///   <item><description>Lock releases are idempotent and safe</description></item>
        ///   <item><description>Expired locks cannot be accidentally released by a new acquirer</description></item>
        /// </list>
        /// <para>
        /// The value space (62^20) is large enough to make collisions extremely unlikely
        /// in practice, though this is not cryptographically secure.
        /// </para>
        /// </remarks>
        public static string GetUniqueValue()
        {
            const int UNIQUE_VALUE_LENGTH = 20;
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            StringBuilder builder = new StringBuilder(UNIQUE_VALUE_LENGTH);
            for (int i = 0; i < UNIQUE_VALUE_LENGTH; i++)
                builder.Append(chars[random.Next(chars.Length)]);
            return builder.ToString();
        }
    }
}
