using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestPartners.Interview.Client
{
    public static class MathHelper
    {
        static private Random _ran = new Random();
        public static double RandomNormal(double mu, double sigma)
        {
            return mu + (sigma * Math.Sqrt(-2.0 * Math.Log(_ran.Next())) * Math.Cos(2.0 * Math.PI * _ran.Next()));
        }

    }
}
