namespace EmotionInstructor.Models;

public class EmotionLogEntry
{
    public string Timestamp { get; set; } = string.Empty;
    public int FaceId { get; set; }
    public string CnnEmotion { get; set; } = string.Empty;
    public string FerPlusEmotion { get; set; } = string.Empty;
    public string SelectedModel { get; set; } = string.Empty;
    public string FinalEmotion { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
}

public class DetectedFace
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public byte[] ImageData { get; set; } = Array.Empty<byte>();
}

public enum EmotionType
{
    Angry = 0,
    Disgust = 1,
    Fear = 2,
    Happy = 3,
    Sad = 4,
    Surprise = 5,
    Neutral = 6
}
