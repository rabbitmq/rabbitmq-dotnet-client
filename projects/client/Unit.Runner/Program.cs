using System;
using NUnitLite;
using NUnit.Common;
using System.Reflection;
using RabbitMQ.Client.Unit;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var writter = new ExtendedTextWrapper(Console.Out);
            new AutoRun(typeof(TestAmqpUri).GetTypeInfo().Assembly).Execute(args, writter, null);
        }
    }
}
