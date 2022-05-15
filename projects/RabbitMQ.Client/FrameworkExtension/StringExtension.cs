using System;

namespace RabbitMQ
{
#nullable enable
#if NETSTANDARD
    internal static class StringExtension
    {
        public static bool Contains(this string toSearch, string value, StringComparison comparisonType)
        {
            return toSearch.IndexOf(value, comparisonType) > 0;
        }
    }
#endif
}
