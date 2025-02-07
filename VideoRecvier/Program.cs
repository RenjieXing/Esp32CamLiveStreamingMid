using System;
using System.Collections.Concurrent;
using System.Dynamic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace VideoRecvier
{
    public static class rocerd
    {
        public static bool init()
        {
            var exeDirectory = AppDomain.CurrentDomain.BaseDirectory; // 获取可执行文件目录
            var appSettingsPath = Path.Combine(exeDirectory, "appsetting.json");
            if (File.Exists(appSettingsPath))
            {
                var JsonFile = File.ReadAllText(appSettingsPath);
                var JsonObj = System.Text.Json.JsonSerializer.Deserialize<ExpandoObject>(JsonFile);
                var dictionary = JsonObj as IDictionary<string, object>;
                FFmpegPath = dictionary?["FFmpegPath"]?.ToString() ?? "";
                Mp4FileRoot = dictionary?["Mp4FileRoot"]?.ToString() ?? "";
                M3U8FileRoot = dictionary?["M3U8FileRoot"]?.ToString() ?? "";
                MaxSegments = int.Parse(dictionary?["MaxSegments"]?.ToString() ?? "5");
                OUTDATEtIME = int.Parse(dictionary?["OUTDATEtIME"]?.ToString() ?? "20");
                MediaType = dictionary?["ListenMediaType"]?.ToString() ?? "mp4";
                return true;
            }
            return false;
        }

        public static string FFmpegPath = "";

        public static string Mp4FileRoot = "";

        public static string M3U8FileRoot = "";

        public static int MaxSegments = 5;

        public static int OUTDATEtIME = 20;

        public static string MediaType = "mp4";
    }

    class Program
    {
        static ConcurrentQueue<string> fileQueue = new ConcurrentQueue<string>();
        static int maxQueueSize = 10; // 最大队列大小
        static ManualResetEvent allDone = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            if (!rocerd.init())
            {
                Console.WriteLine("配置文件不存在");
                Console.ReadLine();
                return;
            }
            StartListening();
        }

        public static void StartListening()
        {
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);
                Console.WriteLine("等待连接...");
                while (true)
                {
                    allDone.Reset();
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    allDone.WaitOne();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            allDone.Set();
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            StateObject state = new StateObject();
            state.workSocket = handler;

            // 接收文件大小
            handler.BeginReceive(state.buffer, 0, sizeof(long), 0, new AsyncCallback(ReadFileSizeCallback), state);
        }

        public static void ReadFileSizeCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead == sizeof(long))
            {
                long fileSize = BitConverter.ToInt64(state.buffer, 0);
                state.fileSize = fileSize;
                state.receivedSize = 0;
                state.fileName = $"{DateTime.Now:yyyyMMddHHmmss}.{rocerd.MediaType}";

                // 开始接收文件数据
                handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadFileDataCallback), state);
            }
        }

        public static void ReadFileDataCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // 将接收到的数据直接写入文件
                using (FileStream fs = new FileStream(state.fileName, FileMode.Append, FileAccess.Write))
                {
                    fs.Write(state.buffer, 0, bytesRead);
                }

                state.receivedSize += bytesRead;

                if (state.receivedSize < state.fileSize)
                {
                    // 继续异步接收数据
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadFileDataCallback), state);
                }
                else
                {
                    // 完成接收，处理文件队列
                    if (fileQueue.Count >= maxQueueSize)
                    {
                        Console.WriteLine("队列已满，丢弃文件：{0}", state.fileName);
                    }
                    else
                    {
                        fileQueue.Enqueue(state.fileName);
                        Console.WriteLine("文件接收并加入队列：{0}", state.fileName);
                    }
                    handler.Close();
                }
            }
        }

        public class StateObject
        {
            public Socket workSocket = null;
            public const int BufferSize = 1024;
            public byte[] buffer = new byte[BufferSize];
            public string fileName;
            public long fileSize;
            public long receivedSize;
        }
    }
}
