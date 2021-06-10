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

            XCEDefaultLocation(Search1.Location);
            IsVisible(AllocateBox, false);
            IsVisible(MemoryBox, false);
            IsVisible(BreakpointBox, false);



        }
        /// <summary>
        /// Changes The State Of Visibility 
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        public void IsVisible(GroupBox x ,bool y)
        {
            ToolStripMenuItem c;
            x.Visible = y;
        }
        private void XCEDefaultLocation(Point x)
        {
            AllocateBox.Location = x;
            MemoryBox.Location = x;
            BreakpointBox.Location = x;
        }


        private void button2_Click(object sender, EventArgs e)
        {
            //Connect Button And Search Disconnect After Every Use (Good Practice)
            ResultBox.MaxLength = Convert.ToInt32(DisplayMax.Text); //create a way to store temp data and display it according to max
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //Defualt Size
        }

        private void ToolBarItemClicked(object sender, EventArgs e)
        {
            
            if(sender.Equals(Search_1))
            {
                Type(Search1);
                Search1.Text = "Search 1";
                InitButton.Size = new Size(156, 27);
                FromSearch1.Visible = false;
                return;
            }
            else if (sender.Equals(Search_2))
            {
                Type(Search1);
                Search1.Text = "Search 2";
                InitButton.Size = new Size(70, 27);
                FromSearch1.Visible = true;
                return;
            }
            else if (sender.Equals(Memory))
            {
                Type(MemoryBox);
                return;
            }
            else if (sender.Equals(Allocate))
            {
                Type(AllocateBox);
                return;
            }
            if (sender.Equals(Breakpoint))
            {
                Type(BreakpointBox);
                return;
            }
            if (sender.Equals(Go))
            {
                if (Go.Text == "Stop")
                {
                    Go.Text = "Go";
                    return;
                }
                else if (Go.Text == "Go")
                {
                    Go.Text = "Stop";
                    return;
                }
            }
        }

        private void Type(GroupBox c)
        {
            if (c.Equals(Search1))
            {
                IsVisible(c, true);
                IsVisible(AllocateBox, false);
                IsVisible(MemoryBox, false);
                IsVisible(BreakpointBox, false);
                return;
            }
            if (c.Equals(MemoryBox))
            {
                IsVisible(AllocateBox, false);
                IsVisible(c, true);
                IsVisible(BreakpointBox, false);
                IsVisible(Search1, false);
                return;
            }
            if (c.Equals(AllocateBox))
            {
                IsVisible(c, true);
                IsVisible(MemoryBox, false);
                IsVisible(BreakpointBox, false);
                IsVisible(Search1, false);
                return;
            }
            if (c.Equals(BreakpointBox))
            {
                IsVisible(AllocateBox, false);
                IsVisible(MemoryBox, false);
                IsVisible(c, true);
                IsVisible(Search1, false);
                return;
            }
            return;
        }

        private void DumpButton_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click(object sender, EventArgs e)
        {

        }
    }
}
