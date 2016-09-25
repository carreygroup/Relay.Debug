#region License
//
//****************************************************************************
// *
// * Copyright (c) Caesar LAB. All Rights Reserved.
// *
// * This software is the confidential and proprietary information of Caesar LAB ("Confidential Information").  
// * You shall not disclose such Confidential Information and shall use it only in
// * accordance with the terms of the license agreement you entered into with Caesar LAB.
// *
// * CAESAR LAB MAKES NO REPRESENTATIONS OR WARRANTIES ABOUT THE SUITABILITY OF THE
// * SOFTWARE, EITHER EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// * IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// * PURPOSE, OR NON-INFRINGEMENT. CRELAB SHALL NOT BE LIABLE FOR ANY DAMAGES
// * SUFFERED BY LICENSEE AS A RESULT OF USING, MODIFYING OR DISTRIBUTING
// * THIS SOFTWARE OR ITS DERIVATIVES.
// *
// * Original Author: Caesar LAB
// * Last checked in by $Author$
// * $Id$
// */
#endregion

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.IO.Ports;

namespace RelayMgr
{
    public enum RelayType : uint
    {
        /// <summary>
        /// 开
        /// </summary>
        ON = 0x12,
        /// <summary>
        /// 关
        /// </summary>
        OFF = 0x11,
        /// <summary>
        /// 位控制
        /// </summary>
        BYTECTRL = 0x13,
        ///
        /// 
        /// 
        GROUPON = 0x15,
        GROUPOFF = 0x14,
        /// <summary>
        /// 查询
        /// </summary>
        INQUIRE = 0x10,
        /// <summary>
        /// 查询板卡参数
        /// </summary>
        GETCONFIG = 0x21,
        /// <summary>
        /// 设置配置参数
        /// </summary>
        SETCONFIG = 0x22
    }

    /// <summary>
    /// 
    /// </summary>
    public class Relay
    {
        public static uint ProtocolHead = 0x55; //协议头

        //private static string PORT_NAME = "COM1";
        //private static short Address;
        //private static int Line;
        //private static RelayType type;

        private static SerialPort serialPort = null;
        //private const int BAUD_RATE = 9600;

        private const int ReadTimeout = 50;
        private const int WriteTimeout = 50;

        //DataReceived事件委托方法
        private static void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            Thread.Sleep(120);
            int DataLength = serialPort.BytesToRead;
            int i = 0;
            StringBuilder sb = new StringBuilder();
            while (i < DataLength)
            {
                byte[] ds = new byte[1024];
                int len = serialPort.Read(ds, 0, 1024);
                sb.Append(Encoding.ASCII.GetString(ds, 0, len));
                i += len;
            }
        }

        private static byte[] chCRCHTalbe = new byte[]
        {
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x00, 0xC1, 0x81, 0x40,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40, 0x01, 0xC0, 0x80, 0x41, 0x01, 0xC0, 0x80, 0x41,
            0x00, 0xC1, 0x81, 0x40
        };

        private static byte[] chCRCLTalbe = new byte[]		// CRC 低位字节值表
        {
            0x00, 0xC0, 0xC1, 0x01, 0xC3, 0x03, 0x02, 0xC2, 0xC6, 0x06, 0x07, 0xC7,
            0x05, 0xC5, 0xC4, 0x04, 0xCC, 0x0C, 0x0D, 0xCD, 0x0F, 0xCF, 0xCE, 0x0E,
            0x0A, 0xCA, 0xCB, 0x0B, 0xC9, 0x09, 0x08, 0xC8, 0xD8, 0x18, 0x19, 0xD9,
            0x1B, 0xDB, 0xDA, 0x1A, 0x1E, 0xDE, 0xDF, 0x1F, 0xDD, 0x1D, 0x1C, 0xDC,
            0x14, 0xD4, 0xD5, 0x15, 0xD7, 0x17, 0x16, 0xD6, 0xD2, 0x12, 0x13, 0xD3,
            0x11, 0xD1, 0xD0, 0x10, 0xF0, 0x30, 0x31, 0xF1, 0x33, 0xF3, 0xF2, 0x32,
            0x36, 0xF6, 0xF7, 0x37, 0xF5, 0x35, 0x34, 0xF4, 0x3C, 0xFC, 0xFD, 0x3D,
            0xFF, 0x3F, 0x3E, 0xFE, 0xFA, 0x3A, 0x3B, 0xFB, 0x39, 0xF9, 0xF8, 0x38,
            0x28, 0xE8, 0xE9, 0x29, 0xEB, 0x2B, 0x2A, 0xEA, 0xEE, 0x2E, 0x2F, 0xEF,
            0x2D, 0xED, 0xEC, 0x2C, 0xE4, 0x24, 0x25, 0xE5, 0x27, 0xE7, 0xE6, 0x26,
            0x22, 0xE2, 0xE3, 0x23, 0xE1, 0x21, 0x20, 0xE0, 0xA0, 0x60, 0x61, 0xA1,
            0x63, 0xA3, 0xA2, 0x62, 0x66, 0xA6, 0xA7, 0x67, 0xA5, 0x65, 0x64, 0xA4,
            0x6C, 0xAC, 0xAD, 0x6D, 0xAF, 0x6F, 0x6E, 0xAE, 0xAA, 0x6A, 0x6B, 0xAB,
            0x69, 0xA9, 0xA8, 0x68, 0x78, 0xB8, 0xB9, 0x79, 0xBB, 0x7B, 0x7A, 0xBA,
            0xBE, 0x7E, 0x7F, 0xBF, 0x7D, 0xBD, 0xBC, 0x7C, 0xB4, 0x74, 0x75, 0xB5,
            0x77, 0xB7, 0xB6, 0x76, 0x72, 0xB2, 0xB3, 0x73, 0xB1, 0x71, 0x70, 0xB0,
            0x50, 0x90, 0x91, 0x51, 0x93, 0x53, 0x52, 0x92, 0x96, 0x56, 0x57, 0x97,
            0x55, 0x95, 0x94, 0x54, 0x9C, 0x5C, 0x5D, 0x9D, 0x5F, 0x9F, 0x9E, 0x5E,
            0x5A, 0x9A, 0x9B, 0x5B, 0x99, 0x59, 0x58, 0x98, 0x88, 0x48, 0x49, 0x89,
            0x4B, 0x8B, 0x8A, 0x4A, 0x4E, 0x8E, 0x8F, 0x4F, 0x8D, 0x4D, 0x4C, 0x8C,
            0x44, 0x84, 0x85, 0x45, 0x87, 0x47, 0x46, 0x86, 0x82, 0x42, 0x43, 0x83,
            0x41, 0x81, 0x80, 0x40
        };

        private static ushort CalculateCrc16(byte[] buffer)
        {
            byte crcHi = 0xff;  // high crc byte initialized
            byte crcLo = 0xff;  // low crc byte initialized

            for (int i = 0; i < buffer.Length - 2; i++)
            {
                int crcIndex = crcHi^buffer[i]; // calculate the crc lookup index

                crcHi = (byte)(crcLo^chCRCHTalbe[crcIndex]);
                crcLo = (byte)chCRCLTalbe[crcIndex];
            }

            return (ushort)(crcHi << 8 | crcLo);
        }

        //创建命令
        public static byte[] CreateCMD(short _Address, int _Line, RelayType _type)
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = (byte)(_Address % 256);
            command[2] = (byte)_type;
            command[3] = 0;
            command[4] = 0;
            command[5] = (byte)(_Line >> 8);
            command[6] = (byte)_Line;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }
        //创建命令
        public static byte[] CreateCMDByDebug(ushort _Address, byte Option, byte data1, byte data2, byte data3, byte data4)
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = (byte)(_Address % 256);
            command[2] = (byte)Option;
            command[3] = (byte)data1;
            command[4] = (byte)data2;
            command[5] = (byte)data3;
            command[6] = (byte)data4;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }
        //创建命令
        public static byte[] CreateCMDByDebugV3(ushort _Address, byte RegH, byte RegL, byte data1, byte data2, byte data3, byte data4, byte data5, byte data6, byte data7, byte data8)
        {
            byte[] command = new byte[11];
            command[0] = (byte)(_Address % 256);
            command[1] = (byte)RegH;
            command[2] = (byte)RegL;
            command[3] = (byte)data1;
            command[4] = (byte)data2;
            command[5] = (byte)data3;
            command[6] = (byte)data4;
            command[7] = (byte)data5;
            command[8] = (byte)data6;
            command[9] = (byte)data7;
            command[10] = (byte)data8;
            return command;
        }
 
        //分析输出状态（继电器）
        public static int GetState(byte[] arrRecMsg, int Line)
        {
            int state = (arrRecMsg[5] << 8) + arrRecMsg[6];
            return (state >> (Line - 1)) & 1;
        }
        //分析输入状态（开关量输入）
        public static int GetInputState(byte[] arrRecMsg, int Line)
        {
            int state = (arrRecMsg[3] << 8) + arrRecMsg[4];
            return (state >> (Line - 1)) & 1;
        }
        //获取配置
        public static byte[] GetConfig()
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = 0;
            command[2] = 0x21;
            command[3] = 0;
            command[4] = 0;
            command[5] = 0;
            command[6] = 0;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }
        //设置板卡配置
        public static byte[] SetBand(ushort Address, ushort Band)
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = (byte)Address;
            command[2] = 0x27;
            command[3] = 0;
            command[4] = 0;
            command[5] = 0;
            command[6] = (byte)Band;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }

        //设置板卡配置
        public static byte[] SetConfig(ushort Address, UInt32 Config, ushort newAddH,ushort NewAddress)
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = (byte)Address;
            command[2] = 0x22;
            command[3] = (byte)newAddH;
            command[4] = (byte)(Config>>8);
            command[5] = (byte)Config;
            command[6] = (byte)NewAddress;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }

        //设置板卡配置
        public static byte[] SetFunc(ushort Address, UInt32 Config)
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = (byte)Address;
            command[2] = 0x2A;
            command[3] = (byte)(Config >> 24);
            command[4] = (byte)(Config >> 16);
            command[5] = (byte)(Config>>8);
            command[6] = (byte)Config;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }

        //设置用户命令计数
        public static byte[] SetUserCMDCount(uint Address, uint Count)
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = (byte)Address;
            command[2] = (byte)(0x29);
            command[3] = (byte)(0x00);
            command[4] = (byte)(0x00);
            command[5] = (byte)(Count >> 8);
            command[6] = (byte)Count;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }
        //设置成无线捕获状态
        public static byte[] SetRFCaption(uint Address, uint Enable)
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = (byte)Address;
            command[2] = (byte)(0x28);
            command[3] = (byte)0x00;
            command[4] = (byte)(0x00);
            command[5] = (byte)(0x00);
            command[6] = (byte)Enable;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }

        //设置成无线捕获状态
        public static byte[] SetRFIDCaption(uint Address, uint Enable)
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = (byte)Address;
            command[2] = (byte)(0x2B);
            command[3] = (byte)0x00;
            command[4] = (byte)(0x00);
            command[5] = (byte)(0x00);
            command[6] = (byte)Enable;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }

        //EEPROM格式化
        public static byte[] Format(ushort Address)
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = (byte)Address;
            command[2] = 0x23;
            command[3] = 0;
            command[4] = 0;
            command[5] = 0;
            command[6] = 0;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }
        //生成输入动作指令
        public static byte[] SetInputActionH(uint Address, uint Line, UInt64 ioValue)
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = (byte)Address;
            command[2] = (byte)(0x30 + Line);
            command[3] = (byte)(ioValue >> 24);
            command[4] = (byte)(ioValue >> 16);
            command[5] = (byte)(ioValue >> 8);
            command[6] = (byte)ioValue;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }
        //生成输入动作指令
        public static byte[] SetInputActionL(uint Address, uint Line, uint tagAddr, uint CMD, uint OH, uint OL)
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = (byte)Address;
            command[2] = (byte)(0x30 + Line);
            command[3] = (byte)tagAddr;
            command[4] = (byte)CMD;
            command[5] = (byte)OH;
            command[6] = (byte)OL;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }

        //设置输入配置
        public static byte[] GetIAConfig(ushort Address, ushort uLine)
        {
            byte[] command = new byte[8];
            command[0] = (byte)ProtocolHead;
            command[1] = (byte)Address;
            command[2] = (byte)(0x25);
            command[3] = (byte)uLine;
            command[4] = 0;
            command[5] = 0;
            command[6] = 0;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + command[i];
            }
            command[7] = (byte)(sum % 256);
            return command;
        }

        public static byte[] CANBuffer(byte[] inBytes)
        {
            //byte[] inBytes = new byte[8];
            byte[] dat = new byte[0x11];
            int num;
            byte[] buffer3 = { 0, 0, 0, 0 };

            //"标准帧")
            int num2 = buffer3[0];
            num2 = num2 << 8;
            num2 += buffer3[1];
            short num3 = buffer3[0];
            num3 = (short)(num3 << 8);
            num3 = (short)(num3 + buffer3[1]);
            num3 = (short)(num3 << 5);
            dat[2] = 0;
            dat[3] = (byte)((num3 & 0xff00) >> 8);
            dat[4] = (byte)(num3 & 0xff);
            num = 3;

            //"数据帧"
            for (int i = 0; i < inBytes.Length; i++)
            {
                dat[(i + num) + 2] = inBytes[i];
            }
            num += inBytes.Length;

            dat[2] = (byte)(dat[2] | ((byte)inBytes.Length));

            dat[0] = 2;
            dat[1] = (byte)num;
            dat[num + 2] = 3;
            dat[num + 3] = BccComput(dat, num + 3);

            return dat;

        }

        public static byte BccComput(byte[] dat, int len)
        {
            byte num = 0;
            for (int i = 0; i < len; i++)
            {
                num = (byte)(num ^ dat[i]);
            }
            return num;
        }
    }
}

