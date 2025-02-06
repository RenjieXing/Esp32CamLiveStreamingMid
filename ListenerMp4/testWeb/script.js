
document.addEventListener("DOMContentLoaded", function () {
    const video = document.getElementById("video");
    const videoSrc = "C:\\Users\\BOSBSO\\OneDrive\\桌面\\M3U8\\index.m3u8"; // 使用 HTTP 地址

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

        hls.on(Hls.Events.ERROR, function (event, data) {
            if (data.type === Hls.ErrorTypes.NETWORK_ERROR && data.details === Hls.ErrorDetails.MANIFEST_LOAD_ERROR) {
                console.warn("播放列表重载失败，尝试重新连接...");
                setTimeout(() => hls.loadSource(videoSrc), 3000);
            }

            if (data.type === Hls.ErrorTypes.MEDIA_ERROR && data.details === Hls.ErrorDetails.FRAG_LOAD_ERROR) {
                console.warn("TS 片段加载失败，可能已被删除，跳过...");
                hls.recoverMediaError();
            }
        });
        hls.on(Hls.Events.LEVEL_UPDATED, function (event, data) {
            const details = data.details;
            console.log(`当前直播窗口: 
       序列号 ${details.live.mediaSequence} - 
       最新片段: ${details.fragments[details.fragments.length - 1].url}`);
        });

    } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
        video.src = videoSrc;
        video.addEventListener('loadedmetadata', () => {
            video.play();
        });
    }
});