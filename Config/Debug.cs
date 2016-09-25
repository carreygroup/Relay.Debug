using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO.Ports;
using System.IO;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using DotNetSpeech;
using RelayMgr;

namespace Config
{
    public partial class frmConfig : Form
    {
        private static SerialPort serialPort = null;
        private int BAUD_RATE = 9600;

        private const int ReadTimeout = 50;
        private const int WriteTimeout = 50;

        private bool iNetThreadRUN = false;
        private static List<byte[]> CMDList = new List<byte[]>();
        public static frmConfig pCurrentWin = null;

        private frmDebug fdebug = new frmDebug();
        /// <summary>
        /// 更新事件
        /// </summary>
        /// <param name="msg"></param>
        public delegate void HandleInterfaceUpdataDelegate(byte[] msg);
        private HandleInterfaceUpdataDelegate interfaceUpdataHandle;

        public bool ReceiveEventFlag = false;  //接收事件是否有效 false表示有效

        SpeechVoiceSpeakFlags spFlags;
        SpVoice voice;

        int privdi = 0;

        public frmConfig(string uid)
        {
            InitializeComponent();

            string SkinFile = string.Empty;
            string iniFile = string.Empty;
            iniFile = System.Environment.CurrentDirectory + "\\config.ini";
            if (System.IO.File.Exists(iniFile))
            {
                this.Text = INIFile.ReadValue(iniFile, "SYSTEM", "title");
                SkinFile = INIFile.ReadValue(iniFile, "SYSTEM", "SkinFile");
            }
            if (SkinFile.Equals(string.Empty))
            {
                SkinFile = "MP10.ssk";
            }
            SkinFile = System.Environment.CurrentDirectory + "\\" + SkinFile;
            skinEngine1.SkinFile = SkinFile;
            tp_Ethernet.Parent = null;
            //tp_utils.Parent = null;
            LoadConfig();
        }

        private void frmConfig_Load(object sender, EventArgs e)
        {
            EnumCOMPort();
            pCurrentWin = this;

            try
            {
                spFlags = SpeechVoiceSpeakFlags.SVSFlagsAsync;
                voice = new SpVoice();
            }
            catch { }
        }
        /// <summary>
        /// 读入连接配置
        /// </summary>
        private void LoadConfig()
        {
            try
            {
                string iniFile = string.Empty;
                iniFile = System.Environment.CurrentDirectory + "\\config.ini";
                if (System.IO.File.Exists(iniFile))
                {
                    txtIP.Text = INIFile.ReadValue(iniFile, "CONNECTION", "DOMAIN");
                    txtPort.Text = INIFile.ReadValue(iniFile, "CONNECTION", "PORT");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show("Speech Engine initialization failure", "Info", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        /// <summary>
        /// 枚举串口
        /// </summary>
        private void EnumCOMPort()
        {
            cb_SerialPort.Items.Clear();
            cb_SerialPort.Items.AddRange(System.IO.Ports.SerialPort.GetPortNames());
            if (cb_SerialPort.Items.Count > 0)
            {
                cb_SerialPort.SelectedIndex = 0;
            }
        }

        private void btnBeginListen_Click(object sender, EventArgs e)
        {
            if (rb_TCP.Checked)
            {
                try
                {
                    if (TCP_Connect(txtIP.Text, txtPort.Text))
                    {
                        btnBeginListen.Enabled = false;
                        btnEndListen.Enabled = true;
                        EnableBtn(true);
                        rbCOM.Enabled = false;
                        rb_TCP.Enabled = false;
                        rb_UDP.Enabled = false;
                        txtPort.Enabled = false;
                        txtIP.Enabled = false;
                    }

                }
                catch (Exception err)
                {
                    MessageBox.Show(err.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
                if (rb_UDP.Checked)
            {
                UDPServer_Start(txtIP.Text, txtPort.Text);

                btnBeginListen.Enabled = false;
                btnEndListen.Enabled = true;
                EnableBtn(true);
                rbCOM.Enabled = false;
                rb_TCP.Enabled = false;
                rb_UDP.Enabled = false;
                txtPort.Enabled = false;
                txtIP.Enabled = false;
            }
            else
                if (rbCOM.Checked)
            {
                try
                {
                    if (serialPort == null)
                    {
                        try
                        {
                            BAUD_RATE = Convert.ToInt32(cb_ConBand.Text);

                        }
                        catch { }
                        serialPort = new SerialPort(cb_SerialPort.Text, BAUD_RATE, Parity.None, 8);
                        serialPort.ReceivedBytesThreshold = 8;
                        serialPort.ReadBufferSize = 8;
                        serialPort.WriteBufferSize = 8;
                        serialPort.DataReceived += new SerialDataReceivedEventHandler(SerialPort_DataReceived);//DataReceived事件委托
                                                                                                               //读写时间超时 
                        serialPort.DtrEnable = true;
                        serialPort.RtsEnable = true;
                        serialPort.ReadTimeout = ReadTimeout;
                        serialPort.WriteTimeout = WriteTimeout;
                        serialPort.Open();
                    }
                    btnBeginListen.Enabled = false;
                    btnEndListen.Enabled = true;
                    EnableBtn(true);

                    EnableBtn(true);
                    interfaceUpdataHandle = new HandleInterfaceUpdataDelegate(SetDevState);
                    rbCOM.Enabled = false;
                    cb_SerialPort.Enabled = false;
                }
                catch (Exception err)
                {
                    MessageBox.Show(err.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            if (btnEndListen.Enabled)
            {
                byte[] m_SendCMD = new byte[8];
                lv_Items.Clear();
                m_SendCMD = Relay.GetConfig();

                CMDList.Add(m_SendCMD);
                tm_Debug.Enabled = true;
            }
            SaveConnectCfg();
        }

        private void SaveConnectCfg()
        {
            string iniFile = string.Empty;
            iniFile = System.Environment.CurrentDirectory + "\\config.ini";
            if (System.IO.File.Exists(iniFile))
            {
                INIFile.Writue(iniFile, "CONNECTION", "DOMAIN", txtIP.Text);
                INIFile.Writue(iniFile, "CONNECTION", "PORT", txtPort.Text);
                INIFile.Writue(iniFile, "CONNECTION", "SerialPort", cb_SerialPort.Text);
                INIFile.Writue(iniFile, "CONNECTION", "Baud", cb_Band.Text);
            }

        }
        /// <summary>
        /// 变更设备状态
        /// </summary>
        /// <param name="statemsg"></param>
        private void SetDevState(byte[] statemsg)
        {
            string hex = BitConverter.ToString(statemsg);
            lbl_Receive.Text = hex;
            fdebug.AddInfo(hex);
            if (Check8bytedata(statemsg))
                at8bytemsg(statemsg);
            else
            if (statemsg.Length == 12)
                at11bytemsg(statemsg);
            else
            if (statemsg.Length > 12)
                atv4bytemsg(statemsg);
        }
        private bool Check8bytedata(byte[] statemsg)
        {
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + statemsg[i];
            }
            if (statemsg[7] == (byte)(sum % 256))
            {
                return true;
            }
            return false;
        }
        private void at8bytemsg(byte[] statemsg)
        {
            bool haveItem = false;
            int sum = 0;
            for (int i = 0; i <= 6; i++)
            {
                sum = sum + statemsg[i];
            }
            if (statemsg[7] == (byte)(sum % 256))
            {
                lbl_Info.Visible = false;
                if (statemsg[0] == 0x22)
                {
                    if (statemsg[2] == 0x10)
                    {
                        int i = 0;
                        if (Convert.ToBoolean(statemsg[6] & 0x01)) pbst_1.BackColor = Color.Green; else pbst_1.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[6] & 0x02)) pbst_2.BackColor = Color.Green; else pbst_2.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[6] & 0x04)) pbst_3.BackColor = Color.Green; else pbst_3.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[6] & 0x08)) pbst_4.BackColor = Color.Green; else pbst_4.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[6] & 0x10)) pbst_5.BackColor = Color.Green; else pbst_5.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[6] & 0x20)) pbst_6.BackColor = Color.Green; else pbst_6.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[6] & 0x40)) pbst_7.BackColor = Color.Green; else pbst_7.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[6] & 0x80)) pbst_8.BackColor = Color.Green; else pbst_8.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[5] & 0x01)) pbst_9.BackColor = Color.Green; else pbst_9.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[5] & 0x02)) pbst_10.BackColor = Color.Green; else pbst_10.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[5] & 0x04)) pbst_11.BackColor = Color.Green; else pbst_11.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[5] & 0x08)) pbst_12.BackColor = Color.Green; else pbst_12.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[5] & 0x10)) pbst_13.BackColor = Color.Green; else pbst_13.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[5] & 0x20)) pbst_14.BackColor = Color.Green; else pbst_14.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[5] & 0x40)) pbst_15.BackColor = Color.Green; else pbst_15.BackColor = Color.Red;
                        if (Convert.ToBoolean(statemsg[5] & 0x80)) pbst_16.BackColor = Color.Green; else pbst_16.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[4] & 0x01))
                        {
                            pbdist_1.BackColor = Color.Green;
                            i = 1;
                        }
                        else pbdist_1.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[4] & 0x02))
                        {
                            pbdist_2.BackColor = Color.Green;
                            i = 2;
                        }
                        else pbdist_2.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[4] & 0x04))
                        {
                            pbdist_3.BackColor = Color.Green;
                            i = 3;
                        }
                        else pbdist_3.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[4] & 0x08))
                        {
                            pbdist_4.BackColor = Color.Green;
                            i = 4;
                        }
                        else pbdist_4.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[4] & 0x10))
                        {
                            pbdist_5.BackColor = Color.Green;
                            i = 5;
                        }
                        else pbdist_5.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[4] & 0x20))
                        {
                            pbdist_6.BackColor = Color.Green;
                            i = 6;
                        }
                        else pbdist_6.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[4] & 0x40))
                        {
                            pbdist_7.BackColor = Color.Green;
                            i = 7;
                        }
                        else pbdist_7.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[4] & 0x80))
                        {
                            pbdist_8.BackColor = Color.Green;
                            i = 8;
                        }
                        else pbdist_8.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[3] & 0x01))
                        {
                            pbdist_9.BackColor = Color.Green;
                            i = 9;
                        }
                        else pbdist_9.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[3] & 0x02))
                        {
                            pbdist_10.BackColor = Color.Green;
                            i = 10;
                        }
                        else pbdist_10.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[3] & 0x04))
                        {
                            pbdist_11.BackColor = Color.Green;
                            i = 11;
                        }
                        else pbdist_11.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[3] & 0x08))
                        {
                            pbdist_12.BackColor = Color.Green;
                            i = 12;
                        }
                        else pbdist_12.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[3] & 0x10))
                        {
                            pbdist_13.BackColor = Color.Green;
                            i = 13;
                        }
                        else pbdist_13.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[3] & 0x20))
                        {
                            pbdist_14.BackColor = Color.Green;
                            i = 14;
                        }
                        else pbdist_14.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[3] & 0x40))
                        {
                            pbdist_15.BackColor = Color.Green;
                            i = 15;
                        }
                        else pbdist_15.BackColor = Color.Red;

                        if (Convert.ToBoolean(statemsg[3] & 0x80))
                        {
                            pbdist_16.BackColor = Color.Green;
                            i = 16;
                        }
                        else pbdist_16.BackColor = Color.Red;

                        if ((i > 0) && (i != privdi))
                        {
                            privdi = i;
                            voice.Speak(i.ToString(), spFlags);
                        }

                    }

                    if (statemsg[2] == 0x21)
                    {
                        foreach (MyListViewItem item in lv_Items.Items)
                        {

                            if (statemsg[1] == item.uAddress)
                            {
                                item.uAddress = statemsg[1];
                                item.CHECKCODE = Convert.ToUInt16(statemsg[5] & 0x01);
                                item.uHoldPower = Convert.ToUInt16((statemsg[5] >> 1) & 0x01);
                                item.DIEnable = Convert.ToUInt16((statemsg[5] >> 2) & 0x01);
                                item.DOEnable = Convert.ToUInt16((statemsg[5] >> 3) & 0x01);
                                item.RFEnable = Convert.ToUInt16((statemsg[5] >> 4) & 0x01);
                                item.RF_SLOW = Convert.ToUInt16((statemsg[5] >> 5) & 0x01);
                                item.DI_SLOW = Convert.ToUInt16((statemsg[5] >> 6) & 0x01);
                                item.Task_EN = Convert.ToUInt16((statemsg[5] >> 7) & 0x01);

                                item.TH_EN = Convert.ToUInt16(statemsg[4] & 0x01);
                                item.Silent = Convert.ToUInt16((statemsg[4] >> 1) & 0x01);
                                item.Exec_EN = Convert.ToUInt16((statemsg[4] >> 2) & 0x01);
                                item.VarOverflow = Convert.ToUInt16((statemsg[4] >> 3) & 0x01);
                                item.SilentReg = Convert.ToUInt16((statemsg[4] >> 4) & 0x01);
                                item.Timer_Vector = Convert.ToUInt16((statemsg[4] >> 5) & 0x01);
                                item.Variable = Convert.ToUInt16((statemsg[4] >> 6) & 0x01);
                                item.Answer = Convert.ToUInt16((statemsg[4] >> 7) & 0x01);

                                if (BAUD_RATE == 115200)
                                {
                                    item.ImageIndex = 3 + ((statemsg[3] >> 7) & 0x01); ;
                                }
                                else
                                {
                                    item.ImageIndex = 0 + ((statemsg[3] >> 7) & 0x01); ;
                                }
                                haveItem = true;

                                lbl_Info.Visible = false;
                            }
                        }
                        if (!haveItem)
                        {
                            MyListViewItem item = new MyListViewItem();
                            item.Text = statemsg[1].ToString();
                            item.uAddress = statemsg[1];
                            item.CHECKCODE = Convert.ToUInt16(statemsg[5] & 0x01);
                            item.uHoldPower = Convert.ToUInt16((statemsg[5] >> 1) & 0x01);
                            item.DIEnable = Convert.ToUInt16((statemsg[5] >> 2) & 0x01);
                            item.DOEnable = Convert.ToUInt16((statemsg[5] >> 3) & 0x01);
                            item.RFEnable = Convert.ToUInt16((statemsg[5] >> 4) & 0x01);
                            item.RF_SLOW = Convert.ToUInt16((statemsg[5] >> 5) & 0x01);
                            item.DI_SLOW = Convert.ToUInt16((statemsg[5] >> 6) & 0x01);
                            item.Task_EN = Convert.ToUInt16((statemsg[5] >> 7) & 0x01);

                            item.TH_EN = Convert.ToUInt16(statemsg[4] & 0x01);
                            item.Silent = Convert.ToUInt16((statemsg[4] >> 1) & 0x01);
                            item.Exec_EN = Convert.ToUInt16((statemsg[4] >> 2) & 0x01);
                            item.VarOverflow = Convert.ToUInt16((statemsg[4] >> 3) & 0x01);
                            item.SilentReg = Convert.ToUInt16((statemsg[4] >> 4) & 0x01);
                            item.Timer_Vector = Convert.ToUInt16((statemsg[4] >> 5) & 0x01);
                            item.Variable = Convert.ToUInt16((statemsg[4] >> 6) & 0x01);
                            item.Answer = Convert.ToUInt16((statemsg[4] >> 7) & 0x01);

                            if (BAUD_RATE == 115200)
                            {
                                item.ImageIndex = 3 + ((statemsg[3] >> 7) & 0x01);
                            }
                            else
                            {
                                item.ImageIndex = 0 + ((statemsg[3] >> 7) & 0x01); ;
                            }
                            lv_Items.Items.Add(item);
                            lbl_Info.Visible = false;
                        }
                    }
                    if (statemsg[2] == 0x3D)
                    {
                        if (statemsg[3] == 0xF3)//PI值
                        {
                            int value = 0;
                            txt_ret_piindex.Text = statemsg[4].ToString();
                            value = statemsg[5];
                            value = value << 8;
                            value |= statemsg[6];
                            txt_pi_ret_value.Text = value.ToString();
                        }
                    }

                    if (statemsg[2] == 0x2D)
                    {
                        if (statemsg[4] == 0x10)//IO数量
                        {
                            try
                            {
                                txt_DI_Count.Text = statemsg[5].ToString();
                                txt_DO_Count.Text = statemsg[6].ToString();
                            }
                            catch { }
                        }
                        if (statemsg[4] == 0x02)//版本号
                        {
                            try
                            {
                                MyListViewItem item = (MyListViewItem)lv_Items.SelectedItems[0];
                                item.hwVersion = statemsg[6] * 1000 + statemsg[5];
                                lbl_Ver.Text = statemsg[6].ToString() + "." + statemsg[5].ToString();
                            }
                            catch { }
                        }
                        if (statemsg[4] == 0x0A)//温度
                        {
                            try
                            {
                                lbl_Temperature.Text = statemsg[6].ToString() + "." + statemsg[5].ToString();
                            }
                            catch { }
                        }
                        if (statemsg[4] == 0x01)
                        {
                            try
                            {
                                cb_HaveDO.Checked = Convert.ToBoolean((statemsg[6]) & 0x01);
                                cb_HaveDI.Checked = Convert.ToBoolean((statemsg[6] >> 1) & 0x01);
                                cb_HaveRTC.Checked = Convert.ToBoolean((statemsg[6] >> 2) & 0x01);
                                cb_HaveSPEEPROM.Checked = Convert.ToBoolean((statemsg[6] >> 3) & 0x01);
                            }
                            catch { }
                        }
                    }

                    if (statemsg[2] == 0x90)
                    {
                        txt_VarValue.Text = statemsg[6].ToString();

                    }
                    if (statemsg[2] == 0xA0)
                    {
                        txt_ad_value.Text = (statemsg[4] * 65535+statemsg[5]*256+ statemsg[6]).ToString();
                    }
                }
            }
        }
        private void at11bytemsg(byte[] statemsg)
        {
            bool haveItem = false;

            if (statemsg[1] == 0x02)
            {
                if (statemsg[2] == 0x02)
                {
                    foreach (MyListViewItem item in lv_Items.Items)
                    {

                        if (statemsg[0] == item.uAddress)
                        {
                            haveItem = true;
                            break;
                        }
                    }
                    if (!haveItem)
                    {
                        MyListViewItem item = new MyListViewItem();
                        item.Text = statemsg[0].ToString();
                        item.uAddress = statemsg[0];

                        item.ImageIndex = 0;
                        lv_Items.Items.Add(item);
                        lbl_Info.Visible = false;
                    }
                }
            }
        }

        private void atv4bytemsg(byte[] statemsg)
        {
            try
            {
                UInt32 crc32;
                UInt32 crc;
                crc = statemsg[34];
                crc <<= 8;
                crc |= statemsg[35];
                crc <<= 8;
                crc |= statemsg[36];
                crc <<= 8;
                crc |= statemsg[37];
                crc32 = CRC32.GetCRC32(statemsg, 34);

                if ((statemsg[0] == 0x21) && (crc32 == crc))
                {
                    bool haveItem = false;
                    MyListViewItem devitem = new MyListViewItem();

                    foreach (MyListViewItem item in lv_Items.Items)
                    {
                        if (statemsg[1] == item.uAddress)
                        {
                            haveItem = true;
                            devitem = item;
                        }
                    }

                    devitem.Text = statemsg[1].ToString();
                    devitem.uAddress = statemsg[1];

                    devitem.Major_Version_Number = statemsg[2];
                    devitem.Revision_Number = statemsg[3];
                    devitem.Product_Version_Number = statemsg[4] * 256 + statemsg[5];
                    devitem.PCB_Version = statemsg[6];
                    devitem.hwVersion = statemsg[2] * 1000 + statemsg[3];

                    devitem.CHECKCODE = Convert.ToUInt16(statemsg[9] & 0x01);
                    devitem.uHoldPower = Convert.ToUInt16((statemsg[9] >> 1) & 0x01);
                    devitem.DIEnable = Convert.ToUInt16((statemsg[9] >> 2) & 0x01);
                    devitem.DOEnable = Convert.ToUInt16((statemsg[9] >> 3) & 0x01);
                    devitem.RFEnable = Convert.ToUInt16((statemsg[9] >> 4) & 0x01);
                    devitem.RF_SLOW = Convert.ToUInt16((statemsg[9] >> 5) & 0x01);
                    devitem.DI_SLOW = Convert.ToUInt16((statemsg[9] >> 6) & 0x01);
                    devitem.Task_EN = Convert.ToUInt16((statemsg[9] >> 7) & 0x01);

                    devitem.TH_EN = Convert.ToUInt16(statemsg[8] & 0x01);
                    devitem.Silent = Convert.ToUInt16((statemsg[8] >> 1) & 0x01);
                    devitem.Exec_EN = Convert.ToUInt16((statemsg[8] >> 2) & 0x01);
                    devitem.VarOverflow = Convert.ToUInt16((statemsg[8] >> 3) & 0x01);
                    devitem.SilentReg = Convert.ToUInt16((statemsg[8] >> 4) & 0x01);
                    devitem.Timer_Vector = Convert.ToUInt16((statemsg[8] >> 5) & 0x01);
                    devitem.Variable = Convert.ToUInt16((statemsg[8] >> 6) & 0x01);
                    devitem.Answer = Convert.ToUInt16((statemsg[8] >> 7) & 0x01);

                    devitem.HaveDO = Convert.ToBoolean((statemsg[10]) & 0x01);
                    devitem.HaveDI = Convert.ToBoolean((statemsg[10] >> 1) & 0x01);
                    devitem.HaveRTC = Convert.ToBoolean((statemsg[10] >> 2) & 0x01);
                    devitem.HaveEXTMEM = Convert.ToBoolean((statemsg[10] >> 3) & 0x01);

                    devitem.ipaddr = statemsg[11].ToString() + "." + statemsg[12].ToString() + "." + statemsg[13].ToString() + "." + statemsg[14].ToString();
                    devitem.submask = statemsg[15].ToString() + "." + statemsg[16].ToString() + "." + statemsg[17].ToString() + "." + statemsg[18].ToString();
                    devitem.getway = statemsg[19].ToString() + "." + statemsg[20].ToString() + "." + statemsg[21].ToString() + "." + statemsg[22].ToString();
                    devitem.ntpserver = statemsg[23].ToString() + "." + statemsg[24].ToString() + "." + statemsg[25].ToString() + "." + statemsg[26].ToString();
                    devitem.ntpport = statemsg[27] * 256 + statemsg[28];
                    devitem.use_ntp = Convert.ToBoolean(statemsg[29]);
                    devitem.v4_port = statemsg[30] * 256 + statemsg[31];

                    devitem.DI_Count = statemsg[32];
                    devitem.DO_Count = statemsg[33];

                    devitem.ImageIndex = 3 + ((statemsg[7] >> 7) & 0x01);

                    if (!haveItem) lv_Items.Items.Add(devitem);
                    lbl_Info.Visible = false;
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
        private void btnEndListen_Click(object sender, EventArgs e)
        {
            EndListen();
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            Close();
        }
        /// <summary>
        /// 变更按钮状态
        /// </summary>
        /// <param name="Enable"></param>
        private void EnableBtn(bool Enable)
        {
            btnSave.Enabled = Enable;
            btnRefresh.Enabled = Enable;
            gb_Property.Enabled = Enable;
            gb_Function.Enabled = Enable;

            if (Enable)
            {
                txt_Connected.ForeColor = Color.Green;
                txt_Connected.Text = "Connected";
            }
            else
            {
                txt_Connected.ForeColor = Color.Red;
                txt_Connected.Text = "Not Connected";
            }
        }

        private void EndListen()
        {
            Abort();

            btnBeginListen.Enabled = true;
            btnEndListen.Enabled = false;

            EnableBtn(false);
            EnableBtn(false);
            if (rbCOM.Checked)
            {
                rbCOM.Enabled = true;
                cb_SerialPort.Enabled = true;
                rb_TCP.Enabled = true;
                rb_UDP.Enabled = true;
            }
            else
            {
                rbCOM.Enabled = true;
                rb_TCP.Enabled = true;
                rb_UDP.Enabled = true;
                txtIP.Enabled = true;
                txtPort.Enabled = true;
            }

        }
        //终止
        private void Abort()
        {
            try
            {
                if (socketClient.Connected)
                {
                    //终止线程
                    threadClient.Abort();
                    //关闭socket
                    socketClient.Close();

                    threadClient = null;
                    socketClient = null;
                }
            }
            catch { };

            try
            {
                if (serialPort.IsOpen)
                {
                    serialPort.Close();
                    serialPort = null;
                }
            }
            catch { };

            try
            {
                UDPServer_Stop();
            }
            catch { }
        }

        //DataReceived事件委托方法
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                {
                    byte[] dn = new byte[8];
                    byte[] ds = new byte[serialPort.BytesToRead];
                    int i = 0;
                    //int count = serialPort.BytesToRead / 8;

                    if (serialPort.BytesToRead == 11)
                    {
                        serialPort.Read(ds, 0, serialPort.BytesToRead);
                        this.Invoke(interfaceUpdataHandle, ds);
                    }
                    else
                    {
                        //for (i = 0; i < count; i++)
                        {
                            serialPort.Read(dn, i * 8, 8);
                            this.Invoke(interfaceUpdataHandle, dn);
                        }
                    }
                }
            }
            catch
            {
                //EndListen();
            }
        }

        private void cb_SerialPort_DropDown(object sender, EventArgs e)
        {
            EnumCOMPort();
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            byte[] m_SendCMD = new byte[8];
            lv_Items.Clear();
            m_SendCMD = Relay.GetConfig();

            SendToDev(m_SendCMD);
        }
        //写一个发送信息到服务端的方法
        public void clientSendMsg(byte[] txtCMsg)
        {
            //获取文本框txtCMsg输入的内容
            //string strClientSendMsg = txtCMsg;
            //将输入的内容字符串转换为机器可以识别的byte数组
            //byte[] arrClientSendMsg = System.Text.Encoding.UTF8.GetBytes(strClientSendMsg);
            //调用客户端套接字发送byte数组
            try
            {
                socketClient.Send(txtCMsg);
            }
            catch
            {
                //MessageBox.Show("连接已断开");
                EndListen();
            }
        }

        private void SendToSerial(byte[] cmd)
        {
            try
            {
                ReceiveEventFlag = true;        //关闭接收事件
                serialPort.DiscardInBuffer();         //清空接收缓冲区    

                if (serialPort == null)
                {
                    //serialPort = new SerialPort(PORT_NAME, BAUD_RATE, Parity.None, 8);
                    //serialPort.ReceivedBytesThreshold = 4;
                    //serialPort.DataReceived += new SerialDataReceivedEventHandler(SerialPort_DataReceived);//DataReceived事件委托
                    ////读写时间超时            
                    //serialPort.ReadTimeout = ReadTimeout;
                    //serialPort.WriteTimeout = WriteTimeout;
                    //serialPort.Open();
                }

                if (serialPort.IsOpen)
                {
                    serialPort.Write(cmd, 0, cmd.Length);
                }
            }
            catch (Exception e)
            {
                EndListen();
            }
            ReceiveEventFlag = false;       //打开事件
        }

        private void lv_Items_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lv_Items.SelectedItems.Count > 0)
            {
                MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                txtAddress.Text = mlvi.uAddress.ToString();
                ckb_CheckCode.Checked = Convert.ToBoolean(mlvi.CHECKCODE);
                cb_HoldPower.Checked = Convert.ToBoolean(mlvi.uHoldPower);
                ckb_DI.Checked = Convert.ToBoolean(mlvi.DIEnable);
                ckb_DO.Checked = Convert.ToBoolean(mlvi.DOEnable);
                ckb_RF.Checked = Convert.ToBoolean(mlvi.RFEnable);
                ckb_RF_SLOW.Checked = Convert.ToBoolean(mlvi.RF_SLOW);
                ckb_Task.Checked = Convert.ToBoolean(mlvi.Task_EN);
                ckb_TH_EN.Checked = Convert.ToBoolean(mlvi.TH_EN);

                cb_VarOverflow.Checked = Convert.ToBoolean(mlvi.VarOverflow);
                ckb_Exec.Checked = Convert.ToBoolean(mlvi.Exec_EN);
                ckb_SilentReg.Checked = Convert.ToBoolean(mlvi.SilentReg);
                ckb_TV_EN.Checked = Convert.ToBoolean(mlvi.Timer_Vector);
                ckb_Var_EN.Checked = Convert.ToBoolean(mlvi.Variable);
                if ((mlvi.Silent == 0) && (mlvi.Answer == 0))
                    rb_noanswer.Checked = true;
                else
                {
                    rb_SilentStatic.Checked = Convert.ToBoolean(mlvi.Silent);
                    rb_Answer.Checked = Convert.ToBoolean(mlvi.Answer);
                }
                lbl_Temperature.Text = "";
                if (mlvi.hwVersion >= 4020)
                {
                    tp_Ethernet.Parent = tc_Property;

                    cb_HaveDI.Checked = mlvi.HaveDI;
                    cb_HaveDO.Checked = mlvi.HaveDO;
                    cb_HaveRTC.Checked = mlvi.HaveRTC;
                    cb_HaveSPEEPROM.Checked = mlvi.HaveEXTMEM;
                    txt_DI_Count.Text = mlvi.DI_Count.ToString();
                    txt_DO_Count.Text = mlvi.DO_Count.ToString();

                    txt_ipaddr.Text = mlvi.ipaddr;
                    txt_submask.Text = mlvi.submask;
                    txt_getway.Text = mlvi.getway;
                    txt_dataport.Text = mlvi.v4_port.ToString();
                    cb_use_ntp.Checked = mlvi.use_ntp;
                    txt_ntpserver.Text = mlvi.ntpserver;
                    txt_ntpport.Text = mlvi.ntpport.ToString();
                    lbl_baud.Visible = false;
                    cb_Band.Visible = false;
                    btn_SetBand.Visible = false;

                    lbl_txttemp.Visible = false;
                    lbl_Temperature.Visible = false;
                    btn_getTemp.Visible = false;

                    lbl_Ver.Text = mlvi.Major_Version_Number.ToString() + "." + mlvi.Revision_Number.ToString();
                }
                else
                {
                    try
                    {
                        cb_HaveDI.Checked = false;
                        cb_HaveDO.Checked = false;
                        cb_HaveRTC.Checked = false;
                        cb_HaveSPEEPROM.Checked = false;

                        txt_DI_Count.Text = "";
                        txt_DO_Count.Text = "";
                        lbl_Ver.Text = "";

                        lbl_baud.Visible = true;
                        cb_Band.Visible = true;
                        btn_SetBand.Visible = true;
                        lbl_txttemp.Visible = true;
                        lbl_Temperature.Visible = true;
                        tp_Ethernet.Parent = null;
                        btn_getTemp.Visible = true;

                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(mlvi.uAddress, 0x2D, 0x00, 0, 0, 0x01);
                        CMDList.Add(tempCMD);
                        tempCMD = Relay.CreateCMDByDebug(mlvi.uAddress, 0x2D, 0x00, 0, 0, 0x10);
                        CMDList.Add(tempCMD);
                        tempCMD = Relay.CreateCMDByDebug(mlvi.uAddress, 0x2D, 0x00, 0, 0, 0xA0);
                        CMDList.Add(tempCMD);
                        tempCMD = Relay.CreateCMDByDebug(mlvi.uAddress, 0x2D, 0x00, 0, 0, 0x02);
                        CMDList.Add(tempCMD);
                        tempCMD = Relay.CreateCMDByDebug(mlvi.uAddress, 0x2D, 0x00, 0, 0, 0x0A);
                        CMDList.Add(tempCMD);
                        tm_Debug.Enabled = true;
                    }
                    catch { }
                }

            }
            else
            {
                tp_Ethernet.Parent = null;
            }
        }
        private void SendToDev(byte[] m_SendCMD)
        {
            try
            {
                string hex = BitConverter.ToString(m_SendCMD);
                lbl_SEND.Text = hex;
                fdebug.AddInfo(hex);

                tm_Status.Enabled = false;
                tm_LoopDebug.Enabled = false;
                if (rbCOM.Checked)
                {
                    SendToSerial(m_SendCMD);
                }
                if (rb_TCP.Checked)
                {
                    clientSendMsg(m_SendCMD);
                }

                if (rb_UDP.Checked)
                {
                    UDP_Client_Send(m_SendCMD);
                }
                tm_Status.Enabled = cb_Status.Checked;
                tm_LoopDebug.Enabled = cb_loop_Debug.Checked;
            }
            catch { }
        }


        private void btnSave_Click(object sender, EventArgs e)
        {
            int address = Convert.ToInt32(txtAddress.Text);
            Byte addH = 0, addL = 0;
            addH = (byte)(address >> 8);
            addL = (byte)address;
            ushort DevAdd = 0;

            try
            {
                if ((addL < 1) || (addL > 254))
                {

                    MessageBox.Show("Address range must be greater than 0 and less than 255");
                    return;
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message);
                return;
            }


            if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
            else
            if (lv_Items.SelectedItems.Count > 0)
            {
                MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                DevAdd = Convert.ToUInt16(mlvi.Text);
            }
            if (DevAdd > 0)
            {
                byte[] m_SendCMD = new byte[8];
                UInt32 config = 0;

                //设置控制器功能
                if (ckb_CheckCode.Checked)
                {
                    config += 1;
                }
                if (cb_HoldPower.Checked)
                {
                    config += 2;
                }
                if (ckb_DI.Checked)
                {
                    config += 4;
                }
                if (ckb_DO.Checked)
                {
                    config += 8;
                }
                if (ckb_RF.Checked)
                {
                    config += 16;
                }
                if (ckb_RF_SLOW.Checked)
                {
                    config += 32;
                }
                if (ckb_Task.Checked)
                {
                    config += 128;
                }
                if (ckb_TH_EN.Checked)
                {
                    config += 256;
                }
                if (rb_SilentStatic.Checked)
                {
                    config += 512;
                }
                if (ckb_Exec.Checked)
                {
                    config += 1024;
                }
                if (cb_VarOverflow.Checked)
                {
                    config += 2048;
                }
                if (ckb_SilentReg.Checked)
                {
                    config += 4096;
                }

                if (ckb_TV_EN.Checked)
                {
                    config += 8192;
                }

                if (ckb_Var_EN.Checked)
                {
                    config += 16384;
                }
                if (rb_Answer.Checked)
                {
                    config += 32768;
                }
                m_SendCMD = Relay.SetConfig(DevAdd, config, addH, addL);

                SendToDev(m_SendCMD);

            }
            else
            {
                MessageBox.Show("Please select a device");
            }
        }

        private void rbCOM_CheckedChanged(object sender, EventArgs e)
        {
            if (this.rbCOM.Checked)
            {
                try
                {
                    if (socketClient.Connected)
                    {
                        EndListen();
                    }
                }
                catch { };

                txtIP.Enabled = false;
                txtPort.Enabled = false;
                cb_SerialPort.Enabled = true;
                cb_ConBand.Enabled = true;
                cb_SerialPort.Items.Clear();
                cb_SerialPort.Items.AddRange(System.IO.Ports.SerialPort.GetPortNames());
            }
        }

        private void rbNetWork_CheckedChanged(object sender, EventArgs e)
        {
            if (this.rb_TCP.Checked || this.rb_UDP.Checked)
            {
                try
                {
                    if (serialPort.IsOpen)
                    {
                        EndListen();
                    }
                }
                catch { };

                txtIP.Enabled = true;
                txtPort.Enabled = true;
                cb_SerialPort.Enabled = false;
                cb_ConBand.Enabled = false;
            }
        }

        #region "网络部分"
        /// <summary>
        /// 网络连接部分
        /// </summary>
        private Socket socketClient = null;
        Thread threadClient = null;
        public bool TCP_Connect(string IP, string Port)
        {
            try
            {
                //定义一个套字节监听  包含3个参数(IP4寻址协议,流式连接,TCP协议)
                socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                //客户端同样 需要获取文本框中的IP地址
                string strDomain = IP;

                string ipAddress = string.Empty;
                System.Text.RegularExpressions.Regex check = new System.Text.RegularExpressions.Regex(@"^(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9])\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[0-9])$");

                if (!check.IsMatch(strDomain))
                {
                    IPHostEntry hostEntry = Dns.Resolve(strDomain);
                    IPEndPoint ipEndPoint = new IPEndPoint(hostEntry.AddressList[0], 0);
                    //这就是你想要的
                    ipAddress = ipEndPoint.Address.ToString();
                }
                else
                {
                    ipAddress = strDomain;
                }

                IPAddress ipaddress = IPAddress.Parse(ipAddress);
                //将获取的ip地址和端口号绑定到网络节点endpoint上
                IPEndPoint endpoint = new IPEndPoint(ipaddress, int.Parse(Port));
                //注意: 这里是客服端套接字连接到Connect网络节点 不是Bind
                iNetThreadRUN = true;
                socketClient.Connect(endpoint);
                //new一个新线程 调用下面的接受服务端发来信息的方法RecMsg
                //if (threadClient == null)
                //{
                threadClient = new Thread(RecMsg);
                //将窗体线程设置为与后台同步
                threadClient.IsBackground = true;
                //启动线程
                threadClient.Start();
                interfaceUpdataHandle = new HandleInterfaceUpdataDelegate(SetDevState);
                //}
                //else
                //{
                //    threadClient.Resume();
                //}

                return true;
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        ////网络连接 消息接收
        //定义一个接受服务端发来信息的方法
        void RecMsg()
        {
            while (true) //持续监听服务端发来的消息
            {
                if (iNetThreadRUN)
                try
                {
                    //客户端 定义一个1M的byte数组空间
                    byte[] arrRecMsg = new byte[socketClient.Available];
                    
                    //定义byte数组的长度
                    int length = socketClient.Receive(arrRecMsg);
                    if (length >= 8)
                    {
                        //SetDevState(arrRecMsg);
                        this.Invoke(interfaceUpdataHandle, arrRecMsg);
                    }
                    //arrRecMsg 接收到的物理数据

                }
                catch (Exception e)
                {
                    //MessageBox.Show(e.Message);
                    //EndListen();
                }

            }
        }

        Thread UDPThread = null;
        int UDP_Server_Port = 0;
        string UDP_Client_IP = string.Empty;
        UdpClient UDPSVR = null;
        IPEndPoint udp_svr_ip = null;
        IPHostEntry iphost = null;
        private void UDPServer_Start(string IP, string Port)
        {
            UDP_Server_Port = Convert.ToInt32(Port);

            string strDomain = IP;

            string ipAddress = string.Empty;
            System.Text.RegularExpressions.Regex check = new System.Text.RegularExpressions.Regex(@"^(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9])\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[1-9]|0)\.(25[0-5]|2[0-4][0-9]|[0-1]{1}[0-9]{2}|[1-9]{1}[0-9]{1}|[0-9])$");

            if (!check.IsMatch(strDomain))
            {
                IPHostEntry hostEntry = Dns.Resolve(strDomain);
                IPEndPoint ipEndPoint = new IPEndPoint(hostEntry.AddressList[0], 0);
                //这就是你想要的
                UDP_Client_IP = ipEndPoint.Address.ToString();
            }
            else
            {
                UDP_Client_IP = strDomain;
            }
            iphost = Dns.GetHostEntry(Dns.GetHostName());
            udp_svr_ip = new IPEndPoint(IPAddress.Any, UDP_Server_Port);

            UDPSVR = new UdpClient(UDP_Server_Port);
            UDPThread = new Thread(new ThreadStart(ThreadCallBack));
            interfaceUpdataHandle = new HandleInterfaceUpdataDelegate(SetDevState);
            iNetThreadRUN = true;
            UDPThread.Start();
        }

        private void ThreadCallBack()
        {
            
            while (true)
            {
                if (iNetThreadRUN)
                    try
                    {
                        if (UDPSVR.Available > 0)
                        {
                            bool havelocal = false;
                            byte[] bData = UDPSVR.Receive(ref udp_svr_ip);
                            foreach (IPAddress ipa in iphost.AddressList)
                            {
                                try
                                {
                                    if (ipa.Address == udp_svr_ip.Address.Address)
                                    {
                                        havelocal = true;
                                        break;
                                    }
                                }
                                catch { }
                            }
                            if (!havelocal) this.Invoke(interfaceUpdataHandle, bData);
                        }
                    }
                    catch { }
            }
        }

        private void UDPServer_Stop()
        {
            if (UDPSVR != null)
            {
                UDPSVR.Close();
                UDPSVR = null;
            }

            if (UDPThread != null)
            {
                UDPThread.Abort();
                Thread.Sleep(30);
                UDPThread = null;
            }
        }

        private void UDP_Client_Send(byte[] m_SendCMD)
        {
            IPAddress ipaddress = IPAddress.Parse(UDP_Client_IP);
            IPEndPoint endpoint = new IPEndPoint(ipaddress, UDP_Server_Port);

            //IPEndPoint endpoint = new IPEndPoint(IPAddress.Broadcast, UDP_Server_Port);
            UdpClient client = new UdpClient();
            client.Send(m_SendCMD, m_SendCMD.Length, endpoint);
            client.Close();
            client = null;
        }
        #endregion

        private void lv_Items_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.Alt)
            {
                fdebug.Show();
            }
            else
            {

            }
        }
        /// <summary>
        /// 延时函数
        /// </summary>
        /// <param name="delayTime">需要延时多少毫秒</param>
        /// <returns></returns>
        public static bool Delay(int delayTime)
        {
            DateTime now = DateTime.Now;
            int s;
            do
            {
                TimeSpan spand = DateTime.Now - now;
                s = spand.Milliseconds;
                Application.DoEvents();
            }
            while (s < delayTime);
            return true;
        }


        private void btn_SetBand_Click(object sender, EventArgs e)
        {
            byte[] m_SendCMD = new byte[8];
            ushort DevAdd = 0;
            if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
            else
                if (lv_Items.SelectedItems.Count > 0)
            {
                MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                DevAdd = Convert.ToUInt16(mlvi.Text);
            }
            if (DevAdd > 0)
            {
                ushort m_band = 255;
                if (cb_Band.Text.Equals("1200"))
                {
                    m_band = 0;
                }
                if (cb_Band.Text.Equals("2400"))
                {
                    m_band = 1;
                }

                if (cb_Band.Text.Equals("4800"))
                {
                    m_band = 2;
                }
                if (cb_Band.Text.Equals("9600"))
                {
                    m_band = 3;
                }
                if (cb_Band.Text.Equals("19200"))
                {
                    m_band = 4;
                }
                m_SendCMD = Relay.SetBand(DevAdd, m_band);

                SendToDev(m_SendCMD);
            }
            else
            {
                MessageBox.Show("Please select a device");
            }
        }

        private void btn_FullyON_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x13, 0x00, 0x00, 0xFF, 0xFF);
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_1216_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            CMDList.Clear();
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    int i = 0;
                    for (i = 1; i <= 16; i++)
                    {
                        try
                        {
                            byte[] tempCMD = new byte[8];
                            tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x12, 0x00, 0x00, 0x00, (byte)i);
                            CMDList.Add(tempCMD);
                            tm_Debug.Enabled = true;
                        }
                        catch { }
                    }
                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }
        private void tm_Debug_Tick(object sender, EventArgs e)
        {
            try
            {
                if (CMDList.Count > 0)
                {
                    SendToDev(CMDList[0]);
                    CMDList.Remove(CMDList[0]);
                }
                else
                {
                    tm_Debug.Enabled = false;

                }
            }
            catch { }
        }
        private void btn_FullyOFF_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x13, 0x00, 0x00, 0x00, 0x00);
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }
        private void btn_RunMyCMD_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x01, 0x00, (byte)(Convert.ToUInt16(txt_CMDNO.Text) >> 16), (byte)(Convert.ToUInt16(txt_CMDNO.Text) >> 8), (byte)Convert.ToUInt16(txt_CMDNO.Text));
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_ReStart_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x26, 0xAA, 0x00, 0x55, 0x00);
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }


        private void btn_Timing_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x02, 0x00, (byte)(Convert.ToUInt16(txt_Timing.Text) >> 16), (byte)(Convert.ToUInt16(txt_Timing.Text) >> 8), (byte)Convert.ToUInt16(txt_Timing.Text));
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_1216_OFF_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            CMDList.Clear();
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    int i = 0;
                    for (i = 1; i <= 16; i++)
                    {
                        try
                        {
                            byte[] tempCMD = new byte[8];
                            tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x11, 0x00, 0x00, 0x00, (byte)i);
                            CMDList.Add(tempCMD);
                            tm_Debug.Enabled = true;
                        }
                        catch { }
                    }
                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }
        private void btn_RFTX_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {

                    //try
                    //{
                    byte[] tempCMD = new byte[8];
                    UInt32 dat = Convert.ToUInt32(txt_RF_Value.Text);

                    tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x25, 0, (byte)(dat >> 16), (byte)(dat >> 8), (byte)dat);
                    SendToDev(tempCMD);
                    //}
                    //catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void ckb_Exec_CheckedChanged(object sender, EventArgs e)
        {
            groupBox8.Enabled = ckb_Exec.Checked;
        }

        private byte[] strToToHexByte(string hexString)
        {
            hexString = hexString.Replace("-", "");
            if ((hexString.Length % 2) != 0)
                hexString += " ";
            byte[] returnBytes = new byte[hexString.Length / 2];
            for (int i = 0; i < returnBytes.Length; i++)
                returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            return returnBytes;
        }

        private void BTN_ONLine(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x12, 0x00, 0x00, 0x00, (byte)Convert.ToUInt16(((Button)sender).Text));
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void BTN_OFFLine(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {

                    try
                    {
                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x11, 0x00, 0x00, 0x00, (byte)Convert.ToUInt16(((Button)sender).Text));
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void tm_Status_Tick(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x10, 0x00, 0x00, 0x00, 0x00);
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    cb_Status.Checked = false;
                }
            }
            catch { }
        }

        private void cb_Status_CheckedChanged(object sender, EventArgs e)
        {
            tm_Status.Enabled = cb_Status.Checked;
            cb_loop_Debug.Checked = false;
            if (!cb_Status.Checked)
            {

                pbst_1.BackColor = Color.Gray;
                pbst_2.BackColor = Color.Gray;
                pbst_3.BackColor = Color.Gray;
                pbst_4.BackColor = Color.Gray;
                pbst_5.BackColor = Color.Gray;
                pbst_6.BackColor = Color.Gray;
                pbst_7.BackColor = Color.Gray;
                pbst_8.BackColor = Color.Gray;

                pbst_9.BackColor = Color.Gray;
                pbst_10.BackColor = Color.Gray;
                pbst_11.BackColor = Color.Gray;
                pbst_12.BackColor = Color.Gray;
                pbst_13.BackColor = Color.Gray;
                pbst_14.BackColor = Color.Gray;
                pbst_15.BackColor = Color.Gray;
                pbst_16.BackColor = Color.Gray;


                pbdist_1.BackColor = Color.Gray;
                pbdist_2.BackColor = Color.Gray;
                pbdist_3.BackColor = Color.Gray;
                pbdist_4.BackColor = Color.Gray;
                pbdist_5.BackColor = Color.Gray;
                pbdist_6.BackColor = Color.Gray;
                pbdist_7.BackColor = Color.Gray;
                pbdist_8.BackColor = Color.Gray;

                pbdist_9.BackColor = Color.Gray;
                pbdist_10.BackColor = Color.Gray;
                pbdist_11.BackColor = Color.Gray;
                pbdist_12.BackColor = Color.Gray;
                pbdist_13.BackColor = Color.Gray;
                pbdist_14.BackColor = Color.Gray;
                pbdist_15.BackColor = Color.Gray;
                pbdist_16.BackColor = Color.Gray;

            }
        }

        private void btn_FA_SelAll_Click(object sender, EventArgs e)
        {
            ckb_DI.Checked = true;
            ckb_DO.Checked = true;
            ckb_RF.Checked = true;
            ckb_RF_SLOW.Checked = true;
            ckb_Task.Checked = true;
            ckb_TH_EN.Checked = true;
        }

        private void btn_FA_UnSel_Click(object sender, EventArgs e)
        {
            ckb_DI.Checked = !ckb_DI.Checked;
            ckb_DO.Checked = !ckb_DO.Checked;
            ckb_RF.Checked = !ckb_RF.Checked;
            ckb_RF_SLOW.Checked = !ckb_RF_SLOW.Checked;
            ckb_Task.Checked = !ckb_Task.Checked;
            ckb_TH_EN.Checked = !ckb_TH_EN.Checked;
        }

        private void btn_Inching_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    //try
                    //{
                    byte cmd = 0x08;
                    if (cb_Inching_ONOFF.SelectedIndex == 1) cmd = 0x06;
                    byte[] tempCMD = new byte[8];
                    ushort hex = Convert.ToUInt16(txt_Inching_Value.Text);
                    int uline = txt_Inching_Line.SelectedIndex + 1;
                    ushort time = Convert.ToUInt16(txt_Inching_Time.Text);
                    tempCMD = Relay.CreateCMDByDebug(DevAdd, cmd, (byte)uline, (byte)time, (byte)(hex >> 8), (byte)hex);
                    SendToDev(tempCMD);
                    //}
                    //catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_Flash_Run_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {

                    //try
                    //{
                    byte cmd = 0x07;
                    byte[] tempCMD = new byte[8];
                    ushort hex = Convert.ToUInt16(txt_Flash_Value.Text);
                    int uline = cb_Flash_Line.SelectedIndex + 1;
                    ushort time = Convert.ToUInt16(txt_Flash_Time.Text);
                    tempCMD = Relay.CreateCMDByDebug(DevAdd, cmd, (byte)uline, (byte)time, (byte)(hex >> 8), (byte)hex);
                    SendToDev(tempCMD);
                    //}
                    //catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_SFlash_Run_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    //try
                    //{
                    byte cmd = 0x17;
                    byte[] tempCMD = new byte[8];
                    int uline = cb_SFlash_Line.SelectedIndex + 1;
                    ushort time = Convert.ToUInt16(txt_SFlash_Time.Text);
                    tempCMD = Relay.CreateCMDByDebug(DevAdd, cmd, 0, 0, (byte)time, (byte)uline);
                    SendToDev(tempCMD);
                    //}
                    //catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_MFlash_Stop_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            CMDList.Clear();
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    int i = 0;
                    for (i = 1; i < 16; i++)
                    {
                        try
                        {
                            byte cmd = 0x07;
                            byte[] tempCMD = new byte[8];
                            tempCMD = Relay.CreateCMDByDebug(DevAdd, cmd, (byte)i, 0, 0, 0);
                            CMDList.Add(tempCMD);
                        }
                        catch { }
                    }
                    tm_Debug.Enabled = true;
                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_SFlash_Stop_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            CMDList.Clear();
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    int i = 0;
                    for (i = 1; i < 16; i++)
                    {
                        try
                        {
                            byte cmd = 0x17;
                            byte[] tempCMD = new byte[8];
                            tempCMD = Relay.CreateCMDByDebug(DevAdd, cmd, 0, 0, 0, (byte)i);
                            CMDList.Add(tempCMD);
                        }
                        catch { }
                    }
                    tm_Debug.Enabled = true;
                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_SInching_Run_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    //try
                    //{
                    byte cmd = 0x18;
                    byte[] tempCMD = new byte[8];
                    int uline = cb_SInching_Line.SelectedIndex + 1;
                    ushort time = Convert.ToUInt16(txt_SInching_Time.Text);
                    tempCMD = Relay.CreateCMDByDebug(DevAdd, cmd, 0, 0, (byte)time, (byte)uline);
                    SendToDev(tempCMD);
                    //}
                    //catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_RunVar_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x90, 0x00, 0, 0, (byte)Convert.ToUInt16(txt_VarAddr.Text));
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_RunVarSet_Click(object sender, EventArgs e)
        {
            VarValue(0x91);
        }

        private void VarValue(byte mode)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, mode, 0x00, 0, (byte)Convert.ToUInt16(txt_VarAddr.Text), (byte)Convert.ToUInt16(txt_VarValue.Text));
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }

        }

        private void btn_RunVarAdd_Click(object sender, EventArgs e)
        {
            VarValue(0x92);
        }

        private void btn_RunVarSub_Click(object sender, EventArgs e)
        {
            VarValue(0x93);
        }

        private void btn_IOQuery_Click(object sender, EventArgs e)
        {
            IOQuery();
        }

        private void IOQuery()
        {
            txt_DI_Count.Text = "";
            txt_DO_Count.Text = "";
            Get_Property(0x10);
        }
        private void Get_Property(byte item)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x2D, 0x00, 0, 0, item);
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_PQuery_Click(object sender, EventArgs e)
        {
            PQuery();
        }
        private void PQuery()
        {
            cb_HaveDI.Checked = false;
            cb_HaveDO.Checked = false;
            cb_HaveRTC.Checked = false;
            cb_HaveSPEEPROM.Checked = false;
            Get_Property(0x01);
        }


        private void btn_getVer_Click(object sender, EventArgs e)
        {
            GetVer();
        }

        private void GetVer()
        {
            lbl_Ver.Text = "";
            Get_Property(0x02);
        }

        private void btn_getTemp_Click(object sender, EventArgs e)
        {
            GetTemp();
        }

        private void GetTemp()
        {
            lbl_Temperature.Text = "";
            Get_Property(0x0A);
        }

        private void frmConfig_FormClosing(object sender, FormClosingEventArgs e)
        {
            EndListen();
        }

        private void cb_loop_Debug_CheckedChanged(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            tm_LoopDebug.Enabled = cb_loop_Debug.Checked;
            if (cb_loop_Debug.Checked)
            {
                CMDList.Clear();

                cb_Status.Checked = false;

                try
                {
                    if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                    else
                        if (lv_Items.SelectedItems.Count > 0)
                    {
                        MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                        DevAdd = Convert.ToUInt16(mlvi.Text);
                    }
                    if (DevAdd > 0)
                    {
                        int i = 0;
                        for (i = 1; i <= 16; i++)
                        {
                            string name = "cb_loop" + i.ToString();
                            Control control = Controls.Find(name, true)[0];
                            if (((CheckBox)control).Checked)
                            {
                                try
                                {
                                    byte[] tempCMD = new byte[8];
                                    tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x12, 0x00, 0x00, 0x00, (byte)i);
                                    CMDList.Add(tempCMD);
                                }
                                catch { }
                            }
                        }

                        for (i = 1; i <= 16; i++)
                        {
                            string name = "cb_loop" + i.ToString();
                            Control control = Controls.Find(name, true)[0];
                            if (((CheckBox)control).Checked)
                            {
                                try
                                {
                                    byte[] tempCMD = new byte[8];
                                    tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x11, 0x00, 0x00, 0x00, (byte)i);
                                    CMDList.Add(tempCMD);
                                }
                                catch { }
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Please select a device");
                    }
                }
                catch (Exception err)
                {

                }
            }
        }

        private int loopIndex = 0;
        private void tm_LoopDebug_Tick(object sender, EventArgs e)
        {
            try
            {
                if (loopIndex < CMDList.Count)
                {
                    SendToDev(CMDList[loopIndex]);
                    loopIndex++;
                }
                else
                {
                    loopIndex = 0;

                }
            }
            catch { }
        }

        private void btn_SelectedON_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            int Lines = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    int i = 0;
                    for (i = 0; i < 16; i++)
                    {
                        string name = "cb_loop" + i.ToString();
                        Control control = Controls.Find(name, true)[0];
                        if (((CheckBox)control).Checked)
                        {
                            Lines |= 1 << i;
                        }
                    }
                    byte[] tempCMD = new byte[8];
                    tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x15, 0x00, 0x00, (byte)(Lines >> 8), (byte)Lines);
                    SendToDev(tempCMD);
                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch { }

        }

        private void btn_SelectedOFF_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            int Lines = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    int i = 0;
                    for (i = 0; i < 16; i++)
                    {
                        string name = "cb_loop" + i.ToString();
                        Control control = Controls.Find(name, true)[0];
                        if (((CheckBox)control).Checked)
                        {
                            Lines |= 1 << i;
                        }
                    }
                    byte[] tempCMD = new byte[8];
                    tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x14, 0x00, 0x00, (byte)(Lines >> 8), (byte)Lines);
                    SendToDev(tempCMD);
                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch { }

        }

        private void btn_pi_zero_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        UInt16 aa = Convert.ToUInt16(txt_pi_index.Text);
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x3D, 0x02, (byte)aa, 0, 0);
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_pi_value_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        UInt16 aa = Convert.ToUInt16(txt_pi_vindex.Text);
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x3D, 0x03, 0, 0, (byte)aa);
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_sel_flip_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            int Lines = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    int i = 0;
                    for (i = 0; i < 16; i++)
                    {
                        string name = "cb_loop" + i.ToString();
                        Control control = Controls.Find(name, true)[0];
                        if (((CheckBox)control).Checked)
                        {
                            Lines |= 1 << i;
                        }
                    }
                    byte[] tempCMD = new byte[8];
                    tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x16, 0x00, 0x00, (byte)(Lines >> 8), (byte)Lines);
                    SendToDev(tempCMD);
                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch { }

        }

        private void btn_ethernet_write_Click(object sender, EventArgs e)
        {
            byte[] data = new byte[29];

            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    //
                    //data[6] = (byte)DevAdd;
                    //data[7] = 0x55;
                    //ip地址
                    try
                    {
                        string[] strTemp = txt_ipaddr.Text.Split(new char[] { '.' }); // textBox1.Text.Split(new char[] { '.' });
                        if (strTemp.Length != 4) throw new Exception();
                        for (int i = 0; i < strTemp.Length; i++)
                        {
                            Int16 tmp = Convert.ToInt16(strTemp[i]);
                            if (tmp > 255) throw new Exception();
                            data[8 + i] = (byte)tmp;
                        }
                    }
                    catch
                    {
                        MessageBox.Show("IP Address Error!");
                        txt_ipaddr.Focus();
                        return;
                    }
                    //端口
                    try
                    {
                        int port = Convert.ToInt32(txt_dataport.Text);
                        if (port >= 65535) throw new Exception();
                        data[12] = (byte)((port >> 8) & 0xFF);
                        data[13] = (byte)(port & 0xFF);
                    }
                    catch
                    {
                        MessageBox.Show("Data Port Error!");
                        txt_dataport.Focus();
                        return;
                    }
                    //子网掩码
                    try
                    {
                        string[] strTemp = txt_submask.Text.Split(new char[] { '.' }); // textBox1.Text.Split(new char[] { '.' });
                        if (strTemp.Length != 4) throw new Exception();
                        for (int i = 0; i < strTemp.Length; i++)
                        {
                            Int16 tmp = Convert.ToInt16(strTemp[i]);
                            if (tmp > 255) throw new Exception();
                            data[14 + i] = (byte)tmp;
                        }
                    }
                    catch
                    {
                        MessageBox.Show("Sub Mask Address Error!");
                        txt_submask.Focus();
                        return;
                    }
                    //网关
                    try
                    {
                        string[] strTemp = txt_getway.Text.Split(new char[] { '.' }); // textBox1.Text.Split(new char[] { '.' });
                        if (strTemp.Length != 4) throw new Exception();
                        for (int i = 0; i < strTemp.Length; i++)
                        {
                            Int16 tmp = Convert.ToInt16(strTemp[i]);
                            if (tmp > 255) throw new Exception();
                            data[18 + i] = (byte)tmp;
                        }
                    }
                    catch
                    {
                        MessageBox.Show("Getway Address Error!");
                        txt_getway.Focus();
                        return;
                    }
                    //使用NTP更新时间
                    if (cb_use_ntp.Checked) data[22] = 1;
                    //NTP服务器地址
                    try
                    {
                        string[] strTemp = txt_ntpserver.Text.Split(new char[] { '.' }); // textBox1.Text.Split(new char[] { '.' });
                        if (strTemp.Length != 4) throw new Exception();
                        for (int i = 0; i < strTemp.Length; i++)
                        {
                            Int16 tmp = Convert.ToInt16(strTemp[i]);
                            if (tmp > 255) throw new Exception();
                            data[23 + i] = (byte)tmp;
                        }
                    }
                    catch
                    {
                        MessageBox.Show("NTP Address Error!");
                        txt_ntpserver.Focus();
                        return;
                    }
                    //NTP端口
                    try
                    {
                        int ntpport = Convert.ToInt32(txt_ntpport.Text);
                        if (ntpport >= 65535) throw new Exception();
                        data[27] = (byte)((ntpport >> 8) & 0xFF);
                        data[28] = (byte)(ntpport & 0xFF);
                    }
                    catch
                    {
                        MessageBox.Show("NTP Port Error!");
                        txt_ntpport.Focus();
                        return;
                    }

                    iNetThreadRUN = false;//关闭线程
                    UInt32 Receipt = 0;
                    int TickCount = 0;
                    TickCount = Environment.TickCount;
                    UInt32 Verification = SendDatabyV4((byte)DevAdd, 0x55,data, 25);

                    while ((Receipt != Verification)&&((Environment.TickCount - TickCount) < 3000))
                    {
                        if((rb_UDP.Checked)&& (UDPSVR.Available > 0))
                        {
                            byte[] bData = UDPSVR.Receive(ref udp_svr_ip);
                            Receipt=CheckReceipt(bData);
                            //MessageBox.Show("Receipt:" + Receipt.ToString() + "   Verification:" + Verification.ToString());
                        }
                        else
                        if(rb_TCP.Checked)
                        {
                            byte[] arrRecMsg = new byte[socketClient.Available];
                            //定义byte数组的长度
                            int length = socketClient.Receive(arrRecMsg);
                            Receipt = CheckReceipt(arrRecMsg);
                            //MessageBox.Show("Receipt:" + Receipt.ToString() + "   Verification:" + Verification.ToString());
                        }
                    }

                    if (Receipt == Verification)
                        MessageBox.Show("Please wait for the device to restart!");
                    else
                        MessageBox.Show("Write timeout!");
                    iNetThreadRUN = true;
                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }


        }
        public UInt32 SendDatabyV4(byte addr,byte op,byte[] pData, int length)
        {

            UInt32 nReg;//CRC寄存器
            UInt32 nTemp = 0;
            UInt16 i, n;

            pData[4] = (byte)(length >> 8);
            pData[5] = (byte)(length);
            pData[6] = (byte)addr;
            pData[7] = op;

            nReg = 0xFFFFFFFF;//
            for (n = 4; n < length + 4; n++)
            {
                nReg ^= (UInt32)pData[n];
                for (i = 0; i < 4; i++)
                {
                    nTemp = CRC32.crcTable[(byte)((nReg >> 24) & 0xff)]; //取一个字节，查表
                    nReg <<= 8;                        //丢掉计算过的头一个BYTE
                    nReg ^= nTemp;                //与前一个BYTE的计算结果异或 
                }
            }
            //return nReg;
            pData[0] = (byte)((nReg >> 24) & 0xFF);
            pData[1] = (byte)((nReg >> 16) & 0xFF);
            pData[2] = (byte)((nReg >> 8) & 0xFF);
            pData[3] = (byte)(nReg & 0xFF);

            SendToDev(pData);
            return nReg;
        }

        private UInt32 CheckReceipt(byte[] statemsg)
        {
            int sum = 0;
            UInt32 Receipt = 0;

            string hex = BitConverter.ToString(statemsg);
            lbl_SEND.Text = hex;
            fdebug.AddInfo(hex);

            for (int i = 0; i <= 6; i++)
            {
                sum = sum + statemsg[i];
            }
            if ((statemsg[7] == (byte)(sum % 256)) && (statemsg[0] == 0x22) && (statemsg[2] == 0xE0))
            {
                Receipt = (UInt32)(statemsg[3] << 24);
                Receipt |= (UInt32)(statemsg[4] << 16);
                Receipt |= (UInt32)(statemsg[5] << 8);
                Receipt |= (UInt32)(statemsg[6]);
            }
            return Receipt;
        }

        private void btn_turn(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {

                    try
                    {
                        byte[] tempCMD = new byte[8];
                        UInt16 line = Convert.ToUInt16(((Button)sender).Text);
                        UInt16 data = (UInt16)(1 << (line - 1));
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0x16, 0x00, 0x00, (byte)(data>>8), (byte)data);
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }

        private void btn_RFRX(object sender, EventArgs e)
        {

        }

        private void btn_DA_SelAll_Click(object sender, EventArgs e)
        {
            int i = 0;
            for (i = 0; i < 16; i++)
            {
                string name = "cb_loop" + i.ToString();
                Control control = Controls.Find(name, true)[0];
                ((CheckBox)control).Checked = true;
            }
        }

        private void btn_DA_UnSel_Click(object sender, EventArgs e)
        {
            int i = 0;
            for (i = 0; i < 16; i++)
            {
                string name = "cb_loop" + i.ToString();
                Control control = Controls.Find(name, true)[0];
                ((CheckBox)control).Checked = !((CheckBox)control).Checked;
            }
        }

        private void btn_ad_Query_Click(object sender, EventArgs e)
        {
            ushort DevAdd = 0;
            try
            {
                if (cb_UseAddr.Checked) DevAdd = Convert.ToUInt16(txtAddress.Text);
                else
                    if (lv_Items.SelectedItems.Count > 0)
                {
                    MyListViewItem mlvi = (MyListViewItem)lv_Items.SelectedItems[0];
                    DevAdd = Convert.ToUInt16(mlvi.Text);
                }
                if (DevAdd > 0)
                {
                    try
                    {
                        byte[] tempCMD = new byte[8];
                        tempCMD = Relay.CreateCMDByDebug(DevAdd, 0xA0, 0x00, 0, 0, (byte)Convert.ToUInt16(txt_ad_index.Text));
                        SendToDev(tempCMD);
                    }
                    catch { }

                }
                else
                {
                    MessageBox.Show("Please select a device");
                }
            }
            catch (Exception err)
            {
                MessageBox.Show(err.Message);
            }
        }
    }
}
