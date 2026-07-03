# 📱 Hướng Dẫn Setup Unity AR Camera Project

> **Project**: `camera_unity_ar` — Chụp ảnh & Ghim không gian với Cloud Anchors  
> **Unity**: 2022.3 LTS hoặc mới hơn | **Platform**: Android / iOS

---

## ✅ Trả lời nhanh: Có cần 3D Object thủ công để chứa ảnh không?

> **KHÔNG CẦN!** Code tự động tạo **Quad** (hình chữ nhật phẳng 3D) mỗi khi chụp ảnh.

### Cụ thể trong `PhotoSpatialAnchor.cs`:
```csharp
// Tự tạo Quad bằng code, không cần Prefab hay 3D object sẵn
GameObject photoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
photoQuad.transform.localScale = new Vector3(quadWidth, quadHeight, 1f);
photoQuad.GetComponent<Renderer>().material = TextureHelper.CreateUnlitMaterial(capturedTexture);
```

**Luồng hoạt động:**
1. Nhấn chụp → `ScreenCapture.CaptureScreenshotAsTexture()`
2. Code tự tạo `Quad` (3D primitive) và áp texture ảnh vừa chụp lên
3. Đặt Quad ở vị trí `1m trước camera` (có thể chỉnh `distanceFromCamera`)
4. Tạo `ARAnchor` và gắn Quad vào Anchor để nổi trong không gian

---

## 📦 BƯỚC 1 — Cài Đặt Package

### Yêu cầu Package Manager

Mở Unity → **Window → Package Manager → + → Add package from git URL...**

| Package | URL / Registry |
|---------|---------------|
| AR Foundation 5.x | `com.unity.xr.arfoundation` (Unity Registry) |
| ARCore XR Plugin 5.x | `com.unity.xr.arcore` (Unity Registry) |
| ARKit XR Plugin 5.x | `com.unity.xr.arkit` (Unity Registry) |
| **ARCore Extensions** | `https://github.com/google-ar/arcore-unity-extensions.git` |

> **Lưu ý**: `manifest.json` đã có sẵn cấu hình đúng. Khi mở project, Unity sẽ **tự tải** các package trên nếu có internet.

### Kiểm tra sau khi cài
Vào **Window → Package Manager**, tab **In Project**, đảm bảo thấy:
- ✅ AR Foundation
- ✅ ARCore XR Plugin
- ✅ ARKit XR Plugin
- ✅ Google ARCore Extensions

---

## ⚙️ BƯỚC 2 — Cấu Hình Project Settings

### 2A. XR Plug-in Management

**Edit → Project Settings → XR Plug-in Management**

| Tab | Cần bật |
|-----|---------|
| **Android** | ✅ ARCore |
| **iOS** | ✅ ARKit |

### 2B. ARCore Extensions (Cloud Anchors API Key)

**Edit → Project Settings → XR Plug-in Management → ARCore Extensions**

1. Chọn platform **Android** và **iOS**
2. **Authentication Strategy** → chọn `API Key`
3. Dán **Google Cloud API Key** vào:
   - `Android API Key`: `AIzaSy...`
   - `iOS API Key`: `AIzaSy...`
4. Phần **Cloud Anchor Mode** → `Enabled`

> ⚠️ Cần tạo API Key tại https://console.cloud.google.com và bật **ARCore API**.
> Với API Key, anchor chỉ tồn tại **24 giờ**. Muốn lâu hơn (365 ngày) cần cấu hình OAuth.

### 2C. Scripting Define Symbols — BẮT BUỘC ⚠️

**Edit → Project Settings → Player → [chọn tab Android/iOS] → Scripting Define Symbols**

Thêm:
```
ARCORE_EXTENSIONS_ENABLED
```

> **BẮT BUỘC** phải thêm symbol này! Nếu không, Cloud Anchor code bị `#if` tắt hoàn toàn — app vẫn chạy nhưng không dùng được Cloud Anchors.

### 2D. Android Build Settings

**Edit → Project Settings → Player → Android**

| Setting | Giá trị |
|---------|---------|
| Minimum API Level | **Android 7.0 (API 24)** trở lên |
| Target API Level | Android 13+ (khuyến nghị) |
| Scripting Backend | **IL2CPP** (bắt buộc cho ARCore) |
| Target Architectures | ✅ ARM64 |
| Internet Access | `Required` |
| Write Permission | `External (SDCard)` (để lưu ảnh) |

### 2E. iOS Build Settings

**Edit → Project Settings → Player → iOS → Other Settings**

| Usage Description | Nội dung |
|-------------------|---------|
| **Camera Usage Description** | `"App cần camera để hiển thị AR"` |
| **Location Usage Description** | `"App cần vị trí để lưu và khôi phục ảnh AR trong không gian thực"` |

> ⚠️ **Location là BẮT BUỘC nếu dùng Cloud Anchors trên iOS!**  
> Google ARCore Extensions dùng location data để tăng độ chính xác Host/Resolve.  
> Apple yêu cầu khai báo `NSLocationWhenInUseUsageDescription` trong `Info.plist`,  
> nếu thiếu → **app crash** hoặc **bị Apple từ chối** khi submit TestFlight/App Store.

Unity tự inject cả 2 key vào `Info.plist` khi export Xcode project.

---

## 🏗️ BƯỚC 3 — Cấu Hình Scene (1.unity)

Scene `Assets/1.unity` đã tồn tại. Bạn cần tạo **Hierarchy sau** trong scene:

```
Scene Hierarchy
├── 📦 XR Origin (hoặc AR Session Origin)
│   ├── AR Camera
│   │   └── [Camera + ARCameraManager + ARCameraBackground]
│   └── [ARAnchorManager component]
│
├── 📦 AR Manager GameObject  ← Script chính gắn vào đây
│   ├── ARSession              (component)
│   ├── ARCameraController     (script)
│   ├── ARPermissionHandler    (script — auto add qua RequireComponent)
│   ├── PhotoSpatialAnchor     (script)
│   ├── AnchorDataManager      (script — auto add qua RequireComponent)
│   └── ARCoreExtensions       (component — nếu dùng Cloud Anchors)
│
└── 📦 Canvas (Screen Space - Overlay)
    ├── PhotoCaptureUI         (script)
    ├── AnchorInfoPanel        (script)
    ├── [Button] CaptureButton
    ├── [Image] FlashPanel     (full-screen, bắt đầu ẩn)
    ├── [TextMeshPro] StatusText
    ├── [TextMeshPro] PhotoCountText
    ├── [Transform] ThumbnailContainer
    │   └── [HorizontalLayoutGroup]
    └── [Panel] InfoPanel      (bắt đầu ẩn)
        ├── [TextMeshPro] AnchorIdText
        ├── [TextMeshPro] CreatedAtText
        ├── [TextMeshPro] PositionText
        ├── [TextMeshPro] TrackingStateText
        ├── [Button] DeleteButton
        └── [Button] CloseButton
```

---

## 🔗 BƯỚC 4 — Gán Reference Trong Inspector

### 4A. AR Manager GameObject — PhotoSpatialAnchor

| Inspector Field | Gán gì |
|----------------|--------|
| **Anchor Manager** | `ARAnchorManager` component (từ XR Origin) |
| **Ar Camera** | Camera AR (AR Camera child của XR Origin) |
| **Camera Controller** | `ARCameraController` script trên cùng GO |
| **Use Cloud Anchors** | ✅ Tick nếu muốn Cloud |
| **Arcore Extensions** | `ARCoreExtensions` component trên cùng GO |
| **Distance From Camera** | `1.0` (mét — khoảng cách Quad xuất hiện) |
| **Quad Height** | `0.5` (mét — chiều cao ảnh trong AR) |
| **Persist To Disk** | ✅ Tick |

### 4B. Canvas — PhotoCaptureUI

| Inspector Field | Gán gì |
|----------------|--------|
| **Photo Anchor** | `PhotoSpatialAnchor` script (trên AR Manager GO) |
| **Camera Controller** | `ARCameraController` script |
| **Capture Button** | Button chụp ảnh |
| **Capture Button Icon** | Image icon trên button |
| **Flash Panel** | Panel trắng phủ full màn hình |
| **Status Text** | TextMeshPro hiện trạng thái |
| **Photo Count Text** | TextMeshPro đếm số ảnh |
| **Thumbnail Container** | Transform chứa thumbnails |
| **Thumbnail Prefab** | Prefab thumbnail (tạo thủ công — xem Bước 5) |
| **Max Thumbnails** | `5` (mặc định) |
| **Clear All Button** | Button xóa tất cả |

### 4C. Canvas — AnchorInfoPanel

| Inspector Field | Gán gì |
|----------------|--------|
| **Ar Camera** | AR Camera |
| **Photo Layer** | Everything (mặc định) hoặc Layer riêng cho Quad |
| **Info Panel** | Panel GO (ẩn/hiện khi tap) |
| **Anchor Id Text** | TextMeshPro |
| **Created At Text** | TextMeshPro |
| **Position Text** | TextMeshPro |
| **Tracking State Text** | TextMeshPro |
| **Delete Button** | Button xóa anchor |
| **Close Button** | Button đóng panel |
| **Photo Anchor** | `PhotoSpatialAnchor` script |

---

## 🖼️ BƯỚC 5 — Tạo Thumbnail Prefab (Thủ Công Duy Nhất)

> Đây là **object duy nhất bạn cần tạo thủ công** — Prefab UI nhỏ để hiển thị strip ảnh ở góc màn hình.

1. **Hierarchy** → chuột phải → `UI → Raw Image`
2. Đặt tên là `ThumbnailPrefab`
3. **RectTransform**: Width `80`, Height `80`
4. (Tùy chọn) Thêm `Image` làm background border
5. Kéo GameObject vào thư mục `Assets/` để tạo **Prefab**
6. Xóa instance khỏi scene
7. Gán Prefab này vào field **Thumbnail Prefab** trong `PhotoCaptureUI` Inspector

---

## 📋 BƯỚC 6 — Checklist Cuối Trước Khi Build

- [ ] Package AR Foundation, ARCore, ARKit đã cài (xem manifest.json)
- [ ] ARCore Extensions đã cài từ git URL
- [ ] `ARCORE_EXTENSIONS_ENABLED` đã thêm vào Scripting Define Symbols
- [ ] API Key Google đã dán vào ARCore Extensions settings
- [ ] XR Plug-in: Android → ARCore ✅, iOS → ARKit ✅
- [ ] Android: IL2CPP, ARM64, API 24+, Internet Access = Required
- [ ] Scene có: XR Origin + AR Camera + ARAnchorManager
- [ ] `ARCameraController` + `ARPermissionHandler` + `PhotoSpatialAnchor` trên cùng 1 GO
- [ ] `ARCoreExtensions` component đã add và gán vào slot trong PhotoSpatialAnchor
- [ ] Tất cả Inspector references đã gán (không có `None (Missing)`)
- [ ] Thumbnail Prefab đã tạo và gán

---

## ❓ FAQ

**Q: Có cần tạo 3D Object/Model thủ công để hiển thị ảnh không?**  
Không. Script tự `CreatePrimitive(PrimitiveType.Quad)` và áp texture. Chỉ cần cấu hình Inspector.

**Q: Ảnh lưu ở đâu trên thiết bị?**  
File JPG + JSON tại `Application.persistentDataPath`:
- Android: `/storage/emulated/0/Android/data/<packagename>/files/`
- iOS: trong app sandbox

**Q: Tại sao Cloud Anchor host thất bại?**  
1. Kiểm tra API Key đúng và đã bật ARCore API trong Google Cloud Console
2. Thiết bị cần scan không gian vài giây trước khi chụp (ARCore cần feature points)
3. Kiểm tra kết nối internet

**Q: Cloud Anchor Resolve thất bại khi mở lại app?**  
Anchor dùng API Key chỉ tồn tại **24 giờ**. Sau đó resolve lỗi và file JSON bị xóa.

**Q: `ARCoreExtensions` component thêm vào đâu?**  
Add component vào **cùng GameObject** với `PhotoSpatialAnchor`, sau đó kéo component đó vào slot `Arcore Extensions` trong Inspector của PhotoSpatialAnchor.

---

## 🚀 Test Nhanh Trên Thiết Bị

```
File → Build Settings → Android → Switch Platform → Build And Run
```

Khi app khởi động:
1. App tự xin quyền Camera
2. Hiện `"Đang khởi động AR..."` (màu vàng)
3. Sau 1-3s → `"Sẵn sàng! Nhấn nút để chụp ảnh."` (màu xanh)
4. Nhấn nút chụp → ảnh nổi trong không gian AR
5. Tap vào ảnh → hiện panel thông tin + nút xóa
