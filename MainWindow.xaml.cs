using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace AppQuipuGmbH
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        List<Task> tasks = new List<Task>();
        CancellationToken token;
        CancellationTokenSource cancelTokenSource = new CancellationTokenSource();

        public MainWindow()
        {
            InitializeComponent();
            btn_choose_file.IsEnabled = true;
            btn_stop.IsEnabled = false;
        }

        public class UrlData
        {
            public string Url { get; set; }
            public int Count { get; set; }
            public string Status { get; set; }
        }

        private void Btn_choose_file_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            openFileDialog.Filter = "Text files (*.txt)|*.txt";
            //openFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            openFileDialog.InitialDirectory = Environment.CurrentDirectory;

            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                string filename = openFileDialog.FileName;
                Start_analysis(filename);
                btn_choose_file.IsEnabled = false;
                btn_stop.IsEnabled = true;
            }
        }

        private void Btn_stop_Click(object sender, RoutedEventArgs e)
        {
            btn_choose_file.IsEnabled = true;
            btn_stop.IsEnabled = false;

            // delete items from list
            listview_urls.ItemsSource = null;
            listview_urls.Items.Refresh();

            // close tasks
            foreach (var task in tasks)
            {
                token = cancelTokenSource.Token;
                cancelTokenSource.Cancel();
                System.Console.WriteLine("Task ID-{0} is closed", task.Id);
            }

            token = new CancellationToken();
        }

        private void Start_analysis(string filename)
        {
            // create ListView
            List<UrlData> urls = new List<UrlData>();
            foreach (string line in System.IO.File.ReadLines(filename))
            {
                urls.Add(new UrlData() { Url = line, Count = 0, Status = "Orange" });
            }
            listview_urls.ItemsSource = urls;

            // start requests
            foreach (var item in urls)
            {
                string result = "";
                var task = Task.Run(() =>
                {
                    WebResponse response = null;
                    try
                    {
                        WebRequest request = WebRequest.Create(item.Url);
                        request.Credentials = CredentialCache.DefaultCredentials;
                        request.Method = "GET";
                        response = request.GetResponse();

                        // status
                        System.Console.WriteLine("Status URL-{0} is {1}", item.Url, ((HttpWebResponse)response).StatusDescription.ToString());
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                    }

                    // get
                    int count = 0;
                    if (response != null)
                    {
                        using (Stream dataStream = response.GetResponseStream())
                        {
                            StreamReader reader = new StreamReader(dataStream);
                            result = reader.ReadToEnd();
                            // search <a>
                            count = new Regex("<a").Matches(result).Count;
                        }

                        // close
                        response.Close();
                    }
                    
                    return count;
                }).ContinueWith((res) =>
                {
                    if (token.IsCancellationRequested)
                        return;
                    if (res.Result == 0)
                        return;

                    System.Console.WriteLine("Complete URL-{0}", item.Url);
                    urls.Find(i => i.Url == item.Url).Status = "Green";
                    urls.Find(i => i.Url == item.Url).Count = res.Result;

                    // search max
                    try
                    {
                        urls.Find(i => i.Status == "Purple").Status = "Green";
                    }
                    catch (Exception e)
                    {
                        System.Console.WriteLine(e);
                    }
                    urls.OrderByDescending(i => i.Count).First().Status = "Purple";

                    listview_urls.ItemsSource = urls;
                    listview_urls.Items.Refresh();
                }, TaskScheduler.FromCurrentSynchronizationContext());

                tasks.Add(task);
            }
        }
    }
}
