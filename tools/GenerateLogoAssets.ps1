Add-Type -AssemblyName System.Drawing

$generator = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

public static class LiveWallLogoGenerator
{
    static Bitmap Draw(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.FromArgb(201, 179, 164));

            using (var tile = new Pen(Color.FromArgb(55, Color.White), 3))
            {
                g.DrawLine(tile, -20, 102, 532, -30);
                g.DrawLine(tile, -20, 284, 532, 152);
                g.DrawLine(tile, 50, 532, 202, -20);
                g.DrawLine(tile, 316, 532, 468, -20);
            }

            using (var outline = new Pen(Color.FromArgb(18, 18, 18), 14) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var white = new SolidBrush(Color.White))
            using (var body = new GraphicsPath())
            {
                body.StartFigure();
                body.AddBezier(174, 300, 140, 344, 138, 418, 150, 500);
                body.AddLine(150, 500, 362, 500);
                body.AddBezier(362, 500, 380, 435, 376, 348, 338, 300);
                body.CloseFigure();
                g.FillPath(white, body);
                g.DrawPath(outline, body);

                using (var arm = new GraphicsPath())
                {
                    arm.AddBezier(155, 352, 98, 342, 99, 416, 148, 436);
                    arm.AddBezier(148, 436, 178, 448, 185, 414, 161, 395);
                    g.DrawPath(outline, arm);
                }

                using (var head = new GraphicsPath())
                {
                    head.AddEllipse(52, 34, 408, 408);
                    g.FillPath(white, head);
                    g.DrawPath(outline, head);
                }
            }

            using (var face = new Pen(Color.FromArgb(10, 10, 10), 12) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawArc(face, 154, 216, 66, 42, 12, 145);
                g.DrawArc(face, 292, 174, 66, 42, 12, 145);
                g.DrawBezier(face, 274, 300, 300, 289, 325, 275, 350, 298);
                g.DrawLine(face, 274, 300, 267, 325);
                g.DrawLine(face, 267, 325, 350, 298);
            }

            using (var cheek = new SolidBrush(Color.FromArgb(240, 139, 163)))
            {
                g.FillEllipse(cheek, 112, 266, 56, 30);
                g.FillEllipse(cheek, 362, 218, 56, 30);
                g.FillEllipse(cheek, 282, 303, 47, 13);
            }
        }
        return bitmap;
    }

    public static void Generate(string pngPath, string icoPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
        using (var bitmap = Draw(512)) bitmap.Save(pngPath, ImageFormat.Png);

        int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
        var images = sizes.Select(size =>
        {
            using (var bitmap = Draw(size))
            using (var stream = new MemoryStream())
            {
                bitmap.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }).ToArray();

        using (var output = new BinaryWriter(File.Create(icoPath)))
        {
            output.Write((short)0);
            output.Write((short)1);
            output.Write((short)sizes.Length);
            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte dimension = sizes[i] == 256 ? (byte)0 : (byte)sizes[i];
                output.Write(dimension);
                output.Write(dimension);
                output.Write((byte)0);
                output.Write((byte)0);
                output.Write((short)1);
                output.Write((short)32);
                output.Write(images[i].Length);
                output.Write(offset);
                offset += images[i].Length;
            }
            foreach (byte[] image in images) output.Write(image);
        }
    }
}
'@

Add-Type -TypeDefinition $generator -ReferencedAssemblies System.Drawing
[LiveWallLogoGenerator]::Generate(
    (Join-Path $PSScriptRoot '..\Assets\LiveWallLogo.png'),
    (Join-Path $PSScriptRoot '..\Assets\LiveWall.ico'))
