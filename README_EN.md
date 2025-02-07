# project

## ListenerMp4(Web Server):

This is a monitoring program that tracks the addition of new MP4 (MayBeAnyMedia,Because ffmepg) files in a folder. 
It then uses FFmpeg to package these files into TS packages, which are placed in the folder specified by the M3U8 configuration in appsettings.json. 
The HLS player streams live output from the Index.m3u8 file in that folder.

# Will Comming

## CamSender(Any LowPower IOT Device)

ESP32-CAM Streaming Files (Any Network Protocol)

## VideoRecvier(Web Server)

Receive streaming files from CamSender and transfer the specified video files to the folder monitored by ListenerMp4.

### File Ownership

ListenerMp4  remove ts file If server need;

If the server requires VideoReceiver to be responsible for removing media files and maintaining the message queue (MQ);
