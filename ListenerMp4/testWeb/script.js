document.addEventListener("DOMContentLoaded", function () {
    var video = document.getElementById("video");
    var videoSrc = "../tsout/index.m3u8"; // 替换为实际的 m3u8 文件路径

    if (Hls.isSupported()) {
        var hls = new Hls();
        hls.loadSource(videoSrc);
        hls.attachMedia(video);
        hls.on(Hls.Events.MANIFEST_PARSED, function () {
            video.play();
        });
    } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
        video.src = videoSrc;
        video.addEventListener('canplay', function () {
            video.play();
        });
    } else {
        alert("您的浏览器不支持 HLS 播放。");
    }
});
