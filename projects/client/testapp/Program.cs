using System;
using RabbitMQ.Client;
using System.Collections.Generic;

namespace ConsoleApplicationTes
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var cf = new ConnectionFactory();
            cf.AutomaticRecoveryEnabled = true;
            cf.RequestedConnectionTimeout = 2000;
            cf.NetworkRecoveryInterval = TimeSpan.FromSeconds(20);
            
            var conn = cf.CreateConnection(
            new List<string>() { "127.0.0.1"
            /*
                    "191.72.44.22",
                    "145.23.22.18",
                    "localhost"
                    */
                });
            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }
    }
}
