using OpenCvSharp;
using System.Collections.Concurrent;
using System.Diagnostics;
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
                Ip= dictionary?["Ip"].ToString() ;
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
        public static string Ip = "";

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

        //public static async void MoveToMP4()
        //{


        //    // 图像文件夹路径
        //    string imageFolder = Recorder.Mp4FileRoot;
        //    int duration = 5;
        //    // 输出视频文件路径
        //    string outputVideo = Recorder.Mp4FileRoot;
        //    int height = 240;
        //    int width = 320;
        //    int Fps = 300;

        //    while (true)
        //    {
        //        await Task.Delay(duration * 1000);
        //        var fileName = $"{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.mp4";
        //        var frameCount = imageQueue.Count;
        //        if (frameCount == 0)
        //        {
        //            continue;
        //        }
        //        List<Byte[]> bytes = new();
        //        // 创建视频写入器
        //        VideoWriter videoWriter = new VideoWriter(fileName, FourCC.MPG4, Fps / duration, new OpenCvSharp.Size(width, height));
        //        if (frameCount != Fps)
        //        {
        //            for (var i = 0; i < frameCount; i++)
        //            {
        //                if (imageQueue.TryDequeue(out var buffer))
        //                {
        //                    bytes.Add([.. buffer]);
        //                    //FileStream fileStream = new FileStream($"{DateTime.Now.ToString("yyyyMMddHHmmssfff")}.jpeg", FileMode.Append, FileAccess.Write);
        //                    //fileStream.Write(buffer.ToArray(), 0, buffer.Count-1);
        //                    //fileStream.Close();
        //                }
        //            }
        //        }
        //        int frameCount2 = bytes.Count;
        //        int repeatCount = Fps / bytes.Count;
        //        var lessframe = bytes.Count / ((bytes.Count - Fps) == 0 ? bytes.Count : bytes.Count - Fps);
        //        if (repeatCount >= 1)
        //        {
        //            for (var i = 0; i < bytes.Count; i++)
        //            {

        //                // 写入每张图像到视频中
        //                for (var j = 0; j < repeatCount; j++)
        //                {
        //                    Mat image = Mat.FromImageData(bytes[i], ImreadModes.Color);
        //                    videoWriter.Write(image);
        //                }

        //            }
        //        }
        //        else
        //        {
        //            for (var i = 0; i < bytes.Count; i++)
        //            {

        //                // 写入每张图像到视频中
        //                for (var j = 0; j < repeatCount; j++)
        //                {
        //                    Mat image = Mat.FromImageData(bytes[i], ImreadModes.Color);
        //                    videoWriter.Write(image);
        //                }
        //                if (i % (lessframe - 1) == 0)
        //                {
        //                    i++;
        //                }

        //            }
        //        }
        //            // 释放视频写入器
        //        videoWriter.Release();
        //        File.Move(fileName, Path.Combine(outputVideo, fileName));
        //    }


        //}
        public static async void MoveToMP4()
        {
            // 图像文件夹路径
            string imageFolder = Recorder.Mp4FileRoot;
            int duration = 5;
            // 输出视频文件路径
            string outputVideo = Recorder.Mp4FileRoot;
            int height = 240;
            int width = 320;
            int fps = 300;

            while (true)
            {
                await Task.Delay(duration * 1000);
                var fileName = $"{DateTime.Now:yyyyMMddHHmmssfff}.mp4";
                var frameCount = imageQueue.Count;
                if (frameCount == 0)
                {
                    continue;
                }
                List<byte[]> bytes = new();

                if (frameCount != fps)
                {
                    for (var i = 0; i < frameCount; i++)
                    {
                        if (imageQueue.TryDequeue(out var buffer))
                        {
                            bytes.Add(buffer.ToArray());
                        }
                    }
                }

                int repeatCount = fps / bytes.Count;
                int lessFrame = bytes.Count / ((bytes.Count - fps) == 0 ? bytes.Count : bytes.Count - fps);

                using (var tempDir = new TemporaryDirectory())
                {
                    for (int i = 0; i < bytes.Count; i++)
                    {
                        for (int j = 0; j < (repeatCount >= 1 ? repeatCount : 1); j++)
                        {
                            string imagePath = Path.Combine(tempDir.Path, $"{i * repeatCount + j}.jpeg");
                            File.WriteAllBytes(imagePath, bytes[i]);
                        }
                    }

                    RunFFmpeg(tempDir.Path, fileName, fps, width, height);
                    File.Move(fileName, Path.Combine(outputVideo, fileName));
                }
            }
        }

        static void RunFFmpeg(string inputDir, string outputFileName, int fps, int width, int height)
        {
            string ffmpegPath = Recorder.FFmpegPath; // 确保ffmpeg已安装并设置了正确的路径
            string inputFiles = Path.Combine(inputDir, "%d.jpeg");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-y -framerate {fps} -i {inputFiles} -s {width}x{height} -pix_fmt yuv420p {outputFileName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
                process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
            }
        }
    

        public static void StartListening()
        {
            IPAddress ipAddress = IPAddress.Any;
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
            public const int BufferSize = 1024;
            public byte[] buffer = new byte[BufferSize];
            public List<byte> AccumulatedBytes = new List<byte>();
        }



        class TemporaryDirectory : IDisposable
        {
            public string Path { get; }

            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
