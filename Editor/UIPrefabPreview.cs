using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Toadfly.UIPrefabPreview
{
    /// <summary>
    /// Adds a "UI Preview" tab to the Inspector preview area for any GameObject that
    /// contains a uGUI Graphic. uGUI does not render through PreviewRenderUtility
    /// (Canvas meshes are only built for loaded scenes), so the prefab is rendered with
    /// a real off-screen Camera into a RenderTexture and the result is cached.
    /// </summary>
    [CustomPreview(typeof(GameObject))]
    public class UIPrefabPreview : ObjectPreview
    {
        private const int PreviewLayer = 31;
        private const float ContentRefitThreshold = 0.85f;

        private Texture2D _cached;
        private int _cachedInstanceId;

        public override bool HasPreviewGUI()
        {
            return UIPrefabPreviewSettings.instance.Enabled && IsRenderableUiPrefab(target as GameObject);
        }

        public override GUIContent GetPreviewTitle()
        {
            return new GUIContent("UI Preview");
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            if (Event.current.type != EventType.Repaint) {
                return;
            }

            var go = target as GameObject;
            if (go == null) {
                return;
            }

            if (_cached == null || _cachedInstanceId != go.GetInstanceID()) {
                Dispose();
                _cached = Render(go);
                _cachedInstanceId = go.GetInstanceID();
            }

            if (_cached != null) {
                GUI.DrawTexture(rect, _cached, ScaleMode.ScaleToFit, true);
            }
        }

        public override void Cleanup()
        {
            base.Cleanup();
            Dispose();
        }

        private void Dispose()
        {
            if (_cached != null) {
                Object.DestroyImmediate(_cached);
                _cached = null;
            }
            _cachedInstanceId = 0;
        }

        internal static bool IsRenderableUiPrefab(GameObject go)
        {
            return go != null && go.GetComponentInChildren<Graphic>(true) != null;
        }

        private static Texture2D Render(GameObject source)
        {
            var settings = UIPrefabPreviewSettings.instance;
            var referenceResolution = settings.ReferenceResolution;
            var maxSize = Mathf.Max(16, settings.MaxTextureSize);
            var padding = Mathf.Max(0.5f, settings.FramingPadding);
            var bgColor = settings.BackgroundColor;

            GameObject root = null;
            GameObject instance = null;
            GameObject camGo = null;
            var previousActive = RenderTexture.active;

            try {
                instance = Object.Instantiate(source);
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;

                var camera = CreateCamera(out camGo, bgColor);

                // Wrap the prefab in a single world-space canvas and remove any canvases
                // inside it, so every Graphic renders through our camera regardless of how
                // the prefab was authored (screen-overlay root, nested sub-canvas, none).
                root = new GameObject(
                    "UIPreviewRoot",
                    typeof(RectTransform),
                    typeof(Canvas));
                root.hideFlags = HideFlags.HideAndDontSave;

                var rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = referenceResolution;
                rootRect.position = Vector3.zero;
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

                instance.transform.SetParent(root.transform, false);

                if (instance.transform is RectTransform fragmentRect && Stretches(fragmentRect)) {
                    fragmentRect.anchorMin = new Vector2(0.5f, 0.5f);
                    fragmentRect.anchorMax = new Vector2(0.5f, 0.5f);
                    fragmentRect.sizeDelta = referenceResolution;
                    fragmentRect.anchoredPosition = Vector2.zero;
                }

                NeutralizeInnerCanvases(instance, camera);

                foreach (var t in root.GetComponentsInChildren<Transform>(true)) {
                    t.gameObject.layer = PreviewLayer;
                }

                var layoutRect = root.GetComponentInChildren<RectTransform>(true);
                if (layoutRect != null) {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRect);
                }
                Canvas.ForceUpdateCanvases();

                var bounds = ComputeGraphicBounds(root);
                if (bounds.size.x < 1f || bounds.size.y < 1f) {
                    bounds = new Bounds(Vector3.zero, referenceResolution);
                }

                var texture = RenderToTexture(camera, bounds, maxSize, padding);

                // Refit to the actually-painted content so sparse prefabs (e.g. a label
                // inside a full-screen rect) are framed tightly instead of tiny.
                if (TryGetContentBounds(texture, camera, out var contentBounds)
                    && (contentBounds.size.x < bounds.size.x * ContentRefitThreshold
                        || contentBounds.size.y < bounds.size.y * ContentRefitThreshold)) {
                    Object.DestroyImmediate(texture);
                    texture = RenderToTexture(camera, contentBounds, maxSize, padding);
                }

                return texture;
            }
            catch (System.Exception e) {
                Debug.LogError($"[UIPrefabPreview] Failed to render '{source.name}': {e}");
                return null;
            }
            finally {
                RenderTexture.active = previousActive;
                if (camGo != null) {
                    Object.DestroyImmediate(camGo);
                }
                if (root != null) {
                    Object.DestroyImmediate(root);
                }
                else if (instance != null) {
                    Object.DestroyImmediate(instance);
                }
            }
        }

        private static Camera CreateCamera(out GameObject camGo, Color backgroundColor)
        {
            camGo = new GameObject("UIPreviewCamera");
            camGo.hideFlags = HideFlags.HideAndDontSave;

            var camera = camGo.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.cullingMask = 1 << PreviewLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = backgroundColor;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 5000f;
            camera.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            return camera;
        }

        private static void NeutralizeInnerCanvases(GameObject instance, Camera camera)
        {
            foreach (var raycaster in instance.GetComponentsInChildren<GraphicRaycaster>(true)) {
                Object.DestroyImmediate(raycaster);
            }

            foreach (var canvas in instance.GetComponentsInChildren<Canvas>(true)) {
                if (canvas == null) {
                    continue;
                }

                try {
                    Object.DestroyImmediate(canvas);
                }
                catch (System.Exception) {
                    // A component depends on the Canvas; fall back to forcing world space.
                    canvas.renderMode = RenderMode.WorldSpace;
                    canvas.worldCamera = camera;
                }
            }
        }

        private static Texture2D RenderToTexture(
            Camera camera,
            Bounds bounds,
            int maxSize,
            float padding)
        {
            var aspect = bounds.size.x / bounds.size.y;
            var width = maxSize;
            var height = Mathf.Max(1, Mathf.RoundToInt(maxSize / aspect));
            if (height > maxSize) {
                height = maxSize;
                width = Mathf.Max(1, Mathf.RoundToInt(maxSize * aspect));
            }

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;

            try {
                camera.orthographicSize = bounds.size.y * 0.5f * padding;
                camera.aspect = aspect;
                camera.targetTexture = rt;
                camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -1000f);
                camera.Render();

                RenderTexture.active = rt;
                var texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
                texture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                texture.Apply();
                texture.hideFlags = HideFlags.HideAndDontSave;
                return texture;
            }
            finally {
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        private static bool TryGetContentBounds(Texture2D texture, Camera camera, out Bounds bounds)
        {
            bounds = default;
            var width = texture.width;
            var height = texture.height;
            var pixels = texture.GetPixels32();

            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (var y = 0; y < height; y++) {
                for (var x = 0; x < width; x++) {
                    if (pixels[y * width + x].a <= 10) {
                        continue;
                    }
                    if (x < minX) { minX = x; }
                    if (x > maxX) { maxX = x; }
                    if (y < minY) { minY = y; }
                    if (y > maxY) { maxY = y; }
                }
            }

            if (maxX < minX || maxY < minY) {
                return false;
            }

            var worldHeight = camera.orthographicSize * 2f;
            var worldWidth = worldHeight * camera.aspect;
            var origin = camera.transform.position;

            var worldMinX = origin.x + ((float)minX / width - 0.5f) * worldWidth;
            var worldMaxX = origin.x + ((float)(maxX + 1) / width - 0.5f) * worldWidth;
            var worldMinY = origin.y + ((float)minY / height - 0.5f) * worldHeight;
            var worldMaxY = origin.y + ((float)(maxY + 1) / height - 0.5f) * worldHeight;

            bounds = new Bounds();
            bounds.SetMinMax(
                new Vector3(worldMinX, worldMinY, 0f),
                new Vector3(worldMaxX, worldMaxY, 0f));
            return true;
        }

        private static bool Stretches(RectTransform rect)
        {
            return rect.anchorMin != rect.anchorMax;
        }

        private static Bounds ComputeGraphicBounds(GameObject hierarchyRoot)
        {
            var corners = new Vector3[4];
            var any = false;
            var min = Vector3.zero;
            var max = Vector3.zero;

            foreach (var graphic in hierarchyRoot.GetComponentsInChildren<Graphic>(true)) {
                if (!graphic.isActiveAndEnabled) {
                    continue;
                }

                graphic.rectTransform.GetWorldCorners(corners);
                for (var i = 0; i < 4; i++) {
                    if (!any) {
                        min = max = corners[i];
                        any = true;
                    }
                    else {
                        min = Vector3.Min(min, corners[i]);
                        max = Vector3.Max(max, corners[i]);
                    }
                }
            }

            if (!any) {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }

    /// <summary>
    /// Makes the "UI Preview" tab the default selection in the Inspector preview dropdown
    /// whenever a UI prefab is selected, instead of Unity's Layout Properties preview.
    /// </summary>
    [InitializeOnLoad]
    internal static class UIPrefabPreviewAutoSelect
    {
        private static readonly System.Type InspectorType =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        private static readonly FieldInfo PreviewsField = InspectorType?.GetField(
            "m_Previews",
            BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo SelectedField = InspectorType?.GetField(
            "m_SelectedPreview",
            BindingFlags.NonPublic | BindingFlags.Instance);

        private static int _framesLeft;

        static UIPrefabPreviewAutoSelect()
        {
            Selection.selectionChanged += OnSelectionChanged;
        }

        private static void OnSelectionChanged()
        {
            if (InspectorType == null || PreviewsField == null || SelectedField == null) {
                return;
            }

            var settings = UIPrefabPreviewSettings.instance;
            var isUiPrefab = UIPrefabPreview.IsRenderableUiPrefab(Selection.activeObject as GameObject);

            if (!settings.Enabled || !settings.AutoSelectTab || !isUiPrefab) {
                _framesLeft = 0;
                EditorApplication.update -= Tick;
                return;
            }

            _framesLeft = 30;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (_framesLeft-- <= 0) {
                EditorApplication.update -= Tick;
                return;
            }

            // Re-apply every frame for the whole window: the preview list is rebuilt
            // (with fresh instances) when the selection changes, which resets the choice.
            TrySelect();
        }

        private static void TrySelect()
        {
            foreach (var obj in Resources.FindObjectsOfTypeAll(InspectorType)) {
                var previews = PreviewsField.GetValue(obj) as IList;
                if (previews == null) {
                    continue;
                }

                foreach (var preview in previews) {
                    if (!(preview is UIPrefabPreview)) {
                        continue;
                    }

                    if (!ReferenceEquals(SelectedField.GetValue(obj), preview)) {
                        SelectedField.SetValue(obj, preview);
                        ((EditorWindow)obj).Repaint();
                    }
                    break;
                }
            }
        }
    }
}
