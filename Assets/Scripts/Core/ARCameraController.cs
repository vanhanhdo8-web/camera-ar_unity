// ============================================================
//  ARCameraController.cs
//  Mo ta: Quan ly vong doi AR Session, kiem tra thiet bi ho tro AR,
//         va thong bao cho cac script khac khi session san sang.
//  Gan len: GameObject "AR Session" hoac "AR Manager" trong Scene.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using CameraAR.Utils;

namespace CameraAR.Core
{
    [RequireComponent(typeof(ARSession))]
    [RequireComponent(typeof(ARPermissionHandler))]
    public class ARCameraController : MonoBehaviour
    {
        // -------------------------------------------------------
        //  INSPECTOR FIELDS
        // -------------------------------------------------------

        [Header("AR Components")]
        [Tooltip("ARSession component (tu dong lay neu de trong)")]
        [SerializeField] private ARSession arSession;

        [Header("Cai dat")]
        [Tooltip("Tu dong bat AR Session khi script Awake")]
        [SerializeField] private bool autoStartOnAwake = true;

        [Tooltip("Thoi gian cho (giay) truoc khi bao loi session timeout")]
        [SerializeField] private float sessionTimeoutSeconds = 15f;

        // -------------------------------------------------------
        //  EVENTS
        // -------------------------------------------------------

        /// <summary>Goi khi AR Session da san sang va dang tracking.</summary>
        public event System.Action OnSessionReady;

        /// <summary>Goi khi thiet bi khong ho tro AR Foundation.</summary>
        public event System.Action OnDeviceNotSupported;

        /// <summary>Goi khi AR Session that bai hoac bi mat.</summary>
        public event System.Action<string> OnSessionError;

        // -------------------------------------------------------
        //  PROPERTIES
        // -------------------------------------------------------

        /// <summary>True khi AR Session dang hoat dong va tracking binh thuong.</summary>
        public bool IsSessionReady { get; private set; } = false;

        /// <summary>True khi thiet bi da duoc xac nhan ho tro AR.</summary>
        public bool IsDeviceSupported { get; private set; } = false;

        // -------------------------------------------------------
        //  PRIVATE FIELDS
        // -------------------------------------------------------

        private ARPermissionHandler _permissionHandler;
        private bool _sessionStarted = false;

        // FIX #2: Dung bien rieng de theo doi xem da log trang thai chua,
        // tranh spam log lien tuc moi giay do Mathf.FloorToInt(elapsed) % 3 == 0
        // se luon true ngay o elapsed=0 va log lien tuc trong khi dieu kien dung.
        private int _lastLoggedSecond = -1;

        // -------------------------------------------------------
        //  UNITY LIFECYCLE
        // -------------------------------------------------------

        private void Awake()
        {
            // Lay cac component can thiet
            if (arSession == null)
                arSession = GetComponent<ARSession>();

            _permissionHandler = GetComponent<ARPermissionHandler>();

            // Dang ky lang nghe ket qua xin quyen
            _permissionHandler.OnAllPermissionsGranted += OnPermissionsGranted;
            _permissionHandler.OnPermissionDenied      += OnPermissionsDenied;
        }

        private void Start()
        {
            if (autoStartOnAwake)
                InitializeAR();
        }

        private void Update()
        {
            // Cap nhat IsSessionReady theo trang thai thuc te cua ARSession
            if (_sessionStarted)
            {
                bool wasReady = IsSessionReady;
                IsSessionReady = (ARSession.state == ARSessionState.SessionTracking);

                // Log khi trang thai thay doi
                if (!wasReady && IsSessionReady)
                    Debug.Log("[ARCameraController] AR Session da san sang va dang TRACKING!");
            }
        }

        private void OnDestroy()
        {
            if (_permissionHandler != null)
            {
                _permissionHandler.OnAllPermissionsGranted -= OnPermissionsGranted;
                _permissionHandler.OnPermissionDenied      -= OnPermissionsDenied;
            }
        }

        // -------------------------------------------------------
        //  PUBLIC API
        // -------------------------------------------------------

        /// <summary>
        /// Khoi dong kiem tra thiet bi va xin quyen.
        /// Goi ham nay neu autoStartOnAwake = false.
        /// </summary>
        public void InitializeAR()
        {
            StartCoroutine(CheckDeviceSupportCoroutine());
        }

        /// <summary>
        /// Tam dung AR Session de tiet kiem pin khi khong can.
        /// </summary>
        public void PauseSession()
        {
            if (arSession != null)
            {
                arSession.enabled = false;
                IsSessionReady    = false;
                Debug.Log("[ARCameraController] Da tam dung AR Session.");
            }
        }

        /// <summary>
        /// Tiep tuc AR Session sau khi da tam dung.
        /// </summary>
        public void ResumeSession()
        {
            if (arSession != null)
            {
                arSession.enabled = true;
                Debug.Log("[ARCameraController] Da tiep tuc AR Session.");
            }
        }

        // -------------------------------------------------------
        //  COROUTINES
        // -------------------------------------------------------

        /// <summary>
        /// Kiem tra thiet bi co ho tro AR Foundation khong.
        /// ARSession.CheckAvailability() la async, can dung coroutine.
        /// </summary>
        private IEnumerator CheckDeviceSupportCoroutine()
        {
            Debug.Log("[ARCameraController] Kiem tra ho tro AR...");

            // Cho ARSession.CheckAvailability() hoan thanh
            if (ARSession.state == ARSessionState.None ||
                ARSession.state == ARSessionState.CheckingAvailability)
            {
                yield return ARSession.CheckAvailability();
            }

            // Xu ly ket qua kiem tra
            switch (ARSession.state)
            {
                case ARSessionState.Ready:
                case ARSessionState.SessionInitializing:
                case ARSessionState.SessionTracking:
                    IsDeviceSupported = true;
                    Debug.Log("[ARCameraController] Thiet bi HO TRO AR Foundation.");
                    // Xin quyen truoc khi bat Session
                    _permissionHandler.RequestRequiredPermissions();
                    break;

                case ARSessionState.Unsupported:
                    IsDeviceSupported = false;
                    Debug.LogError("[ARCameraController] Thiet bi KHONG HO TRO AR Foundation.");
                    OnDeviceNotSupported?.Invoke();
                    break;

                case ARSessionState.NeedsInstall:
                    IsDeviceSupported = false;
                    Debug.LogWarning("[ARCameraController] Can cai dat them AR software (ARCore/ARKit).");
                    // Tu dong yeu cau cai dat
                    yield return ARSession.Install();
                    // Kiem tra lai sau khi cai dat
                    StartCoroutine(CheckDeviceSupportCoroutine());
                    break;

                default:
                    Debug.LogWarning($"[ARCameraController] Trang thai khong xac dinh: {ARSession.state}");
                    break;
            }
        }

        /// <summary>Bat AR Session va doi cho den khi tracking.</summary>
        private IEnumerator StartARSessionCoroutine()
        {
            Debug.Log("[ARCameraController] Bat dau AR Session...");
            arSession.enabled = true;
            _sessionStarted   = true;

            // Reset bien theo doi log
            _lastLoggedSecond = -1;

            // Doi cho den khi session tracking hoac timeout
            float elapsed = 0f;
            while (ARSession.state != ARSessionState.SessionTracking
                   && elapsed < sessionTimeoutSeconds)
            {
                elapsed += Time.deltaTime;

                // FIX #2: Chi log khi vuot qua moc 3 giay moi, dung bien
                // _lastLoggedSecond de tranh spam debug log moi frame.
                int currentSecond = Mathf.FloorToInt(elapsed);
                if (currentSecond % 3 == 0 && currentSecond != _lastLoggedSecond)
                {
                    _lastLoggedSecond = currentSecond;
                    Debug.Log($"[ARCameraController] Dang khoi tao AR... ({elapsed:F1}s) " +
                              $"State: {ARSession.state}");
                }

                yield return null;
            }

            if (ARSession.state == ARSessionState.SessionTracking)
            {
                IsSessionReady = true;
                Debug.Log("[ARCameraController] AR Session READY!");
                OnSessionReady?.Invoke();
            }
            else
            {
                string error = $"AR Session timeout sau {sessionTimeoutSeconds}s. " +
                               $"State cuoi: {ARSession.state}";
                Debug.LogError($"[ARCameraController] {error}");
                OnSessionError?.Invoke(error);
            }
        }

        // -------------------------------------------------------
        //  PERMISSION CALLBACKS
        // -------------------------------------------------------

        private void OnPermissionsGranted()
        {
            Debug.Log("[ARCameraController] Quyen da duoc cap, bat AR Session.");
            StartCoroutine(StartARSessionCoroutine());
        }

        private void OnPermissionsDenied()
        {
            string error = "Camera permission bi tu choi. Khong the chay AR.";
            Debug.LogError($"[ARCameraController] {error}");
            OnSessionError?.Invoke(error);
        }
    }
}
