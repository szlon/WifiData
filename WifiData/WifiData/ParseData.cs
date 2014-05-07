using System;
using System.Collections.Generic;
using System.Text;
using Lon.Common;

namespace TcpLib
{
    //数据转换
    class ParseData
    {

        //封装数据，对数据帧进行转义处理并加上头尾
        static public byte[] PackData(byte[] data)
        {
            List<byte> byteList = new List<byte>();

            byteList.Add(0x10);     //头
            byteList.Add(0x02);

            byte bNum;
            for (int i = 0; i < data.Length; i++)
            {
                bNum = data[i];
                byteList.Add(bNum);
                if (bNum == 0x10)
                {
                    //如果有0x10则再添加一个0x10字节
                    byteList.Add(bNum);
                }
            }

            byteList.Add(0x10);     //尾
            byteList.Add(0x03);

            return byteList.ToArray();
        }
        
        //解封数据，去掉转义符，去掉头尾，提取数据
        static public byte[] UnPackData(byte[] data)
        {
            //机车信号信息帧：头信息(8B) + 设备号(2B)+数据(16B)
            byte head1 = data[0],               //0x10
                head2 = data[1],                //0x02
                tail1 = data[data.Length-2],    //0x10
                tail2 = data[data.Length-1];    //0x03

            //检查头尾是否正确
            if ((head1 == 0x10) && (head2 == 0x02) && (tail1 == 0x10) && (tail2 == 0x03))
            {
                List<byte> byteList = new List<byte>();
                byte bNum;
                
                for (int i = 2; i < data.Length - 2; i++)
                {
                    bNum = data[i];
                    byteList.Add(bNum);
                    if (bNum == 0x10)
                    {
                        //如果有0x10，则跳过一个字节
                        i++;
                    }
                }
             
                return byteList.ToArray();
            }
            else
            {
                return null;
            }

        }

       
        //生成TCP通信数据包
        static public byte[] MakePacketData(byte srcPort, //byte srcLen, byte[] srcAddress,
                                    byte dstPort, //byte dstLen, byte[] dstAddress,
                                    byte optType, byte command, byte[] data)
        {

            UInt16 dataLen = (UInt16)(8 + data.Length);  //数据长度:从源端口代码到CRC结束

            byte[] dat = new byte[dataLen + 2]; //0-23:计算CRC所需的数据，24-25:CRC结果
            DataConvert.UInt16toBytes(dataLen, dat, 0);     //数据长度

            dat[2] = srcPort;                   //源端口代码
            dat[3] = 0x00;                      //源通信地址长度

            dat[4] = dstPort;                  //目标端口代码
            dat[5] = 0x00;                     //目标通信地址长度

            dat[6] = optType;                   //业务类型
            dat[7] = command;                   //命令

            //数据
            Array.Copy(data, 0, dat, 8, data.Length);


            //添加CRC校验码
            UInt16 wCRC16 = Crc16.GetCRC16(dat, 0, dataLen); //计算从信息长度到数据结束的CRC校验
            DataConvert.UInt16toBytes(wCRC16, dat, dataLen);   //添加CRC16校验

            byte[] outData = PackData(dat);   //加头尾，转义处理

            return outData;
        }


        //生成数据帧
        //static public byte[] MakeFrameData(byte srcPort, byte srcLen, byte[] srcAddr,
        //                                    byte dstPort, byte dstLen, byte[] dstAddr,
        //                                    byte optType, byte cmd, byte[] data)

        static public byte[] MakeFrameData(FrameStruct frameData)
        {
            byte[] dat = null;

            try
            {
                UInt16 dataLen = (UInt16)(8 + frameData.srcLen + frameData.dstLen + frameData.data.Length);
                //dataLen = DataConvert.InvWord(dataLen);

                int i = 0;

                dat = new byte[dataLen + 2]; //数据长度 + CRC长度

                DataConvert.UInt16toBytes(DataConvert.InvWord(dataLen), dat, i);
                i = i + 2;

                // Source
                dat[i] = frameData.srcPort;
                i++;

                dat[i] = frameData.srcLen;
                i++;
                
                if (frameData.srcLen > 0)
                {
                    Array.Copy(frameData.srcAddr, 0, dat, i, frameData.srcAddr.Length);
                    i = i + frameData.srcLen;
                }

                // Destination
                dat[i] = frameData.dstPort;
                i++;

                dat[i] = frameData.dstLen;
                i++;

                if (frameData.dstLen > 0)
                {
                    Array.Copy(frameData.dstAddr, 0, dat, i, frameData.dstAddr.Length);
                    i = i + frameData.dstLen;
                }

                // Command
                dat[i] = frameData.optType;
                i++;

                dat[i] = frameData.cmd;
                i++;

                Array.Copy(frameData.data, 0, dat, i, frameData.data.Length);


                //添加CRC校验码
                UInt16 wCRC16 = Crc16.GetCRC16(dat, 0, dataLen); //计算从信息长度到数据结束的CRC校验
                wCRC16 = DataConvert.InvWord(wCRC16);
                DataConvert.UInt16toBytes(wCRC16, dat, dataLen);   //添加CRC16校验

            }
            catch (System.Exception ex)
            {
                throw ex;
            }



            byte[] outData = PackData(dat);   //加头尾，转义处理

            return outData;
        }

        /// <summary>
        /// 从数据帧提取数据
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        static public FrameStruct DeFrameData(byte[] data)
        {
            FrameStruct frameData = new FrameStruct();
            frameData.validity = false;

            try
            {
                byte[] dat = ParseData.UnPackData(data);   //去掉头尾和转义字节

                //检查CRC校验码                       
                UInt16 wCrc1 = Crc16.GetCRC16(dat, 0, dat.Length - 2);
                UInt16 wCrc2 = DataConvert.BytestoUInt16(dat, dat.Length - 2);

                wCrc2 = DataConvert.InvWord(wCrc2);

                if (wCrc1 == wCrc2)
                {
                    int i = 0;

                    UInt16 dataLen = DataConvert.BytestoUInt16(dat, i);
                    dataLen = DataConvert.InvWord(dataLen);

                    i = i + 2;

                    frameData.srcPort = dat[i];
                    i++;

                    frameData.srcLen = dat[i];
                    i++;

                    frameData.srcAddr = new byte[frameData.srcLen];
                    Array.Copy(dat, i, frameData.srcAddr, 0, frameData.srcLen);
                    i = i + frameData.srcLen;

                    frameData.dstPort = dat[i];
                    i++;

                    frameData.dstLen = dat[i];
                    i++;

                    frameData.dstAddr = new byte[frameData.dstLen];
                    Array.Copy(dat, i, frameData.dstAddr, 0, frameData.dstLen);
                    i = i + frameData.dstLen;

                    frameData.optType = dat[i];
                    i++;

                    frameData.cmd = dat[i];
                    i++;

                    int iLen = dataLen - i;
                    frameData.data = new byte[iLen];

                    Array.Copy(dat, i, frameData.data, 0, iLen);

                    frameData.validity = true;
                }



            }
            catch (System.Exception ex)
            {
                throw ex;
            }





            return frameData;
        }

        //老的车次号转换
        static public string GetCCH(string code)
        {
            string strCat = "",
                    strNum = "0";
            if (code.Length < 2) return strNum;
            try
            {
                int iCode = Int32.Parse(code.Substring(0, 2));
                strCat = iCode.ToString();
                strNum = code.Remove(0, 2);

                switch (iCode)
                {
                    case 20:
                        strCat = "T";
                        break;
                    case 21:
                        strCat = "K";
                        break;
                    case 22:
                        strCat = "L";
                        break;
                    case 23:
                        strCat = "Y";
                        break;
                    case 24:
                        strCat = "00";
                        break;
                    case 26:
                        strCat = "N";
                        break;
                    case 29:
                        strCat = "X";
                        break;
                    case 30:
                        strCat = "LT";
                        break;
                    case 31:
                        strCat = "0K";
                        break;
                    case 32:
                        strCat = "0L";
                        break;
                    case 33:
                        strCat = "0Y";
                        break;
                    case 39:
                        strCat = "0X";
                        break;

                    default:
                        break;
                }

            }
            catch (Exception ex)
            {
                throw ex;
                //Console.WriteLine(e.ToString());
                //Console.WriteLine(DateTime.Now.ToString());
                //Console.WriteLine("");
            }

            return strCat + strNum;

        }
         
    }




    /// <summary>
    /// 数据帧结构
    /// </summary>
    public struct FrameStruct
    {        
        public byte srcPort;
        public byte srcLen;
        public byte[] srcAddr;

        public byte dstPort;
        public byte dstLen;
        public byte[] dstAddr;
        
        public byte optType;
        public byte cmd;
        public byte[] data;

        public bool validity;

        public byte[] buffer;   //收到的原始数据，用于转发到终端
    }


}
