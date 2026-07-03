// ============================================================
//  PhotoCaptureUI.cs
//  Mo ta: Dieu khien giao dien nguoi dung cho tinh nang chup
//         va ghim anh. Quan ly: nut chup, hieu ung, thumbnail
//         strip, trang thai session, va dem so luong anh da ghim.
//  Gan len: Canvas GameObject trong Scene.
//  Yeu cau: Unity UI (uGUI)
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CameraAR.Core;

namespace CameraAR.UI
{
    public class PhotoCaptureUI : MonoBehaviour
    {
        // -------------------------------------------------------
        //  INSPECTOR FIELDS
        // -------------------------------------------------------

        [Header("Core Reference")]
        [Tooltip("PhotoSpatialAnchor script dieu phoi logic chinh")]
        [SerializeField] private PhotoSpatialAnchor photoAnchor;

        [Tooltip("ARCameraController de theo doi trang thai session")]
        [SerializeField] private ARCameraController cameraController;

        [Header("Nut Chup Anh")]
        [Tooltip("Nut chu dao de chup anh")]
        [SerializeField] private Button captureButton;

        [Tooltip("Icon / Image tren nut chup")]
        [SerializeField] private Image captureButtonIcon;

        [Tooltip("Color cua nut khi dang chup (disabled)")]
        [SerializeField] private Color buttonDisabledColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

        [Tooltip("Color cua nut khi san sang")]
        [SerializeField] private Color buttonReadyColor = new Color(1f, 1f, 1f, 1f);

        [Header("Hieu ung Flash khi chup")]
        [Tooltip("Panel trang phu len toan man hinh khi chup anh")]
        [SerializeField] private Image flashPanel;

        [Tooltip("Thoi gian hieu ung flash (giay)")]
        [SerializeField] private float flashDuration = 0.2f;

        [Header("Trang thai & Dem so")]
        [Tooltip("Text hien thi trang thai AR Session va thong bao")]
        [SerializeField] private TextMeshProUGUI statusText;

        [Tooltip("Text hien thi so anh da ghim")]
        [SerializeField] private TextMeshProUGUI photoCountText;

        [Header("Thumbnail Strip")]
        [Tooltip("Transform cha chua cac thumbnail (horizontal layout)")]
        [SerializeField] private Transform thumbnailContainer;

        [Tooltip("Prefab cho moi thumbnail (Image + RawImage ben trong)")]
        [SerializeField] private GameObject thumbnailPrefab;

        [Tooltip("So thumbnail toi da hien thi")]
        [SerializeField] private int maxThumbnails = 5;

        [Header("Nut Xoa Tat Ca")]
        [SerializeField] private Button clearAllButton;

        // -------------------------------------------------------
        //  PRIVATE FIELDS
        // -------------------------------------------------------

        private int _photoCount      = 0;
        private bool _isSessionReady = false;
        private Queue<GameObject> _thumbnails = new Queue<GameObject>();

        // Cache delegates de co the unsubscribe chinh xac
        private System.Action      _onSessionReadyDelegate;
        private System.Action      _onDeviceNotSupportedDelegate;
        private System.Action<string> _onSessionErrorDelegate;

        // -------------------------------------------------------
        //  UNITY LIFECYCLE
        // -------------------------------------------------------

        private void Awake()
        {
            // Ket noi nut chup voi ham xu ly
            if (captureButton != null)
                captureButton.onClick.AddListener(OnCaptureButtonClicked);

            // Ket noi nut xoa tat ca
            if (clearAllButton != null)
                clearAllButton.onClick.AddListener(OnClearAllClicked);

            // An flash panel ngay tu dau
            if (flashPanel != null)
            {
                flashPanel.gameObject.SetActive(false);
                flashPanel.color = Color.clear;
            }
        }

        private void Start()
        {
            // Dang ky lang nghe su kien tu PhotoSpatialAnchor
            if (photoAnchor != null)
            {
                photoAnchor.OnPhotoPinned += OnPhotoPinnedHandler;
                photoAnchor.OnPinError    += OnPinErrorHandler;
            }

            // FIX #8: Cache delegate vao bien de co the unsubscribe chinh xac trong OnDestroy.
            // Dung lambda truc tiep khi subscribe khong the unsubscribe duoc vi moi lan tao ra
            // mot instance lambda khac nhau, gay memory leak.
            if (cameraController != null)
            {
                _onSessionReadyDelegate      = () => SetSessionReady(true);
                _onDeviceNotSupportedDelegate = OnDeviceNotSupported;
                _onSessionErrorDelegate      = OnSessionError;

                cameraController.OnSessionReady      += _onSessionReadyDelegate;
                cameraController.OnDeviceNotSupported += _onDeviceNotSupportedDelegate;
                cameraController.OnSessionError      += _onSessionErrorDelegate;
            }

            // Cap nhat UI trang thai ban dau
            UpdateCaptureButton(isEnabled: false);
            SetStatusText("Dang khoi dong AR...", Color.yellow);
            UpdatePhotoCount();
        }

        private void OnDestroy()
        {
            if (captureButton != null)
                captureButton.onClick.RemoveListener(OnCaptureButtonClicked);

            if (clearAllButton != null)
                clearAllButton.onClick.RemoveListener(OnClearAllClicked);

            if (photoAnchor != null)
            {
                photoAnchor.OnPhotoPinned -= OnPhotoPinnedHandler;
                photoAnchor.OnPinError    -= OnPinErrorHandler;
            }

            // FIX #8: Unsubscribe chinh xac bang cached delegate
            if (cameraController != null)
            {
                cameraController.OnSessionReady      -= _onSessionReadyDelegate;
                cameraController.OnDeviceNotSupported -= _onDeviceNotSupportedDelegate;
                cameraController.OnSessionError      -= _onSessionErrorDelegate;
            }
        }

        // -------------------------------------------------------
        //  BUTTON HANDLERS
        // -------------------------------------------------------

        private void OnCaptureButtonClicked()
        {
            if (!_isSessionReady) return;

            // Goi logic chup anh
            photoAnchor?.CaptureAndPinPhoto();

            // Chay hieu ung flash
            StartCoroutine(PlayFlashEffect());

            // Tam vo hieu nut trong khi dang xu ly
            UpdateCaptureButton(isEnabled: false);
            SetStatusText("Dang chup va ghim anh...", Color.cyan);
        }

        private void OnClearAllClicked()
        {
            photoAnchor?.RemoveAllAnchors();

            // FIX #9: Khong nen iterate va Destroy trong foreach (Transform child in container)
            // vi Unity se skip phan tu khi danh sach thay doi. Lay danh sach truoc roi Destroy.
            var children = new List<Transform>();
            foreach (Transform child in thumbnailContainer)
                children.Add(child);
            foreach (Transform child in children)
                Destroy(child.gameObject);

            _thumbnails.Clear();
            _photoCount = 0;
            UpdatePhotoCount();
            SetStatusText("Da xoa tat ca anh.", Color.white);
        }

        // -------------------------------------------------------
        //  EVENT HANDLERS tu PhotoSpatialAnchor
        // -------------------------------------------------------

        private void OnPhotoPinnedHandler(AnchorData data)
        {
            _photoCount++;
            UpdatePhotoCount();
            UpdateCaptureButton(isEnabled: _isSessionReady);
            SetStatusText($"Da ghim anh #{_photoCount}!", Color.green);

            // Hien thumbnail neu co anh luu tren disk
            if (data != null && System.IO.File.Exists(data.texturePath))
                AddThumbnail(data.texturePath);

            // Tu dong xoa thong bao sau 3 giay
            StartCoroutine(ClearStatusAfterDelay(3f));
        }

        private void OnPinErrorHandler(string error)
        {
            UpdateCaptureButton(isEnabled: _isSessionReady);
            SetStatusText($"Loi: {error}", Color.red);
            StartCoroutine(ClearStatusAfterDelay(5f));
        }

        // -------------------------------------------------------
        //  SESSION HANDLERS tu ARCameraController
        // -------------------------------------------------------

        private void SetSessionReady(bool ready)
        {
            _isSessionReady = ready;
            UpdateCaptureButton(isEnabled: ready);
            SetStatusText(ready ? "San sang! Nhan nut de chup anh." : "Mat ket noi AR...",
                          ready ? Color.green : Color.yellow);
        }

        private void OnDeviceNotSupported()
        {
            _isSessionReady = false;
            UpdateCaptureButton(isEnabled: false);
            SetStatusText("Thiet bi nay khong ho tro AR Foundation!", Color.red);
        }

        private void OnSessionError(string error)
        {
            _isSessionReady = false;
            UpdateCaptureButton(isEnabled: false);
            SetStatusText($"Loi AR: {error}", Color.red);
        }

        // -------------------------------------------------------
        //  HELPERS
        // -------------------------------------------------------

        private void UpdateCaptureButton(bool isEnabled)
        {
            if (captureButton == null) return;
            captureButton.interactable = isEnabled;

            if (captureButtonIcon != null)
                captureButtonIcon.color = isEnabled ? buttonReadyColor : buttonDisabledColor;
        }

        private void SetStatusText(string message, Color color)
        {
            if (statusText == null) return;
            statusText.text  = message;
            statusText.color = color;
        }

        private void UpdatePhotoCount()
        {
            if (photoCountText != null)
                photoCountText.text = $"Anh da ghim: {_photoCount}";
        }

        /// <summary>Them thumbnail moi vao strip. Xoa anh cu nhat neu qua gioi han.</summary>
        private void AddThumbnail(string texturePath)
        {
            if (thumbnailContainer == null || thumbnailPrefab == null) return;

            // Xoa thumbnail cu nhat neu qua gioi han
            if (_thumbnails.Count >= maxThumbnails)
            {
                GameObject oldest = _thumbnails.Dequeue();
                Destroy(oldest);
            }

            // Tao thumbnail moi
            GameObject thumbGO = Instantiate(thumbnailPrefab, thumbnailContainer);
            _thumbnails.Enqueue(thumbGO);

            // Gan texture vao RawImage (load async de khong block main thread)
            StartCoroutine(LoadThumbnailAsync(thumbGO, texturePath));
        }

        private IEnumerator LoadThumbnailAsync(GameObject thumbGO, string path)
        {
            yield return null; // Cho 1 frame

            if (thumbGO == null) yield break; // Co the bi Destroy truoc khi coroutine chay

            var rawImage = thumbGO.GetComponentInChildren<RawImage>();
            if (rawImage != null)
            {
                Texture2D tex = CameraAR.Utils.TextureHelper.LoadTextureFromFile(path);
                if (tex != null)
                {
                    // Thu nho thumbnail xuong 128px
                    Texture2D small = CameraAR.Utils.TextureHelper.ResizeTexture(tex, 128);
                    CameraAR.Utils.TextureHelper.DestroyTextureSafe(tex);
                    rawImage.texture = small;
                }
            }
        }

        // -------------------------------------------------------
        //  COROUTINES
        // -------------------------------------------------------

        /// <summary>Hieu ung flash trang khi chup anh.</summary>
        private IEnumerator PlayFlashEffect()
        {
            if (flashPanel == null) yield break;

            flashPanel.gameObject.SetActive(true);
            flashPanel.color = Color.white;

            // Fade out
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / flashDuration);
                flashPanel.color = new Color(1f, 1f, 1f, alpha);
                yield return null;
            }

            flashPanel.color = Color.clear;
            flashPanel.gameObject.SetActive(false);
        }

        /// <summary>Tu dong xoa text trang thai sau mot khoang thoi gian.</summary>
        private IEnumerator ClearStatusAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_isSessionReady)
                SetStatusText("San sang! Nhan nut de chup anh.", Color.green);
        }
    }
}
