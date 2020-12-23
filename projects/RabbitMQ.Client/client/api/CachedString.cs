using System;
using System.Text;

namespace RabbitMQ.Client
{
    /// <summary>
    /// Caches a string's byte representation to be used for certain methods like <see cref="IModel.BasicPublish(CachedString,CachedString,bool,IBasicProperties,ReadOnlyMemory{byte})"/>.
    /// </summary>
    public sealed class CachedString
    {
        public static readonly CachedString Empty = new CachedString(string.Empty, ReadOnlyMemory<byte>.Empty);

        /// <summary>
        /// The string value to cache.
        /// </summary>
        public readonly string Value;
        /// <summary>
        /// Gets the bytes representing the <see cref="Value"/>.
        /// </summary>
        public readonly ReadOnlyMemory<byte> Bytes;

        /// <summary>
        /// Creates a new <see cref="CachedString"/> based on the provided string.
        /// </summary>
        /// <param name="value">The string to cache.</param>
        public CachedString(string value)
        {
            Value = value;
            Bytes = Encoding.UTF8.GetBytes(value);
        }

        /// <summary>
        /// Creates a new <see cref="CachedString"/> based on the provided bytes.
        /// </summary>
        /// <param name="bytes">The bytes.</param>
        public CachedString(ReadOnlyMemory<byte> bytes)
        {
#if !NETSTANDARD
            Value = Encoding.UTF8.GetString(bytes.Span);
#else
            unsafe
            {
                fixed (byte* bytePointer = bytes.Span)
                {
                    Value = Encoding.UTF8.GetString(bytePointer, bytes.Length);
                }
            }
#endif
            Bytes = bytes;
        }

        /// <summary>
        /// Creates a new <see cref="CachedString"/> based on the provided values.
        /// </summary>
        /// <param name="value">The string to cache.</param>
        /// <param name="bytes">The byte representation of the string value.</param>
        public CachedString(string value, ReadOnlyMemory<byte> bytes)
        {
            Value = value;
            Bytes = bytes;
        }
    }
}
