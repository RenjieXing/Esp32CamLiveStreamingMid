using OpenCvSharp;
using System.Collections.Concurrent;
using System.Dynamic;
using System.Net;
using System.Net.Sockets;

namespace VideoReceiver
{
    public static class Recorder
    {
        public static bool Init()
        {
            var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var appSettingsPath = Path.Combine(exeDirectory, "appsetting.json");
            if (File.Exists(appSettingsPath))
            {
                var jsonFile = File.ReadAllText(appSettingsPath);
                var jsonObj = System.Text.Json.JsonSerializer.Deserialize<ExpandoObject>(jsonFile);
                var dictionary = jsonObj as IDictionary<string, object>;
                FFmpegPath = dictionary?["FFmpegPath"]?.ToString() ?? "";
                Mp4FileRoot = dictionary?["Mp4FileRoot"]?.ToString() ?? "";
                M3U8FileRoot = dictionary?["M3U8FileRoot"]?.ToString() ?? "";
                MaxSegments = int.Parse(dictionary?["MaxSegments"]?.ToString() ?? "5");
                OutdateTime = int.Parse(dictionary?["OUTDATEtIME"]?.ToString() ?? "20");
                MediaType = dictionary?["ListenMediaType"]?.ToString() ?? "mp4";
                return true;
            }
            return false;
        }

        public static string FFmpegPath = "";
        public static string Mp4FileRoot = "";
        public static string M3U8FileRoot = "";
        public static int MaxSegments = 5;
        public static int OutdateTime = 20;
        public static string MediaType = "mp4";
    }

    class Program
    {
        static ManualResetEvent allDone = new ManualResetEvent(false);
        static ConcurrentQueue<List<Byte>> imageQueue = new();
    
        static void Main(string[] args)
        {
            if (!Recorder.Init())
            {
                Console.WriteLine("配置文件不存在");
                Console.ReadLine();
                return;
            }
            Task.Run(() => MoveToMP4());
            StartListening();
        }

        public static async void MoveToMP4()
        {


            // 图像文件夹路径
            string imageFolder = Recorder.Mp4FileRoot;
            int duration = 5;
            // 输出视频文件路径
            string outputVideo = Recorder.Mp4FileRoot;
            int height = 800;
            int width = 600;
            int Fps = 10;

            while (true)
            {
                await Task.Delay(duration * 1000);
                var frameCount = imageQueue.Count;
                if (frameCount == 0)
                {
                    continue;
                }
                List<Byte[]> bytes = new();
                // 创建视频写入器
                VideoWriter videoWriter = new VideoWriter(Path.Combine(outputVideo,$"{frameCount}.mp4"), VideoWriter.FourCC('m', 'p', '4', 'v'), Fps, new OpenCvSharp.Size(width, height));
                if(frameCount !=Fps)
                {
                    for (var i = 0; i < frameCount; i++)
                    {
                        if (imageQueue.TryDequeue(out var buffer))
                        {
                            bytes.Add([.. buffer]);
                            FileStream fileStream = new FileStream($"{i}.jpeg", FileMode.Append, FileAccess.Write);
                            fileStream.Write(buffer.ToArray(), 0, buffer.Count-1);
                            fileStream.Close();
                        }
                    }
                }
                var RATE = ((double)frameCount) / ((double)Fps);
                for (var i = 0; i < Fps* duration; i++)
                {
                 // 写入每张图像到视频中
                  Mat image = Cv2.ImDecode(bytes[i * RATE>=bytes.Count? bytes.Count-1: (int)(i * RATE)], ImreadModes.Color);
                 if (image.Empty())
                  {
                        Console.WriteLine($"Failed to decode image ");
                        continue;
                  }
                    videoWriter.Write(image);
                }
                // 释放视频写入器
                videoWriter.Release();
                videoWriter?.Dispose();
            }

            
        }

        
        public static void StartListening()
        {
            IPAddress ipAddress = Dns.GetHostEntry(Dns.GetHostName())
                                     .AddressList.First(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);
                Console.WriteLine($"等待连接... 本机IP: {ipAddress}");
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

            StateObject state = new StateObject
            {
                workSocket = handler,
            };

            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
        }

        public static void ReadCallback(IAsyncResult ar)
        {
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            try
            {
                int bytesRead = handler.EndReceive(ar);
                if (bytesRead > 0)
                {
                    state.AccumulatedBytes.AddRange(state.buffer.Take(bytesRead));

                    while (true)
                    {
                        if (state.AccumulatedBytes.Count < 4) break;

                        int dataLength = BitConverter.ToInt32(state.AccumulatedBytes.Take(4).ToArray(), 0);
                       
                        if (state.AccumulatedBytes.Count < 4 + dataLength) break;

                        var packet = state.AccumulatedBytes.Skip(4).Take(dataLength).ToList();
                        
                        imageQueue.Enqueue(packet);

                        state.AccumulatedBytes.RemoveRange(0, 4 + dataLength);
                    }
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                }
             
              
            }
            catch (Exception e)
            {
                Console.WriteLine($"数据接收错误: {e.Message}");
            }
        }


        public class StateObject
        {
            public Socket workSocket = null;
            public const int BufferSize = 1024 * 10;
            public byte[] buffer = new byte[BufferSize];
            public List<byte> AccumulatedBytes = new List<byte>();
        }




    }
}
