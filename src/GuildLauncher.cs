using UnityEngine;

namespace ErenshorGuildLife
{
    internal sealed class GuildLauncher
    {
        private const int WindowId = 0x4552474C;
        internal const float Width = LauncherLayout.Width;
        internal const float Height = LauncherLayout.Height;

        // Grip strip owns dragging; the button owns clicking. These must never overlap - see
        // DrawContents. Matches ErenshorJournal's JournalLauncher interaction model exactly.
        // Geometry is sourced from LauncherLayout so GuildLifeCoreTests (Unity-free)
        // can assert the two rects never overlap.
        internal const float GripWidth = LauncherLayout.GripWidth;

        private bool _requestToggle;
        private bool _open;
        private Texture2D _panelTexture;
        private Texture2D _buttonTexture;
        private Texture2D _buttonHoverTexture;
        private Texture2D _buttonOpenTexture;
        private GUIStyle _windowStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _openButtonStyle;
        private GUIStyle _gripStyle;

        internal bool RequestToggle
        {
            get { return _requestToggle; }
        }

        internal Rect Draw(Rect rect, bool open)
        {
            EnsureStyles();
            _open = open;
            _requestToggle = false;
            int previousDepth = GUI.depth;
            Rect result;
            try
            {
                GUI.depth = -55;
                result = GUI.Window(WindowId, rect, DrawContents, GUIContent.none, _windowStyle);
            }
            finally { GUI.depth = previousDepth; }
            return result;
        }

        internal void Dispose()
        {
            Destroy(ref _panelTexture);
            Destroy(ref _buttonTexture);
            Destroy(ref _buttonHoverTexture);
            Destroy(ref _buttonOpenTexture);
            _windowStyle = null;
            _buttonStyle = null;
            _openButtonStyle = null;
            _gripStyle = null;
        }

        private void DrawContents(int id)
        {
            GUI.Label(new Rect(3f, 5f, GripWidth - 4f, Height - 10f), "||", _gripStyle);
            GUIStyle style = _open ? _openButtonStyle : _buttonStyle;
            PureRect buttonRect = LauncherLayout.ButtonRect();
            if (GUI.Button(new Rect(buttonRect.X, buttonRect.Y, buttonRect.Width, buttonRect.Height), "GUILD LIFE", style))
                _requestToggle = true;

            // A narrow grip owns dragging. The action surface remains a pure open/close toggle so
            // clicking it never also moves the launcher (matches JournalLauncher's model - the
            // overlapping full-width DragWindow rect used to swallow clicks meant for the button).
            PureRect dragRect = LauncherLayout.DragRect();
            GUI.DragWindow(new Rect(dragRect.X, dragRect.Y, dragRect.Width, dragRect.Height));
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null) return;
            Color cyan = new Color(0.03f, 0.67f, 0.86f, 0.95f);
            Color soft = new Color(0.13f, 0.55f, 0.68f, 0.90f);
            _panelTexture = Framed(new Color(0.015f, 0.09f, 0.125f, 0.74f), cyan);
            _buttonTexture = Framed(new Color(0.035f, 0.17f, 0.22f, 0.88f), soft);
            _buttonHoverTexture = Framed(new Color(0.12f, 0.38f, 0.48f, 0.94f), cyan);
            _buttonOpenTexture = Framed(new Color(0.08f, 0.30f, 0.36f, 0.96f), cyan);

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _panelTexture;
            _windowStyle.border = new RectOffset(1, 1, 1, 1);
            _windowStyle.padding = new RectOffset(0, 0, 0, 0);

            _buttonStyle = Button(_buttonTexture, _buttonHoverTexture);
            _openButtonStyle = Button(_buttonOpenTexture, _buttonHoverTexture);
            _openButtonStyle.fontStyle = FontStyle.Bold;

            _gripStyle = new GUIStyle(GUI.skin.label);
            _gripStyle.fontSize = 10;
            _gripStyle.fontStyle = FontStyle.Bold;
            _gripStyle.alignment = TextAnchor.MiddleCenter;
            _gripStyle.normal.textColor = new Color(0.56f, 0.88f, 1f, 0.95f);
        }

        private static GUIStyle Button(Texture2D normal, Texture2D hover)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = hover;
            style.normal.textColor = new Color(0.84f, 0.94f, 1f, 1f);
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
            style.fontSize = 11;
            style.border = new RectOffset(1, 1, 1, 1);
            return style;
        }

        private static Texture2D Framed(Color center, Color edge)
        {
            Texture2D texture = new Texture2D(3, 3, TextureFormat.RGBA32, false);
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 3; x++)
                    texture.SetPixel(x, y, x == 0 || x == 2 || y == 0 || y == 2 ? edge : center);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Point;
            texture.Apply(false, true);
            return texture;
        }

        private static void Destroy(ref Texture2D texture)
        {
            if (texture == null) return;
            Object.Destroy(texture);
            texture = null;
        }
    }
}
