//#define auto_test

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
using Newtonsoft.Json;
using Microsoft.VisualBasic;

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

        public volatile float step_dist = 0.005f;  //电机单步运行步长

        public int home_mode = 3;  //回零模式
        public float home_speed = 2;  //回零速度
        public float slow_speed = 1;  //回零爬行速度

        public int node_num = 0;  //待测试的节点总数
        public volatile int[] nodes = new int[20];  //每一个节点的力度大小
        public volatile int node_counter = 0;  //已到达的节点计数器
        public volatile int old_counter = 0;  //上一次的节点计数器
        public float node_offset = 0.05f; //节点力度的浮动范围
        public volatile bool motor_GoOn = false;  //是否允许电机继续运行
        public volatile float current_forceVal = 0.0f;  //当前拉力值
        public volatile float capture_forceVal = 0.0f;  //上一次的捕获值
        public volatile bool Accept_Succ = false;  //动了一步之后等待数据传回

        public DateTime dtFrom = new DateTime(2021, 1, 1, 0, 0, 0, 0);  //起始时间
        public long start_mills = 0;  //程序开始时的毫秒值

        public System.Timers.Timer timer_TO;  //捕获超时定时器
        public System.Timers.Timer timer_AJ;  //行程调节定时器

        public string Socket_IP = "127.0.0.1";  //待连接的socketIP
        public int Socket_Port = 50088;    //监听的socket端口

        private Socket socketwatch = null;  //socket实例
        private Thread threadwatch = null;  //socket监听线程
        private Thread threadMainTest = null; //主测试线程

        //=====================================================
        public int j_min_force = 0;  //采样推力点最小值
        public int j_max_force = 0;  //采样推力点最大值
        public int j_step;  //采样推力步进

        //自动测试用
#if auto_test
        private Thread autoThread = null;
        private bool auto_test_completed;
        public int auto_test_counter = 0;
#endif
        #endregion
        #region  系统加载，无需更改
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            for (int i = 0; i < 11; i++)
            {
                nodes[i] = 0;
            }
            getConfig(null);
            timer_TO = new System.Timers.Timer(4000);//实例化Timer类，设置间隔时间为10000毫秒；
            timer_TO.Elapsed += new System.Timers.ElapsedEventHandler(timeOut);//到达时间的时候执行事件；
            timer_TO.AutoReset = false;//设置是执行一次（false）还是一直执行(true)；
            //力的差值过大自动调节一步
            timer_AJ = new System.Timers.Timer(1500);//实例化Timer类，设置间隔时间为2000毫秒；
            timer_AJ.Elapsed += new System.Timers.ElapsedEventHandler(adjustment);//到达时间的时候执行事件；
            timer_AJ.AutoReset = false;//设置是执行一次（false）还是一直执行(true)；
#if auto_test
            autoThread = new Thread(autoTest);
            autoThread.IsBackground = true;
            autoThread.Start();
#endif
        }
        #endregion
        #region 连接到控制器事件
        private void button1_Click(object sender, EventArgs e)  //连接到控制器
        {
            if(connButt.Text.Equals("连接"))
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
                    //连接到控制器后先转动电机，再归零。
                    single_axis = Convert.ToInt32(axisnum.Text);
                    single_speed[single_axis] = Convert.ToSingle(single_sp.Text);
                    motorRun(-1);
                    Thread.Sleep(50);
                    zmcaux.ZAux_Direct_Single_Cancel(g_handle, single_axis, 2);
                    Thread.Sleep(50);
                    motorGoHome(3);
                    //======================
                    connect = 1;
                    timer1.Enabled = true;
                    connButt.Text = "断开";
                    listenButt.PerformClick(); //监听端口
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
            else if(connButt.Text.Equals("断开"))
            {
                if (g_handle != (IntPtr)0)
                {
                    zmcaux.ZAux_Close(g_handle);
                    g_handle = (IntPtr)0;
                    connButt.Text = "连接";
                    connect = 0;
                    c_type.Text = " ";
                    c_id.Text = " ";
                    c_version.Text = " ";
                }
                addInfoString("未连接!!!");
                timer1.Enabled = false;
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
                    if (node_num > 11)
                    {
                        addInfoString("超出节点配置范围!!!");
                        return;
                    }
                    richTextBox1.Clear();  //清除日志显示区域

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
                    nodes[10] = Convert.ToInt32(node11Box.Text);
                    step_dist = Convert.ToSingle(stepBox.Text);

                    testTimeLabel.Text = "";

                    //主测试线程启动
                    threadMainTest = new Thread(test);
                    threadMainTest.IsBackground = true;
                    threadMainTest.Start();  //启动主测试流程
                    start_mills = getCurrentMills();
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
                        timer_TO.Stop();
                        motor_GoOn = false;
                        node_counter = 0;
                        old_counter = 0;
                        long mills = getCurrentMills() - start_mills;
                        testTimeLabel.Text = "" + mills;
                        addInfoString("测试中断,用时:" + mills + "ms");
                    }
                }
            }
            else
            {
                addInfoString("请先连接控制器并监听端口!");
            }
        }
        #endregion
        #region 远程控制启动和停止测试
        private void remote_start()
        {
            if (connect == 1 && listenButt.Text.Equals("Listened"))
            {
                testButt.Text = "测试中";
                testButt.BackColor = Color.Green;
                //获取当前设置的轴
                single_axis = Convert.ToInt32(axisnum.Text);
                //获取当前轴的速度
                single_speed[single_axis] = Convert.ToSingle(single_sp.Text);
                //获取需要测试的节点数和每个节点的值
                cac_nodes();
                richTextBox1.Clear();  //清除日志显示区域

                node_counter = 0;
                old_counter = 0;

                step_dist = Convert.ToSingle(stepBox.Text);

                testTimeLabel.Text = "";

                //主测试线程启动
                threadMainTest = new Thread(test);
                threadMainTest.IsBackground = true;
                threadMainTest.Start();  //启动主测试流程
                start_mills = getCurrentMills();
            }
        }
        private void remote_stop()
        {
            testButt.Text = "启动测试";
            testButt.BackColor = Color.Snow;
            threadMainTest.Abort();
            threadMainTest = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            motorGoHome(3);  //正向回零
            timer_TO.Stop();
            motor_GoOn = false;
            node_counter = 0;
            old_counter = 0;
            long mills = getCurrentMills() - start_mills;
            testTimeLabel.Text = "" + mills;
            addInfoString("测试中断,用时:" + mills + "ms");
        }
        #endregion
        #region 计算节点并填充各节点
        private void cac_nodes()
        {
            int tem = j_max_force;
            node_num = (j_max_force - j_min_force) / j_step + 1;
            nodes[0] = j_max_force;
            int i = 1;
            for (; i < node_num - 1; i++)
            {
                tem -= j_step;
                nodes[i] = tem;
            }
            nodes[i] = j_min_force;
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
                    connButt.Text = "连接";
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
            this.richTextBox1.Clear();
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
            catch (Exception ex)
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
                checkBox1.Text = "运动方向：下";
                dir = 1;
            }
            else
            {
                checkBox1.Text = "运动方向：上";
                dir = -1;
            }
        }
        #endregion
        #region  显示调试信息字符串
        private void addInfoString(string src)
        {
            this.richTextBox1.AppendText(string.Format("{0:T}", DateTime.Now) + "::" + src + "\r\n");
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
                if (detailBox.Checked)
                {
                    addInfoString("From Port " + (remoteEndPoint as IPEndPoint).Port.ToString() + ">>" + message);
                }
                //===增加JSON解析===========================================================
                try
                {
                    DataFormat srcData = JsonConvert.DeserializeObject<DataFormat>(message);
                    //指令解析
                    switch (srcData.cmd)
                    {
                        case "0":
                            motor_GoOn = true;
                            capture_forceVal = current_forceVal;
                            addInfoString("Capture=" + capture_forceVal);
                            //判断是否需要动态调节下一节点的测试值
                            if (node_counter > 0 && node_counter < node_num && adjustCheckBox.Checked)
                            {
                               if(capture_forceVal <= (j_min_force + j_step * 2))
                                {
                                    nodes[node_counter] = j_min_force; 
                                }
                               else
                                {
                                    nodes[node_counter] = (int)capture_forceVal - j_step;
                                }
                            }
                            break;
                        case "1":
                            current_forceVal = Convert.ToSingle(srcData.data[1]);
                            if (current_forceVal > 20)
                            {
                                step_dist = Convert.ToSingle(step1Box.Text);
                            }
                            Accept_Succ = true;
                            break;
                        case "2":
                            j_min_force = Convert.ToInt32(Convert.ToSingle(srcData.parameters[0]));
                            j_max_force = Convert.ToInt32(Convert.ToSingle(srcData.parameters[1]));
                            j_step = Convert.ToInt32(Convert.ToSingle(srcData.parameters[2])) + 15;
                            break;
                        //start
                        case "3":
                            j_min_force = Convert.ToInt32(Convert.ToSingle(srcData.parameters[0]));
                            j_max_force = Convert.ToInt32(Convert.ToSingle(srcData.parameters[1]));
                            j_step = Convert.ToInt32(Convert.ToSingle(srcData.parameters[2])) + 15;
                            remote_start();
                            break;
                        //stop
                        case "4":
                            remote_stop();
                            break;
                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Exception");
                }

                //socketwatch.SendTo(Encoding.UTF8.GetBytes("测试是否能收到UDP消息"), remoteEndPoint);
                //================非JSON解析方式
                /*if (message.Contains("OK"))
                {
                    motor_GoOn = true;
                    capture_forceVal = current_forceVal;
                    addInfoString("Capture=" + capture_forceVal);
                    //判断是否需要动态调节下一节点的测试值
                    if (node_counter > 0 && node_counter < node_num && adjustCheckBox.Checked)
                    {
                        nodes[node_counter] = (capture_forceVal > 80) ? ((int)capture_forceVal - 50) : ((int)capture_forceVal + 50);
                    }
                }
                else
                {
                    //客户端发送ascii数据>>1:371.7,36986,3678,0
                    string[] vals = message.Split(':')[1].Split(',');
                    current_forceVal = Convert.ToSingle(vals[0]);  //获取到当前拉力值
                    if (current_forceVal > 20)
                    {
                        step_dist = Convert.ToSingle(step1Box.Text);
                    }
                    Accept_Succ = true;
                }*/
                // 回收临时数据
                GC.Collect();
                GC.WaitForPendingFinalizers();
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
                    if (old_counter == node_counter)  //到达节点前不停止走动
                    {
                        /*if (current_forceVal <= (nodes[node_counter] + nodes[node_counter] * node_offset) &&
                            current_forceVal >= (nodes[node_counter] - nodes[node_counter] * node_offset))*/
                        if (current_forceVal <= (nodes[node_counter] + 10) &&
                        current_forceVal >= (nodes[node_counter] - 10))
                        {
                            addInfoString("到达节点" + (node_counter + 1) + ",F=" + current_forceVal);
                            if (node_counter == 0)
                            {
                                addInfoString("已运行:" + (getCurrentMills() - start_mills) + "ms");
                            }
                            current_forceVal = 0; //清空拉力值
                            single_axis = Convert.ToInt32(axisnum.Text);
                            zmcaux.ZAux_Direct_Single_Cancel(g_handle, single_axis, 2);  //停止当前轴
                            node_counter++;
                            //检测捕获是否超时
                            timer_TO.Start();
                            //启动自我调节
                            timer_AJ.Start();
                        }
                        //每次读取拉力大小，确保电机要走的方向
                        //else if (current_forceVal < (nodes[node_counter] - nodes[node_counter] * node_offset))
                        else if (current_forceVal < (nodes[node_counter] - 10))
                        {
                            motorRunStep(single_axis, single_speed[single_axis], step_dist * -1);
                        }
                        //else if (current_forceVal > (nodes[node_counter] + nodes[node_counter] * node_offset))
                        else if (current_forceVal > (nodes[node_counter] + 10))
                        {
                            motorRunStep(single_axis, single_speed[single_axis], step_dist);
                        }
                    }

                    if (motor_GoOn) //电机是否继续运行标志
                    {
                        motor_GoOn = false;
                        timer_TO.Stop(); //取消超时检测
                        timer_AJ.Stop(); //取消自动位置调整
                        if (node_counter != 0 && node_counter < node_num && old_counter != node_counter) //至少已经走到了第一节点
                        {
                            old_counter = node_counter;  //防止未收到OK信息程序就运行
                        }
                        else if (node_counter == node_num)
                        {
                            //addInfoString("所有节点测试完毕，电机归位\r\n用时:" + (getCurrentMills() - start_mills) + "ms");
                            testTimeLabel.Text = "" + (getCurrentMills() - start_mills);
                            testButt.Text = "启动测试";
                            testButt.BackColor = Color.Snow;
                            
                            motorGoHome(3); //回原点
                            node_counter = 0;
                            old_counter = 0;
                            motor_GoOn = false;
#if auto_test
                            auto_test_completed = true;
#endif
                            threadMainTest.Abort();
                            threadMainTest = null;
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                    }
                }
                //等待拉力数据传回，以判定是否继续前行
                while (!Accept_Succ)
                {
                    Thread.Sleep(1);  //线程休眠1ms
                }
                Accept_Succ = false;
            }
        }
        #endregion
        #region  电机运动
        private void motorRun(int m_dir)
        {
            //调整运动方向
            dir = m_dir;
            if (dir == 1)
            {
                checkBox1.Checked = false;
                checkBox1.Text = "运动方向：下";
            }
            else
            {
                checkBox1.Checked = true;
                checkBox1.Text = "运动方向：上";
            }


            //设置轴参数
            zmcaux.ZAux_Direct_SetAtype(g_handle, single_axis, 1);
            zmcaux.ZAux_Direct_SetUnits(g_handle, single_axis, 4000); //脉冲当量为4000
            zmcaux.ZAux_Direct_SetSpeed(g_handle, single_axis, single_speed[single_axis]);  //1mm/s
            zmcaux.ZAux_Direct_SetInvertStep(g_handle, single_axis, 1); //运动模式为脉冲+方向
            zmcaux.ZAux_Direct_Single_Vmove(g_handle, single_axis, dir); //正向运动
            addInfoString("速度:" + single_speed[single_axis] + "mm/s," + checkBox1.Text);
        }
        private void motorSetDir(int m_dir)
        {
            //调整运动方向
            dir = m_dir;
            if (dir == 1)
            {
                checkBox1.Checked = false;
                checkBox1.Text = "运动方向：下";
            }
            else
            {
                checkBox1.Checked = true;
                checkBox1.Text = "运动方向：上";
            }
            zmcaux.ZAux_Direct_Single_Vmove(g_handle, single_axis, dir);
        }
        #endregion
        #region  电机发生位移
        /**
         * 电机移动一步
         * @axis 轴号 0-X 1-Y
         * @speed 速度 mm/s
         * @dist  距离  负数代表向上，正数代表向下
         * @return none
         */
        private void motorRunStep(int axis, float speed, float dist)
        {
            //相对
            zmcaux.ZAux_Direct_SetSpeed(g_handle, axis, speed);     //设置速度
            zmcaux.ZAux_Direct_Single_Move(g_handle, axis, dist);
            if (runDetailBox.Checked)
            {
                if (dist > 0)
                {
                    addInfoString("速度:" + speed + "mm/s,向下走" + dist + "mm");
                }
                else
                {
                    addInfoString("速度:" + speed + "mm/s,向上走" + (dist * -1) + "mm");
                }
            }
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
        #region 保存默认配置事件
        private void button2_Click(object sender, EventArgs e)
        {
            setConfig(null);
            addInfoString("成功保存默认配置");
        }
        #endregion
        #region 加载及保存参数
        private void getConfig(string fileName)
        {
            Configuration config;
            if (fileName == null)
            {
                config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            }
            else
            {
                ExeConfigurationFileMap configFileMap = new ExeConfigurationFileMap();
                configFileMap.ExeConfigFilename = fileName;
                config = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);
            }
            comboBox1.Text = config.AppSettings.Settings["Eth_IP"].Value;
            SocketIpBox.Text = config.AppSettings.Settings["Soc_IP"].Value;
            portBox.Text = config.AppSettings.Settings["Soc_PORT"].Value;
            axisnum.Text = config.AppSettings.Settings["axisnum"].Value;
            single_sp.Text = config.AppSettings.Settings["single_sp"].Value;
            datum.Text = config.AppSettings.Settings["datum"].Value;
            datumsp.Text = config.AppSettings.Settings["datumsp"].Value;
            datum_slow.Text = config.AppSettings.Settings["datum_slow"].Value;
            node_numBox.Text = config.AppSettings.Settings["node_num"].Value;
            node1Box.Text = config.AppSettings.Settings["node1"].Value;
            node2Box.Text = config.AppSettings.Settings["node2"].Value;
            node3Box.Text = config.AppSettings.Settings["node3"].Value;
            node4Box.Text = config.AppSettings.Settings["node4"].Value;
            node5Box.Text = config.AppSettings.Settings["node5"].Value;
            node6Box.Text = config.AppSettings.Settings["node6"].Value;
            node7Box.Text = config.AppSettings.Settings["node7"].Value;
            node8Box.Text = config.AppSettings.Settings["node8"].Value;
            node9Box.Text = config.AppSettings.Settings["node9"].Value;
            node10Box.Text = config.AppSettings.Settings["node10"].Value;
            node11Box.Text = config.AppSettings.Settings["node11"].Value;
            stepBox.Text = config.AppSettings.Settings["step"].Value;
            step1Box.Text = config.AppSettings.Settings["step1"].Value;

        }
        private void setConfig(string fileName)
        {
            Configuration config;
            if (fileName == null)
            {
                config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            }
            else
            {
                ExeConfigurationFileMap configFileMap = new ExeConfigurationFileMap();
                configFileMap.ExeConfigFilename = fileName;
                config = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);
            }

            config.AppSettings.Settings["Eth_IP"].Value = this.comboBox1.Text;
            config.AppSettings.Settings["Soc_IP"].Value = this.SocketIpBox.Text;
            config.AppSettings.Settings["Soc_PORT"].Value = this.portBox.Text;
            config.AppSettings.Settings["axisnum"].Value = this.axisnum.Text;
            config.AppSettings.Settings["single_sp"].Value = this.single_sp.Text;
            config.AppSettings.Settings["datum"].Value = this.datum.Text;
            config.AppSettings.Settings["datumsp"].Value = this.datumsp.Text;
            config.AppSettings.Settings["datum_slow"].Value = this.datum_slow.Text;
            config.AppSettings.Settings["node_num"].Value = this.node_numBox.Text;
            config.AppSettings.Settings["node1"].Value = this.node1Box.Text;
            config.AppSettings.Settings["node2"].Value = this.node2Box.Text;
            config.AppSettings.Settings["node3"].Value = this.node3Box.Text;
            config.AppSettings.Settings["node4"].Value = this.node4Box.Text;
            config.AppSettings.Settings["node5"].Value = this.node5Box.Text;
            config.AppSettings.Settings["node6"].Value = this.node6Box.Text;
            config.AppSettings.Settings["node7"].Value = this.node7Box.Text;
            config.AppSettings.Settings["node8"].Value = this.node8Box.Text;
            config.AppSettings.Settings["node9"].Value = this.node9Box.Text;
            config.AppSettings.Settings["node10"].Value = this.node10Box.Text;
            config.AppSettings.Settings["node11"].Value = this.node11Box.Text;
            config.AppSettings.Settings["step"].Value = this.stepBox.Text;
            config.AppSettings.Settings["step1"].Value = this.step1Box.Text;
            config.Save();
            ConfigurationManager.RefreshSection("appSettings");//重新加载新的配置文件
        }
        #endregion
        #region  获取当前时间的毫秒值
        private long getCurrentMills()
        {
            long current = (DateTime.Now.Ticks - dtFrom.Ticks) / 10000;
            return current;
        }
        #endregion
        #region 到达节点和捕获数据超时处理
        private void timeOut(object source, System.Timers.ElapsedEventArgs e)
        {
            if (node_counter > 1)
            {
                addInfoString("捕获超时,自动调节");
                node_counter -= 1;
                if (capture_forceVal <= (j_min_force + j_step * 2))
                {
                    nodes[node_counter] = j_min_force;
                }
                else
                {
                    nodes[node_counter] = (int)capture_forceVal - j_step;
                }
            }
        }
        #endregion
        #region 自动调节电机位置
        private void adjustment(object source, System.Timers.ElapsedEventArgs e)
        {
            //if (current_forceVal < (nodes[node_counter] - nodes[node_counter] * node_offset))
            if (current_forceVal < (nodes[node_counter - 1] - 10))
            {
                motorRunStep(single_axis, single_speed[single_axis], step_dist * -1);
            }
            //else if (current_forceVal > (nodes[node_counter] + nodes[node_counter] * node_offset))
            else if (current_forceVal > (nodes[node_counter - 1] + 10))
            {
                motorRunStep(single_axis, single_speed[single_axis], step_dist);
            }
        }
        #endregion
        #region 点动按键事件
        private void upButt_Click(object sender, EventArgs e)
        {
            motorRunStep(single_axis, single_speed[single_axis], step_dist * -1);
        }

        private void downButt_Click(object sender, EventArgs e)
        {
            motorRunStep(single_axis, single_speed[single_axis], step_dist);
        }
        #endregion
        #region 自动调节复选框
        private void adjustCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!adjustCheckBox.Checked) //不需要自动调节，则按照设定的值
            {
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
                nodes[10] = Convert.ToInt32(node11Box.Text);
            }
        }
        #endregion
#if auto_test
        private void autoTest()
        {
            while (true)
            {
                if (auto_test_completed)
                {
                    auto_test_completed = false;
                    Thread.Sleep(10000);
                    testButt.PerformClick();
                    auto_test_counter++;
                    this.Text = "THOR测试系统===" + auto_test_counter;
                }
                Thread.Sleep(100);
            }
        }
#endif
        #region 按指定步长运行
        private void button3_Click_1(object sender, EventArgs e)
        {
            int step_num = Convert.ToInt32(stepNumBox.Text);
            //获取当前设置的轴
            single_axis = Convert.ToInt32(axisnum.Text);
            //获取当前轴的速度
            single_speed[single_axis] = Convert.ToSingle(single_sp.Text);
            //获取步长
            step_dist = Convert.ToSingle(step1Box.Text);
            motorRunStep(single_axis, single_speed[single_axis], step_dist * dir * step_num);
        }
        #endregion
        #region 加载默认配置事件
        private void defaultLoadButt_Click(object sender, EventArgs e)
        {
            getConfig(null);
            addInfoString("成功加载默认配置");
        }
        #endregion
        #region 保存用户配置
        private void userSaveButt_Click(object sender, EventArgs e)
        {
            OpenFileDialog openfile = new OpenFileDialog();
            //初始显示文件目录
            //openfile.InitialDirectory = @"";
            openfile.Title = "请选择用户配置文件";
            //过滤文件类型
            openfile.Filter = "配置文件|*.Config";
            if (DialogResult.OK == openfile.ShowDialog())
            {
                setConfig(openfile.FileName);
                addInfoString("成功保存到用户配置!");
            }
        }
        #endregion
        #region  加载用户配置
        private void userLoadButt_Click(object sender, EventArgs e)
        {
            OpenFileDialog openfile = new OpenFileDialog();
            //初始显示文件目录
            //openfile.InitialDirectory = @"";
            openfile.Title = "请选择用户配置文件";
            //过滤文件类型
            openfile.Filter = "配置文件|*.Config";
            if (DialogResult.OK == openfile.ShowDialog())
            {
                getConfig(openfile.FileName);
                addInfoString("成功加载用户配置!");
            }
        }
        #endregion
        #region  解锁参数事件
        private void lockButt_Click(object sender, EventArgs e)
        {
            if(lockButt.Text.Equals("locked"))
            {
                string pwd = InputBox.ShowInputBox("请输入管理员(admin)的密码", string.Empty);
                if (pwd.Trim() != string.Empty)
                {
                    if(pwd.Equals("THOR123")) //密码正确
                    {
                        lockButt.Text = "unlock";
                        comboBox1.Enabled = true;
                        SocketIpBox.ReadOnly = false;
                        portBox.ReadOnly = false;
                        axisnum.Enabled = true;
                        single_StopButt.Enabled = true;
                        single_sp.ReadOnly = false;
                        checkBox1.Enabled = true;
                        datum.Enabled = true;
                        datumsp.ReadOnly = false;
                        datum_slow.ReadOnly = false;
                        stepBox.ReadOnly = false;
                        step1Box.ReadOnly = false;
                        stepNumBox.ReadOnly = false;
                        goButt.Enabled = true;
                        sigle_moveButt.Enabled = true;
                        datumButt.Enabled = true;
                        upButt.Enabled = true;
                        downButt.Enabled = true;
                        node_numBox.ReadOnly = false;
                        node1Box.ReadOnly = false;
                        node2Box.ReadOnly = false;
                        node3Box.ReadOnly = false;
                        node4Box.ReadOnly = false;
                        node5Box.ReadOnly = false;
                        node6Box.ReadOnly = false;
                        node7Box.ReadOnly = false;
                        node8Box.ReadOnly = false;
                        node9Box.ReadOnly = false;
                        node10Box.ReadOnly = false;
                        node11Box.ReadOnly = false;
                    }
                }
            }
            else if(lockButt.Text.Equals("unlock"))
            {
                lockButt.Text = "locked";
                comboBox1.Enabled = false;
                SocketIpBox.ReadOnly = true;
                portBox.ReadOnly = true;
                axisnum.Enabled = false;
                single_StopButt.Enabled = false;
                single_sp.ReadOnly = true;
                checkBox1.Enabled = false;
                datum.Enabled = false;
                datumsp.ReadOnly = true;
                datum_slow.ReadOnly = true;
                stepBox.ReadOnly = true;
                step1Box.ReadOnly = true;
                stepNumBox.ReadOnly = true;
                goButt.Enabled = false;
                sigle_moveButt.Enabled = false;
                datumButt.Enabled = false;
                upButt.Enabled = false;
                downButt.Enabled = false;
                node_numBox.ReadOnly = true;
                node1Box.ReadOnly = true;
                node2Box.ReadOnly = true;
                node3Box.ReadOnly = true;
                node4Box.ReadOnly = true;
                node5Box.ReadOnly = true;
                node6Box.ReadOnly = true;
                node7Box.ReadOnly = true;
                node8Box.ReadOnly = true;
                node9Box.ReadOnly = true;
                node10Box.ReadOnly = true;
                node11Box.ReadOnly = true;
            }
        }
        #endregion
    }
}
