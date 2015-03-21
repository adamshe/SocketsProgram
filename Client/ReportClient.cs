using QuestPartners.Interview.CommonLib;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QuestPartners.Interview.Client
{
    public class ReportClient : IDisposable
    {
        private Socket _socket;
        private string _ipAddress;
        private int _port;
        private IPEndPoint _endPoint;
        private Timer _timer;
        private static ManualResetEvent _connectDone = new ManualResetEvent(false);
        private static ManualResetEvent _sendDone = new ManualResetEvent(false);

        private int _waitTime = 600;
        public ReportClient(string ipAddress, int port)
        {
            _waitTime = int.Parse(ConfigurationManager.AppSettings["interval"]);
            _ipAddress = ipAddress;
            _port = port;
            _endPoint = UtilityHelper.GetEndPoint(ipAddress, port);

            _timer = new Timer(SendReport, null, _waitTime, _waitTime);
        }

        private void SendReport(object state)
        {
            try
            {
                _connectDone.Reset();
                _sendDone.Reset();
                _socket = new Socket(this._endPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);  
                double val = UtilityHelper.RandomNormal(0, 2);
                byte[] buffer = Encoding.UTF8.GetBytes(val.ToString());

                _socket.BeginConnect(_endPoint, ConnectCallBack, _socket);

                _connectDone.WaitOne();
                Send(_socket,buffer);
                _sendDone.WaitOne();
                Console.WriteLine("socket connect to server @" + _endPoint.Address + ":" + _endPoint.Port);
                Console.WriteLine("value sent " + Encoding.Default.GetString(buffer));
                               
                Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void Send(Socket socket, byte[] byteData)
        {          
            // Begin sending the data to the remote device.
            socket.BeginSend(byteData, 0, byteData.Length, 0,
                new AsyncCallback(SendCallback), socket);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.
                Socket client = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.
                int bytesSent = client.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to server.", bytesSent);

                // Signal that all bytes have been sent.
                _sendDone.Set();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private void ConnectCallBack(IAsyncResult ar)
        {
            try
            {
                Socket s = ar.AsyncState as Socket;
                if (_socket != null)
                {
                    _socket.EndConnect(ar);
                    _connectDone.Set();

                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }          
        }

        public void Dispose()
        {
            if (_socket != null)
            {
                _socket.Shutdown(SocketShutdown.Both);
                _socket.Close();
            }
            _socket = null;
        }
    }
}
