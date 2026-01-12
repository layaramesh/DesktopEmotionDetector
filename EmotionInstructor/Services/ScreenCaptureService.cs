using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace EmotionInstructor.Services;

public class ScreenCaptureService
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight,
        IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private const int SRCCOPY = 0x00CC0020;

    public Bitmap CaptureScreen()
    {
        int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
        int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

        IntPtr desktopHandle = GetDesktopWindow();
        IntPtr desktopDC = GetDC(desktopHandle);
        IntPtr memoryDC = CreateCompatibleDC(desktopDC);
        IntPtr bitmap = CreateCompatibleBitmap(desktopDC, screenWidth, screenHeight);
        IntPtr oldBitmap = SelectObject(memoryDC, bitmap);

        BitBlt(memoryDC, 0, 0, screenWidth, screenHeight, desktopDC, 0, 0, SRCCOPY);

        SelectObject(memoryDC, oldBitmap);
        DeleteDC(memoryDC);
        ReleaseDC(desktopHandle, desktopDC);

        Bitmap screenshot = Image.FromHbitmap(bitmap);
        DeleteObject(bitmap);

        return screenshot;
    }

    public byte[] BitmapToByteArray(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }
}
