using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace Config
{
    public class MyListViewItem : ListViewItem
    {
        /// <summary>
        /// 设备地址
        /// </summary>
        public ushort uAddress;
        /// <summary>
        /// 断电保持
        /// </summary>
        public ushort uHoldPower;
        /// <summary>
        /// DO使能
        /// </summary>
        public ushort DOEnable;
        /// <summary>
        /// DI使能
        /// </summary>
        public ushort DIEnable;
        /// <summary>
        /// RF使能
        /// </summary>
        public ushort RFEnable;
        /// <summary>
        /// RF低速模式
        /// </summary>
        public ushort RF_SLOW;

        public ushort DI_SLOW;
        public ushort Task_EN;

        public ushort TH_EN;
        /// <summary>
        /// 模块是否数据校验
        /// </summary>
        public ushort CHECKCODE;
        /// <summary>
        /// 允许执行器执行
        /// </summary>
        public ushort Exec_EN;
        /// <summary>
        /// 静默模式
        /// </summary>
        public ushort Silent;

        public ushort SilentReg;

        public ushort VarOverflow;

        public ushort Answer;

        public ushort Timer_Vector;

        public ushort Variable;

        public int hwVersion;

        public int Major_Version_Number;
        public int Revision_Number;
        public int Product_Version_Number;
        public int PCB_Version;

        public bool HaveDO;
        public bool HaveDI;
        public bool HaveRTC;
        public bool HaveEXTMEM;

        public int DI_Count;
        public int DO_Count;

        public string ipaddr;

        public string submask;

        public string getway;

        public string ntpserver;

        public int ntpport;

        public bool use_ntp;

        public int v4_port;
        //public List<byte[]> SN = new List<byte[]>();
    }
}
