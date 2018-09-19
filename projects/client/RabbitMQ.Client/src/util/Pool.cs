using System;
using System.Threading;

namespace RabbitMQ.Util
{
    internal class Pool<TElement> where TElement : class
    {
        private readonly Func<TElement> factory;
        private readonly TElement[] elements;

        public Pool(int capacity, Func<TElement> factory)
        {
            this.factory = factory;
            elements = new TElement[capacity];
        }

        public bool TryRent(out TElement element)
        {
            for (var i = 0; i < elements.Length; ++i)
            {
                var found = Interlocked.Exchange(ref elements[i], null);
                if (found != null)
                {
                    element = found;
                    return true;
                }
            }

            element = default(TElement);
            return false;
        }

        public TElement Rent()
        {
            return TryRent(out var element) ? element : factory.Invoke();
        }

        public void Return(TElement element)
        {
            for (var i = 0; i < elements.Length; ++i)
            {
                if (Interlocked.CompareExchange(ref elements[i], element, null) == null)
                {
                    return;
                }
            }
        }
    }
}