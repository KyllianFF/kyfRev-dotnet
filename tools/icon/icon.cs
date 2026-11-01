#:property TargetFramework=net10.0-windows
#:property UseWPF=true
#:property ImplicitUsings=enable
#:property PublishAot=false

// Génère l'icône et les images de l'installateur depuis assets/icon/kyfrev.svg, leur source unique
// (voir assets/icon/README.md).
// Usage, à la racine du dépôt : dotnet run tools/icon/icon.cs
// Écrit assets/icon/kyfrev.ico (16 à 256 px) et, dans installer/images, les images de l'assistant
// pour chaque échelle d'affichage de 100 à 250 %.
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

var thread = new Thread(() => Generate(args.Length > 0 ? args[0] : "."));
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();

static void Generate(string root)
{
    var svg = XDocument.Load(Path.Combine(root, "assets", "icon", "kyfrev.svg")).Root!;
    WriteIcon(Path.Combine(root, "assets", "icon", "kyfrev.ico"), svg);

    var images = Path.Combine(root, "installer", "images");
    Directory.CreateDirectory(images);
    foreach (var size in Sizes.Small)
    {
        Save(Render(size, size, dc => DrawIcon(dc, svg, size, 0, 0, size)), Path.Combine(images, $"petit-{size}.png"));
    }
    foreach (var (width, height) in Sizes.Large)
    {
        Save(Render(width, height, dc => DrawPanel(dc, svg, width, height)), Path.Combine(images, $"assistant-{width}.png"));
    }
}

// Le panneau de l'assistant : l'éventail sur un fond forêt, sur de grandes fiches découpées dans le
// même vert. Dessiné sur la grille de 164 × 314 de ce panneau.
static void DrawPanel(DrawingContext dc, XElement svg, int width, int height)
{
    dc.PushTransform(new ScaleTransform(width / 164.0, height / 314.0));
    dc.DrawRectangle(Colored("#234F3B"), null, new Rect(0, 0, 164, 314));
    dc.PushTransform(new RotateTransform(-13, 40, 300));
    dc.DrawRoundedRectangle(Colored("#2E6A4E"), null, new Rect(-34, 206, 150, 190), 8, 8);
    dc.Pop();
    dc.PushTransform(new RotateTransform(11, 124, 300));
    dc.DrawRoundedRectangle(Colored("#357A57"), null, new Rect(58, 224, 150, 190), 8, 8);
    dc.Pop();
    dc.Pop();
    DrawIcon(dc, svg, width, width * 14 / 164.0, width * 62 / 164.0, width * 136 / 164.0);
}

static void DrawIcon(DrawingContext dc, XElement svg, int pixels, double x, double y, double size)
{
    dc.PushTransform(new TranslateTransform(x, y));
    dc.PushTransform(new ScaleTransform(size / 256, size / 256));
    DrawElements(dc, Variant(svg, pixels));
    dc.Pop();
    dc.Pop();
}

// La version simplifiée pour cette taille : le plus petit groupe qui la couvre, sinon le dessin principal.
static XElement Variant(XElement svg, int pixels)
{
    var groups = svg.Elements().Where(e => e.Name.LocalName == "defs").Elements()
        .Select(group => (Size: int.Parse(((string)group.Attribute("id")!)["taille-".Length..], CultureInfo.InvariantCulture), Group: group))
        .OrderBy(entry => entry.Size);
    foreach (var (size, group) in groups)
    {
        if (pixels <= size)
        {
            return group;
        }
    }
    return svg;
}

static void DrawElements(DrawingContext dc, XElement parent)
{
    foreach (var element in parent.Elements())
    {
        var name = element.Name.LocalName;
        if (name is not ("rect" or "circle" or "path"))
        {
            continue;
        }
        var rotated = Rotation(element);
        if (rotated is not null)
        {
            dc.PushTransform(rotated);
        }
        var fill = Fill(element);
        var pen = Outline(element);
        switch (name)
        {
            case "rect":
                var radius = Number(element, "rx");
                dc.DrawRoundedRectangle(fill, pen, new Rect(Number(element, "x"), Number(element, "y"), Number(element, "width"), Number(element, "height")), radius, radius);
                break;
            case "circle":
                var r = Number(element, "r");
                dc.DrawEllipse(fill, pen, new Point(Number(element, "cx"), Number(element, "cy")), r, r);
                break;
            default:
                dc.DrawGeometry(fill, pen, Geometry.Parse((string)element.Attribute("d")!));
                break;
        }
        if (rotated is not null)
        {
            dc.Pop();
        }
    }
}

static double Number(XElement element, string name) =>
    double.Parse((string?)element.Attribute(name) ?? "0", CultureInfo.InvariantCulture);

static RotateTransform? Rotation(XElement element)
{
    if ((string?)element.Attribute("transform") is not { } transform)
    {
        return null;
    }
    var parts = transform["rotate(".Length..^1].Split(' ');
    return new RotateTransform(
        double.Parse(parts[0], CultureInfo.InvariantCulture),
        double.Parse(parts[1], CultureInfo.InvariantCulture),
        double.Parse(parts[2], CultureInfo.InvariantCulture));
}

static SolidColorBrush? Fill(XElement element) =>
    (string?)element.Attribute("fill") is { } color and not "none" ? Colored(color) : null;

static Pen? Outline(XElement element) =>
    (string?)element.Attribute("stroke") is { } color
        ? new Pen(Colored(color), Number(element, "stroke-width")) { LineJoin = PenLineJoin.Round }
        : null;

static SolidColorBrush Colored(string color) => new((Color)ColorConverter.ConvertFromString(color));

static BitmapSource Render(int width, int height, Action<DrawingContext> draw)
{
    var visual = new DrawingVisual();
    using (var dc = visual.RenderOpen())
    {
        draw(dc);
    }
    var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
    bitmap.Render(visual);
    return new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
}

static void Save(BitmapSource image, string path)
{
    using var file = File.Create(path);
    file.Write(Png(image));
}

static byte[] Png(BitmapSource image)
{
    var encoder = new PngBitmapEncoder();
    encoder.Frames.Add(BitmapFrame.Create(image));
    using var stream = new MemoryStream();
    encoder.Save(stream);
    return stream.ToArray();
}

// Un .ico classique : des images BGRA avec leur masque jusqu'à 128 px, en PNG à 256 px.
static void WriteIcon(string path, XElement svg)
{
    var frames = Sizes.Icon
        .Select(size => (Size: size, Bytes: Encoded(Render(size, size, dc => DrawIcon(dc, svg, size, 0, 0, size)), size)))
        .ToList();
    using var file = File.Create(path);
    using var writer = new BinaryWriter(file);
    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)frames.Count);
    var offset = 6 + (16 * frames.Count);
    foreach (var (size, bytes) in frames)
    {
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(bytes.Length);
        writer.Write(offset);
        offset += bytes.Length;
    }
    foreach (var (_, bytes) in frames)
    {
        writer.Write(bytes);
    }
}

static byte[] Encoded(BitmapSource image, int size)
{
    if (size >= 256)
    {
        return Png(image);
    }
    var stride = size * 4;
    var pixels = new byte[stride * size];
    image.CopyPixels(pixels, stride, 0);
    var maskStride = ((size + 31) / 32) * 4;
    using var stream = new MemoryStream();
    using var writer = new BinaryWriter(stream);
    writer.Write(40);
    writer.Write(size);
    writer.Write(size * 2);
    writer.Write((ushort)1);
    writer.Write((ushort)32);
    writer.Write(0);
    writer.Write(pixels.Length + (maskStride * size));
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    // Les lignes vont du bas vers le haut.
    for (var row = size - 1; row >= 0; row--)
    {
        writer.Write(pixels, row * stride, stride);
    }
    for (var row = size - 1; row >= 0; row--)
    {
        var mask = new byte[maskStride];
        for (var column = 0; column < size; column++)
        {
            if (pixels[(row * stride) + (column * 4) + 3] == 0)
            {
                mask[column / 8] |= (byte)(0x80 >> (column % 8));
            }
        }
        writer.Write(mask);
    }
    writer.Flush();
    return stream.ToArray();
}

internal static class Sizes
{
    public static readonly int[] Icon = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256];

    // Les zones d'image de l'assistant moderne d'Inno Setup, de 100 à 250 %.
    public static readonly int[] Small = [58, 77, 97, 116, 124, 143, 159];

    public static readonly (int Width, int Height)[] Large = [(202, 386), (269, 515), (336, 643), (403, 772), (430, 824), (498, 953), (534, 1022)];
}
