// ============================================================
//  PhotoSpatialAnchor.cs
//  Mo ta: Chup anh, ghim cuc bo, HOST len Google Cloud Anchors,
//         va RESOLVE lai nhung anh tu phien truoc.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using CameraAR.Utils;

// FIX #3: Bo '|| true' - phai kiem tra symbol thuc su, khong duoc force-include.
// Neu khong co ARCore Extensions package, viec include namespace se gay compile error.
// De su dung Cloud Anchors, them define symbol 'ARCORE_EXTENSIONS_ENABLED'
// trong: Project Settings -> Player -> Scripting Define Symbols
#if ARCORE_EXTENSIONS_ENABLED
using Google.XR.ARCoreExtensions;
#endif

namespace CameraAR.Core
{
    [RequireComponent(typeof(AnchorDataManager))]
    public class PhotoSpatialAnchor : MonoBehaviour
    {
        [Header("AR Components")]
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private Camera arCamera;
        [SerializeField] private ARCameraController cameraController;

        [Header("Google ARCore Cloud Anchors")]
        [Tooltip("True neu ban muon host/resolve anchor voi Google Cloud")]
        [SerializeField] private bool useCloudAnchors = true;

        // FIX #4: ARAnchorManagerExtensions la class cu da deprecated.
        // Class dung trong ARCore Extensions moi la 'ARCoreExtensions'.
        // Thay the toan bo bang ARCoreExtensions.
#if ARCORE_EXTENSIONS_ENABLED
        [Tooltip("Keo ARCoreExtensions component vao day (thay the ARAnchorManagerExtensions da deprecated)")]
        [SerializeField] private ARCoreExtensions arcoreExtensions;
#endif

        [Header("Cai dat hien thi")]
        [SerializeField] private float distanceFromCamera = 1.0f;
        [SerializeField] private float quadHeight = 0.5f;
        [SerializeField] private bool persistToDisk = true;

        public event System.Action<AnchorData> OnPhotoPinned;
        public event System.Action<string> OnPinError;

        private AnchorDataManager _dataManager;
        private Dictionary<string, ARAnchor> _runtimeAnchors = new Dictionary<string, ARAnchor>();
        private bool _isCapturing = false;

        private void Awake()
        {
            _dataManager = GetComponent<AnchorDataManager>();
            if (arCamera == null) arCamera = Camera.main;

#if ARCORE_EXTENSIONS_ENABLED
            // Tu dong tim ARCoreExtensions tren cung GameObject neu chua duoc gan thu cong
            if (arcoreExtensions == null)
                arcoreExtensions = GetComponent<ARCoreExtensions>();

            if (useCloudAnchors && arcoreExtensions == null)
            {
                Debug.LogError("[PhotoSpatialAnchor] Ban da bat useCloudAnchors nhung chua gan ARCoreExtensions!");
            }
#else
            if (useCloudAnchors)
            {
                Debug.LogWarning("[PhotoSpatialAnchor] useCloudAnchors = true nhung ARCORE_EXTENSIONS_ENABLED chua duoc dinh nghia. " +
                                 "Them define symbol 'ARCORE_EXTENSIONS_ENABLED' trong Player Settings de dung Cloud Anchors.");
                useCloudAnchors = false; // Tu dong tat de tranh loi runtime
            }
#endif
        }

        private void Start()
        {
            // Dang ky doi Session Ready de resolve cac anchor cu
            if (cameraController != null && useCloudAnchors)
            {
                cameraController.OnSessionReady += ResolveSavedCloudAnchors;
            }
        }

        private void OnDestroy()
        {
            // Huy dang ky tranh memory leak
            if (cameraController != null)
            {
                cameraController.OnSessionReady -= ResolveSavedCloudAnchors;
            }
        }

        // ==================================================
        // RESOLVE (Khoi phuc tu Cloud)
        // ==================================================

        private void ResolveSavedCloudAnchors()
        {
#if ARCORE_EXTENSIONS_ENABLED
            // API moi: dung anchorManager (ARAnchorManager) thay vi arcoreExtensions
            if (!useCloudAnchors || anchorManager == null) return;

            List<AnchorData> saved = _dataManager.GetAllAnchors();
            if (saved.Count == 0) return;

            Debug.Log($"[PhotoSpatialAnchor] Bat dau Resolve {saved.Count} Cloud Anchors tu phien truoc...");
            StartCoroutine(ResolveCloudAnchorsRoutine(saved));
#endif
        }

#if ARCORE_EXTENSIONS_ENABLED
        private IEnumerator ResolveCloudAnchorsRoutine(List<AnchorData> savedDataList)
        {
            foreach (var data in savedDataList)
            {
                if (string.IsNullOrEmpty(data.cloudAnchorId)) continue;

                Debug.Log($"[PhotoSpatialAnchor] Dang Resolve Cloud Anchor: {data.cloudAnchorId}");

                // API moi (ARCore Extensions 1.43+): goi tren ARAnchorManager, khong phai ARCoreExtensions
                ResolveCloudAnchorPromise promise = anchorManager.ResolveCloudAnchorAsync(data.cloudAnchorId);

                yield return new WaitUntil(() => promise.State == PromiseState.Done);

                ResolveCloudAnchorResult result = promise.Result;
                if (result.CloudAnchorState == CloudAnchorState.Success)
                {
                    Debug.Log($"[PhotoSpatialAnchor] Resolve thanh cong! Tao Quad hien thi...");
                    RecreateQuadForResolvedAnchor(result.Anchor, data);
                }
                else
                {
                    Debug.LogWarning($"[PhotoSpatialAnchor] Resolve that bai: {result.CloudAnchorState} - Xoa khoi disk");
                    _dataManager.DeleteAnchor(data.anchorId);
                }
            }
        }
#endif

        private void RecreateQuadForResolvedAnchor(Component resolvedAnchorComponent, AnchorData data)
        {
            Texture2D tex = TextureHelper.LoadTextureFromFile(data.texturePath);
            if (tex == null) return; // Mat file anh thi thoi

            GameObject photoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            photoQuad.name = "ResolvedPhoto_" + data.anchorId;
            Destroy(photoQuad.GetComponent<MeshCollider>());
            photoQuad.AddComponent<BoxCollider>();

            photoQuad.transform.localScale = new Vector3(data.quadWidth, data.quadHeight, 1f);
            photoQuad.GetComponent<Renderer>().material = TextureHelper.CreateUnlitMaterial(tex);
            photoQuad.AddComponent<MaterialCleanup>();

            // Quad di chuyen theo resolvedAnchor
            photoQuad.transform.SetParent(resolvedAnchorComponent.transform, false);

            // Gan Tag len resolvedAnchor (chua anchorId de AnchorInfoPanel co the tim thay)
            PinnedPhotoTag tag = resolvedAnchorComponent.gameObject.AddComponent<PinnedPhotoTag>();
            tag.Initialize(data.anchorId, data.createdAt);

            // Luu vao danh sach quan ly
            ARAnchor arAnchor = resolvedAnchorComponent.GetComponent<ARAnchor>();
            if (arAnchor == null) arAnchor = resolvedAnchorComponent.gameObject.AddComponent<ARAnchor>();
            _runtimeAnchors[data.anchorId] = arAnchor;
        }

        // ==================================================
        // CAPTURE & HOST (Chup va Host len Cloud)
        // ==================================================

        public void CaptureAndPinPhoto()
        {
            if (_isCapturing) return;
            if (cameraController != null && !cameraController.IsSessionReady)
            {
                OnPinError?.Invoke("AR Session chua san sang.");
                return;
            }
            StartCoroutine(CaptureAndPinCoroutine());
        }

        private IEnumerator CaptureAndPinCoroutine()
        {
            _isCapturing = true;

            yield return new WaitForEndOfFrame();
            Texture2D capturedTexture = ScreenCapture.CaptureScreenshotAsTexture();
            if (capturedTexture == null)
            {
                OnPinError?.Invoke("CaptureScreenshotAsTexture that bai!");
                _isCapturing = false;
                yield break;
            }

            Vector3 spawnPos   = arCamera.transform.position + arCamera.transform.forward * distanceFromCamera;
            Quaternion spawnRot = arCamera.transform.rotation;
            float quadWidth    = quadHeight * ((float)capturedTexture.width / capturedTexture.height);

            GameObject photoQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            photoQuad.name = "Photo_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            Destroy(photoQuad.GetComponent<MeshCollider>());
            photoQuad.AddComponent<BoxCollider>();
            photoQuad.transform.position   = spawnPos;
            photoQuad.transform.rotation   = spawnRot;
            photoQuad.transform.localScale = new Vector3(quadWidth, quadHeight, 1f);

            photoQuad.GetComponent<Renderer>().material = TextureHelper.CreateUnlitMaterial(capturedTexture);
            photoQuad.AddComponent<MaterialCleanup>();

            GameObject anchorGO = new GameObject("Anchor_" + photoQuad.name);
            anchorGO.transform.position = spawnPos;
            anchorGO.transform.rotation = spawnRot;
            ARAnchor anchor = anchorGO.AddComponent<ARAnchor>();
            photoQuad.transform.SetParent(anchorGO.transform, true);

            PinnedPhotoTag tag  = anchorGO.AddComponent<PinnedPhotoTag>();
            string tempId       = System.Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            string createdAt    = System.DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            AnchorData savedData = null;
            if (persistToDisk)
            {
                savedData = _dataManager.SaveAnchor(tempId, "", capturedTexture, spawnPos, spawnRot, quadWidth, quadHeight);
            }

            // FIX #5: Giai phong capturedTexture sau khi da luu va tao material xong.
            // Material giu tham chieu den texture qua MaterialCleanup, capturedTexture
            // la ban sao rieng tu ScreenCapture nen phai Destroy de tranh memory leak.
            TextureHelper.DestroyTextureSafe(capturedTexture);

            tag.Initialize(tempId, createdAt);
            _runtimeAnchors[tempId] = anchor;
            OnPhotoPinned?.Invoke(savedData);

            // Host len Google Cloud ngam ben duoi
#if ARCORE_EXTENSIONS_ENABLED
            // API moi: chi can anchorManager, khong can arcoreExtensions de host
            if (useCloudAnchors && anchorManager != null)
            {
                StartCoroutine(HostCloudAnchorRoutine(anchor, tempId));
            }
#endif

            _isCapturing = false;
        }

#if ARCORE_EXTENSIONS_ENABLED
        private IEnumerator HostCloudAnchorRoutine(ARAnchor localAnchor, string localId)
        {
            Debug.Log($"[PhotoSpatialAnchor] Dang Host Cloud Anchor cho cuc bo ID: {localId}");

            // API moi (ARCore Extensions 1.43+): goi tren ARAnchorManager thay vi ARCoreExtensions
            // TTL = 1 ngay voi API Key. Dung Keyless Auth de dat toi da 365 ngay.
            HostCloudAnchorPromise promise = anchorManager.HostCloudAnchorAsync(localAnchor, 1);

            yield return new WaitUntil(() => promise.State == PromiseState.Done);

            HostCloudAnchorResult result = promise.Result;
            if (result.CloudAnchorState == CloudAnchorState.Success)
            {
                Debug.Log($"[PhotoSpatialAnchor] Host thanh cong! Cloud ID: {result.CloudAnchorId}");
                _dataManager.UpdateCloudAnchorId(localId, result.CloudAnchorId);
            }
            else
            {
                Debug.LogWarning($"[PhotoSpatialAnchor] Host that bai. Loi: {result.CloudAnchorState}");
            }
        }
#endif

        // ==================================================
        // DELETE
        // ==================================================

        public void RemoveAnchor(string anchorId)
        {
            if (_runtimeAnchors.TryGetValue(anchorId, out ARAnchor anchor))
            {
                if (anchor != null) Destroy(anchor.gameObject);
                _runtimeAnchors.Remove(anchorId);
            }
            if (persistToDisk) _dataManager.DeleteAnchor(anchorId);
        }

        public void RemoveAllAnchors()
        {
            foreach (var kvp in _runtimeAnchors)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            _runtimeAnchors.Clear();
            if (persistToDisk) _dataManager.DeleteAllAnchors();
        }
    }

    public class PinnedPhotoTag : MonoBehaviour
    {
        public string AnchorId  { get; private set; }
        public string CreatedAt { get; private set; }

        public void Initialize(string anchorId, string createdAt)
        {
            AnchorId  = anchorId;
            CreatedAt = createdAt;
        }
    }

    public class MaterialCleanup : MonoBehaviour
    {
        private void OnDestroy()
        {
            var r = GetComponent<Renderer>();
            if (r != null && r.material != null)
            {
                var tex = r.material.mainTexture;
                if (tex != null) Destroy(tex);
                Destroy(r.material);
            }
        }
    }
}
