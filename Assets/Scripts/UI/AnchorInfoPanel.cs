// ============================================================
//  AnchorInfoPanel.cs
//  Mo ta: Bat su kien tap (touch) cua nguoi dung vao Quad anh
//         da ghim, hien thi panel thong tin chi tiet, va cho
//         phep nguoi dung xoa anchor do.
//  Gan len: Cung GameObject voi PhotoCaptureUI (Canvas).
// ============================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CameraAR.Core;

namespace CameraAR.UI
{
    public class AnchorInfoPanel : MonoBehaviour
    {
        // -------------------------------------------------------
        //  INSPECTOR FIELDS
        // -------------------------------------------------------

        [Header("Camera & Raycast")]
        [Tooltip("Camera AR de thuc hien raycast")]
        [SerializeField] private Camera arCamera;

        [Tooltip("Layer mask chi cho phep raycast tren cac Quad anh (de tang performance)")]
        [SerializeField] private LayerMask photoLayer = ~0; // Mac dinh: moi thu

        [Header("Panel UI")]
        [Tooltip("Root GameObject cua panel thong tin (bat/tat khi can)")]
        [SerializeField] private GameObject infoPanel;

        [Tooltip("Hien thi Anchor ID")]
        [SerializeField] private TextMeshProUGUI anchorIdText;

        [Tooltip("Hien thi thoi gian tao anchor")]
        [SerializeField] private TextMeshProUGUI createdAtText;

        [Tooltip("Hien thi toa do vi tri anchor")]
        [SerializeField] private TextMeshProUGUI positionText;

        [Tooltip("Hien thi trang thai tracking cua anchor")]
        [SerializeField] private TextMeshProUGUI trackingStateText;

        [Header("Nut Hanh Dong")]
        [Tooltip("Nut xoa anchor dang chon")]
        [SerializeField] private Button deleteButton;

        [Tooltip("Nut dong panel")]
        [SerializeField] private Button closeButton;

        [Header("References")]
        [Tooltip("PhotoSpatialAnchor de goi ham RemoveAnchor")]
        [SerializeField] private PhotoSpatialAnchor photoAnchor;

        // -------------------------------------------------------
        //  PRIVATE FIELDS
        // -------------------------------------------------------

        // Anchor dang duoc chon hien tai
        private PinnedPhotoTag _selectedTag = null;

        // Mau sac highlight khi chon
        private readonly Color _highlightColor = new Color(1f, 0.9f, 0.3f, 1f);
        private readonly Color _normalColor    = Color.white;

        // Renderer cua Quad dang duoc chon de bo highlight
        private Renderer _selectedRenderer = null;

        // -------------------------------------------------------
        //  UNITY LIFECYCLE
        // -------------------------------------------------------

        private void Awake()
        {
            if (arCamera == null)
                arCamera = Camera.main;

            // Tu dong tim Button theo ten neu chua gan trong Inspector
            // (tranh truong hop InfoPanel dang inactive nen khong keo duoc)
            if (infoPanel != null)
            {
                if (deleteButton == null)
                {
                    var t = infoPanel.transform.Find("DeleteButton");
                    if (t != null) deleteButton = t.GetComponent<Button>();
                }
                if (closeButton == null)
                {
                    var t = infoPanel.transform.Find("CloseButton");
                    if (t != null) closeButton = t.GetComponent<Button>();
                }
            }

            // An panel khi khoi dong
            SetPanelVisible(false);

            // Ket noi cac nut
            if (deleteButton != null)
                deleteButton.onClick.AddListener(OnDeleteButtonClicked);

            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseButtonClicked);
        }

        private void OnDestroy()
        {
            if (deleteButton != null)
                deleteButton.onClick.RemoveListener(OnDeleteButtonClicked);

            if (closeButton != null)
                closeButton.onClick.RemoveListener(OnCloseButtonClicked);
        }

        private void Update()
        {
            DetectTouchOrClick();

            // Cap nhat tracking state neu panel dang mo
            if (infoPanel != null && infoPanel.activeSelf)
                UpdateTrackingState();
        }

        // -------------------------------------------------------
        //  TOUCH / CLICK DETECTION
        // -------------------------------------------------------

        /// <summary>
        /// Phat hien nguoi dung cham vao mot Quad anh da ghim.
        /// Ho tro ca touch (thiet bi) va mouse click (Editor).
        /// </summary>
        private void DetectTouchOrClick()
        {
            bool hasInput    = false;
            Vector2 inputPos = Vector2.zero;

#if UNITY_EDITOR
            if (Input.GetMouseButtonDown(0))
            {
                hasInput = true;
                inputPos = Input.mousePosition;
            }
#else
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                hasInput = true;
                inputPos = Input.GetTouch(0).position;
            }
#endif
            if (!hasInput) return;

            // Thuc hien Raycast tu camera qua vi tri cham
            Ray ray = arCamera.ScreenPointToRay(inputPos);

            if (Physics.Raycast(ray, out RaycastHit hit, maxDistance: 10f, layerMask: photoLayer))
            {
                // FIX #7: GetComponentInParent<Renderer>() se tim Renderer tren anchor cha (khong co).
                // Phan tu bi hit la BoxCollider tren Quad con, nen phai lay Renderer tu chinh no
                // hoac GetComponentInChildren tren doi tuong goc anchor.
                // Tim PinnedPhotoTag tren anchor cha, con Renderer lay tu doi tuong bi hit.
                PinnedPhotoTag tag         = hit.collider.GetComponentInParent<PinnedPhotoTag>();
                Renderer       quadRenderer = hit.collider.GetComponent<Renderer>();

                if (tag != null)
                {
                    // Nguoi dung cham vao mot Quad anh -> hien panel
                    SelectAnchor(tag, quadRenderer);
                }
                else if (_selectedTag != null)
                {
                    // Cham vao vung rong -> dong panel va bo chon
                    DeselectAnchor();
                }
            }
            else if (_selectedTag != null)
            {
                // Raycast khong trung gi -> dong panel
                DeselectAnchor();
            }
        }

        // -------------------------------------------------------
        //  SELECT / DESELECT
        // -------------------------------------------------------

        private void SelectAnchor(PinnedPhotoTag tag, Renderer quadRenderer)
        {
            // Bo highlight anchor truoc do (neu co)
            ClearHighlight();

            _selectedTag      = tag;
            _selectedRenderer = quadRenderer;

            // Them vien sang khi duoc chon
            ApplyHighlight();

            // Lay vi tri anchor (lay tu parent cua tag, tuc ARAnchor gameObject)
            Vector3 pos = tag.transform.position;

            // Cap nhat noi dung panel
            if (anchorIdText  != null) anchorIdText.text  = $"ID: {tag.AnchorId  ?? "N/A"}";
            if (createdAtText != null) createdAtText.text = $"Tao luc: {FormatDate(tag.CreatedAt)}";
            if (positionText  != null) positionText.text  = $"Vi tri: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})";

            UpdateTrackingState();
            SetPanelVisible(true);

            Debug.Log($"[AnchorInfoPanel] Da chon anchor: {tag.AnchorId}");
        }

        private void DeselectAnchor()
        {
            ClearHighlight();
            _selectedTag      = null;
            _selectedRenderer = null;
            SetPanelVisible(false);
        }

        // -------------------------------------------------------
        //  BUTTON HANDLERS
        // -------------------------------------------------------

        private void OnDeleteButtonClicked()
        {
            if (_selectedTag == null) return;

            string idToDelete = _selectedTag.AnchorId;

            // PhotoSpatialAnchor.RemoveAnchor xu ly xoa ARAnchor va du lieu tren disk
            photoAnchor?.RemoveAnchor(idToDelete);

            DeselectAnchor();
            Debug.Log($"[AnchorInfoPanel] Da yeu cau xoa anchor: {idToDelete}");
        }

        private void OnCloseButtonClicked()
        {
            DeselectAnchor();
        }

        // -------------------------------------------------------
        //  HELPERS
        // -------------------------------------------------------

        private void SetPanelVisible(bool visible)
        {
            if (infoPanel != null)
                infoPanel.SetActive(visible);
        }

        private void UpdateTrackingState()
        {
            if (trackingStateText == null || _selectedTag == null) return;

            // Lay ARAnchor tu cha cua tag (PinnedPhotoTag gan len ARAnchor gameObject)
            var anchor = _selectedTag.GetComponent<UnityEngine.XR.ARFoundation.ARAnchor>();
            if (anchor != null)
            {
                string state = anchor.trackingState.ToString();
                Color stateColor = anchor.trackingState == UnityEngine.XR.ARSubsystems.TrackingState.Tracking
                    ? Color.green : Color.yellow;
                trackingStateText.text  = $"Tracking: {state}";
                trackingStateText.color = stateColor;
            }
            else
            {
                trackingStateText.text = "Tracking: N/A";
            }
        }

        private void ApplyHighlight()
        {
            if (_selectedRenderer != null)
            {
                // Doi mau Albedo / BaseColor cua material sang mau vang
                // (chi tac dong runtime, khong anh huong asset goc)
                _selectedRenderer.material.color = _highlightColor;
            }
        }

        private void ClearHighlight()
        {
            if (_selectedRenderer != null)
                _selectedRenderer.material.color = _normalColor;
        }

        /// <summary>Dinh dang chuoi thoi gian ISO 8601 thanh dang doc duoc.</summary>
        private string FormatDate(string isoDate)
        {
            if (string.IsNullOrEmpty(isoDate)) return "Khong ro";

            if (System.DateTime.TryParse(isoDate,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out System.DateTime dt))
            {
                // Chuyen sang gio dia phuong
                return dt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
            }
            return isoDate;
        }
    }
}
