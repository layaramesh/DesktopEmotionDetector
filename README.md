# DesktopEmotionDetector

A Windows WPF desktop app that monitors faces on screen every 5 seconds, detects emotions with dual ONNX models, and flags students who may need help. Built from the research outcome of [CodeWiseFacialRecognitionCNN](https://github.com/layaramesh/CodeWiseFacialRecognitionCNN).

EXAMPLE USAGE:
[![Watch the video](https://github.com/layaramesh/DesktopEmotionDetector/blob/main/thumbnail.png)](https://github.com/layaramesh/DesktopEmotionDetector/blob/main/emotiondetectorexample.mp4)

## 🚀 Quick Start

```powershell
cd "C:\Users\laya_\OneDrive\Documents\GitHub\DesktopEmotionDetector\EmotionInstructor"
dotnet build
dotnet run
```

If the ONNX files are not copied automatically, place them in `bin\Debug\net8.0-windows\`:

```powershell
Copy-Item "..\Models\emotion_cnn.onnx" "bin\Debug\net8.0-windows\"
Copy-Item "..\Models\emotion-ferplus-8.onnx" "bin\Debug\net8.0-windows\"
```

## 🎯 Features

- Real-time screen monitoring every 5 seconds with multi-face detection (Haar Cascades via OpenCV)
- Hybrid emotion detection: `emotion_cnn.onnx` for “Needs Help”, `emotion-ferplus-8.onnx` for “Proceed Ahead”
- Binary classification: **Proceed Ahead** (Happy, Neutral) vs **Needs Help** (Fear, Anger, Disgust, Sadness, Surprise)
- Alerting: when 5+ entries are “Needs Help,” a flashing banner appears; **Stop Flashing** halts it, **Clear Data** resets log and counter
- Comprehensive logging: timestamp, face ID, CNN emotion, FerPlus emotion, selected model, final emotion, classification (color-coded)

## 🧠 Hybrid Model Strategy (92.24% Accuracy)

- If predicted emotion is Happy or Neutral → use `emotion-ferplus-8.onnx` (precision for “Proceed Ahead”)
- Otherwise → use `emotion_cnn.onnx` (recall for “Needs Help”)

## 🧩 Architecture

```
DesktopEmotionDetector/
├── Models/                      # ONNX models (copied to output on build or manually)
│   ├── emotion_cnn.onnx        # CNN model (better recall for “Needs Help”)
│   └── emotion-ferplus-8.onnx  # FerPlus model (better precision for “Proceed Ahead”)
└── EmotionInstructor/          # WPF app source
    ├── App.xaml, App.xaml.cs
    ├── MainWindow.xaml, MainWindow.xaml.cs
    ├── Models/
    └── Services/ (Screen capture, Face detection, Emotion detection, Cascade helper)
```

## 🛠 Requirements

- Windows 10/11
- .NET 8.0 Runtime
- ONNX model files in the application directory (`emotion_cnn.onnx`, `emotion-ferplus-8.onnx`)

## 📦 Dependencies

- Microsoft.ML.OnnxRuntime
- OpenCvSharp4 (+ Extensions, runtime.win)
- System.Drawing.Common

## 📈 How to Use

1. Start Monitoring: begin capture/analysis every 5 seconds.
2. View Results: data grid shows per-face predictions and classification.
3. Alerts: after 5 “Needs Help” entries, alert banner flashes; use **Stop Flashing** or **Clear Data**.
4. Stop Monitoring: cleanly stop capture and analysis.

## 📝 Notes

- Screen capture targets the primary monitor.
- Log retention keeps the latest 1000 entries.
- All processing is local; no data leaves the machine.

## 📜 License

This application builds upon research from the CodeWiseFacialRecognitionCNN project.
