using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestPartners.Interview.Server
{
    class ReportObject
    {
        public string TimeStamp { get; set; }
        public int Sessions { get; set; }
        public double SumAggregation { get; set; }

        public static ReportObject Create (string timeStamp, int totalSessions, double sumAggregate)
        {
            return new ReportObject
            {
                TimeStamp = timeStamp,
                Sessions = totalSessions,
                SumAggregation = sumAggregate
            };
        }
    }
}
