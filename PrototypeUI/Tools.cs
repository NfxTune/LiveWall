using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LiveWall.UI;

/// <summary>Gọi ffmpeg/ffprobe: thumbnail, đọc thông tin video, tối ưu video.
/// Tìm trong thư mục tools\ cạnh exe trước, rồi tới PATH.</summary>
static class Tools
{
    public sealed record MediaInfo(string Codec, int Width, int Height, double Fps)
    {
        public override string ToString() => $"{Width}×{Height} · {Fps:0.#} fps";
    }

    static string ThumbDir => Path.Combine(AppSettings.DataDir, "thumbs");

    static string ThumbPath(string video)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(video.ToLowerInvariant()));
        return Path.Combine(ThumbDir, Convert.ToHexString(hash)[..16] + ".jpg");
    }

    static string Exe(string name)
    {
        var local = Path.Combine(AppContext.BaseDirectory, "tools", name + ".exe");
        return File.Exists(local) ? local : name;
    }

    // null = không chạy được (thiếu file exe)
    static async Task<(int Code, string Out)?> RunAsync(string exe, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(exe)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var p = Process.Start(psi);
            if (p == null) return null;
            var stdout = p.StandardOutput.ReadToEndAsync();
            var stderr = p.StandardError.ReadToEndAsync(); // phải đọc để không bị treo đường ống
            await p.WaitForExitAsync();
            await stderr;
            return (p.ExitCode, await stdout);
        }
        catch (Win32Exception) { return null; }
    }

    public static async Task<MediaInfo?> ProbeAsync(string path)
    {
        var r = await RunAsync(Exe("ffprobe"), "-v", "error", "-select_streams", "v:0",
            "-show_entries", "stream=codec_name,width,height,r_frame_rate",
            "-of", "default=noprint_wrappers=1", path);
        if (r is not { Code: 0 }) return null;

        var kv = r.Value.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim().Split('=', 2))
            .Where(a => a.Length == 2)
            .ToDictionary(a => a[0], a => a[1]);

        if (!kv.TryGetValue("width", out var w) || !kv.TryGetValue("height", out var h)) return null;
        double fps = 0;
        if (kv.TryGetValue("r_frame_rate", out var fr))
        {
            var parts = fr.Split('/');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], CultureInfo.InvariantCulture, out var n) &&
                double.TryParse(parts[1], CultureInfo.InvariantCulture, out var d) && d > 0)
                fps = n / d;
        }
        return new MediaInfo(kv.GetValueOrDefault("codec_name", "?"), int.Parse(w), int.Parse(h), fps);
    }

    public static async Task EnsureThumbAsync(string video)
    {
        var thumb = ThumbPath(video);
        if (File.Exists(thumb)) return;
        Directory.CreateDirectory(ThumbDir);
        foreach (var seek in new[] { "1", "0" }) // video quá ngắn thì lấy khung đầu
        {
            await RunAsync(Exe("ffmpeg"), "-nostdin", "-loglevel", "error", "-y",
                "-ss", seek, "-i", video, "-frames:v", "1", "-vf", "scale=320:-2", thumb);
            if (File.Exists(thumb)) return;
        }
    }

    /// <summary>Đọc thumbnail thành Bitmap mới (không khóa file). Trả null nếu chưa có.</summary>
    public static Bitmap? LoadThumb(string video)
    {
        var thumb = ThumbPath(video);
        if (!File.Exists(thumb)) return null;
        try
        {
            using var fs = new FileStream(thumb, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var img = Image.FromStream(fs);
            return new Bitmap(img);
        }
        catch { return null; }
    }

    /// <summary>Re-encode về tối đa 1080p, 30 fps, H.264, bỏ âm thanh. Trả đường dẫn file mới hoặc null.</summary>
    public static async Task<string?> OptimizeAsync(string input)
    {
        var output = Path.Combine(Path.GetDirectoryName(input)!,
            Path.GetFileNameWithoutExtension(input) + "_1080p30.mp4");

        var r = await RunAsync(Exe("ffmpeg"), "-nostdin", "-loglevel", "error", "-y", "-i", input,
            "-vf", "scale=-2:'min(1080,ih)',fps=30",
            "-c:v", "libx264", "-preset", "medium", "-crf", "23", "-pix_fmt", "yuv420p",
            "-an", "-movflags", "+faststart", output);

        return r is { Code: 0 } && File.Exists(output) ? output : null;
    }
}
