// ============================================================
//  AnchorDataManager.cs
//  Mo ta: Quan ly luu tru va phuc hoi du lieu cac Spatial Anchor.
//         Ho tro luu Cloud Anchor ID (Google ARCore).
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CameraAR.Utils;

namespace CameraAR.Core
{
    // -------------------------------------------------------
    //  DATA MODELS
    // -------------------------------------------------------

    [Serializable]
    public class AnchorData
    {
        public string anchorId;          // ID cuc bo
        public string cloudAnchorId;     // ID tren Google Cloud
        public string texturePath;       // Duong dan file anh JPG
        public float posX, posY, posZ;   // Vi tri trong khong gian
        public float rotX, rotY, rotZ, rotW; // Goc quay
        public float quadWidth;
        public float quadHeight;
        public string createdAt;
    }

    // -------------------------------------------------------
    //  MAIN COMPONENT
    // -------------------------------------------------------

    public class AnchorDataManager : MonoBehaviour
    {
        // FIX #1: AnchorDataList phai nam BEN TRONG class, khong duoc khai bao
        // 'private' o cap namespace (scope ngoai class la compile error trong C#).
        [Serializable]
        private class AnchorDataList
        {
            public List<AnchorData> anchors = new List<AnchorData>();
        }

        private const string JSON_FILE_NAME = "anchors_data.json";
        private const string TEXTURE_FOLDER = "AnchorTextures";

        public string TextureSavePath => Path.Combine(Application.persistentDataPath, TEXTURE_FOLDER);
        public string JsonSavePath    => Path.Combine(Application.persistentDataPath, JSON_FILE_NAME);

        private List<AnchorData> _savedAnchors = new List<AnchorData>();

        private void Awake()
        {
            if (!Directory.Exists(TextureSavePath))
                Directory.CreateDirectory(TextureSavePath);
            LoadAnchorsFromDisk();
        }

        public AnchorData SaveAnchor(string localId, string cloudId, Texture2D texture,
                                     Vector3 pos, Quaternion rot, float quadWidth, float quadHeight)
        {
            if (texture == null) return null;

            string texFileName = $"anchor_{localId}";
            Texture2D resized  = TextureHelper.ResizeTexture(texture, maxSize: 1024);
            string texPath     = TextureHelper.SaveTextureToFile(resized, TextureSavePath, texFileName, 85);
            TextureHelper.DestroyTextureSafe(resized);

            if (texPath == null) return null;

            var data = new AnchorData
            {
                anchorId      = localId,
                cloudAnchorId = cloudId,
                texturePath   = texPath,
                posX = pos.x, posY = pos.y, posZ = pos.z,
                rotX = rot.x, rotY = rot.y, rotZ = rot.z, rotW = rot.w,
                quadWidth     = quadWidth,
                quadHeight    = quadHeight,
                createdAt     = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };

            _savedAnchors.Add(data);
            FlushToDisk();
            return data;
        }

        public void UpdateCloudAnchorId(string localId, string newCloudId)
        {
            AnchorData target = _savedAnchors.Find(a => a.anchorId == localId);
            if (target != null)
            {
                target.cloudAnchorId = newCloudId;
                FlushToDisk();
            }
        }

        public void DeleteAnchor(string anchorId)
        {
            AnchorData target = _savedAnchors.Find(a => a.anchorId == anchorId);
            if (target != null)
            {
                if (File.Exists(target.texturePath))
                    File.Delete(target.texturePath);
                _savedAnchors.Remove(target);
                FlushToDisk();
            }
        }

        public void DeleteAllAnchors()
        {
            foreach (var data in _savedAnchors)
            {
                if (File.Exists(data.texturePath))
                    File.Delete(data.texturePath);
            }
            _savedAnchors.Clear();
            FlushToDisk();
        }

        public List<AnchorData> GetAllAnchors() => new List<AnchorData>(_savedAnchors);
        public int Count => _savedAnchors.Count;

        private void LoadAnchorsFromDisk()
        {
            if (!File.Exists(JsonSavePath)) return;
            try
            {
                string json         = File.ReadAllText(JsonSavePath);
                AnchorDataList list = JsonUtility.FromJson<AnchorDataList>(json);
                if (list != null && list.anchors != null)
                    _savedAnchors = list.anchors;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AnchorDataManager] Doc JSON that bai: {e.Message}");
            }
        }

        private void FlushToDisk()
        {
            try
            {
                var list    = new AnchorDataList { anchors = _savedAnchors };
                string json = JsonUtility.ToJson(list, prettyPrint: true);
                File.WriteAllText(JsonSavePath, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AnchorDataManager] Ghi JSON that bai: {e.Message}");
            }
        }
    }
}
