using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace AI_AOI.Utils
{
    public class TCPClientConnection
    {
        TcpClient _Client;
        private readonly string _IP;
        private readonly int _Port;
        private StreamWriter _Writer;
        private StreamReader _Reader;
        private readonly int _TimeOut;
        public TCPClientConnection(string ip, int port,int timeout)
        {

            _IP = ip;
            _Port = port;
            _TimeOut = timeout;
        }

        public bool Open()
        {
            bool ret = true;
            _Client = new TcpClient {
                ReceiveTimeout = _TimeOut,
                SendTimeout = _TimeOut
            };

            if (_Client.ConnectAsync(_IP, _Port).Wait(_TimeOut))
            {
                try
                {
                    var networkStream = _Client.GetStream();
                    _Writer = new StreamWriter(networkStream, Encoding.UTF8) { AutoFlush = true };
                    _Reader = new StreamReader(networkStream, Encoding.UTF8);
                }
                catch
                {
                    ret = false;
                }
                
                
            }
            else
            {
                ret = false;
            }
            return ret;
        }
        public void  Close()
        {
            try
            {
                _Writer.Close();
                _Reader.Close();
                _Client.Close();
            }
            catch
            {

            }
           
        }
        public void WriteLine(string msg)
        {
            try
            {
                _Writer.WriteLine(msg);
            }
            catch
            {

            }
            
        }

        public string ReadLine()
        {
            string msg = string.Empty;
            try
            {
                msg = _Reader.ReadLine();
            }
            catch
            {

            }
          
            return msg;
        }


    }
}
