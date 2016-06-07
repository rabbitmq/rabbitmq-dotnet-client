using System;
using RabbitMQ.Client;
namespace ConsoleApplicationTes
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var cf = new ConnectionFactory();
            Console.WriteLine("Hello World!");
            var conn = cf.CreateConnection();
            Console.ReadLine();
        }
    }
}
