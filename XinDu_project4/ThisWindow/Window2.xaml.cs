using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TestHarness;
using System.Threading;

namespace ThisWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Window2 : Window
    {
        Client client;
        IAsyncResult cbResult;
        testRequest requestHolder;
        testElement elementHolder;

        public Window2()
        {
            Console.Title = "Client 1";
            InitializeComponent();
            Title = "Client 1";
            RequestCreateButton.IsEnabled = false;
            ElementCreateButton.IsEnabled = false;
            SendButton.IsEnabled = false;
            UploadButton.IsEnabled = false;
            SelectButton.IsEnabled = false;
            ContentQueryButton.IsEnabled = false;
            LogQueryButton.IsEnabled = false;
        }
        /// <summary>
        /// create and send message to demo the functionality
        /// </summary>
        void demo(Client client)
        {

            //demo test request
            Message msg = client.buildTestMessage("XinDu", client.buildDemoRequest());
            Message msg2 = client.buildTestMessage("XinDu", client.buildDemoRequest2());
            client.comm.sndr.PostMessage(msg);
            client.comm.sndr.PostMessage(msg);
            client.comm.sndr.PostMessage(msg2);

            //assume client come back to check the result after 10000mls
            Thread.Sleep(10000);

            //demo content query
            Message CTmsg = client.buildContentQueryMessage("*.txt");
            client.comm.sndr.PostMessage(CTmsg);

            //and then decide the query word in 5000mls
            Thread.Sleep(5000);

            //demo result query
            Message Qmsg = client.buildResultQueryMessage("XinDu");
            client.comm.sndr.PostMessage(Qmsg);
        }
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            string localAddress = LocalAddressTextBox.Text;
            string localPort = LocalPortTextBox.Text;
            int x = Int32.Parse(localPort);

            try
            {
                client = new Client(localAddress, x);

                StartButton.IsEnabled = false;
                RequestCreateButton.IsEnabled = true;
                ElementCreateButton.IsEnabled = true;
                SendButton.IsEnabled = true;
                UploadButton.IsEnabled = true;
                SelectButton.IsEnabled = true;
                ContentQueryButton.IsEnabled = true;
                LogQueryButton.IsEnabled = true;
                Action<Client> proc = this.demo;
                cbResult = proc.BeginInvoke(client, null, null);
            }
            catch (Exception ex)
            {
                Window temp = new Window();
                StringBuilder msg = new StringBuilder(ex.Message);
                msg.Append("\nport = ");
                msg.Append(localPort.ToString());
                temp.Content = msg.ToString();
                temp.Height = 100;
                temp.Width = 500;
                temp.Show();
            }
        }
        private void RequestCreate_Click(object sender, RoutedEventArgs e)
        {
            requestHolder = new testRequest();
            requestHolder.author = (string)NameBox.Text;
            ShowBox.Text = requestHolder.ToString();
        }
        private void ElementCreateButton_Click(object sender, RoutedEventArgs e)
        {
            elementHolder = new testElement(TestBox.Text);
            elementHolder.addDriver(DriverBox.Text);
            elementHolder.addCode(CodeBox.Text);
            requestHolder.tests.Add(elementHolder);
            ShowBox.Text = requestHolder.ToString();
        }
        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            Message msg = new Message();
            msg.to = client.thUrl;
            msg.from = client.endPoint;
            msg.author = (string)NameBox.Text;
            msg.type = "TestRequest";
            msg.body = requestHolder.ToString();
            client.comm.sndr.PostMessage(msg);
        }
        //file upload tab
        string filename;
        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            string path = "..\\..\\toSend\\";
            dlg.DefaultExt = ".dll";
            dlg.Filter = "DLL Files (*.dll)|*.dll";
            path = System.IO.Path.GetFullPath(path);
            dlg.InitialDirectory = path;
            Nullable<bool> result = dlg.ShowDialog();

            if (result == true)
            {
                filename = dlg.FileName;
                textBlock1.Text = filename;
            }
        }
        private void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            Message msg = client.buildUplodMessage(filename);
            client.comm.sndr.PostMessage(msg);
            listBox1.Items.Insert(0, textBlock1.Text);
        }
        //result query tab
        private void ContentQueryButton_Click(object sender, RoutedEventArgs e)
        {
            Message CTmsg = client.buildContentQueryMessage("*.txt");
            client.comm.sndr.PostMessage(CTmsg);
        }
        private void LogQueryButton_Click(object sender, RoutedEventArgs e)
        {
            Message Qmsg = client.buildResultQueryMessage(KeyWordBox.Text);
            client.comm.sndr.PostMessage(Qmsg);
            listBox2.Items.Insert(0, KeyWordBox.Text);
        }

    }
}
