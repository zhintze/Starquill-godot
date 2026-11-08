using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Starquill.Display
{
    public class CharacterDisplay : MonoBehaviour
    {
        [Header("Compositing Settings")]
        [SerializeField] private int textureSize = 400;
        [SerializeField] private int compositingLayer = 31;

        [Header("Output")]
        [SerializeField] private RawImage outputImage;

        private RenderTexture renderTexture;
        private Camera compositingCamera;
        private readonly List<SpriteRenderer> spritePool = new();
        private ImageResolver imageResolver;
        private Transform compositingRoot;

        public RenderTexture Texture => renderTexture;

        public void Initialize(ImageResolver resolver)
        {
            imageResolver = resolver;
            SetupCompositingCamera();
            SetupRenderTexture();
            ExcludeCompositingLayerFromAllCameras();
        }

        /// size: render texture width and height in pixels (200 = the 1x canvas).
        public void Initialize(ImageResolver resolver, int size)
        {
            textureSize = size;
            Initialize(resolver);
        }

        public void SetPieces(List<DisplayPiece> pieces)
        {
            if (compositingCamera == null || imageResolver == null) return;

            while (spritePool.Count < pieces.Count)
                CreatePooledRenderer();

            // Activate root for setup
            compositingRoot.gameObject.SetActive(true);

            for (int i = 0; i < pieces.Count; i++)
            {
                var piece = pieces[i];
                var sr = spritePool[i];
                sr.gameObject.SetActive(true);

                sr.sprite = imageResolver.Resolve(piece.SpritePath);
                sr.sortingOrder = piece.Layer;
                sr.color = piece.TintColor;
                sr.flipX = piece.FlipH;

                var t = sr.transform;
                t.localPosition = new Vector3(piece.Offset.x / 100f, piece.Offset.y / 100f, 0);
                t.localScale = new Vector3(piece.Scale.x, piece.Scale.y, 1f);
                t.localRotation = Quaternion.Euler(0, 0, piece.Rotation);
            }

            for (int i = pieces.Count; i < spritePool.Count; i++)
                spritePool[i].gameObject.SetActive(false);

            // Render to texture then hide compositing objects
            Composite();
            compositingRoot.gameObject.SetActive(false);
        }

        private void Composite()
        {
            compositingCamera.targetTexture = renderTexture;
            compositingCamera.Render();

            if (outputImage != null)
                outputImage.texture = renderTexture;
        }

        private void SetupCompositingCamera()
        {
            var camObj = new GameObject("CompositingCamera");
            camObj.transform.SetParent(transform);
            camObj.transform.localPosition = new Vector3(0, 0, -10);

            compositingCamera = camObj.AddComponent<Camera>();
            compositingCamera.orthographic = true;
            compositingCamera.orthographicSize = 1f;
            compositingCamera.cullingMask = 1 << compositingLayer;
            compositingCamera.clearFlags = CameraClearFlags.SolidColor;
            compositingCamera.backgroundColor = new Color(0, 0, 0, 0);
            compositingCamera.enabled = false;

            compositingRoot = new GameObject("CompositingRoot").transform;
            compositingRoot.SetParent(transform);
            compositingRoot.localPosition = Vector3.zero;
        }

        private void SetupRenderTexture()
        {
            renderTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
            renderTexture.filterMode = FilterMode.Bilinear;
            renderTexture.Create();
        }

        private void ExcludeCompositingLayerFromAllCameras()
        {
            int mask = ~(1 << compositingLayer);
            foreach (var cam in Camera.allCameras)
            {
                if (cam == compositingCamera) continue;
                cam.cullingMask &= mask;
            }

            if (Camera.main != null && Camera.main != compositingCamera)
                Camera.main.cullingMask &= mask;
        }

        private void CreatePooledRenderer()
        {
            var obj = new GameObject($"Layer_{spritePool.Count}");
            obj.transform.SetParent(compositingRoot);
            obj.transform.localPosition = Vector3.zero;
            obj.layer = compositingLayer;

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Default";
            spritePool.Add(sr);
        }

        private void OnDestroy()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }

        public void RebuildFromData(
            SpeciesInstanceData speciesInstance,
            SpeciesDisplayData speciesData,
            List<EquipmentDisplayInfo> equipment,
            DisplayBuilder builder)
        {
            var pieces = builder.Build(speciesInstance, speciesData, equipment);
            SetPieces(pieces);
        }
    }
}
