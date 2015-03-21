using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace QuestPartners.Interview.CommonLib
{
    public static class UtilityHelper
    {
        static private Random _ran = new Random();
        public static double RandomNormal(double mu, double sigma)
        {
            return mu + (sigma * Math.Sqrt(-2.0 * Math.Log(_ran.NextDouble())) * Math.Cos(2.0 * Math.PI * _ran.NextDouble()));
        }

        public static IPEndPoint GetEndPoint(string address, int port)
        {
            IPEndPoint result = null;
            IPAddress validatedAddress = null;
            try
            {
                if (address == "0.0.0.0")
                {
                    result = new IPEndPoint(IPAddress.Any, port);
                }
                else
                {
                    if (IPAddress.TryParse(address, out validatedAddress))
                    {
                        result = new IPEndPoint(validatedAddress, port);
                    }
                    else
                    {
                        IPHostEntry hostEntry = Dns.GetHostEntry(address);
                        IPAddress address2 = null;
                        IPAddress[] addressList = hostEntry.AddressList;
                        for (int i = 0; i < addressList.Length; i++)
                        {
                            IPAddress iPAddress = addressList[i];
                            if (iPAddress.AddressFamily == AddressFamily.InterNetwork)
                            {
                                address2 = iPAddress;
                                if (iPAddress.ToString() == address)
                                {
                                    break;
                                }
                            }
                        }
                        result = new IPEndPoint(address2, port);
                    }
                }
            }
            catch (Exception e)
            {
              
                throw;
            }
            return result;
        }
    }



    
}
