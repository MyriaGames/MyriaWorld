namespace MyriaWorld.Services;

public class AppSettings
{
    public bool Fullscreen      { get; set; } = false;
    public int  ResolutionIndex { get; set; } = 0;

    public static readonly (int W, int H, string Label)[] Resolutions =
    {
        (1280,  720, "1280 × 720   (HD)"),
        (1600,  900, "1600 × 900   (HD+)"),
        (1920, 1080, "1920 × 1080  (Full HD)"),
        (2560, 1440, "2560 × 1440  (2K)"),
    };

    public (int W, int H, string Label) CurrentResolution =>
        Resolutions[Math.Clamp(ResolutionIndex, 0, Resolutions.Length - 1)];
}
