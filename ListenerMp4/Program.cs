using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using File = System.IO.File;

public static class rocerd
{

    public const string FFmpegPath = "C:\\Users\\14798\\Desktop\\ffmepg\\ffmpeg-7.1-essentials_build\\bin\\ffmpeg";

    public const string Mp4FileRoot = "C:\\Users\\14798\\Desktop\\mp4in";

    public const string M3U8FileRoot = "C:\\Users\\14798\\Desktop\\tsout";

    public const int tsLenth = 2;

    public const int tsCount = 3;

}


class Program
{
    private static int mediaSequence = 0;

    private static Dictionary<int,string> audioSequence = new();

    static async Task Main()
    {
        // 获取当前目录
        string currentDirectory = rocerd.Mp4FileRoot;
        // 创建 FileSystemWatcher 监听 MP4 文件变化
        FileSystemWatcher watcher = new FileSystemWatcher
        {
            Path = currentDirectory,
            Filter = "*.mp4",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        watcher.Created += async (sender, e) => await OnNewMp4File(e);
        watcher.EnableRaisingEvents = true;

        Console.WriteLine("监听文件夹中新的 MP4 文件...");
        await Task.Delay(Timeout.Infinite); // 使任务持续运行，不退出
    }

    static async Task OnNewMp4File(FileSystemEventArgs e)
    {
        string mp4File = e.FullPath;
        string currentDirectory = Path.GetDirectoryName(mp4File);
        string fileName = Path.GetFileNameWithoutExtension(mp4File);

        // 指定输出文件夹
        string outputDirectory = rocerd.M3U8FileRoot;
        Directory.CreateDirectory(outputDirectory); // 如果文件夹不存在则创建

        string tsFile = Path.Combine(outputDirectory, fileName + ".ts");
        string m3u8File = Path.Combine(outputDirectory, "index.m3u8");
        string tempM3u8File = Path.Combine(outputDirectory, "temp_index.m3u8");

        // 使用 FFmpeg 转换 MP4 为 TS，TS 包长度与 MP4 文件长度一致
        string arguments = $"-i \"{mp4File}\" -c copy -f mpegts \"{tsFile}\"";
        await ExecuteFFmpegCommand(arguments);

        // 更新临时的 index.m3u8 文件
        UpdateM3U8File(tempM3u8File, tsFile);

        // 替换原始的 index.m3u8 文件
        File.Copy(tempM3u8File, m3u8File, true);
    }

    static async Task ExecuteFFmpegCommand(string arguments)
    {
        Process ffmpeg = new Process
        {
            StartInfo =
            {
                FileName = rocerd.FFmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        ffmpeg.Start();

        string output = await ffmpeg.StandardOutput.ReadToEndAsync();
        string error = await ffmpeg.StandardError.ReadToEndAsync();
        await ffmpeg.WaitForExitAsync();

        Console.WriteLine(output);
        Console.WriteLine(error);
    }

    static void UpdateM3U8File(string m3u8File, string tsFile)
    {
        StringBuilder m3u8Content = new StringBuilder();

        // 读取现有的 m3u8 文件并解析内容
        if (File.Exists(m3u8File))
        {
            string[] lines = File.ReadAllLines(m3u8File);
            foreach (var line in lines)
            {
                if (line.StartsWith("#EXT-X-MEDIA-SEQUENCE"))
                {
                    // 更新媒体序列号
                    mediaSequence = int.Parse(line.Split(':')[1]) + 1;
              
                    m3u8Content.AppendLine($"#EXT-X-MEDIA-SEQUENCE:{mediaSequence}");
                }
                else
                {
                    m3u8Content.AppendLine(line);
                }
            }

       
        }
        else
        {
            // 创建新的 m3u8 文件头部
            m3u8Content.AppendLine("#EXTM3U");
            m3u8Content.AppendLine("#EXT-X-VERSION:3");
            m3u8Content.AppendLine("#EXT-X-ALLOW-CACHE:YES");
            m3u8Content.AppendLine("#EXT-X-TARGETDURATION:10"); // 设置一个默认的目标时长
            m3u8Content.AppendLine($"#EXT-X-MEDIA-SEQUENCE:{mediaSequence}");
        }

        // 获取 TS 文件的持续时间
        TimeSpan tsDuration = GetMediaDuration(tsFile);

        // 添加新的 TS 文件段
        m3u8Content.AppendLine($"#EXTINF:{tsDuration.TotalSeconds:F3},");
        m3u8Content.AppendLine(Path.GetFileName(tsFile));
        if (!audioSequence.TryAdd(mediaSequence, Path.GetFileName(tsFile)))
        {
            Console.WriteLine("出现问题");
        }

        // 删除旧的 TS 文件段（如果需要）
        if (mediaSequence > 5) // 例如，保留最新的5个片段
        {
            string oldTsFile = Path.Combine(rocerd.M3U8FileRoot, audioSequence.TryGetValue(mediaSequence-6,out var item)?item:"??");
            if (File.Exists(oldTsFile))
            {
                File.Delete(oldTsFile);
                Console.WriteLine($"删除旧的 TS 文件：{oldTsFile}");
            }
        }

        // 保存更新后的 m3u8 文件
        File.WriteAllText(m3u8File, m3u8Content.ToString());
    }

    static TimeSpan GetMediaDuration(string tsFile)
    {
        var ffmpeg = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = rocerd.FFmpegPath,
                Arguments = $"-i \"{tsFile}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        ffmpeg.Start();

        string output = ffmpeg.StandardError.ReadToEnd();
        ffmpeg.WaitForExit();

        // 解析输出获取持续时间
        var durationMatch = Regex.Match(output, @"Duration: (\d+):(\d+):(\d+)\.(\d+)");
        if (durationMatch.Success)
        {
            int hours = int.Parse(durationMatch.Groups[1].Value);
            int minutes = int.Parse(durationMatch.Groups[2].Value);
            int seconds = int.Parse(durationMatch.Groups[3].Value);
            int milliseconds = int.Parse(durationMatch.Groups[4].Value);
            return new TimeSpan(0, hours, minutes, seconds, milliseconds * 100);
        }

        return TimeSpan.Zero;
    }
}



