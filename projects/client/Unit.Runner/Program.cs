using System;
using NUnitLite;
using NUnit.Common;
using NUnit.Framework;
using System.Reflection;
using RabbitMQ.Client.Unit;

namespace ConsoleApplication
{
    public class Program
    {
        public static int Main(string[] args)
        {
            using(var writter = new ExtendedTextWrapper(Console.Out))
            {
                return new AutoRun(typeof(TestAmqpUri).GetTypeInfo().Assembly).Execute(args, writter, null);
            }
        }
    }
}
