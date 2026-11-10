using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Kalories.Server.Services;

public static class ImageTools
{
    private const long MaxPixels = 40_000_000;
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // Comprueba el contenido real del archivo, no su nombre ni su Content-Type.
    public static async Task<bool> IsJpegOrPngAsync(IFormFile file)
    {
        var header = new byte[8];
        await using var input = file.OpenReadStream();
        var read = await input.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false);

        var isJpeg = read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = read >= 8 && header.AsSpan().SequenceEqual(PngSignature);
        return isJpeg || isPng;
    }

    // Devuelve un JPG nuevo, girado según su orientación, reducido y sin metadatos
    // (ubicación GPS, cámara, fecha...). Devuelve null si la imagen tiene demasiados píxeles.
    public static async Task<byte[]?> ToCleanJpegAsync(IFormFile file, int maxSide, int quality = 85)
    {
        ImageInfo info;
        await using (var probe = file.OpenReadStream())
        {
            info = await Image.IdentifyAsync(probe);
        }

        if ((long)info.Width * info.Height > MaxPixels)
            return null;

        await using var input = file.OpenReadStream();
        using var image = await Image.LoadAsync(input);

        var needsResize = Math.Max(image.Width, image.Height) > maxSide;

        image.Mutate(x =>
        {
            x.AutoOrient();
            x.BackgroundColor(Color.White);
            if (needsResize)
            {
                x.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxSide, maxSide)
                });
            }
        });

        image.Metadata.ExifProfile = null;
        image.Metadata.XmpProfile = null;
        image.Metadata.IptcProfile = null;
        image.Metadata.IccProfile = null;

        using var output = new MemoryStream();
        await image.SaveAsJpegAsync(output, new JpegEncoder { Quality = quality });
        return output.ToArray();
    }
}