document.addEventListener("DOMContentLoaded", async function () {
    const video = document.getElementById("video");
    let videoSrc = "";

    try {
        // 从上级文件夹读取 appsettings.json 文件
        const response = await fetch("../appsetting.json");
        const config = await response.json();
        // 构建跨平台兼容的 videoSrc 路径
        //let videoSrc = config.M3U8FileRoot + "/index.m3u8";
        // 确保路径分隔符在不同操作系统上都能兼容
        //videoSrc = videoSrc.replace(/\\/g, '/');
        videoSrc = "C:\\Users\\BOSBSO\\OneDrive\\桌面\\M3U8\\index.m3u8";
    } catch (error) {
        console.error("读取或解析 appsettings.json 文件时出错:", error);
        return;
    }

    if (Hls.isSupported()) {
        const hls = new Hls({
            liveDurationInfinity: true,
            liveSyncDurationCount: 5,
            liveMaxLatencyDurationCount: 10,
            highBufferWatchdogPeriod: 1,
            maxMaxBufferLength: 30,
            maxBufferSize: 60 * 1000,
            enableWorker: true
        });

        hls.loadSource(videoSrc);
        hls.attachMedia(video);

        hls.on(Hls.Events.MANIFEST_PARSED, () => {
            video.play().catch(() => {
                console.log("需要用户交互后才能自动播放");
            });
        });

        // 处理 HLS 播放器错误
        hls.on(Hls.Events.ERROR, function (event, data) {
            if (data.type === Hls.ErrorTypes.NETWORK_ERROR && data.details === Hls.ErrorDetails.MANIFEST_LOAD_ERROR) {
                console.warn("播放列表重载失败,尝试重新连接...");
                setTimeout(() => {
                    hls.loadSource(videoSrc);
                    hls.attachMedia(video);
                    video.play();
                }, 3000); // 重试间隔
            }

            if (data.type === Hls.ErrorTypes.MEDIA_ERROR && data.details === Hls.ErrorDetails.FRAG_LOAD_ERROR) {
                console.warn("TS 片段加载失败,可能已被删除,尝试恢复...");
                hls.recoverMediaError();
            }

            if (data.type === Hls.ErrorTypes.NETWORK_ERROR && data.details === Hls.ErrorDetails.FRAG_LOAD_TIMEOUT) {
                console.warn("TS 片段加载超时,尝试重新连接...");
                hls.startLoad();
            }
        });

        // 监听 LEVEL_UPDATED 事件
        hls.on(Hls.Events.LEVEL_UPDATED, function (event, data) {
            const details = data.details;
            console.log(`当前直播窗口: 序列号 ${details.live.mediaSequence} - 最新片段: ${details.fragments[details.fragments.length - 1].url}`);
        });

    } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
        video.src = videoSrc;
        video.addEventListener('loadedmetadata', () => {
            video.play();
        });
    }
});