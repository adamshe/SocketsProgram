using QuestPartners.Interview.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    class Program
    {
        static void Main(string[] args)
        {
            ConsoleKeyInfo ck;
            using (var client = new ReportClient("127.0.0.1", 990))
            {
               // client.Start();
              do
              {
                ck = Console.ReadKey();
              }   while (ck.Key != ConsoleKey.Escape);
            }
        }
    }
}
