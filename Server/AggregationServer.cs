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
using System.Text;
using System.Threading;

namespace QuestPartners.Interview.Server
{
    public class AggregationServer : IDisposable
    {
        #region private member 
       
        private Socket _listener;
        private List<Socket> _handlers;
        private BlockingCollection<SocketInfoHost> _infoRecords;
        private IPEndPoint _localEndPoint;
        private string _ipAddress;
        private int _port;
        private readonly ILog _log;
        private ConcurrentDictionary<string, double> _set;
        private Timer _timer;
        private int _period ;
        private object _lock = new object();
        #endregion

        #region Constructor
   
        public AggregationServer(string ipAddress, int port)
        {
            _period = int.Parse(ConfigurationManager.AppSettings["interval"]);
            _ipAddress = ipAddress;
            _port = port;
            _localEndPoint = UtilityHelper.GetEndPoint(ipAddress, port);
            _log = LogManager.GetLogger(this.GetType().ToString());
            _set = new ConcurrentDictionary<string, double>(20, 100);
            _handlers = new List<Socket>(60);
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
                this._listener.Listen(60);
                Console.WriteLine("server is listening @" + _localEndPoint.Address + ":" + _localEndPoint.Port + " with 60 concurrent capacity");
                ThreadPool.QueueUserWorkItem(new WaitCallback(this.BeginAccept), null);
            }
            catch (Exception e)
            {
                _log.Warn(e.Message);
                this.Stop();
            }
        }

        private void BeginAccept(object state)
        {
            try
            {
                this._listener.BeginAccept(new AsyncCallback(this.AcceptConnectionCallback), this._listener);
            }
            catch (Exception e)
            {
                _log.Warn(e.Message);
            }
        }

        private void InitializeHandlerSocker(Socket handlerSocket)
        {
            try
            {
                if (handlerSocket != null)
                {
                    lock (_lock)
                        _handlers.Add(handlerSocket);
                    handlerSocket.NoDelay = true;
                    handlerSocket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.Debug, true);
                    handlerSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, 128);
                }               
            }
            catch (Exception e)
            {
                _log.Warn(e.Message);
               
                Disconnect(handlerSocket);
                throw;
            }
        }

        public void Disconnect (Socket handler)
        {
            if (handler != null && handler.Connected)
            {
                handler.Close();
                _handlers.Remove(handler);
            }
           
            //if (this._listener != null)
            //{
            //    this._listener.Close();
            //    _listener = null;
            //}
        }

        private void AcceptConnectionCallback(IAsyncResult AsyncResult)
        {
            Socket socket = null;
            try
            {              
                socket = this._listener.EndAccept(AsyncResult);
                Console.WriteLine("socket connected!");
                IPEndPoint iPEndPoint = (IPEndPoint)socket.RemoteEndPoint;
 
                InitializeHandlerSocker(socket);
                SocketInfoHost buffer = new SocketInfoHost(socket);
                socket.BeginReceive(buffer.buffer, 0, SocketInfoHost.BUFFER_SIZE, 0, new AsyncCallback(ReceiveCallBack), buffer);                                  
            }

            catch (Exception e)
            {
                if (socket != null)
                {
                    socket.Close();
                }
                _log.Warn(e.Message);
            }
            finally
            {                                
                ThreadPool.QueueUserWorkItem(new WaitCallback(this.BeginAccept), null);                
            }
        }

        private void ReceiveCallBack(IAsyncResult ar)
        {
            SocketInfoHost infoHost = (SocketInfoHost)ar.AsyncState;
            Socket handler = infoHost.SocketHandler;
            int read = handler.EndReceive(ar);
            //TODO
            if (read > 0)
            {
                infoHost.Storage.Append(Encoding.ASCII.GetString(infoHost.buffer, 0, read));
                handler.BeginReceive(infoHost.buffer, 0, SocketInfoHost.BUFFER_SIZE, 0, new AsyncCallback(ReceiveCallBack), infoHost);
            }
            else
            {
                if (infoHost.Storage.Length > 0)
                {
                    //All of the data has been read, construct SocketInfoHost
                    infoHost.PunchTimeStamp();
                    _infoRecords.Add(infoHost);
                    Console.WriteLine("received " + infoHost.Value);
                    
                }
                handler.Close();
            }           
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
                socket.Close();
                _handlers.Remove(socket);
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
            _handlers.ForEach(socket => socket.Close());
        }
    }
}
