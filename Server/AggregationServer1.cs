using log4net;
using QuestPartners.Interview.CommonLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace QuestPartners.Interview.Server
{
    //https://msdn.microsoft.com/en-us/library/fx6588te(v=vs.110).aspx
    //This server doesn't destroy handler socket, client can keep socket alive all the way
    public class AggregationServer1 : IDisposable
    {
        #region private member 
        
        private Socket _listener;
     //   private List<Socket> _handlers;
        private BlockingCollection<SocketInfoHost> _infoRecords;
        private IPEndPoint _localEndPoint;
        private string _ipAddress;
        private int _port;
        private static readonly ILog _log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ConcurrentDictionary<string, double> _set;
        private Timer _timer;
        private int _period ;
        private object _lock = new object();
        private int _backLog = 1000;
        public static AutoResetEvent _connected = new AutoResetEvent(false);
        #endregion

        #region Constructor
   
        public AggregationServer1(string ipAddress, int port)
        {
            _period = int.Parse(ConfigurationManager.AppSettings["interval"]);
            _ipAddress = ipAddress;
            _port = port;
            _localEndPoint = UtilityHelper.GetEndPoint(ipAddress, port);
            _set = new ConcurrentDictionary<string, double>(20, 100);
        //    _handlers = new List<Socket>(100);
            _infoRecords = new BlockingCollection<SocketInfoHost>(360);
            _timer = new Timer(GenerateReport, null, _period, _period); //1 minute interval = 60000 mili seconds            
        }

        #endregion

        #region Public Method
        public void Start()
        {
            Listen();            
        }

        public void Listen()
        {
            try
            {
                this.Stop();
                this._listener = new Socket(this._localEndPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                this._listener.Bind(this._localEndPoint);
                //int maxCapacity = (int)SocketOptionName.MaxConnections;
                this._listener.Listen(_backLog);
                while (true)
                {
                    //_connected.Reset();
                    Console.WriteLine("server is listening @" + _localEndPoint.Address + ":" + _localEndPoint.Port + " with 60 concurrent capacity");
                    this.BeginAccept(null);
                    _connected.WaitOne();
                   // ThreadPool.QueueUserWorkItem(new WaitCallback(this.BeginAccept), null); //don't have control when threadpool to start
                }
            }
            catch (Exception e)
            {
                _log.Error("Listen Exception", e);
                this.Start();
            }
        }

        private void BeginAccept(object state)
        {
            try
            {
                this._listener.BeginAccept(new AsyncCallback(this.OnConnected), this._listener);
            }
            catch (Exception e)
            {
                _log.Error("BeginAccept", e);
            }
        }

        private void OnConnected(IAsyncResult AsyncResult)
        {
            Socket socketHandler = null;
            try
            {              
                socketHandler = this._listener.EndAccept(AsyncResult);
                _connected.Set();
                
                IPEndPoint iPEndPoint = (IPEndPoint)socketHandler.RemoteEndPoint;
                Console.WriteLine("socket connected from {0}!", iPEndPoint.Address.ToString());

                ConfigureHandlerSocket(socketHandler);
                var infoHost =  SocketInfoHost.Create(socketHandler);
                socketHandler.BeginReceive(infoHost.buffer, 0, SocketInfoHost.BUFFER_SIZE, 0, new AsyncCallback(OnReceived), infoHost);                                  
            }

            catch (Exception e)
            {
                _connected.Set();
                if (socketHandler != null)
                {
                    socketHandler.Close();
                }
                _log.Error(e.Message);
            }
            //finally
            //{                                
            //    ThreadPool.QueueUserWorkItem(new WaitCallback(this.BeginAccept), null);                
            //}
        }

        private void OnReceived(IAsyncResult ar)
        {
            SocketInfoHost infoHost = (SocketInfoHost)ar.AsyncState;
            Socket handler = infoHost.SocketHandler;
            int read = handler.EndReceive(ar);
           
            var content = Encoding.ASCII.GetString(infoHost.buffer, 0, read);
            if (content.Length > 30)
                _log.Warn(content);
            //content = content.Trim('\0');
            //if (!string.IsNullOrEmpty(content))
            
            infoHost.Storage.Append(content);
            //All of the data has been read, construct SocketInfoHost
            infoHost.PunchTimeStamp();
            _infoRecords.Add(infoHost);
            Console.WriteLine("received " + infoHost.Value);
            
            SocketInfoHost newInfoHost = SocketInfoHost.Create(handler);
            handler.BeginReceive(newInfoHost.buffer, 0, SocketInfoHost.BUFFER_SIZE, 0, new AsyncCallback(OnReceived), newInfoHost);
              //  CloseHandle(handler);                       
        }

        private void ConfigureHandlerSocket(Socket handlerSocket)
        {
            try
            {
                if (handlerSocket != null)
                {
                    //lock (_lock)
                    //    _handlers.Add(handlerSocket);
                    handlerSocket.NoDelay = true;
                    handlerSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.Debug, true);
                    handlerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, SocketInfoHost.BUFFER_SIZE);
                    handlerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
                }               
            }
            catch (Exception e)
            {
                _log.Error("Initialize Handler Socket", e);
               
                Disconnect(handlerSocket);
                throw;
            }
        }

        public void Disconnect (Socket handler)
        {
            if (handler != null && handler.Connected)
            {
                handler.Close();
             //   _handlers.Remove(handler);
            }
           
            //if (this._listener != null)
            //{
            //    this._listener.Close();
            //    _listener = null;
            //}
        }        
        
        public void Stop()
        {
            try
            {                
                if (this._listener != null)
                {
                    this._listener.Close();
                    this._listener = null;
                }
            }
            catch (Exception e)
            {
                _log.Warn(e.Message);
            }
        }
        #endregion

        #region Private Helper
        private void GenerateReport(object state)
        {
            string timeStamp = GetTimeStamp();
            double total = _infoRecords.Sum(host => host.Value);
            ReportObject obj = ReportObject.Create(timeStamp, _infoRecords.Count, total);
            AppendToReport(obj);
            _infoRecords = new BlockingCollection<SocketInfoHost>(60); //reset concurrent collection

        }

        private void CloseHandle (Socket socket)
        {
            lock (_lock)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
           //     _handlers.Remove(socket);
            }
        }
        private string GetTimeStamp()
        {
            return DateTime.Now.ToString("yyyymmddhhmmss");
        }

        private void AppendToReport (ReportObject obj)
        {
            var fileName = ConfigurationManager.AppSettings["resultFile"];
            using (StreamWriter writer = File.AppendText(fileName))
            {
                var content = string.Format("{0},{1},{2:######.000000}", obj.TimeStamp, obj.Sessions, obj.SumAggregation);
                writer.WriteLineAsync(content);
                Console.WriteLine(content);
            }            
        }

        #endregion

        public void Dispose()
        {
            Stop();
            //_handlers.ForEach(socket => socket.Close());
        }
    }
}
