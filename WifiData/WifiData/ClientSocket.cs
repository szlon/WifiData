using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace TcpLib
{
    class ClientSocket
    {
        static int BUFFER_SIZE = 256 * 1024;  

        byte[] frameBuffer = new byte[BUFFER_SIZE];
        byte[] receiveBuffer = new byte[BUFFER_SIZE];

        Socket socket = null;

        public event SocketReceivedHandler OnSocketReceived = null;
        public event SocketStateHandler OnSocketState = null;
        public string serverIP = "127.0.0.1";

        public int serverPort = 7777;

        public int ReconectCount = -1;   //重新连接10次放弃连接，-1表示一直试图连接
        public int ReconnectTime = 5;    //重新连接间隔5秒

        private int connectCount = 0; 
        private bool isInit = true;
        private bool connect = false;
        public bool Connect
        {
            get { return connect; }
        }


        public ClientSocket()
        {
        }

        public ClientSocket(string ip, int port)
        {
            this.serverIP = ip;

            this.serverPort = port;
        }

        public void Start()
        {
            Thread threadData = new Thread(new ThreadStart(ThreadProc));

            threadData.IsBackground = true;

            threadData.Start();

        }

        public void Send(byte[] buffer)
        {
            if (connect)
            {
                socket.Send(buffer, buffer.Length, SocketFlags.None);
            }
        }

        private void ThreadProc()
        {
            while (true)
            {
                try
                {
                    IPEndPoint ipe = new IPEndPoint(IPAddress.Parse(this.serverIP), this.serverPort);

                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    socket.Connect(ipe);

                    connect = true;
                    if (OnSocketState != null) OnSocketState(this, SocketState.Connected);

                    //Lon.IO.OutMessage.Write("CommModule", Lon.IO.MessageType.Error, String.Format("服务器[{0}:{1}] 已连接！", this.serverIP, this.serverPort));

                    SocketDataReceived();

                    connect = false;

                }
                catch
                {
                    if (connect || isInit)
                    {
                        //Lon.IO.OutMessage.Write("CommModule", Lon.IO.MessageType.Error, String.Format("服务器[{0}:{1}] 连接失败！", this.serverIP, this.serverPort));
                    }
                    isInit = false;
                    connect = false;
                }

                if (OnSocketState != null) OnSocketState(this, SocketState.Disconnected);

                if (ReconectCount > -1 && ++connectCount > ReconectCount) break;     //重新连接3次后不再试图连接

                Thread.Sleep(ReconnectTime * 1000); //等待ReconnectTime秒后重新连接
            }

        }


        private void SocketDataReceived()
        {
            //接收数据
            while (true)
            {
                try
                {
                    //int frameLength = 0;
                    //int receiveLength = 0;

                    if (socket.Poll(-1, SelectMode.SelectRead))
                    {
                        int length = socket.Receive(receiveBuffer);
                        if (length <= 0) break;

                        byte[] buf = new byte[length];
                        Array.Copy(receiveBuffer, 0, buf, 0, length);
                        OnSocketReceived(this, buf);

                        //for (int i = 0; i < length; i++)
                        //{
                        //    frameBuffer[frameLength] = receiveBuffer[i];
                        //    frameLength++;

                        //    if (frameLength == 4)
                        //    {
                        //        receiveLength = frameBuffer[0] + (frameBuffer[1] << 8) + (frameBuffer[2] << 16) + (frameBuffer[3] << 24);

                        //        if (receiveLength < 8 || receiveLength + 5 > frameBuffer.Length) //长度格式
                        //        {
                        //            break;
                        //        }
                        //    }
                        //    else if (frameLength == 5)
                        //    {
                        //        //数据帧标志 8fH, 心跳帧标志 0FH
                        //        if ((frameBuffer[4] != 0x8f) && (frameBuffer[4] != 0x0f))
                        //        {
                        //            break;
                        //        }
                        //    }
                        //    else if (receiveLength > 0 && (frameLength == receiveLength + 5)) //解析到数据帧
                        //    {
                        //        //处理数据帧
                        //        if (OnSocketReceived != null && frameBuffer[4] == 0x8F)
                        //        {
                        //            OnSocketReceived(this, frameBuffer, frameLength);
                        //        }

                        //    }
                        //    else if (frameLength >= frameBuffer.Length)
                        //    {
                        //        break;
                        //    }
                        //}
                    }

                }
                catch (SocketException ex)
                {
                    if (OnSocketState != null) OnSocketState(this, SocketState.Unkown);
                    //Lon.IO.OutMessage.Write("CommModule", Lon.IO.MessageType.Error, String.Format("[{0}] {1} \r\n{2}", DateTime.Now, "ClientSocket.SocketDataReceived", ex.Message));
                    break;
                }

            }

        }


    }

    delegate void SocketReceivedHandler(object sender, byte[] buffer);
    delegate void SocketStateHandler(object sender, SocketState state);

    enum SocketState
    {
        Connected,
        Disconnected,
        Unkown,
    }
}
