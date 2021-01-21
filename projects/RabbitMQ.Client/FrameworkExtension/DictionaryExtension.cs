using System.Collections.Generic;

namespace RabbitMQ
{
#nullable enable
#if NETSTANDARD
    internal static class DictionaryExtension
    {
        public static bool Remove<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey key, out TValue value)
        {
            return dictionary.TryGetValue(key, out value) && dictionary.Remove(key);
        }
    }
#endif
}
