# High Definition Screen Capture (Unity)

Utility to capture **ultra high-resolution screenshots** in Unity by splitting the camera render into tiles, then merging them into a final PNG (with optional transparency and compression).  
Built on **UnityEngine** and [Pngcs](https://github.com/leonbloy/pngcs).

---

## ✨ Features
- Capture screenshots **far beyond GPU texture limits** (e.g. 64k+ resolution).  
- Support for **perspective** and **orthographic** cameras.  
- **Transparency support** (alpha channel, no skybox).  
- **Supersampling (2x/4x)** for better antialiasing.  
- Progress callbacks for both **tile rendering** and **merging**.  
- Returns the final screenshot as a **PNG byte array**.  
- Provides a helper to get **common resolutions** up to 131k.

---

## 📦 Installation
You can install this package directly from GitHub via Unity’s **Package Manager**:

1. In Unity, open `Window > Package Manager`.
2. Click `+` > `Add package from git URL`.
3. Enter:

   ```
   https://github.com/Keksls/unity_highdefinition_screencapture.git
   ```

---

## 🚀 Usage

⚠️ **Important:** `HDScreenCapture` is a `MonoBehaviour` singleton. You must place it **somewhere in your scene** (e.g. an empty GameObject) before calling `Capture`.

```csharp
using UnityEngine;
using HDSC;

public class ScreenshotExample : MonoBehaviour
{
    public Camera targetCamera;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F12))
        {
            HDScreenCapture.Instance.Capture(
                targetCamera,
                8192, 4320,                  // width, height
                supersample: true,           // 2x supersampling
                transparentBG: false,        // skybox background
                pngCompression: 6,           // PNG compression level (0-9)
                onRendered: (pngBytes) =>
                {
                    System.IO.File.WriteAllBytes("screenshot.png", pngBytes);
                    Debug.Log("Screenshot saved!");
                },
                onRendering: (stage, progress) =>
                {
                    // stage: 0 = capturing tiles, 1 = merging
                    Debug.Log($"Stage {stage} progress: {progress:P}");
                }
            );
        }
    }
}
```

---

## 📐 Common Resolutions

You can query pre-defined resolutions:

```csharp
var resList = HDScreenCapture.Instance.GetCommonResolutions();
foreach (var res in resList)
    Debug.Log($"{res.width} x {res.height}");
```

---

## 📝 License
MIT License – see [LICENSE](LICENSE).

---

## 👤 Author
- **Kévin Bouetard** – [vrdtmstudio.com](https://vrdtmstudio.com)  
- GitHub: [@Keksls](https://github.com/Keksls)  

---

## 🙏 Acknowledgements
- [Pngcs](https://github.com/leonbloy/pngcs) – PNG encoder/decoder in pure C#.
