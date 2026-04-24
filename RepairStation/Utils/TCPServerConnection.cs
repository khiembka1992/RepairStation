using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI_AOI.Utils
{
    public class TCPServerConnection
    {
        public string IP { get; set; }
        public int Port { get; set; }
        public bool _IsOpen = false;
        private readonly ManualResetEvent _AllDone = new ManualResetEvent(false);
        private Socket _ServerSocket;
        private readonly int _ReadTimeOut = 3000;
        private readonly int _WriteTimeOut = 3000;
        private string _MsgFromClient = null;

        private const int SLEEP_TO_READ = 10;
        public TCPServerConnection(string ip, int port, int readTimeout = 3000, int writeTimeout = 3000)
        {
            IP = ip;
            Port = port;
            _ReadTimeOut = readTimeout;
            _WriteTimeOut = writeTimeout;
        }

      

        public void Open()
        {
            _IsOpen = true;
            Task t = new Task(() =>
            {
                IPAddress address = IPAddress.Parse(IP);
                IPEndPoint localEndPoint = new IPEndPoint(address, Port);
                Socket listener = new Socket(address.AddressFamily,
                    SocketType.Stream, ProtocolType.Tcp) {
                    ReceiveTimeout = _ReadTimeOut,
                    SendTimeout = _WriteTimeOut
                };
                try
                {
                    listener.Bind(localEndPoint);
                    listener.Listen(100);

                    while (_IsOpen)
                    {
                        _AllDone.Reset();
                        listener.BeginAccept(
                            new AsyncCallback(AcceptCallback),
                            listener);                        
                        _AllDone.WaitOne();
                    }
                    listener.Close();

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);

                }
            });
            t.Start();

        }

        public void Close()
        {
            _AllDone.Set();
            _IsOpen = false;

            try
            {
                TcpClient client = new TcpClient();
                client.Connect("127.0.0.1", Port);
                Stream stream = client.GetStream();
                var writer = new StreamWriter(stream);
                writer.WriteLine("Close");
                writer.Close();
                stream.Close();
                client.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }


        public bool Write(string msg)
        {

            if ((_ServerSocket == null) || (!_ServerSocket.Connected))
            {
                Console.WriteLine("SFIS ERROR: No Connection");
                return false;
            }
            NetworkStream stream = new NetworkStream(_ServerSocket);
            StreamWriter streamWriter = new StreamWriter(stream) {
                AutoFlush = true
            };
            streamWriter.WriteLine(msg);
            streamWriter.Close();
            stream.Close();
            return true;
        }

        public bool Read(ref string msg)
        {
            int step = _ReadTimeOut / SLEEP_TO_READ;
            for (int i = 0; i < step; i++)
            {
                if (!_IsOpen)
                {
                    break;
                }
                if (_MsgFromClient != null)
                {
                    msg = _MsgFromClient;
                    _MsgFromClient = null;
                    return true;
                }
                Thread.Sleep(SLEEP_TO_READ);
            }
            msg = null;
            return false;
        }



        private void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                _AllDone.Set();
                Socket listener = (Socket)ar.AsyncState;
                if (listener == null)
                {
                    return;
                }
                _ServerSocket = listener.EndAccept(ar);
                StateObject state = new StateObject {
                    workSocket = _ServerSocket
                };
                _ServerSocket.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallbackHost), state);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }


        }


        private void ReadCallbackHost(IAsyncResult ar)
        {
            try
            {
                String res = String.Empty;

                StateObject state = (StateObject)ar.AsyncState;
                if (state == null)
                {
                    return;
                }
                Socket handler = state.workSocket;
                if (handler == null)
                {
                    return;
                }

                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {


                    if (_IsOpen)
                    {
                        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallbackHost), state);
                        _MsgFromClient = Encoding.ASCII.GetString(state.buffer, 0, bytesRead);
                        _MsgFromClient = Regex.Replace(_MsgFromClient, @"\s+", " ");
                       

                    }

                    else
                        handler.Close();


                }
                else
                    handler.Close();
            }
            catch (Exception e)
            {

                Console.WriteLine(e.Message);

            }

        }
        private class StateObject
        {
            public Socket workSocket;
            public const int BufferSize = 1024;
            public byte[] buffer = new byte[BufferSize];
        }
    }
}
