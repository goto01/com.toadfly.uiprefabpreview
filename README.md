# UI Prefab Preview

Renders uGUI prefab previews in the Inspector via an off-screen camera, and can auto-select the **UI Preview** tab when a UI prefab is selected.

uGUI does not render through `PreviewRenderUtility` (Canvas meshes are only built for loaded scenes), so this package instantiates the prefab into a temporary world-space canvas, renders it with a real off-screen `Camera` into a `RenderTexture`, tightly frames the painted content, and caches the result.

## Features

- **UI Preview tab** in the Inspector preview area for any `GameObject` containing a uGUI `Graphic`.
- Handles prefabs authored as screen-overlay roots, nested sub-canvases, or bare fragments — inner canvases are neutralized so every `Graphic` renders through the preview camera.
- Content-aware refit: sparse prefabs (e.g. a label inside a full-screen rect) are framed tightly instead of tiny.
- Optional **auto-select** of the UI Preview tab instead of Unity's default Layout Properties preview.
- Settings live in **Project Settings → UI Prefab Preview** and are stored per-user.

## Installation

Add via the Unity Package Manager using the git URL:

```
https://github.com/goto01/com.toadfly.uiprefabpreview.git#0.1.0
```

Or add it to `Packages/manifest.json`:

```json
"com.toadfly.uiprefabpreview": "https://github.com/goto01/com.toadfly.uiprefabpreview.git#0.1.0"
```

## Settings

Configure under **Project Settings → UI Prefab Preview** (per-user):

- **Enabled** — master toggle for the preview and auto-select.
- **Auto Select Tab** — make UI Preview the default preview tab for UI prefabs.
- **Reference Resolution** — canvas size used to lay out the prefab.
- **Max Texture Size** — upper bound for the rendered preview texture.
- **Framing Padding** — extra space around the framed content.
- **Background Color** — preview clear color.

## Requirements

- Unity 6000.2+
- `com.unity.ugui` (UnityEngine.UI)

Editor-only package.
