using System;
//using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TcpLib
{
    //TCP通讯模块
    class TcpServer
    {
        private Socket tcpListener;         //Tcp服务器监听

        //后台创建一个监听线程，将所有终端的socket保存到ArrayList        
        List<ClientObject> m_ClientList = new List<ClientObject>();
        //ClientList m_ClientList = new ClientList();

        private string m_Address;   //服务器IP地址
        private int m_Port;         //监听端口

        private int m_MaxClient;    //最大客户端连接数
        private volatile bool m_Stop = false;

        private IPEndPoint m_LocalEndPoint;

        //事件
        public event DataEventHandler OnClientConnect;          //客户端连接

        public event DataEventHandler OnClientDisconnect;       //客户端断开

        public event DataEventHandler OnClientDataAvailable;    //客户端有数据

        public event DataEventHandler OnServerFull;             //服务器已满


        // Thread signal.
        private ManualResetEvent allDone = new ManualResetEvent(false);

        public string Address
        {
            get { return m_Address; }
            set { m_Address = value; }
        }

        public int Port
        {
            get { return m_Port; }
            set { m_Port = value; }
        }

        public int MaxClient
        {
            get { return m_MaxClient; }
            set { m_MaxClient = value; }
        }

        public void Close()
        {
            m_Stop = true;
            tcpListener.Close();
        }


        public List<ClientObject> ClientList
        {
            get { return m_ClientList; }
        }

        public TcpServer(string address, int port)
        {
            m_Address = address;
            m_Port = port;

            m_MaxClient = 100;   //最大连接终端数

            if ((m_Address == null) || (m_Address == ""))
            {
                //当m_Address为空的时候，侦听本地所有地址
                m_LocalEndPoint = new IPEndPoint(IPAddress.Any, m_Port);
            }
            else
            {
                //否则侦听指定的地址
                m_LocalEndPoint = new IPEndPoint(IPAddress.Parse(m_Address), m_Port);
            }

            //// Create a TCP/IP socket.
            //tcpListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);



            BindLocal();

        }

        private void BindLocal()
        {
            // Create a TCP/IP socket.
            tcpListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            if (m_LocalEndPoint == null) return;

            try
            {
                tcpListener.Bind(m_LocalEndPoint);
                tcpListener.Listen(100);
            }
            catch
            {
            }

        }

        public void StartListening()
        {
            try
            {
                while (!m_Stop)     //是否退出线程
                {
                    try
                    {
                        allDone.Reset();

                        tcpListener.BeginAccept(new AsyncCallback(AcceptCallback), tcpListener);

                        allDone.WaitOne();
                    }
                    catch(SocketException)
                    {
                        BindLocal();
                        Thread.Sleep(250);
                    }
                }
            }
            finally
            {
                //关闭所有已连接的Socket
                CloseAllClient();
            }
        }

        public void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                allDone.Set();

                //如果服务器停止了服务,就不能再接收新的客户端连接
                if (m_Stop)
                {
                    return;
                }

                Socket listener = (Socket)ar.AsyncState;
                Socket handler = listener.EndAccept(ar);

                //检查是否达到最大的允许的客户端数目
                if (m_ClientList.Count < m_MaxClient)
                {
                    //如果是新客户端，将客户端Socket加入ClientList;
                    if (!ClientExists(handler))
                    {
                        m_ClientList.Add(new ClientObject(handler));

                        //新的客户端连接
                        if (OnClientConnect != null)
                        {
                            PackClientList();
                            OnClientConnect(this, new DataEventArgs(handler));
                        }
                    }


                    // Create the state object.
                    StateObject state = new StateObject();
                    state.workSocket = handler;
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                }
                else
                {
                    //通知客户端连接已满
                    PackClientList();
                    if (OnServerFull != null) OnServerFull(this, new DataEventArgs(handler));

                    //断开连接
                    handler.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine(DateTime.Now.ToString());
                Console.WriteLine("");
            }

        }

        public void ReadCallback(IAsyncResult ar)
        {
            if ((m_Stop) || (ar == null))
            {
                return;
            }

            try
            {
                StateObject state = (StateObject)ar.AsyncState;
                Socket handler = state.workSocket;

                // Read data from the client socket. 
                int bytesRead = handler.EndReceive(ar);

                if (bytesRead > 0)
                {
                    byte[] dat = new byte[bytesRead];

                    Array.Copy(state.buffer, dat, bytesRead);

                    //收到客户端数据
                    if (OnClientDataAvailable != null)
                        OnClientDataAvailable(this, new DataEventArgs(handler, dat));

                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                       new AsyncCallback(ReadCallback), state);
                }
                else
                {
                    RemoveClientSocket(handler);

                    PackClientList();
                    //通知该客户端已断开
                    if (OnClientDisconnect != null)
                        OnClientDisconnect(this, new DataEventArgs(handler));

                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine(DateTime.Now.ToString());
                Console.WriteLine("");
            }
        }

        public void Send(Socket handler, byte[] data)
        {
            // Begin sending the data to the remote device.
            if (handler == null) return;
            handler.BeginSend(data, 0, data.Length, 0,
                new AsyncCallback(SendCallback), handler);
        }

        private void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;
                handler.EndSend(ar);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.WriteLine(DateTime.Now.ToString());
                Console.WriteLine("");
            }
        }



        //更新登陆的客户端的权限
        public void UpdateClientList(Socket client, ClientLevel level)
        {
            for (int i = 0; i < m_ClientList.Count; i++)
            {
                ClientObject clientObj = m_ClientList[i];
                if (clientObj.Client == client)
                {
                    clientObj.Level = level;
                    break;
                }
            }
        }

        //检查客户端Socket是否已添加到ClientList
        private bool ClientExists(Socket client)
        {
            if (client == null) return false;

            for (int i = 0; i < m_ClientList.Count; i++)
            {
                if (m_ClientList[i].Client == client)
                {
                    return true;
                }
            }

            return false;
        }

        //删除指定的客户端Socket
        private void RemoveClientSocket(Socket client)
        {
            if (client == null) return;
            ClientObject clientObj = null;
            bool find = false;
            for (int i = 0; i < m_ClientList.Count; i++)
            {
                clientObj = m_ClientList[i];
                if (clientObj.Client == client)
                {
                    find = true;
                    break;
                }
            }

            if (find && (clientObj != null))
                m_ClientList.Remove(clientObj);

        }


        //关闭并释放客户端Socket
        public void CloseClient(Socket client)
        {
            try
            {
                RemoveClientSocket(client);
                if (client != null) client.Close();
            }
            catch
            {
            }

        }

        //检查客户端连接列表,删除无用的连接
        private void PackClientList()
        {
            int i = 0;
            while (i < m_ClientList.Count)
            {
                ClientObject clientObj = m_ClientList[i];
                try
                {
                    if (!clientObj.Client.Connected)
                    {
                        m_ClientList.Remove(clientObj);
                        if (clientObj.Client != null) clientObj.Client.Close();
                        i--;
                    }
                }
                catch
                {
                }

                i++;
            }

        }

        //关闭并释放所有的客户端Socket
        public void CloseAllClient()
        {
            while (m_ClientList.Count > 0)
            {
                Socket client = m_ClientList[0].Client;
                RemoveClientSocket(client);
                if (client != null)
                {
                    client.Shutdown(SocketShutdown.Both);
                    client.Close();
                }

            }

        }

        //向所有连接的终端发送数据
        public void BroadcastData(ClientLevel dataLevel, byte[] data)
        {
            //dataLevel为数据查看的权限,只有大于或等于该权限的用户才可以接收
            byte curLevelNo = (byte)dataLevel;
            for (int i = 0; i < m_ClientList.Count; i++)
            {
                try
                {
                    ClientObject clientObj = m_ClientList[i];
                    if (clientObj.Client.Connected)
                    {
                        if ((byte)clientObj.Level <= curLevelNo)
                        {
                            Send(clientObj.Client, data);
                        }
                    }

                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    Console.WriteLine(DateTime.Now.ToString());
                    Console.WriteLine("");

                }
            }
        }

        //向客户端断发送文本消息
        public void SendMessageText(ClientObject clientObject, string msgText)
        {
            if (clientObject == null) return;
            byte[] msgData = Encoding.Default.GetBytes(msgText);
            Send(clientObject.Client, msgData);
        }
    }

    //*************************************************************************
    public class StateObject
    {
        public Socket workSocket;
        public const int BufferSize = 1024;
        public byte[] buffer = new byte[BufferSize];
    }

    /// <summary>
    /// 远程节点数据
    /// </summary>
    public class ClientObject
    {
        private Socket m_Client;        //客户端Socket
        private ClientLevel m_Level;    //客户端权限

        public Socket Client
        {
            get { return m_Client; }
        }

        public ClientLevel Level
        {
            get { return m_Level; }
            set { m_Level = value; }
        }

        public ClientObject(Socket client)
        {
            m_Client = client;
        }

        public ClientObject(Socket client, ClientLevel level)
        {
            m_Client = client;
            m_Level = level;
        }

    }


    //TCP服务器参数数据
    public delegate void DataEventHandler(object sender, DataEventArgs e);

    public class DataEventArgs : EventArgs
    {
        private Socket m_Client;
        private byte[] m_data;

        public Socket Client
        {
            get { return m_Client; }
        }

        public byte[] Data
        {
            get { return m_data; }
        }

        public DataEventArgs(Socket client)
        {
            m_Client = client;
        }

        public DataEventArgs(Socket client, byte[] data)
        {
            m_Client = client;
            m_data = data;
        }

    }

    /// <summary>
    /// 客户端权限
    /// </summary>
    public enum ClientLevel
    {
        None = 0x00,
        Super = 0x10,
        Admin = 0x11,
        User = 0x12,
        Guest = 0x13,
    }

    //public class ClientList : CollectionBase
    //{
    //    public ClientObject this[int index]
    //    {
    //        get
    //        {
    //            return ((ClientObject)List[index]);
    //        }

    //        set
    //        {
    //            List[index] = value;
    //        }
    //    }

    //    public int Add(ClientObject value)
    //    {
    //        return (List.Add(value));
    //    }

    //    public int IndexOf(ClientObject value)
    //    {
    //        return (List.IndexOf(value));
    //    }

    //    public void Insert(int index, ClientObject value)
    //    {
    //        List.Insert(index, value);
    //    }

    //    public void Remove(ClientObject value)
    //    {
    //        List.Remove(value);
    //    }

    //}

}