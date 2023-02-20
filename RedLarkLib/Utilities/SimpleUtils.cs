using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedLarkLib.Utilities
{
    public static class SimpleUtils
    {
        private static readonly Random random = new Random();

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
