using System.Diagnostics;
using System.Dynamic;
using System.Text;
using System.Text.RegularExpressions;
using static System.Net.Mime.MediaTypeNames;
using File = System.IO.File;

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

    public static int MaxSegments = 20;

    public static int OUTDATEtIME = 20;

    public static string MediaType = "mp4";
}


class Program
{
    static async Task Main()
    {
        if (!rocerd.init())
        {
            Console.WriteLine("配置文件不存在");
            Console.ReadLine();
            return;
        }
        // 获取当前目录
        string currentDirectory = rocerd.Mp4FileRoot;
        // 创建 FileSystemWatcher 监听 MP4 文件变化
        FileSystemWatcher watcher = new FileSystemWatcher
        {
            Path = currentDirectory,
            Filter = $"*.{rocerd.MediaType}",
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastAccess
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
        Directory.CreateDirectory(outputDirectory); 

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
        File.Delete(mp4File);

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


    }

    static void UpdateM3U8File(string m3u8File, string newTsFile)
    {
        var tsDuration = GetVideoDuration(rocerd.FFmpegPath, newTsFile);
        var newSegmentName = Path.GetFileName(newTsFile);
        // 读取或初始化播放列表
        var m3u8Lines = File.Exists(m3u8File)
            ? File.ReadAllLines(m3u8File).ToList()
            : new List<string>
            {
                "#EXTM3U",
                "#EXT-X-VERSION:6",
                "#EXT-X-TARGETDURATION:5",
                "#EXT-X-MEDIA-SEQUENCE:0",
                "#EXT-X-DISCONTINUITY-SEQUENCE:0"
            };

        // 动态维护序列号
        int mediaSequence = int.Parse(GetTagValue(m3u8Lines, "#EXT-X-MEDIA-SEQUENCE") ?? "0");
        int discontinuitySequence = int.Parse(GetTagValue(m3u8Lines, "#EXT-X-DISCONTINUITY-SEQUENCE") ?? "0");

        //插入新片段（强制DISCONTINUITY）
        m3u8Lines.Add($"#EXT-X-DISCONTINUITY");
        m3u8Lines.Add($"#EXTINF:{tsDuration.TotalSeconds:F3},");
        m3u8Lines.Add(newSegmentName);
        discontinuitySequence++; // 每次新增递增全局序列

        // 滚动延迟删除旧内容
        int segmentCount = m3u8Lines.Count(line => line.StartsWith("#EXTINF:"));
        while (segmentCount > rocerd.MaxSegments)
        {
            // 删除最旧的一个片段（3行：DISCONTINUITY + EXTINF + TS）
            int firstDiscontinuityIndex = m3u8Lines.FindIndex(line => line.Contains("EXT-X-DISCONTINUITY") && !line.Contains("SEQUENCE"));
            if (firstDiscontinuityIndex != -1)
            {
                var tsName = m3u8Lines[firstDiscontinuityIndex + 2];
                //移除列表时延时删除该文件
                //依据HLS规范，延迟删除的时间应该大于最大片段时间+本段时间,这里简化处理以配置为准
                new DelayDelete().Delete(Path.Combine(rocerd.M3U8FileRoot, tsName));
                m3u8Lines.RemoveRange(firstDiscontinuityIndex, 3);
                mediaSequence++; // 必须递增媒体序列号
                segmentCount--;
            }
        }

        // 更新头部标签
        UpdateTag(m3u8Lines, "#EXT-X-MEDIA-SEQUENCE", mediaSequence.ToString());
        UpdateTag(m3u8Lines, "#EXT-X-DISCONTINUITY-SEQUENCE", discontinuitySequence.ToString());

        // 写入文件
        File.WriteAllLines(m3u8File, m3u8Lines);

      
        
     
    }

    // 辅助方法：获取标签值
    static string? GetTagValue(List<string> lines, string tagName)
    {
        return lines.FirstOrDefault(line => line.StartsWith($"{tagName}:"))?
            .Split(':').Last();
    }


    // 辅助方法：更新标签
    static void UpdateTag(List<string> lines, string tagName, string value)
    {
        int index = lines.FindIndex(line => line.Contains(tagName));
        if (index != -1)
        {
            lines[index] = $"{tagName}:{value}";
        }
        else
        {
            lines.Insert(3, $"{tagName}:{value}"); // 插入到版本标签之后
        }
    }

    public static TimeSpan GetVideoDuration(string ffmpegPath, string videoPath)
    {
        Process ffmpegProcess = new Process();
        ffmpegProcess.StartInfo.FileName = ffmpegPath;
        ffmpegProcess.StartInfo.Arguments = $"-i \"{videoPath}\"";
        ffmpegProcess.StartInfo.RedirectStandardError = true;
        ffmpegProcess.StartInfo.UseShellExecute = false;
        ffmpegProcess.StartInfo.CreateNoWindow = true;
        ffmpegProcess.Start();

        string ffmpegOutput = ffmpegProcess.StandardError.ReadToEnd();
        ffmpegProcess.WaitForExit();

        string durationPattern = @"Duration: (\d{2}):(\d{2}):(\d{2})\.(\d{2})";
        Match match = Regex.Match(ffmpegOutput, durationPattern);
        if (match.Success)
        {
            int hours = int.Parse(match.Groups[1].Value);
            int minutes = int.Parse(match.Groups[2].Value);
            int seconds = int.Parse(match.Groups[3].Value);
            int milliseconds = int.Parse(match.Groups[4].Value) * 10;

            TimeSpan duration = new TimeSpan(0, hours, minutes, seconds, milliseconds);
            return duration;
        }
        else
        {
            throw new Exception("Could not get video length.");
        }
    }

    //异步延时删除
    public class DelayDelete
    {
        public void Delete(string FileFullPath)
        {

            Task.Run(async () =>
            {

                await Task.Delay(rocerd.OUTDATEtIME * 1000);

                if (File.Exists(FileFullPath))
                {
                    File.Delete(FileFullPath);
                }
            });
        }
    }

}



