using UnityEditor;
using UnityEngine;

namespace Toadfly.UIPrefabPreview
{
    /// <summary>
    /// Exposes the UI prefab preview settings in Project Settings.
    /// The underlying data is stored per-user (see <see cref="UIPrefabPreviewSettings"/>).
    /// </summary>
    internal static class UIPrefabPreviewSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/Toadfly/UI Prefab Preview", SettingsScope.Project)
            {
                label = "UI Prefab Preview",
                keywords = new[] { "UI", "Preview", "Prefab", "Toadfly", "Inspector" },
                guiHandler = _ =>
                {
                    var settings = UIPrefabPreviewSettings.instance;
                    var so = new SerializedObject(settings);

                    EditorGUILayout.HelpBox(
                        "Stored per-user (Unity preferences folder), not shared via version control.",
                        MessageType.None);

                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.PropertyField(
                        so.FindProperty("enabled"),
                        new GUIContent("Enable package", "Master switch. Turn off to disable the package entirely: no UI Preview tab and no auto-selection."));

                    if (!so.FindProperty("enabled").boolValue) {
                        EditorGUILayout.HelpBox("Package is disabled. It does nothing until re-enabled.", MessageType.Info);
                    }

                    using (new EditorGUI.DisabledScope(!so.FindProperty("enabled").boolValue)) {
                        EditorGUILayout.PropertyField(
                            so.FindProperty("autoSelectTab"),
                            new GUIContent("Auto-select UI Preview tab", "Make UI Preview the default preview tab when a UI prefab is selected."));
                        EditorGUILayout.PropertyField(
                            so.FindProperty("referenceResolution"),
                            new GUIContent("Reference Resolution", "Canvas size used to lay out stretch-anchored (full-screen) prefabs."));
                        EditorGUILayout.PropertyField(
                            so.FindProperty("maxTextureSize"),
                            new GUIContent("Max Texture Size", "Largest dimension of the rendered preview texture."));
                        EditorGUILayout.PropertyField(
                            so.FindProperty("framingPadding"),
                            new GUIContent("Framing Padding", "Extra margin around the content (1.0 = tight fit)."));
                        EditorGUILayout.PropertyField(
                            so.FindProperty("backgroundColor"),
                            new GUIContent("Background Color", "Preview background; alpha 0 = transparent."));
                    }

                    if (EditorGUI.EndChangeCheck()) {
                        so.ApplyModifiedPropertiesWithoutUndo();
                        settings.Persist();
                    }
                }
            };
        }
    }
}
