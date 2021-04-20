using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Configuration;

using System.IO.Ports;

using cszmcaux;
using System.Net;
using System.Net.Sockets;
using System.Threading;


namespace THOR_T_Csharpe
{
    public partial class Form1 : Form
    {
        #region 全局变量及字段
        public IntPtr g_handle;
        public int aaa;             //用于调试时，查看返回值
        public string Adrr;        //连接的IP地址

        public int connect = 0;     //链接方式，1-网口

        public uint com;            //串口号
        public uint baudrate;       //波特率

        public int[] axis_list = new int[4];   //运动轴列表

        public int single_axis = 0;  //单轴轴号
        public float[] single_speed = new float[4] { 1, 1, 1, 1 };  //单轴运动速度
        public int dir = -1;             //运动方向(默认负向==向上)

        public int home_mode = 3;  //回零模式
        public float home_speed = 2;  //回零速度
        public float slow_speed = 1;  //回零爬行速度

        public int node_num = 0;  //待测试的节点总数
        public int[] nodes = new int[11];  //每一个节点的力度大小
        public bool[] node_flags = new bool[11];  //到达每个节点的标志位
        public volatile int node_counter = 0;  //已到达的节点计数器
        public volatile int old_counter = 0;  //上一次的节点计数器
        public float node_offset = 0.05f; //节点力度的浮动范围
        public volatile bool motor_GoOn = false;  //是否允许电机继续运行
        public volatile float current_forceVal = 0.0f;

        public byte[] force_bytes = new byte[4]; //压力的十六进制表示数组
        public float force_f = 0.0f;   //压力的浮点数表示

        public string Socket_IP = "127.0.0.1";
        public int Socket_Port = 50088;

        private Socket socketwatch = null;
        private Thread threadwatch = null;
        private Thread threadMainTest = null;
        #endregion
        #region  系统加载，无需更改
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
            {
                nodes[i] = 0;
            }
            getConfig();
        }
        #endregion
        #region 连接到控制器事件
        private void button1_Click(object sender, EventArgs e)  //连接到控制器
        {
            if (g_handle == (IntPtr)0)
            {
                Adrr = comboBox1.Text;
                addInfoString("尝试连接IP:" + Adrr);
                //zmcaux.ZAux_OpenEth(Adrr, out g_handle);
                if (Adrr == "127.0.0.1")
                {
                    zmcaux.ZAux_OpenEth(Adrr, out g_handle);
                }
                else
                {
                    aaa = zmcaux.ZAux_SearchEth(Adrr, 100);     //搜索控制器
                    if (aaa == 0)
                    {
                        zmcaux.ZAux_OpenEth(Adrr, out g_handle);
                    }
                    else
                    {
                        addInfoString("找不到控制器!");
                    }
                }
            }
            if (g_handle != (IntPtr)0)
            {
                connect = 1;
                timer1.Enabled = true;
                connButt.Enabled = false;
                addInfoString("成功连接到控制器");

                StringBuilder SoftType = new StringBuilder(20);
                StringBuilder SoftVersion = new StringBuilder(20);
                StringBuilder ControllerId = new StringBuilder(20);

                zmcaux.ZAux_GetControllerInfo(g_handle, SoftType, SoftVersion, ControllerId);

                c_type.Text = SoftType.ToString();
                c_id.Text = ControllerId.ToString();
                c_version.Text = SoftVersion.ToString();
            }
            else
            {
                addInfoString("连接到控制器失败!");
            }
        }
        #endregion
        #region 启动测试事件
        private void button3_Click(object sender, EventArgs e)
        {
            if (connect == 1 && listenButt.Text.Equals("Listened"))
            {
                //如果未启动测试，则开始
                if (testButt.Text.Equals("启动测试"))
                {
                    testButt.Text = "测试中";
                    testButt.BackColor = Color.Green;
                    //获取当前设置的轴
                    single_axis = Convert.ToInt32(axisnum.Text);
                    //获取当前轴的速度
                    single_speed[single_axis] = Convert.ToSingle(single_sp.Text);
                    //获取需要测试的节点数和每个节点的值
                    node_num = Convert.ToInt32(node_numBox.Text);
                    node_counter = 0;
                    old_counter = 0;
                    nodes[0] = Convert.ToInt32(node1Box.Text);
                    nodes[1] = Convert.ToInt32(node2Box.Text);
                    nodes[2] = Convert.ToInt32(node3Box.Text);
                    nodes[3] = Convert.ToInt32(node4Box.Text);
                    nodes[4] = Convert.ToInt32(node5Box.Text);
                    nodes[5] = Convert.ToInt32(node6Box.Text);
                    nodes[6] = Convert.ToInt32(node7Box.Text);
                    nodes[7] = Convert.ToInt32(node8Box.Text);
                    nodes[8] = Convert.ToInt32(node9Box.Text);
                    nodes[9] = Convert.ToInt32(node10Box.Text);

                    motorRun(-1);  //电机开始走动

                    //主测试线程启动
                    threadMainTest = new Thread(test);
                    threadMainTest.IsBackground = true;
                    threadMainTest.Start();  //启动主测试流程
                }
                //若在测试中，则可以取消
                else if (testButt.Text.Equals("测试中"))
                {
                    DialogResult dr = MessageBox.Show("确定取消测试？", "退出测试", MessageBoxButtons.OKCancel);
                    if (dr == DialogResult.OK) //确认取消
                    {
                        testButt.Text = "启动测试";
                        testButt.BackColor = Color.Snow;
                        threadMainTest.Abort();
                        threadMainTest = null;
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        motorGoHome(3);  //正向回零
                        motor_GoOn = false;
                        clearNodeFlags();
                        addInfoString("测试中断");
                    }
                }
            }
            else
            {
                addInfoString("请先连接控制器并监听端口!");
            }
        }
        #endregion
        #region 定时器1-检查控制器连接
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (g_handle != (IntPtr)0)
            {
                aaa = zmcaux.ZAux_SearchEth(Adrr, 2000);   //搜索控制器
                if (aaa != 0)                             //找不到IP了
                {
                    g_handle = (IntPtr)0;
                    addInfoString("未连接!!!");
                    connButt.Enabled = true;
                    connect = 0;
                    timer1.Enabled = false;
                    c_type.Text = " ";
                    c_id.Text = " ";
                    c_version.Text = " ";
                }
            }
        }
        #endregion
        #region  关闭控制器连接事件
        private void closeButt_Click(object sender, EventArgs e)
        {
            if (g_handle != (IntPtr)0)
            {
                zmcaux.ZAux_Close(g_handle);
                g_handle = (IntPtr)0;
                connButt.Enabled = true;
                connect = 0;
                c_type.Text = " ";
                c_id.Text = " ";
                c_version.Text = " ";
            }
            addInfoString("未连接!!!");
            timer1.Enabled = false;
        }
        #endregion
        #region  打开串口按键事件
        private void serialPortButt_Click(object sender, EventArgs e)
        {
            if (serialPortButt.Text.Equals("打开串口"))
            {
                if (serialPort1.IsOpen)
                {
                    serialPort1.Close();
                }
                openSerialport(comboBox2.Text, Convert.ToInt32(comboBox3.Text));
            }
            else if (serialPortButt.Text.Equals("关闭串口"))
            {
                if (serialPort1.IsOpen)
                {
                    serialPort1.Close();
                    serialPortButt.Text = "打开串口";
                }
            }
        }
        #endregion
        #region 日志显示区域清理
        private void button1_Click_1(object sender, EventArgs e)
        {
            richTextBox1.Clear();

            //connect = 1;

            /*byte[] bs = {0x31, 0x3A, 0x33, 0x37, 0x31, 0x2E, 0x37, 0x2C, 0x33, 0x36, 0x39, 0x33, 0x38, 0x2C, 0x31, 0x35, 0x34, 0x35, 0x2C, 0x35};

            string message = Encoding.UTF8.GetString(bs, 0, bs.Length);
            addInfoString(message);
            string[] vals = message.Split(':')[1].Split(',');
            current_forceVal = Convert.ToSingle(vals[0]);
            addInfoString("" + current_forceVal);
            addInfoString(vals[3]);*/
            /*byte[] bytes = new byte[4] {0x9A, 0x72, 0x3B, 0x3E};
            float f = BitConverter.ToSingle(bytes, 0);//从第0个字节开始转换
            addInfoString(string.Format("{0:F8}", f));
            byte[] f_bs = BitConverter.GetBytes(f);
            string s = "";
            for(int i = 0; i < 4; i++)
            {
                s += string.Format("{0:X00}", f_bs[i]);
                s += " ";
            }
            addInfoString(s);*/
        }
        #endregion
        #region 单轴运动
        private void sigle_moveButt_Click(object sender, EventArgs e)  //单轴运动
        {
            single_axis = Convert.ToInt32(axisnum.Text);
            single_speed[single_axis] = Convert.ToSingle(single_sp.Text);

            if (g_handle != (IntPtr)0)
            {
                motorRun(dir);
            }
            else
            {
                addInfoString("请先连接到控制器!");
            }
        }
        #endregion
        #region 单轴停止
        private void single_StopButt_Click(object sender, EventArgs e)  //单轴停止
        {
            single_axis = Convert.ToInt32(axisnum.Text);
            if (g_handle != (IntPtr)0)
            {
                zmcaux.ZAux_Direct_Single_Cancel(g_handle, single_axis, 2);
                addInfoString("Motor Stopped!");
            }
            else
            {
                addInfoString("未连接");
            }
        }
        #endregion
        #region 单轴运动速度改变
        private void single_sp_TextChanged(object sender, EventArgs e)
        {
            try
            {
                single_axis = Convert.ToInt32(axisnum.Text);
                single_speed[single_axis] = Convert.ToSingle(single_sp.Text);
            }
            catch(Exception ex)
            {

            }
        }
        #endregion
        #region 单轴切换
        private void axisnum_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (Convert.ToInt32(axisnum.Text))
            {
                case 0:
                    single_sp.Text = single_speed[0].ToString("f");
                    break;
                case 1:
                    single_sp.Text = single_speed[1].ToString("f");
                    break;
                case 2:
                    single_sp.Text = single_speed[2].ToString("f");
                    break;
                case 3:
                    single_sp.Text = single_speed[3].ToString("f");
                    break;
                default:
                    break;
            }
        }
        #endregion
        #region 打开串口操作
        private void openSerialport(string comname, int combaudrate)
        {
            string[] pnames = SerialPort.GetPortNames();
            foreach (string n in pnames)
            {
                if (n.Equals(comname))
                {
                    try
                    {
                        serialPort1.PortName = comname;
                        serialPort1.BaudRate = combaudrate;
                        serialPort1.Parity = Parity.None;
                        serialPort1.StopBits = StopBits.One;
                        serialPort1.DataBits = 8;
                        serialPort1.Handshake = Handshake.None;
                        serialPort1.RtsEnable = true;
                        serialPort1.ReadTimeout = 2000;
                        serialPort1.NewLine = "\r\n";
                        serialPort1.Open();
                        serialPort1.ReceivedBytesThreshold = 1; //设置触发接收事件的字节数为1
                        serialPort1.DataReceived += serialPort1_DataReceived;
                    }
                    catch (Exception ex)
                    {
                        addInfoString("连接串口异常:" + ex.Message);
                        serialPortButt.Text = "打开串口";
                    }
                }
            }
            if (serialPort1.IsOpen)
            {
                serialPortButt.Text = "关闭串口";
            }
            else
            {
                addInfoString("端口可能不存在，无法打开串口!");
            }
        }
        #endregion
        #region 串口接收回调函数
        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            try
            {
                System.Threading.Thread.Sleep(20); //等待20ms
                byte[] SP1_Buf = new byte[serialPort1.BytesToRead];
                serialPort1.Read(SP1_Buf, 0, SP1_Buf.Length);
                //成功读取到数据，下面开始分析数据





            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
        #endregion
        #region 电机方向调试
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked == false)
            {
                checkBox1.Text = "运动方向：正";
                dir = 1;
            }
            else
            {
                checkBox1.Text = "运动方向：负";
                dir = -1;
            }
        }
        #endregion
        #region  显示调试信息字符串
        private void addInfoString(string src)
        {
            richTextBox1.AppendText(string.Format("{0:T}", DateTime.Now) + "::" + src + "\r\n");
        }

        #endregion
        #region  监听按键操作
        private void listenButt_Click(object sender, EventArgs e)
        {
            if (listenButt.Text.Equals("监听"))
            {
                //定义一个套接字用于监听客户端发来的消息，包含三个参数（IP4寻址协议，数据包，UDP协议）  
                socketwatch = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                Socket_IP = SocketIpBox.Text;
                Socket_Port = Convert.ToInt32(portBox.Text);
                //服务端发送信息需要一个IP地址和端口号  
                IPAddress address = IPAddress.Parse(Socket_IP);
                //将IP地址和端口号绑定到网络节点point上  
                IPEndPoint point = new IPEndPoint(address, Socket_Port);

                //监听绑定的网络节点  
                socketwatch.Bind(point);
                //创建Udp数据包接收线程
                threadwatch = new Thread(watchconnecting);
                //将窗体线程设置为与后台同步，随着主线程结束而结束  
                threadwatch.IsBackground = true;
                //启动线程     
                threadwatch.Start();
                addInfoString("成功监听端口！");
                listenButt.Text = "Listened";
            }
            else if (listenButt.Text.Equals("Listened"))
            {
                DialogResult dr = MessageBox.Show("确定退出监听？", "退出监听", MessageBoxButtons.OKCancel);
                if (dr == DialogResult.OK) //确认取消
                {
                    threadwatch.Abort();
                    socketwatch.Close();
                    threadwatch = null;
                    socketwatch = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    listenButt.Text = "监听";
                }
            }

        }
        #endregion
        #region 监听及通信线程
        private void watchconnecting()
        {
            //持续不断监听客户端发来的请求     
            while (true)
            {
                //3.接受数据
                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = new byte[1024];//用来接受数据
                int length = socketwatch.ReceiveFrom(data, ref remoteEndPoint);//这个方法会把数据的来源（IP地址，端口号）放在第二个参数

                string message = Encoding.UTF8.GetString(data, 0, length);
                if(detailBox.Checked)
                {
                    addInfoString("From Port " + (remoteEndPoint as IPEndPoint).Port.ToString() + ">>" + message);
                }
                //socketwatch.SendTo(Encoding.UTF8.GetBytes("测试是否能收到UDP消息"), remoteEndPoint);
                if (message.Contains("OK"))
                {
                    motor_GoOn = true;
                }
                else
                {
                    //客户端发送ascii数据>>1:371.7,36986,3678,0
                    string[] vals = message.Split(':')[1].Split(',');
                    current_forceVal = Convert.ToSingle(vals[0]);  //获取到当前拉力值
                }
            }
        }
        #endregion
        #region 主测试流程
        private void test()
        {
            while (true)
            {
                if (node_counter <= node_num)  //节点数据未走完
                {
                    if (current_forceVal <= (nodes[node_counter] + nodes[node_counter] * node_offset) && 
                        current_forceVal >= (nodes[node_counter] - nodes[node_counter] * node_offset) &&
                        old_counter == node_counter)
                    {
                        current_forceVal = 0; //清空拉力值
                        addInfoString("到达节点" + (node_counter + 1));
                        single_axis = Convert.ToInt32(axisnum.Text);
                        zmcaux.ZAux_Direct_Single_Cancel(g_handle, single_axis, 2);
                        node_counter++;
                    }
                    if (motor_GoOn) //电机是否继续运行标志
                    {
                        motor_GoOn = false;
                        if (node_counter != 0 && node_counter < node_num && old_counter != node_counter) //至少已经走到了第一节点
                        {
                            old_counter = node_counter;  //防止未收到OK信息程序就运行
                            if (nodes[node_counter] > nodes[node_counter - 1])
                            {
                                single_speed[single_axis] = 0.1f;
                                motorRun(-1);
                            }
                            else
                            {
                                single_speed[single_axis] = 0.1f;
                                motorRun(1);
                            }
                        }
                        else if(node_counter == node_num)
                        {
                            addInfoString("所有节点测试完毕，电机归位!");
                            testButt.Text = "启动测试";
                            testButt.BackColor = Color.Snow;
                            clearNodeFlags();
                            motorGoHome(3); //回原点
                            node_counter = 0;
                            old_counter = 0;
                            motor_GoOn = false;
                            threadMainTest.Abort();
                            threadMainTest = null;
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }
                }
                /*else
                {
                    addInfoString("所有节点测试完毕，电机归位!");
                    testButt.Text = "启动测试";
                    testButt.BackColor = Color.Snow;
                    clearNodeFlags();
                    motorGoHome(3); //回原点
                    node_counter = 0;
                    motor_GoOn = false;
                    threadMainTest.Abort();
                    threadMainTest = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }*/
            }
        }
        #endregion
        #region  电机运动
        private void motorRun(int m_dir)
        {
            //设置轴参数
            zmcaux.ZAux_Direct_SetAtype(g_handle, single_axis, 1);
            zmcaux.ZAux_Direct_SetUnits(g_handle, single_axis, 4000); //脉冲当量为4000
            zmcaux.ZAux_Direct_SetSpeed(g_handle, single_axis, single_speed[single_axis]);  //1mm/s
            zmcaux.ZAux_Direct_SetInvertStep(g_handle, single_axis, 1); //运动模式为脉冲+方向
            zmcaux.ZAux_Direct_Single_Vmove(g_handle, single_axis, m_dir); //正向运动
            addInfoString("速度:" + single_speed[single_axis] + "mm/s," + checkBox1.Text);
        }
        #endregion
        #region 电机寻找零位
        private void motorGoHome(int mode)
        {
            single_axis = Convert.ToInt32(axisnum.Text);
            home_speed = Convert.ToSingle(datumsp.Text);
            slow_speed = Convert.ToSingle(datum_slow.Text);

            zmcaux.ZAux_Direct_SetSpeed(g_handle, single_axis, home_speed);         //设置速度
            zmcaux.ZAux_Direct_SetCreep(g_handle, single_axis, slow_speed);         //2次回零反找速度
            zmcaux.ZAux_Direct_Single_Datum(g_handle, single_axis, mode);       //回零
        }
        #endregion
        #region  单轴回零
        private void datumButt_Click(object sender, EventArgs e)
        {
            if (g_handle != (IntPtr)0)
            {
                home_mode = Convert.ToInt32(datum.Text);
                motorGoHome(home_mode);
                addInfoString("Motor Datum!");
            }
            else
            {
                addInfoString("请先连接到控制器!");
            }
        }
        #endregion
        #region  清除节点标志位
        private void clearNodeFlags()
        {
            node_counter = 0;
            for (int i = 0; i < 10; i++)
            {
                node_flags[i] = false;
            }
        }
        #endregion
        #region 保存参数事件
        private void button2_Click(object sender, EventArgs e)
        {
            //connect = 1;
            setConfig();
        }
        #endregion
        #region 加载及保存参数
        private void getConfig()
        {
            comboBox1.Text = ConfigurationManager.AppSettings["Eth_IP"];
            SocketIpBox.Text = ConfigurationManager.AppSettings["Soc_IP"];
            portBox.Text = ConfigurationManager.AppSettings["Soc_PORT"];
            axisnum.Text = ConfigurationManager.AppSettings["axisnum"];
            single_sp.Text = ConfigurationManager.AppSettings["single_sp"];
            datum.Text = ConfigurationManager.AppSettings["datum"];
            datumsp.Text = ConfigurationManager.AppSettings["datumsp"];
            datum_slow.Text = ConfigurationManager.AppSettings["datum_slow"];
            node_numBox.Text = ConfigurationManager.AppSettings["node_num"];
            node1Box.Text = ConfigurationManager.AppSettings["node1"];
            node2Box.Text = ConfigurationManager.AppSettings["node2"];
            node3Box.Text = ConfigurationManager.AppSettings["node3"];
            node4Box.Text = ConfigurationManager.AppSettings["node4"];
            node5Box.Text = ConfigurationManager.AppSettings["node5"];
            node6Box.Text = ConfigurationManager.AppSettings["node6"];
            node7Box.Text = ConfigurationManager.AppSettings["node7"];
            node8Box.Text = ConfigurationManager.AppSettings["node8"];
            node9Box.Text = ConfigurationManager.AppSettings["node9"];
            node10Box.Text = ConfigurationManager.AppSettings["node10"];
            node11Box.Text = ConfigurationManager.AppSettings["node11"];
        }
        private void setConfig()
        {
            // 写入参数设置
            Configuration configuration = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            configuration.AppSettings.Settings["Eth_IP"].Value = this.comboBox1.Text;
            configuration.AppSettings.Settings["Soc_IP"].Value = this.SocketIpBox.Text;
            configuration.AppSettings.Settings["Soc_PORT"].Value = this.portBox.Text;
            configuration.AppSettings.Settings["axisnum"].Value = this.axisnum.Text;
            configuration.AppSettings.Settings["single_sp"].Value = this.single_sp.Text;
            configuration.AppSettings.Settings["datum"].Value = this.datum.Text;
            configuration.AppSettings.Settings["datumsp"].Value = this.datumsp.Text;
            configuration.AppSettings.Settings["datum_slow"].Value = this.datum_slow.Text;
            configuration.AppSettings.Settings["node_num"].Value = this.node_numBox.Text;
            configuration.AppSettings.Settings["node1"].Value = this.node1Box.Text;
            configuration.AppSettings.Settings["node2"].Value = this.node2Box.Text;
            configuration.AppSettings.Settings["node3"].Value = this.node3Box.Text;
            configuration.AppSettings.Settings["node4"].Value = this.node4Box.Text;
            configuration.AppSettings.Settings["node5"].Value = this.node5Box.Text;
            configuration.AppSettings.Settings["node6"].Value = this.node6Box.Text;
            configuration.AppSettings.Settings["node7"].Value = this.node7Box.Text;
            configuration.AppSettings.Settings["node8"].Value = this.node8Box.Text;
            configuration.AppSettings.Settings["node9"].Value = this.node9Box.Text;
            configuration.AppSettings.Settings["node10"].Value = this.node10Box.Text;
            configuration.AppSettings.Settings["node11"].Value = this.node11Box.Text;

            configuration.Save();
            ConfigurationManager.RefreshSection("appSettings");//重新加载新的配置文件
        }
        #endregion

    }
}
