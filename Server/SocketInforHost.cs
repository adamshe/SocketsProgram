using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace QuestPartners.Interview.Server
{
    public class SocketInfoHost
    {
        private Socket _handler;
        private string _timeStamp;
        public const int BUFFER_SIZE = 64;
        public byte[] buffer = new byte[BUFFER_SIZE];

        public SocketInfoHost(Socket handler)
        {
            _handler = handler;
            Storage = new StringBuilder(60);
        }

        public Socket SocketHandler
        {
            get { return _handler; }
            set { _handler = value; }
        }

        public StringBuilder Storage { get; set; }
       
        public double Value { get { return double.Parse(Storage.ToString()); } }

        public void PunchTimeStamp()
        {            
            if (String.IsNullOrEmpty(_timeStamp))
            {
                _timeStamp = DateTime.Now.ToString("yyyymmddhhmmss");
            }                  
        }

        public string TimeStamp { get { return _timeStamp; } }
    }
}
