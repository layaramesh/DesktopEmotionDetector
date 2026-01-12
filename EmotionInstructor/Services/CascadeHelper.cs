using System.IO;
using System.Net.Http;

namespace EmotionInstructor.Services;

public static class CascadeHelper
{
    private const string HAAR_CASCADE_URL = "https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_frontalface_default.xml";
    
    public static async Task<string> EnsureCascadeFileExists()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var cascadePath = Path.Combine(baseDir, "haarcascade_frontalface_default.xml");
        
        if (!File.Exists(cascadePath))
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                var cascadeData = await client.GetStringAsync(HAAR_CASCADE_URL);
                await File.WriteAllTextAsync(cascadePath, cascadeData);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to download Haar Cascade file from {HAAR_CASCADE_URL}. Please download it manually and place it in {baseDir}", ex);
            }
        }
        
        // Verify file exists and is readable
        if (!File.Exists(cascadePath))
        {
            throw new FileNotFoundException($"Cascade file not found at: {cascadePath}");
        }
        
        // Verify file has content
        var fileInfo = new FileInfo(cascadePath);
        if (fileInfo.Length == 0)
        {
            File.Delete(cascadePath);
            throw new Exception($"Cascade file is empty. Deleted it. Please try again.");
        }
        
        return cascadePath;
    }
}
