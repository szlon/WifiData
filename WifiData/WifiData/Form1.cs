using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Reflection;
using System.Net;
using Lon.Common;
using TcpLib;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;

namespace WifiData
{
    public partial class Form1 : Form
    {
        private ConfigManager configManager;
        private ClientSocket tcpClient = null;
        private TcpServer tcpServer;
        private Thread tcpThread;
        private SerialPort serialPort;

        public bool ServerMode = bool.Parse(System.Configuration.ConfigurationManager.AppSettings["ServerMode"]);
        public string ServerIP = System.Configuration.ConfigurationManager.AppSettings["ServerIP"];
        public int ServerPort = int.Parse(System.Configuration.ConfigurationManager.AppSettings["ServerPort"]);

        public bool StartMinimize = bool.Parse(System.Configuration.ConfigurationManager.AppSettings["StartMinimize"]);
        public int ActiveTabIndex = int.Parse(System.Configuration.ConfigurationManager.AppSettings["TabIndex"]);

        public string ComPortName = System.Configuration.ConfigurationManager.AppSettings["PortName"];
        public int ComBaudRate = int.Parse(System.Configuration.ConfigurationManager.AppSettings["BaudRate"]);

        public static string PathApplication;



        public Form1()
        {
            InitializeComponent();
        }

        #region TcpComm
        public void TcpStart()
        {
            //启动TCP监听线程        
            if (ServerMode)
            {
                //服务器模式
                tcpServer = new TcpServer(ServerIP, ServerPort); ;
                tcpThread = new Thread(new ThreadStart(tcpServer.StartListening));

                tcpServer.OnClientConnect += new DataEventHandler(tcpComm_OnClientConnect);
                tcpServer.OnClientDisconnect += new DataEventHandler(tcpComm_OnClientDisconnect);
                tcpServer.OnServerFull += new DataEventHandler(tcpComm_OnServerFull);
                tcpServer.OnClientDataAvailable += new DataEventHandler(tcpComm_OnClientDataAvailable);
                tcpThread.Start();

                while (!tcpThread.IsAlive) ;

            }
            else
            {
                //客户端模式
                tcpClient = new ClientSocket(ServerIP, ServerPort);
                tcpClient.OnSocketReceived += new SocketReceivedHandler(tcpClient_OnSocketReceived);
                tcpClient.OnSocketState += new SocketStateHandler(tcpClient_OnSocketState);
                tcpClient.Start();
            }

        }

        void tcpClient_OnSocketState(object sender, SocketState state)
        {
            string stateText = (state == SocketState.Connected ? "成功":"失败");
            statusLabel1.Text = string.Format("连接服务器[ {0}：{1}] {2}!", ServerIP, ServerPort, stateText);

        }

        void tcpClient_OnSocketReceived(object sender, byte[] buffer)
        {
            //收到数据
            OnDataReceived(this, buffer);
        }

        public void TcpClose()
        {
            //TCP停止
            if (tcpServer != null)
            {
                tcpThread.Abort();
                tcpServer.Close();
            }

        }

        void tcpComm_OnClientDataAvailable(object sender, DataEventArgs e)
        {
            //收到数据
            OnDataReceived(this, e.Data);
        }

        void tcpComm_OnServerFull(object sender, DataEventArgs e)
        {
            //TCP服务器连接已满
            SafeOutText("[" + DateTime.Now.ToString() + "]");
            SafeOutText("[TCP]服务器连接已满!");
            SafeOutText("");
        }

        void tcpComm_OnClientDisconnect(object sender, DataEventArgs e)
        {
            //TCP客户端断开
            IPEndPoint iep = ((IPEndPoint)e.Client.RemoteEndPoint);

            SafeOutText("[" + DateTime.Now.ToString() + "]");
            SafeOutText("[TCP] " + iep.Address.ToString() + ":" + iep.Port.ToString() + "断开! ("
                + tcpServer.ClientList.Count.ToString() + "/" + tcpServer.MaxClient.ToString() + ")");
            SafeOutText("");

            //更新用户连接列表
            ShowClientList();
        }

        void tcpComm_OnClientConnect(object sender, DataEventArgs e)
        {
            //TCP客户端连接
            IPEndPoint iep = ((IPEndPoint)e.Client.RemoteEndPoint);

            SafeOutText("[" + DateTime.Now.ToString() + "]");
            SafeOutText("[TCP] " + iep.Address.ToString() + ":" + iep.Port.ToString() + "连接! ("
                + tcpServer.ClientList.Count.ToString() + "/" + tcpServer.MaxClient.ToString() + ")");
            SafeOutText("");

            //更新用户连接列表
            ShowClientList();
        }

        #endregion

        private void WindowMinimized()
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            notifyIcon1.ShowBalloonTip(1000, "程序已最小化", "WIFI-COM网络数据调试工具", ToolTipIcon.Info);
        }

        private bool canClose = false;

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            canClose = true;

            string text = cboCom.Text.Trim();
            if (text != "") configManager.SaveConfig("Application", "PortName", text);
            
            text = cboDefine.Text.Trim();
            if (text != "") configManager.SaveConfig("Application", "Define", text);
            
            text = numCount.Value.ToString();
            if (text != "") configManager.SaveConfig("Application", "RepeatCount", text);

            if (serialPort.IsOpen) serialPort.Close();
            TcpClose();
        }

        protected override void WndProc(ref Message m)
        {
            //关闭后最小化到任务栏
            //const int WM_SYSCOMMAND = 0x0112;
            //const int SC_CLOSE = 0xF060;
            //if (m.Msg == WM_SYSCOMMAND && (int)m.WParam == SC_CLOSE)
            //{
            //    if (!canClose) WindowMinimized();
            //    return;
            //}
            base.WndProc(ref m);
        }


        private void Form1_Load(object sender, EventArgs e)
        {
            PathApplication = Application.StartupPath + "\\";
            
            configManager = new ConfigManager(Path.Combine(Application.StartupPath, "Config.ini"));
            configManager.FillComboBox(cboDefine);

            string text = configManager.LoadConfig("Application", "PortName", "");
            if (text == "") text = ComPortName;
            cboCom.Text = text;

            text = configManager.LoadConfig("Application", "Define", "");
            if (text != "") cboDefine.Text = text;

            text = configManager.LoadConfig("Application", "RepeatCount", "");
            if (text != "") numCount.Value = int.Parse(text);


            serialPort = new SerialPort(ComPortName);
            serialPort.BaudRate = ComBaudRate;
            serialPort.Parity = Parity.None;
            serialPort.StopBits = StopBits.One;
            serialPort.DataBits = 8;
            serialPort.Handshake = Handshake.None;
            serialPort.DataReceived += new SerialDataReceivedEventHandler(serialPort_DataReceived);

            splitContainer1.Panel1Collapsed = !ServerMode;

            timer1.Interval = 1000;
            timer1.Start();

            tabControl1.SelectedIndex = ActiveTabIndex;

            if (StartMinimize)
            {
                WindowMinimized();
            }

        }

        void serialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //串口接收
            //SerialPort sp = (SerialPort)sender;           
            //string indata = sp.ReadExisting();

            byte[] data = new byte[serialPort.BytesToRead];
            serialPort.Read(data, 0, data.Length);
            

            string outText = string.Format("[{0} {1}] COM接收 \r\n{2}\r\n", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString(), ByteToHexStr(data));
            SafeOutText(outText);

        }


        private void OnDataReceived(object sender, byte[] data)
        {
            string outText = string.Format("[{0} {1}] TCP接收 \r\n{2}\r\n", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString(), ByteToHexStr(data));                
            SafeOutText(outText);
          
        }

        #region GUI显示

        public void SafeOutText(string text)
        {
            //输出数据
            try
            {
                if (txtInfo.InvokeRequired)
                {
                    txtInfo.Invoke(new SetTextCallback(SafeOutText), new object[] { text });
                }
                else
                {
                    OutText(text);
                }
            }
            catch
            {
            }
        }

        delegate void SetTextCallback(string text);

        private void OutText(string text)
        {
            //信息区文本
            txtInfo.AppendText(text + "\r\n");
            txtInfo.ScrollToCaret();
        }

        public static string ByteToHexStr(byte[] da)
        {
            string s = "";
            for (int i = 0; i < da.Length; i++)
            {
                s += Convert.ToString(da[i], 16).PadLeft(2, '0') + " ";
            }
            return s;
        }

        //使用Invoke显示UI信息
        private delegate void TreeViewUpdater(TreeView tv, List<ClientObject> clientList);

        private void ShowClientList()
        {
            if (tcpServer == null) return;
            TreeViewUpdater updateTreeView = new TreeViewUpdater(UpdateTreeView);
            tvClientList.Invoke(updateTreeView, new object[] { tvClientList, tcpServer.ClientList });
        }

        private void UpdateTreeView(TreeView tv, List<ClientObject> clientList)
        {
            //线程安全的添加节点信息
            tv.Nodes.Clear();
            //if (clientList.Count <= 0) return;
            //TreeNode rootNode = tv.Nodes.Add("用户列表");

            for (int i = 0; i < clientList.Count; i++)
            {
                ClientObject clientObj = clientList[i];
                //if (!clientObj.Client.Connected) continue;
                IPEndPoint iep = (IPEndPoint)clientObj.Client.RemoteEndPoint;

                TreeNode node = tv.Nodes.Add(iep.Address.ToString());
                TreeNode subNode1 = new TreeNode("Port: " + iep.Port.ToString());
                subNode1.ImageIndex = 1;
                subNode1.SelectedImageIndex = 2;
                //TreeNode subNode2 = new TreeNode("Level: " + clientObj.Level.ToString());
                //subNode2.ImageIndex = 1;
                //subNode2.SelectedImageIndex = 2;

                node.Nodes.Add(subNode1);
                //node.Nodes.Add(subNode2);

            }


            //tv.ExpandAll();
        }
        #endregion

        private void mnuAbout_Click(object sender, EventArgs e)
        {
            MessageBox.Show("WIFI-COM网络数据调试工具 Ver 1.0.0.2 [20130506]     ", "深圳长龙", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void mnuClear_Click(object sender, EventArgs e)
        {
            txtInfo.Clear();
        }


        private void mnuExit_Click(object sender, EventArgs e)
        {
            canClose = true;
            Close();
        }

        private void mnuStart_Click(object sender, EventArgs e)
        {
            mnuStart.Enabled = false;
            mnuStop.Enabled = true;

            TcpStart();
            if (ServerMode)
                statusLabel1.Text = string.Format("服务器已启动! 监听端口:{0}", ServerPort);
        }

        private void mnuStop_Click(object sender, EventArgs e)
        {
            mnuStart.Enabled = true;
            mnuStop.Enabled = false;

            TcpClose();
            ShowClientList();
            if (ServerMode)
                statusLabel1.Text = "服务器已停止!";

        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            ShowClientList();
        }


        private void timer1_Tick(object sender, EventArgs e)
        {
            mnuStart.PerformClick();
            timer1.Enabled = false;
        }

        private void mnuClose_Click(object sender, EventArgs e)
        {
            mnuExit.PerformClick();
        }

        private void mnuShow_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;

            WindowAPI.SetForegroundWindow(this.Handle);                    
            WindowAPI.OpenIcon(this.Handle);            
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            mnuShow.PerformClick();
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) mnuShow.PerformClick();
        }

        private void mnuShowLog_Click(object sender, EventArgs e)
        {
            //显示日志
            //string logFileName = Path.Combine(Application.StartupPath, "log.txt");
            //System.Diagnostics.Process.Start(logFileName); 
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtSend.Clear();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            string value = txtSend.Text.Trim();
            if (value.Length < 1) return;

            byte[] data = DataConvert.StrToHexByte(value);

            if (ServerMode)
            {
                tcpServer.BroadcastData(ClientLevel.None, data);
            }
            else
            {
                tcpClient.Send(data);
            }

            //tcpServer.BroadcastData(ClientLevel.None, data);

        }

        #region 数据包
        private void btnNew_Click(object sender, EventArgs e)
        {
            //新建
            cboDefine.Text = "";
            txtSrcID.Text = "";
            txtSrcAddr.Text = "";
            txtDstID.Text = "";
            txtDstAddr.Text = "";
            txtType.Text = "";
            txtCmd.Text = "";
            txtData.Text = "";
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            //保存
            string title = cboDefine.Text;
            if (title.Length < 1)
            {
                //新增
                title = InputBox.Show("Input", "输入预定义方案名:", "").Trim();

                if (cboDefine.Items.IndexOf(title) > 0)
                {
                    MessageBox.Show("输入的名称重复!", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (title.Length < 1) return;

                configManager.SaveItem(title, GetCurRecord());

                cboDefine.Items.Add(title);

                cboDefine.Text = title;

            }
            else
            {
                //更新
                configManager.SaveItem(title, GetCurRecord());
                if (cboDefine.Items.IndexOf(title) < 0)
                {
                    cboDefine.Items.Add(title);
                }
            }



        }

        private configRecord GetCurRecord()
        {
            configRecord record = new configRecord();
            record.SrcID = txtSrcID.Text.Trim();
            record.SrcAddr = txtSrcAddr.Text.Trim();
            record.DstID = txtDstID.Text.Trim();
            record.DstAddr = txtDstAddr.Text.Trim();
            record.OpType = txtType.Text.Trim();
            record.Cmd = txtCmd.Text.Trim();
            record.Data = txtData.Text.Trim();
            //record.RepeatCount = (int)numCount.Value;
            return record;
        }

        private bool ValidCurRecord()
        {
            configRecord record = GetCurRecord();

            bool boo = (record.SrcID.Length > 0 && record.DstID.Length > 0 && record.OpType.Length > 0 && record.Cmd.Length > 0 && record.Data.Length > 0);

            return boo;

        }

        private void cboDefine_SelectedIndexChanged(object sender, EventArgs e)
        {
            string title = cboDefine.Items[cboDefine.SelectedIndex].ToString();
            configRecord record = configManager.LoadItem(title);

            txtSrcID.Text = record.SrcID;
            txtSrcAddr.Text = record.SrcAddr;
            txtDstID.Text = record.DstID;
            txtDstAddr.Text = record.DstAddr;
            txtType.Text = record.OpType;
            txtCmd.Text = record.Cmd;
            txtData.Text = record.Data;
            //numCount.Value = record.RepeatCount;

        }

        int repeatCount = 0;
        private void btnSendPack_Click(object sender, EventArgs e)
        {
            //重复发送n次
            if (!ValidCurRecord())
            {
                MessageBox.Show("还有未填的数据项！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            timer2.Enabled = true;
            repeatCount = 0;
            labCount.Text = numCount.Value.ToString();
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            SendPackage();
            labCount.Text = (numCount.Value - repeatCount - 1).ToString();
            if (++repeatCount >= numCount.Value)
            {
                timer2.Enabled = false;
                labCount.Text = "";
            }

        }

        private void SendPackage()
        {
            //生成并发送数据包
            configRecord record = GetCurRecord();

            FrameStruct fsData = new FrameStruct();
            fsData.srcPort = Convert.ToByte(record.SrcID, 16);

            if (record.SrcAddr.Length > 0)
            {
                fsData.srcAddr = DataConvert.StrToHexByte(record.SrcAddr);
                fsData.srcLen = (byte)fsData.srcAddr.Length;
            }
            else
                fsData.srcLen = 0;

            fsData.dstPort = Convert.ToByte(record.DstID, 16);
            if (record.DstAddr.Length > 0)
            {
                fsData.dstAddr = DataConvert.StrToHexByte(record.DstAddr);
                fsData.dstLen = (byte)fsData.dstAddr.Length;
            }
            else
                fsData.dstLen = 0;

            fsData.optType =  Convert.ToByte(record.OpType, 16);
            fsData.cmd = Convert.ToByte(record.Cmd, 16);
            fsData.data = DataConvert.StrToHexByte(record.Data);    

            //------------------------------------------------
            byte[] data = ParseData.MakeFrameData(fsData);

            string outText = "";
            if (chkCom.Checked)
            {
                //串口发送
                //tcpComm.BroadcastData(ClientLevel.None, data);   
                if (serialPort.IsOpen)
                {
                    outText = string.Format("[{0} {1}] COM发送 \r\n{2}\r\n", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString(), ByteToHexStr(data));
                    serialPort.Write(data, 0, data.Length);
                }

            }
            else
            {
                //网络发送
                outText = string.Format("[{0} {1}] TCP发送 \r\n{2}\r\n", DateTime.Now.ToLongDateString(), DateTime.Now.ToLongTimeString(), ByteToHexStr(data));
                if (ServerMode)
                {
                    tcpServer.BroadcastData(ClientLevel.None, data);
                }
                else
                {
                    tcpClient.Send(data);
                }
            }

            SafeOutText(outText);
        }


        #endregion

        private void chkCom_CheckedChanged(object sender, EventArgs e)
        {
            //serialPort.PortName = cboCom.Items[cboCom.SelectedIndex].ToString();
            
            if (serialPort.IsOpen)
            {
                serialPort.Close();
            }

            if (chkCom.Checked)
            {
                serialPort.PortName = cboCom.Text;
                serialPort.Open();
            }
        }


    }

    class ConfigManager
    {
        string[] paramConfig = new string[] { "Application" };
        IniFile ini;
        public ConfigManager(string fileName)
        {
            ini = new IniFile(fileName);

        }

        public string LoadConfig(string ASection, string AKey, string ADefault)
        {
            string value = ini.ReadString(ASection, AKey, ADefault);
            return value;
        }

        public void SaveConfig(string ASection, string AKey, string AValue)
        {
            ini.WriteString(ASection, AKey, AValue);
        }

        public void FillComboBox(ComboBox cbo)
        {
            ArrayList secList = ini.ReadAllSections();

            cbo.Items.Clear();
            foreach (object obj in secList)
            {
                string value = (string)obj;
                if (!IsParam(value))
                {
                    cbo.Items.Add(value);
                }
            }

            if (cbo.Items.Count > 0) cbo.SelectedIndex = 0;

        }

        private bool IsParam(string value)
        {
            bool boExist = false;
            foreach (string item in paramConfig)
            {
                if (item == value)
                {
                    boExist = true;
                    break;
                }
            }

            return boExist;

        }

        public void SaveItem(string name, configRecord value)
        {
            ini.WriteString(name, "SrcID", value.SrcID);
            ini.WriteString(name, "SrcAddr", value.SrcAddr);
            ini.WriteString(name, "DstID", value.DstID);
            ini.WriteString(name, "DstAddr", value.DstAddr);
            ini.WriteString(name, "OpType", value.OpType);
            ini.WriteString(name, "Cmd", value.Cmd);
            ini.WriteString(name, "Data", value.Data);
            //ini.WriteString(name, "RepeatCount", value.RepeatCount.ToString());

        }

        public configRecord LoadItem(string name)
        {
            configRecord record = new configRecord();

            record.SrcID = ini.ReadString(name, "SrcID", "");
            record.SrcAddr = ini.ReadString(name, "SrcAddr", "");
            record.DstID = ini.ReadString(name, "DstID", "");
            record.DstAddr = ini.ReadString(name, "DstAddr", "");
            record.OpType = ini.ReadString(name, "OpType", "");
            record.Cmd = ini.ReadString(name, "Cmd", "");
            record.Data = ini.ReadString(name, "Data", "");
            //record.RepeatCount = int.Parse(ini.ReadString(name, "RepeatCount", ""));

            return record;
        }

    }

    class configRecord
    {
        public string SrcID;
        public string SrcAddr;
        public string DstID;
        public string DstAddr;
        public string OpType;
        public string Cmd;
        public string Data;

        //public int RepeatCount;
    }

    static class WindowAPI
    {
        public static int WM_SYSCOMMAND = 0x112;
        public static int SC_MAXIMIZE = 0xF030;
        public static int SC_RESTORE = 0xF120;
        public static int SC_DEFAULT = 0xF160;

        [DllImport("user32.dll ", CharSet = CharSet.Unicode)]
        public static extern IntPtr PostMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        [DllImport("User32.dll ", EntryPoint = "FindWindow")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", EntryPoint = "ShowWindow", CharSet = CharSet.Auto)]
        public static extern int ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32")]
        public static extern bool OpenIcon(IntPtr hwnd);

        [DllImport("user32")]
        public static extern bool SetForegroundWindow(IntPtr hwnd);

    }
}
