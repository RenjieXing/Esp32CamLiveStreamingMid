# 实现:

## ListenerMp4(云端):

这是一个监控程序,用于监控文件夹的MP4(也许是任意类型)文件新增,

然后再通过FFmepg封装至ts包,

放到appsetting.json的M3U8配置的文件夹下,

HLS播放器从该文件夹下的Index.m3u8进行直播流输出

# 接下来的计划

## CamSender(任意物联网设备)

Esp32Cam 流送文件TCP/IP协议

## VideoRecvier(云端)

接受CamSender的流式文件

并将指定视频文件移送至ListenerMp4所监控文件夹下

### 文件归属

如果服务器需要,ListenerMp4负责移除ts文件

如果服务器需要 VideoRecvier 负责移除媒体文件,并且维持消息管道(MQ)
