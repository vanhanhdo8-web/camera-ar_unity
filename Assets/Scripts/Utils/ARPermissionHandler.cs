// ============================================================
//  ARPermissionHandler.cs
//  Mo ta: Xu ly xin quyen Camera (va Storage tren Android cu)
//         truoc khi khoi dong AR Session hoac chup anh.
//         Ho tro Android (API 23+) va iOS.
// ============================================================

using System.Collections;
using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace CameraAR.Utils
{
    public class ARPermissionHandler : MonoBehaviour
    {
        // -------------------------------------------------------
        //  EVENTS
        // -------------------------------------------------------

        /// <summary>Goi khi tat ca quyen can thiet da duoc cap.</summary>
        public event System.Action OnAllPermissionsGranted;

        /// <summary>Goi khi it nhat mot quyen bi tu choi.</summary>
        public event System.Action OnPermissionDenied;

        // -------------------------------------------------------
        //  PRIVATE FIELDS
        // -------------------------------------------------------

        // FIX #11: Dung flag de dam bao event chi duoc Invoke duy nhat mot lan,
        // tranh race condition giua Android callback va polling loop.
        private bool _permissionResolved = false;

        // -------------------------------------------------------
        //  PUBLIC API
        // -------------------------------------------------------

        /// <summary>
        /// Kiem tra va xin quyen Camera (va Write External Storage tren Android cu).
        /// Nen goi ham nay khi app khoi dong, truoc khi bat AR Session.
        /// </summary>
        public void RequestRequiredPermissions()
        {
            _permissionResolved = false;
            StartCoroutine(RequestPermissionsCoroutine());
        }

        /// <summary>
        /// Kiem tra nhanh xem quyen Camera da co chua (khong xin them).
        /// </summary>
        public bool HasCameraPermission()
        {
#if UNITY_ANDROID
            return Permission.HasUserAuthorizedPermission(Permission.Camera);
#elif UNITY_IOS
            return Application.HasUserAuthorization(UserAuthorization.WebCam);
#else
            return true; // Editor: luon co quyen
#endif
        }

        // -------------------------------------------------------
        //  COROUTINE XIN QUYEN
        // -------------------------------------------------------

        private IEnumerator RequestPermissionsCoroutine()
        {
#if UNITY_ANDROID
            // --- Android: Xin quyen Camera ---
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("[ARPermissionHandler] Xin quyen Camera...");

                // FIX #11: Chi dung HOAC callbacks HOAC polling loop, khong dung ca hai cung luc.
                // Callbacks chi dung de log, viec quyet dinh ket qua cuoi cung van do polling loop +
                // flag _permissionResolved xu ly. Neu dung callbacks de Invoke event truc tiep,
                // ket hop voi polling loop cung co the Invoke → event bi goi 2 lan.
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted              += OnAndroidPermissionGranted;
                callbacks.PermissionDenied               += OnAndroidPermissionDenied;
                callbacks.PermissionDeniedAndDontAskAgain += OnAndroidPermissionDeniedPermanent;

                Permission.RequestUserPermission(Permission.Camera, callbacks);

                // Doi cho den khi nguoi dung phan hoi (polling moi 0.5 giay, toi da 30 giay)
                float timeout = 30f;
                while (!Permission.HasUserAuthorizedPermission(Permission.Camera)
                       && !_permissionResolved   // Thoat som neu callback da xu ly
                       && timeout > 0)
                {
                    timeout -= 0.5f;
                    yield return new WaitForSeconds(0.5f);
                }
            }

            // Kiem tra ket qua cuoi cung - chi thuc hien neu chua resolve tu callback
            if (!_permissionResolved)
            {
                if (Permission.HasUserAuthorizedPermission(Permission.Camera))
                {
                    Debug.Log("[ARPermissionHandler] Camera permission GRANTED.");
                    _permissionResolved = true;
                    OnAllPermissionsGranted?.Invoke();
                }
                else
                {
                    Debug.LogWarning("[ARPermissionHandler] Camera permission DENIED.");
                    _permissionResolved = true;
                    OnPermissionDenied?.Invoke();
                }
            }

#elif UNITY_IOS
            // --- iOS: Xin quyen Camera ---
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.Log("[ARPermissionHandler] Xin quyen Camera tren iOS...");
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }

            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.LogWarning("[ARPermissionHandler] iOS Camera permission DENIED.");
                if (!_permissionResolved)
                {
                    _permissionResolved = true;
                    OnPermissionDenied?.Invoke();
                }
                yield break;
            }

            Debug.Log("[ARPermissionHandler] iOS Camera permission GRANTED.");

            // --- iOS: Xin quyen Location (bat buoc cho Google Cloud Anchors) ---
            // ARCore Extensions dung location data de tang do chinh xac Host/Resolve.
            // Apple yeu cau phai xin quyen truoc khi truy cap, neu khong app bi crash.
            if (Input.location.status == LocationServiceStatus.Stopped)
            {
                Debug.Log("[ARPermissionHandler] Xin quyen Location tren iOS (can cho Cloud Anchors)...");
                Input.location.Start();

                // Cho toi da 10 giay de location service khoi dong
                float locationTimeout = 10f;
                while (Input.location.status == LocationServiceStatus.Initializing && locationTimeout > 0)
                {
                    locationTimeout -= 0.5f;
                    yield return new WaitForSeconds(0.5f);
                }
            }

            if (Input.location.status == LocationServiceStatus.Failed)
            {
                // Nguoi dung tu choi Location — Cloud Anchors se khong hoat dong
                // nhung AR co ban van chay duoc, nen van Invoke Granted
                Debug.LogWarning("[ARPermissionHandler] iOS Location DENIED. " +
                                 "Cloud Anchors co the khong hoat dong chinh xac.");
            }
            else
            {
                Debug.Log("[ARPermissionHandler] iOS Location GRANTED.");
            }

            // Camera da duoc cap — khoi dong AR. Location la optional cho Cloud Anchors.
            if (!_permissionResolved)
            {
                _permissionResolved = true;
                OnAllPermissionsGranted?.Invoke();
            }
#else
            // Unity Editor: mac dinh co quyen, goi thang callback
            Debug.Log("[ARPermissionHandler] Unity Editor - bo qua kiem tra quyen.");
            yield return null;
            OnAllPermissionsGranted?.Invoke();
#endif
        }

        // -------------------------------------------------------
        //  ANDROID CALLBACKS
        // -------------------------------------------------------

#if UNITY_ANDROID
        private void OnAndroidPermissionGranted(string permission)
        {
            // FIX #11: Dung flag de dam bao chi goi event mot lan
            Debug.Log($"[ARPermissionHandler] Android granted: {permission}");
            if (!_permissionResolved)
            {
                _permissionResolved = true;
                OnAllPermissionsGranted?.Invoke();
            }
        }

        private void OnAndroidPermissionDenied(string permission)
        {
            Debug.LogWarning($"[ARPermissionHandler] Android denied: {permission}");
            if (!_permissionResolved)
            {
                _permissionResolved = true;
                OnPermissionDenied?.Invoke();
            }
        }

        private void OnAndroidPermissionDeniedPermanent(string permission)
        {
            Debug.LogError($"[ARPermissionHandler] Android denied permanently: {permission}. " +
                           "Nguoi dung can vao Settings -> App -> Permissions de cap quyen thu cong.");
            if (!_permissionResolved)
            {
                _permissionResolved = true;
                OnPermissionDenied?.Invoke();
            }
        }
#endif
    }
}
