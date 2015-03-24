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
    //https://msdn.microsoft.com/en-us/library/fx6588te(v=vs.110).aspx
    // this server destroy handler socket
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
        private AutoResetEvent _connected = new AutoResetEvent(false);
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
            //Listen();            
            ListenWhile();
        }

        public void Listen()
        {
            try
            {
                this.Stop();
                this._listener = new Socket(this._localEndPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                this._listener.Bind(this._localEndPoint);
                //int maxCapacity = (int)SocketOptionName.MaxConnections;
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

        public void ListenWhile()
        {
            try
            {
                this.Stop();
                this._listener = new Socket(this._localEndPoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                this._listener.Bind(this._localEndPoint);
               Console.WriteLine("server is listening @" + _localEndPoint.Address + ":" + _localEndPoint.Port + " with 60 concurrent capacity");
               this._listener.Listen(60);
               while (true)
                {                    
                    this._listener.BeginAccept(new AsyncCallback(this.OnConnectionAccepted1), this._listener);
                    _connected.WaitOne();
                }
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
                this._listener.BeginAccept(new AsyncCallback(this.OnConnectionAccepted), this._listener);
            }
            catch (Exception e)
            {
                _log.Error(e.Message);
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

        private void OnConnectionAccepted1(IAsyncResult AsyncResult)
        {
            Socket socketHandler = null;
            try
            {
                socketHandler = this._listener.EndAccept(AsyncResult);
                //this._listener.BeginAccept(new AsyncCallback(this.OnConnectionAccepted1), this._listener);
                _connected.Set();                                
                IPEndPoint iPEndPoint = (IPEndPoint)socketHandler.RemoteEndPoint;
                Console.WriteLine("socket connected from {0}!" , iPEndPoint.ToString());

                InitializeHandlerSocker(socketHandler);
                SocketInfoHost hostInfo = SocketInfoHost.Create(socketHandler);
                socketHandler.BeginReceive(hostInfo.buffer, 0, SocketInfoHost.BUFFER_SIZE, 0, new AsyncCallback(OnReceived), hostInfo);
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
        }

        private void OnConnectionAccepted(IAsyncResult AsyncResult)
        {
            Socket socketHandler = null;
            try
            {              
                socketHandler = this._listener.EndAccept(AsyncResult);
                ThreadPool.QueueUserWorkItem(new WaitCallback(this.BeginAccept), null);         
                Console.WriteLine("socket connected!");
                IPEndPoint iPEndPoint = (IPEndPoint)socketHandler.RemoteEndPoint;
 
                InitializeHandlerSocker(socketHandler);
                SocketInfoHost buffer = new SocketInfoHost(socketHandler);
                socketHandler.BeginReceive(buffer.buffer, 0, SocketInfoHost.BUFFER_SIZE, 0, new AsyncCallback(OnReceived), buffer);                                  
            }
            catch (Exception e)
            {
                ThreadPool.QueueUserWorkItem(new WaitCallback(this.BeginAccept), null);         
                if (socketHandler != null)
                {
                    socketHandler.Close();
                }
                _log.Warn(e.Message);
            }
            //finally
            //{                                
            //    ThreadPool.QueueUserWorkItem(new WaitCallback(this.BeginAccept), null);                
            //}
        }

        private void OnReceived(IAsyncResult ar)
        {  
            SocketInfoHost infoHost = (SocketInfoHost)ar.AsyncState;
            try
            {
              
                Socket handler = infoHost.SocketHandler;
                int read = handler.EndReceive(ar);
                //TODO when sending content is larger than receiving buffer size
                //if (read > 0)
                //{
                //    infoHost.Storage.Append(Encoding.ASCII.GetString(infoHost.buffer, 0, read));
                //    handler.BeginReceive(infoHost.buffer, 0, SocketInfoHost.BUFFER_SIZE, 0, new AsyncCallback(OnReceived), infoHost);
                //}
                //else
                //{
                //    if (infoHost.Storage.Length > 0)
                //    {
                //        //All of the data has been read, construct SocketInfoHost
                //        infoHost.PunchTimeStamp();
                //        _infoRecords.Add(infoHost);
                //        Console.WriteLine("received " + infoHost.Value);

                //    }
                //    handler.Close();
                //}           
                infoHost.Storage.Append(Encoding.ASCII.GetString(infoHost.buffer, 0, read));
                infoHost.PunchTimeStamp();
                _infoRecords.Add(infoHost);
                var contentReceived = infoHost.Storage.ToString();
                Console.WriteLine("received " + contentReceived);

                if (contentReceived.Length>10)
                    _log.Warn(contentReceived);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception ex)
            {
                _log.Error("Receive Error " + infoHost.Storage.ToString(), ex);
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
