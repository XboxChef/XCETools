using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace XCETools
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void goToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (goToolStripMenuItem.Text == "Stop")
            {
                goToolStripMenuItem.Text = "Go";
                return;
            }
            else if (goToolStripMenuItem.Text == "Go")
            {
                goToolStripMenuItem.Text = "Stop";
                return;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {

        }
    }
}
