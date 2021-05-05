using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;


namespace THOR_T_Csharpe
{
    class InputBox : System.Windows.Forms.Form
    {
        private TextBox textBox_Data;
        private Button button_Enter;
        private Button button_Esc;
        private System.ComponentModel.Container components = null;

        private InputBox()
        {
            InitializeComponent();
            this.TopMost = true;
            //this.StartPosition = FormStartPosition.CenterScreen;
            //inputbox.Location.X = 0; inputbox.Location.Y = 0;
            //inputbox.StartPosition = FormStartPosition.CenterScreen;
            //inputbox.Left = 0;
            //inputbox.Top = 0;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {

            this.textBox_Data = new System.Windows.Forms.TextBox();
            this.button_Enter = new System.Windows.Forms.Button();
            this.button_Esc = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // textBox_Data
            // 
            this.textBox_Data.Location = new System.Drawing.Point(8, 8);
            this.textBox_Data.Name = "textBox_Data";
            this.textBox_Data.PasswordChar = '*';
            this.textBox_Data.Size = new System.Drawing.Size(230, 21);
            this.textBox_Data.TabIndex = 2;
            this.textBox_Data.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textBox_Data_KeyDown);
            // 
            // button_Enter
            // 
            this.button_Enter.Location = new System.Drawing.Point(25, 43);
            this.button_Enter.Name = "button_Enter";
            this.button_Enter.Size = new System.Drawing.Size(75, 23);
            this.button_Enter.TabIndex = 3;
            this.button_Enter.Text = "确 认";
            this.button_Enter.UseVisualStyleBackColor = true;
            this.button_Enter.Click += new System.EventHandler(this.button_Enter_Click);
            // 
            // button_Esc
            // 
            this.button_Esc.Location = new System.Drawing.Point(140, 43);
            this.button_Esc.Name = "button_Esc";
            this.button_Esc.Size = new System.Drawing.Size(75, 23);
            this.button_Esc.TabIndex = 4;
            this.button_Esc.Text = "取 消";
            this.button_Esc.UseVisualStyleBackColor = true;
            this.button_Esc.Click += new System.EventHandler(this.button_Esc_Click);
            // 
            // InputBox
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(6, 14);
            this.ClientSize = new System.Drawing.Size(250, 80);
            this.Controls.Add(this.button_Esc);
            this.Controls.Add(this.button_Enter);
            this.Controls.Add(this.textBox_Data);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ControlBox = false;
            this.Name = "InputBox";
            this.Text = "InputBox";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        //对键盘进行响应
        private void textBox_Data_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { button_Enter_Click(sender, e); }
            else if (e.KeyCode == Keys.Escape) { button_Esc_Click(sender, e); }
        }
        private void button_Enter_Click(object sender, EventArgs e)
        {
            this.Close();
        }
        private void button_Esc_Click(object sender, EventArgs e)
        {
            textBox_Data.Text = string.Empty; this.Close();
        }


        //显示InputBox
        public static string ShowInputBox(int Left, int Top, string Title, string Prompt, string DefaultResponse)
        {
            InputBox inputbox = new InputBox();
            if (Title.Trim() != string.Empty) inputbox.Text = Title;
            if (DefaultResponse.Trim() != string.Empty) inputbox.textBox_Data.Text = DefaultResponse;
            inputbox.ShowDialog();
            inputbox.Left = Left; inputbox.Top = Top;
            return inputbox.textBox_Data.Text;
        }
        public static string ShowInputBox(FormStartPosition Position, string Title, string Prompt, string DefaultResponse)
        {
            InputBox inputbox = new InputBox();
            inputbox.StartPosition = Position;
            if (Title.Trim() != string.Empty) inputbox.Text = Title;
            if (DefaultResponse.Trim() != string.Empty) inputbox.textBox_Data.Text = DefaultResponse;
            inputbox.ShowDialog();
            return inputbox.textBox_Data.Text;
        }
        public static string ShowInputBox()
        {
            return ShowInputBox(FormStartPosition.CenterScreen, string.Empty, string.Empty, string.Empty);
        }
        public static string ShowInputBox(string Title)
        {
            return ShowInputBox(FormStartPosition.CenterScreen, Title, string.Empty, string.Empty);
        }
        public static string ShowInputBox(string Title, string Prompt)
        {
            return ShowInputBox(FormStartPosition.CenterScreen, Title, Prompt, string.Empty);
        }
        public static string ShowInputBox(string Title, string Prompt, string DefaultResponse)
        {
            return ShowInputBox(FormStartPosition.CenterScreen, Title, Prompt, DefaultResponse);
        }
    }
}
