# ListenerMp4: 
This is a monitoring program that tracks the addition of new MP4 files in a folder. 
It then uses FFmpeg to package these files into TS packages, which are placed in the folder specified by the M3U8 configuration in appsettings.json. 
The HLS player streams live output from the Index.m3u8 file in that folder.

这是一个监控程序,用于监控文件夹的MP4文件新增,然后再通过FFmepg封装至ts包,放到appsetting.json的M3U8配置的文件夹下,HLS播放器从该文件夹下的Index.m3u8进行直播流输出
