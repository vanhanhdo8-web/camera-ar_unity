// ============================================================
//  TextureHelper.cs
//  Mo ta: Cac ham tien ich xu ly Texture2D dung chung cho toan project.
//         Bao gom: resize, nen JPG, luu/doc file anh.
// ============================================================

using System.IO;
using UnityEngine;

namespace CameraAR.Utils
{
    public static class TextureHelper
    {
        // -------------------------------------------------------
        //  RESIZE
        // -------------------------------------------------------

        /// <summary>
        /// Thu nho texture ve kich thuoc toi da maxSize (giu ty le khung hinh).
        /// Tranh viec luu texture full-resolution qua lon vao bo nho.
        /// </summary>
        public static Texture2D ResizeTexture(Texture2D source, int maxSize = 1024)
        {
            if (source == null) return null;

            // Tinh kich thuoc moi giu nguyen ty le
            float aspect = (float)source.width / source.height;
            int newWidth, newHeight;

            if (source.width >= source.height)
            {
                newWidth  = Mathf.Min(source.width, maxSize);
                newHeight = Mathf.RoundToInt(newWidth / aspect);
            }
            else
            {
                newHeight = Mathf.Min(source.height, maxSize);
                newWidth  = Mathf.RoundToInt(newHeight * aspect);
            }

            // Render vao RenderTexture roi doc lai
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0,
                                    RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);

            Texture2D resized = new Texture2D(newWidth, newHeight, TextureFormat.RGB24, false);
            resized.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            resized.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return resized;
        }

        // -------------------------------------------------------
        //  LUU FILE
        // -------------------------------------------------------

        /// <summary>
        /// Luu Texture2D thanh file .jpg tren thiet bi.
        /// Tra ve duong dan day du neu thanh cong, null neu that bai.
        /// </summary>
        public static string SaveTextureToFile(Texture2D texture, string folderPath,
                                               string fileName, int jpgQuality = 85)
        {
            if (texture == null)
            {
                Debug.LogError("[TextureHelper] Texture null, khong the luu file.");
                return null;
            }

            try
            {
                // Tao thu muc neu chua ton tai
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string fullPath = Path.Combine(folderPath, fileName + ".jpg");
                byte[] jpgBytes = texture.EncodeToJPG(jpgQuality);
                File.WriteAllBytes(fullPath, jpgBytes);

                Debug.Log($"[TextureHelper] Da luu anh tai: {fullPath}");
                return fullPath;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TextureHelper] Luu anh that bai: {e.Message}");
                return null;
            }
        }

        // -------------------------------------------------------
        //  DOC FILE
        // -------------------------------------------------------

        /// <summary>
        /// Doc file anh tu duong dan va tra ve Texture2D.
        /// </summary>
        public static Texture2D LoadTextureFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Debug.LogWarning($"[TextureHelper] Khong tim thay file: {filePath}");
                return null;
            }

            try
            {
                byte[] bytes    = File.ReadAllBytes(filePath);
                Texture2D tex   = new Texture2D(2, 2, TextureFormat.RGB24, false);

                if (tex.LoadImage(bytes)) // Tu dong resize texture theo du lieu
                {
                    Debug.Log($"[TextureHelper] Da tai anh tu: {filePath}");
                    return tex;
                }
                else
                {
                    // FIX #10: Phai Destroy texture da new truoc khi return null,
                    // neu khong se bi memory leak vi khong co ai giu tham chieu den no.
                    Debug.LogError("[TextureHelper] Khong the parse du lieu anh.");
                    Object.Destroy(tex);
                    return null;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[TextureHelper] Doc anh that bai: {e.Message}");
                return null;
            }
        }

        // -------------------------------------------------------
        //  TAO MATERIAL UNLIT
        // -------------------------------------------------------

        /// <summary>
        /// Tao Material Unlit/Texture voi texture cho san.
        /// Unlit de anh khong bi anh huong boi anh sang moi truong AR.
        /// </summary>
        public static Material CreateUnlitMaterial(Texture2D texture)
        {
            Shader unlitShader = Shader.Find("Unlit/Texture");

            // Fallback sang Standard neu khong tim thay Unlit/Texture
            if (unlitShader == null)
            {
                Debug.LogWarning("[TextureHelper] Khong tim thay 'Unlit/Texture', " +
                                 "dung Standard thay the. Nen them shader vao Always Included Shaders.");
                unlitShader = Shader.Find("Standard");
            }

            Material mat = new Material(unlitShader);
            mat.mainTexture = texture;
            return mat;
        }

        // -------------------------------------------------------
        //  XOA TEXTURE KHOI BO NHO
        // -------------------------------------------------------

        /// <summary>
        /// Giai phong Texture2D khoi bo nho mot cach an toan.
        /// Luon goi ham nay khi khong dung texture nua de tranh memory leak.
        /// </summary>
        public static void DestroyTextureSafe(Texture2D texture)
        {
            if (texture != null)
                Object.Destroy(texture);
        }
    }
}
