using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace RelayMgr
{
    public partial class frmDebug : Form
    {
        private bool b_pause = false;

        public frmDebug()
        {
            InitializeComponent();
        }

        public void AddInfo(string info)
        {
            if (!b_pause)
            {
                txt_Debug.AppendText(info + "\r\n");
            }

        }

        private void frmDebug_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.Hide();
            e.Cancel=true;
        }

        private void btn_Close_Click(object sender, EventArgs e)
        {
            this.Hide();
        }

        private void btn_Clear_Click(object sender, EventArgs e)
        {
            txt_Debug.Clear();
        }

        private void btn_Pause_Click(object sender, EventArgs e)
        {
            b_pause= !b_pause;
            if (b_pause)
            {
                btn_Pause.Text = "继续";
            }
            else
            {
                btn_Pause.Text = "暂停";
            }
        }

    }
}
