using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Bitmap = Avalonia.Media.Imaging.Bitmap;

namespace TimerTone.Core;

public class ExeIconsService
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int ExtractIconEx(string lpszFile, int nIconIndex,
        IntPtr[] phiconLarge, IntPtr[] phiconSmall, int nIcons);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public async Task<Bitmap?> GetExeIcon(string exePath)
    {
        return await Task.Run(() =>
        {
            if (!File.Exists(exePath))
                return null;

            var largeIcons = new IntPtr[1];
            var smallIcons = new IntPtr[1];

            int count = ExtractIconEx(exePath, 0, largeIcons, smallIcons, 1);
            if (count == 0 || largeIcons[0] == IntPtr.Zero)
                return null;

            try
            {
                using var icon = Icon.FromHandle(largeIcons[0]);
                using var bmp = icon.ToBitmap();
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                return new Bitmap(ms);
            }
            finally
            {
                if (largeIcons[0] != IntPtr.Zero)
                    DestroyIcon(largeIcons[0]);
                if (smallIcons[0] != IntPtr.Zero)
                    DestroyIcon(smallIcons[0]);
            }
        });
    }
}