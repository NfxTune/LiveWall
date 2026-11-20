# LiveWall V1

Ứng dụng hình nền động cho Windows, viết bằng C# WinForms và dùng libmpv để phát video.

## Cấu trúc

- Các tệp `.cs`, `LiveWallpaper.csproj` và `app.manifest`: mã nguồn ứng dụng chính.
- `PrototypeUI/`: các control và giao diện WinForms được dự án chính sử dụng; có project riêng để chạy thử giao diện.
- `Assets/`: tài nguyên được sao chép vào thư mục chạy.
- `Assets/LiveWallLogo.png` và `Assets/LiveWall.ico`: logo hiển thị trong giao diện, khay hệ thống và biểu tượng ứng dụng.
- `tools/GenerateLogoAssets.ps1`: tạo lại ảnh logo PNG và icon nhiều kích thước từ hình vẽ vector trong script.
- `ThirdParty/mpv-devkit/`: header, import library và gói phát triển mpv dùng khi cần làm việc với native API.
- `libmpv-2.dll`: thư viện runtime cần đặt cạnh ứng dụng; project tự sao chép nó vào output khi build.
- `docs/`: tài liệu kiến trúc và hướng dẫn.
- `bin/`, `obj/`: output và cache do .NET tạo, có thể tạo lại bằng build.

Project chính nhắm tới `net8.0-windows`, x64.
