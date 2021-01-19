using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using RabbitMQ.Benchmarks;
using RabbitMQ.Client.Impl;

namespace Benchmarks.Eventing
{
    [Config(typeof(Config))]
    [BenchmarkCategory("Eventing")]
    public class EventingBase
    {
        public static IEnumerable<object> CountSource()
        {
            yield return 1;
            yield return 5;
        }

        protected readonly EventHandler<ulong> _action = (_, __) => { };
        protected List<EventHandler<ulong>> _list;
        private protected EventingWrapper<ulong> _wrapper;
    }

    public class Eventing_AddRemove : EventingBase
    {
#pragma warning disable 67 // Required for add / remove
        private event EventHandler<ulong> _event;
#pragma warning restore 67

        [Benchmark(Baseline = true)]
        [ArgumentsSource(nameof(CountSource))]
        public void Event(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _event += _action;
            }

            for (int i = 0; i < count; i++)
            {
                _event -= _action;
            }
        }

        [Benchmark]
        [ArgumentsSource(nameof(CountSource))]
        public void Wrapper(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _wrapper.AddHandler(_action);
            }

            for (int i = 0; i < count; i++)
            {
                _wrapper.RemoveHandler(_action);
            }
        }

        [Benchmark]
        [ArgumentsSource(nameof(CountSource))]
        public void List(int count)
        {
            _list = new List<EventHandler<ulong>>(1);
            for (int i = 0; i < count; i++)
            {
                _list.Add(_action);
            }

            for (int i = 0; i < count; i++)
            {
                _list.Remove(_action);
            }
        }
    }

    public class Eventing_Invoke : EventingBase
    {
        private event EventHandler<ulong> _event;

        public Eventing_Invoke()
        {
            _wrapper = new EventingWrapper<ulong>("ContextString", (ex, context) => OnUnhandledExceptionOccurred(ex, context));
        }

        [ParamsSource(nameof(CountSource))]
        public int Length
        {
            set
            {
                _list = new List<EventHandler<ulong>>(value);
                var action = new EventHandler<ulong>((exception, context) => { });
                for (int i = 0; i < value; i++)
                {
                    _event += action;
                    _wrapper.AddHandler(action);
                    _list.Add(action);
                }
            }
        }

        [Benchmark(Baseline = true)]
        public void SingleInvoke()
        {
            _event?.Invoke(this, 5UL);
        }

        [Benchmark]
        public void GetInvocationList()
        {
            var handler = _event;
            if (handler != null)
            {
                foreach (EventHandler<ulong> action in handler.GetInvocationList())
                {
                    try
                    {
                        action(this, 5UL);
                    }
                    catch (Exception e)
                    {
                        OnUnhandledExceptionOccurred(e, "ContextString");
                    }
                }
            }
        }

        [Benchmark]
        public void List()
        {
            var handler = _event;
            if (handler != null)
            {
                foreach (EventHandler<ulong> action in _list)
                {
                    try
                    {
                        action(this, 5UL);
                    }
                    catch (Exception e)
                    {
                        OnUnhandledExceptionOccurred(e, "ContextString");
                    }
                }
            }
        }

        [Benchmark]
        public void Wrapper()
        {
            _wrapper.Invoke(this, 5UL);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OnUnhandledExceptionOccurred(Exception exception, string context)
        {
        }
    }
}
