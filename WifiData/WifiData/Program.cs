using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WifiData
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool SingleApp = bool.Parse(System.Configuration.ConfigurationManager.AppSettings["SingleApp"]);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (!SingleApp)
            {
                Application.Run(new Form1());
            }
            else
            {
                bool bExist = false;
                System.Threading.Mutex mm = new System.Threading.Mutex(true, "IntServer", out bExist);
                if (bExist)
                {
                    Application.Run(new Form1());
                    mm.ReleaseMutex();
                }
                else
                {
                    //查找窗体
                    IntPtr handle = WindowAPI.FindWindow(null, "WIFI-COM网络数据调试工具");
                    if (handle != IntPtr.Zero)
                    {
                        //恢复窗口并设置为前台窗口
                        WindowAPI.SetForegroundWindow(handle);
                        WindowAPI.OpenIcon(handle);
                        //PostMessage(handle, WM_SYSCOMMAND, (IntPtr)SC_DEFAULT, IntPtr.Zero);
                    }
                }
            }
        }
    }
}
