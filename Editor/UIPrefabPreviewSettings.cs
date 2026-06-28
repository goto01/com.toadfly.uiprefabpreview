using UnityEditor;
using UnityEngine;

namespace Toadfly.UIPrefabPreview
{
    /// <summary>
    /// Per-user settings for the UI prefab preview, stored in the Unity preferences
    /// folder (user scope) rather than committed to version control.
    /// </summary>
    [FilePath("Toadfly/UIPrefabPreview.asset", FilePathAttribute.Location.PreferencesFolder)]
    internal sealed class UIPrefabPreviewSettings : ScriptableSingleton<UIPrefabPreviewSettings>
    {
        [SerializeField]
        private bool enabled = true;

        [SerializeField]
        private bool autoSelectTab = true;

        [SerializeField]
        private Vector2 referenceResolution = new Vector2(1920f, 1080f);

        [SerializeField]
        private int maxTextureSize = 1024;

        [SerializeField]
        private float framingPadding = 1.03f;

        [SerializeField]
        private Color backgroundColor = new Color(0f, 0f, 0f, 0f);

        public bool Enabled => enabled;

        public bool AutoSelectTab => autoSelectTab;

        public Vector2 ReferenceResolution => referenceResolution;

        public int MaxTextureSize => maxTextureSize;

        public float FramingPadding => framingPadding;

        public Color BackgroundColor => backgroundColor;

        public void Persist()
        {
            Save(true);
        }
    }
}
