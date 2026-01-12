using System.Drawing;
using System.IO;
using EmotionInstructor.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace EmotionInstructor.Services;

public class EmotionDetectionService : IDisposable
{
    private readonly InferenceSession _cnnSession;
    private readonly InferenceSession _ferPlusSession;
    
    private readonly string[] _emotionLabels = new[]
    {
        "Angry", "Disgust", "Fear", "Happy", "Sad", "Surprise", "Neutral"
    };

    // FerPlus uses different order
    private readonly string[] _ferPlusLabels = new[]
    {
        "Neutral", "Happy", "Surprise", "Sad", "Angry", "Disgust", "Fear", "Contempt"
    };

    public EmotionDetectionService(string cnnModelPath, string ferPlusModelPath)
    {
        _cnnSession = new InferenceSession(cnnModelPath);
        _ferPlusSession = new InferenceSession(ferPlusModelPath);
        
        // Log model input/output metadata for debugging
        System.Diagnostics.Debug.WriteLine("=== CNN Model Metadata ===");
        foreach (var input in _cnnSession.InputMetadata)
        {
            System.Diagnostics.Debug.WriteLine($"Input: {input.Key}, Shape: {string.Join(",", input.Value.Dimensions)}");
        }
        
        System.Diagnostics.Debug.WriteLine("=== FerPlus Model Metadata ===");
        foreach (var input in _ferPlusSession.InputMetadata)
        {
            System.Diagnostics.Debug.WriteLine($"Input: {input.Key}, Shape: {string.Join(",", input.Value.Dimensions)}");
        }
    }

    public (string cnnEmotion, string ferPlusEmotion, string finalEmotion, string selectedModel, string classification) 
        PredictEmotion(byte[] faceImageData)
    {
        // Predict with both models
        var cnnEmotion = PredictWithCNN(faceImageData);
        var ferPlusEmotion = PredictWithFerPlus(faceImageData);

        // Apply hybrid model strategy
        string selectedModel;
        string finalEmotion;

        if (ferPlusEmotion == "Happy")
        {
            // Use FerPlus for Happy (better precision for "Proceed Ahead")
            selectedModel = "FerPlus";
            finalEmotion = ferPlusEmotion;
        }
        else
        {
            // Use CNN for Neutral and all other emotions
            selectedModel = "CNN";
            finalEmotion = cnnEmotion;
        }

        // Binary classification
        string classification = (finalEmotion == "Happy" || finalEmotion == "Neutral") 
            ? "Proceed Ahead" 
            : "Needs Help";

        return (cnnEmotion, ferPlusEmotion, finalEmotion, selectedModel, classification);
    }

    private string PredictWithCNN(byte[] faceImageData)
    {
        try
        {
            using var ms = new MemoryStream(faceImageData);
            using var bitmap = new Bitmap(ms);
            
            // Preprocess for CNN model (48x48 grayscale)
            var tensor = PreprocessForCNN(bitmap);
            
            // Get the actual input name from the model
            var inputName = _cnnSession.InputMetadata.Keys.First();
            
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            using var results = _cnnSession.Run(inputs);
            var output = results.First().AsEnumerable<float>().ToArray();
            
            if (output == null || output.Length == 0)
            {
                return "Unknown";
            }
            
            int maxIndex = Array.IndexOf(output, output.Max());
            
            if (maxIndex < 0 || maxIndex >= _emotionLabels.Length)
            {
                return "Unknown";
            }
            
            return _emotionLabels[maxIndex];
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            System.Diagnostics.Debug.WriteLine($"CNN Prediction Error: {ex.Message}");
            return $"Error: {ex.Message.Substring(0, Math.Min(20, ex.Message.Length))}";
        }
    }

    private string PredictWithFerPlus(byte[] faceImageData)
    {
        try
        {
            using var ms = new MemoryStream(faceImageData);
            using var bitmap = new Bitmap(ms);
            
            // Preprocess for FerPlus model (64x64 grayscale)
            var tensor = PreprocessForFerPlus(bitmap);
            
            // Get the actual input name from the model
            var inputName = _ferPlusSession.InputMetadata.Keys.First();
            
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, tensor)
            };

            using var results = _ferPlusSession.Run(inputs);
            var output = results.First().AsEnumerable<float>().ToArray();
            
            if (output == null || output.Length == 0)
            {
                return "Unknown";
            }
            
            int maxIndex = Array.IndexOf(output, output.Max());
            
            if (maxIndex < 0 || maxIndex >= _ferPlusLabels.Length)
            {
                return "Unknown";
            }
            
            // Map FerPlus labels to standard emotion labels
            var ferPlusEmotion = _ferPlusLabels[maxIndex];
            
            // Convert Contempt to Disgust for consistency
            if (ferPlusEmotion == "Contempt")
                return "Disgust";
                
            return ferPlusEmotion;
        }
        catch (Exception ex)
        {
            // Log the error for debugging
            System.Diagnostics.Debug.WriteLine($"FerPlus Prediction Error: {ex.Message}");
            return $"Error: {ex.Message.Substring(0, Math.Min(20, ex.Message.Length))}";
        }
    }

    private DenseTensor<float> PreprocessForCNN(Bitmap bitmap)
    {
        // CNN expects 48x48 grayscale image with pixel values [0, 255]
        using var mat = BitmapConverter.ToMat(bitmap);
        using var gray = new Mat();
        using var resized = new Mat();
        
        Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
        
        // Apply histogram equalization for better contrast
        Cv2.EqualizeHist(gray, gray);
        
        // Resize to 48x48 using cubic interpolation
        Cv2.Resize(gray, resized, new OpenCvSharp.Size(48, 48), 0, 0, InterpolationFlags.Cubic);
        
        var tensor = new DenseTensor<float>(new[] { 1, 1, 48, 48 });
        
        for (int y = 0; y < 48; y++)
        {
            for (int x = 0; x < 48; x++)
            {
                // CNN expects raw pixel values [0, 255], not normalized
                tensor[0, 0, y, x] = resized.At<byte>(y, x);
            }
        }
        
        return tensor;
    }

    private DenseTensor<float> PreprocessForFerPlus(Bitmap bitmap)
    {
        // FerPlus expects 64x64 grayscale image with pixel values [0, 255]
        using var mat = BitmapConverter.ToMat(bitmap);
        using var gray = new Mat();
        using var resized = new Mat();
        
        Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
        
        // Apply histogram equalization for better contrast
        Cv2.EqualizeHist(gray, gray);
        
        // Resize to 64x64 using cubic interpolation for better quality
        Cv2.Resize(gray, resized, new OpenCvSharp.Size(64, 64), 0, 0, InterpolationFlags.Cubic);
        
        var tensor = new DenseTensor<float>(new[] { 1, 1, 64, 64 });
        
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                // FerPlus expects raw pixel values [0, 255], not normalized
                tensor[0, 0, y, x] = resized.At<byte>(y, x);
            }
        }
        
        return tensor;
    }

    public void Dispose()
    {
        _cnnSession?.Dispose();
        _ferPlusSession?.Dispose();
    }
}
