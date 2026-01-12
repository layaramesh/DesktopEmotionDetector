using System.Drawing;
using System.IO;
using EmotionInstructor.Models;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace EmotionInstructor.Services;

public class FaceDetectionService
{
    private readonly CascadeClassifier _faceCascade;
    private readonly string _cascadePath;

    public FaceDetectionService(string cascadePath)
    {
        if (string.IsNullOrEmpty(cascadePath))
        {
            throw new ArgumentNullException(nameof(cascadePath), "Cascade path cannot be null or empty");
        }
        
        if (!File.Exists(cascadePath))
        {
            throw new FileNotFoundException($"Cascade file not found at: {cascadePath}");
        }
        
        _cascadePath = cascadePath;
        
        try
        {
            _faceCascade = new CascadeClassifier(cascadePath);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to initialize CascadeClassifier with file: {cascadePath}. OpenCvSharp native libraries may not be loaded correctly.", ex);
        }
        
        if (_faceCascade.Empty())
        {
            throw new Exception($"CascadeClassifier loaded but is empty. File may be corrupted: {cascadePath}");
        }
    }

    public List<DetectedFace> DetectFaces(Bitmap screenshot)
    {
        var faces = new List<DetectedFace>();

        using var mat = BitmapConverter.ToMat(screenshot);
        using var gray = new Mat();
        Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gray, gray);

        // Balanced detection parameters to avoid false positives
        var detectedFaces = _faceCascade.DetectMultiScale(
            gray,
            scaleFactor: 1.1,        // Increased back to 1.1 for better accuracy
            minNeighbors: 5,         // Increased to 5 to reduce false positives
            flags: HaarDetectionTypes.ScaleImage,
            minSize: new OpenCvSharp.Size(30, 30),  // Increased to 30x30 to avoid tiny false positives
            maxSize: new OpenCvSharp.Size(500, 500)
        );

        System.Diagnostics.Debug.WriteLine($"Detected {detectedFaces.Length} faces in screenshot");
        
        // Merge overlapping detections (same face detected multiple times)
        var filteredFaces = MergeOverlappingFaces(detectedFaces);

        for (int i = 0; i < filteredFaces.Count; i++)
        {
            var face = filteredFaces[i];
            
            // Extract face region
            var faceRect = new Rect(face.X, face.Y, face.Width, face.Height);
            using var faceMat = new Mat(mat, faceRect);
            using var faceBitmap = BitmapConverter.ToBitmap(faceMat);
            
            // Convert to byte array
            using var ms = new MemoryStream();
            faceBitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            
            faces.Add(new DetectedFace
            {
                X = face.X,
                Y = face.Y,
                Width = face.Width,
                Height = face.Height,
                ImageData = ms.ToArray()
            });
        }

        return faces;
    }

    private List<Rect> MergeOverlappingFaces(Rect[] faces)
    {
        if (faces.Length == 0)
            return new List<Rect>();

        // Group overlapping rectangles
        var merged = new List<Rect>();
        var used = new bool[faces.Length];

        for (int i = 0; i < faces.Length; i++)
        {
            if (used[i])
                continue;

            var current = faces[i];
            used[i] = true;

            // Find all overlapping faces
            for (int j = i + 1; j < faces.Length; j++)
            {
                if (used[j])
                    continue;

                // Check if faces overlap significantly
                var intersection = current.Intersect(faces[j]);
                var unionArea = current.Width * current.Height + faces[j].Width * faces[j].Height - intersection.Width * intersection.Height;
                var iou = unionArea > 0 ? (float)(intersection.Width * intersection.Height) / unionArea : 0;

                // If IoU > 0.3, consider them the same face and use the larger one
                if (iou > 0.3f)
                {
                    used[j] = true;
                    // Keep the larger bounding box
                    if (faces[j].Width * faces[j].Height > current.Width * current.Height)
                    {
                        current = faces[j];
                    }
                }
            }

            merged.Add(current);
        }

        return merged;
    }

    public void Dispose()
    {
        _faceCascade?.Dispose();
    }
}
