namespace SmartHome.Backend.Configuration;

public sealed class AudioOptions
{
    public string MpvPath { get; set; } = "mpv";

    public string YtDlpPath { get; set; } = "yt-dlp";

    public string AmixerPath { get; set; } = "amixer";

    public string MixerName { get; set; } = "Master";

    public int MaxQueryLength { get; set; } = 200;
}
