using QuestPartners.Interview.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestPartners
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleKeyInfo ck;
            using (var server = new AggregationServer("127.0.0.1", 990))
            {
                server.Start();
                Console.WriteLine("server started");
                do
                {
                    ck = Console.ReadKey();
                } while (ck.Key != ConsoleKey.Escape);
            }
        }
    }
}
