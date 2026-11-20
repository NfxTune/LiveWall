using System.Text.Json;
using System.Text.Json.Serialization;

namespace LiveWall.UI;

/// <summary>Cấu hình người dùng, lưu ở %AppData%\LiveWall\settings.json.</summary>
public sealed class AppSettings
{
    [JsonIgnore]
    public Action<AppSettings>? Persist { get; set; }

    public List<string> Library { get; set; } = new();
    public string? Current { get; set; }      // video đang chọn trong giao diện
    public string? Active { get; set; }       // video đã "Đặt làm nền"
    public int MonitorIndex { get; set; } = 0; // -1 = trải dài mọi màn hình

    public bool Mute { get; set; } = true;
    public bool Loop { get; set; } = true;
    public int Volume { get; set; } = 40;

    public bool PauseFullscreen { get; set; } = true;
    public bool PauseLock { get; set; } = true;
    public bool PauseBattery { get; set; } = false;
    public bool AutoStart { get; set; }

    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiveWall");
    public static string FilePath => Path.Combine(DataDir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { /* file hỏng: dùng mặc định */ }
        return new();
    }

    public void Save()
    {
        if (Persist is not null)
        {
            Persist(this);
            return;
        }

        try
        {
            Directory.CreateDirectory(DataDir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch { /* không để lỗi ghi file làm sập giao diện */ }
    }
}
