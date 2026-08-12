using System;
using UnityEngine;

namespace ErenshorGuildLife
{
    internal sealed class GuildWindow
    {
        private const int WindowId = 0x45524757;
        private const float HeaderHeight = 31f;
        private const int TabRoster = 0;
        private const int TabBulletin = 1;

        private GuildSnapshot _snapshot;
        private GuildLifeDocument _document;
        private Action _clearBulletin;
        private bool _requestClose;
        private int _tab;
        private Vector2 _rosterScroll;
        private Vector2 _bulletinScroll;
        private Rect _currentRect;
        private bool _resizing;
        private Vector2 _resizeDelta;

        private Texture2D _panelTexture;
        private Texture2D _buttonTexture;
        private Texture2D _buttonHoverTexture;
        private Texture2D _selectedTexture;
        private Texture2D _dangerTexture;
        private GUIStyle _windowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _selectedButtonStyle;
        private GUIStyle _dangerButtonStyle;
        private GUIStyle _closeButtonStyle;
        private GUIStyle _resizeGripStyle;

        internal bool RequestClose
        {
            get { return _requestClose; }
        }

        internal Rect Draw(Rect rect, GuildSnapshot snapshot, GuildLifeDocument document, Action clearBulletin)
        {
            EnsureStyles();
            _snapshot = snapshot;
            _document = document;
            _clearBulletin = clearBulletin;
            _requestClose = false;
            _currentRect = rect;
            _resizeDelta = Vector2.zero;

            int previousDepth = GUI.depth;
            Rect result;
            try
            {
                GUI.depth = -60;
                result = GUI.Window(WindowId, rect, DrawContents, GUIContent.none, _windowStyle);
            }
            finally { GUI.depth = previousDepth; }

            if (_resizeDelta != Vector2.zero)
            {
                result.width += _resizeDelta.x;
                result.height += _resizeDelta.y;
            }
            return result;
        }

        internal void Dispose()
        {
            Destroy(ref _panelTexture);
            Destroy(ref _buttonTexture);
            Destroy(ref _buttonHoverTexture);
            Destroy(ref _selectedTexture);
            Destroy(ref _dangerTexture);
            _windowStyle = null;
            _titleStyle = null;
            _sectionStyle = null;
            _bodyStyle = null;
            _hintStyle = null;
            _buttonStyle = null;
            _selectedButtonStyle = null;
            _dangerButtonStyle = null;
            _closeButtonStyle = null;
            _resizeGripStyle = null;
        }

        private void DrawContents(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal(GUILayout.Height(HeaderHeight));
            GUILayout.Label("ERENSHOR GUILD LIFE", _titleStyle, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("X", _closeButtonStyle, GUILayout.Width(28f), GUILayout.Height(22f)))
                _requestClose = true;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Roster", _tab == TabRoster ? _selectedButtonStyle : _buttonStyle, GUILayout.Width(92f), GUILayout.Height(26f)))
                _tab = TabRoster;
            if (GUILayout.Button("Bulletin", _tab == TabBulletin ? _selectedButtonStyle : _buttonStyle, GUILayout.Width(92f), GUILayout.Height(26f)))
                _tab = TabBulletin;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            if (_tab == TabRoster) DrawRoster();
            else DrawBulletin();

            GUILayout.EndVertical();
            DrawResizeGrip();
            GUI.DragWindow(new Rect(0f, 0f, Mathf.Max(0f, _currentRect.width - 42f), HeaderHeight));
        }

        private void DrawRoster()
        {
            if (_snapshot == null || !_snapshot.RuntimeAvailable)
            {
                GUILayout.Label("GUILD DATA UNAVAILABLE", _sectionStyle);
                GUILayout.Label(_snapshot == null ? "No snapshot." : _snapshot.Diagnostic, _hintStyle);
                return;
            }

            if (!_snapshot.InGuild)
            {
                GUILayout.Label("NO VERIFIED PLAYER GUILD", _sectionStyle);
                GUILayout.Label(_snapshot.Diagnostic, _hintStyle);
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label(_snapshot.GuildName, _titleStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label(_snapshot.Members.Count.ToString() + " members", _hintStyle, GUILayout.Width(90f));
            GUILayout.EndHorizontal();
            GUILayout.Label("Read-only native roster. Guild actions remain in Erenshor's Guild Manager.", _hintStyle);
            GUILayout.Space(4f);

            _rosterScroll = GUILayout.BeginScrollView(_rosterScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            for (int i = 0; i < _snapshot.Members.Count; i++)
            {
                GuildMemberSnapshot member = _snapshot.Members[i];
                if (member == null) continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(member.Name, _bodyStyle, GUILayout.ExpandWidth(true));
                string level = member.Level > 0 ? "Lv " + member.Level.ToString() : string.Empty;
                GUILayout.Label(level, _hintStyle, GUILayout.Width(48f));
                GUILayout.Label(string.IsNullOrWhiteSpace(member.Zone) ? "location unknown" : member.Zone, _hintStyle, GUILayout.Width(160f));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.Label(_snapshot.Diagnostic, _hintStyle);
        }

        private void DrawBulletin()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("VERIFIED GUILD BULLETIN", _sectionStyle, GUILayout.ExpandWidth(true));
            int count = _document == null ? 0 : _document.Bulletin.Count;
            GUILayout.Label(count.ToString() + " entries", _hintStyle, GUILayout.Width(70f));
            if (count > 0 && GUILayout.Button("Clear", _dangerButtonStyle, GUILayout.Width(58f), GUILayout.Height(24f)))
            {
                if (_clearBulletin != null) _clearBulletin();
            }
            GUILayout.EndHorizontal();

            GUILayout.Label("Only native roster changes or events explicitly reported by another mod are recorded.", _hintStyle);
            GUILayout.Space(4f);

            _bulletinScroll = GUILayout.BeginScrollView(_bulletinScroll, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_document == null || _document.Bulletin.Count == 0)
            {
                GUILayout.Label("Nothing verified has happened in this bulletin yet.", _hintStyle);
            }
            else
            {
                for (int i = Math.Max(0, _document.Bulletin.Count - 200); i < _document.Bulletin.Count; i++)
                {
                    GuildBulletinEntry value = _document.Bulletin[i];
                    DateTime local = value.TimestampUtc.Kind == DateTimeKind.Utc ? value.TimestampUtc.ToLocalTime() : value.TimestampUtc;
                    string prefix = local.ToString("yyyy-MM-dd HH:mm");
                    if (!string.IsNullOrWhiteSpace(value.Category)) prefix += " [" + value.Category + "]";
                    if (!string.IsNullOrWhiteSpace(value.Actor)) prefix += " " + value.Actor;
                    if (!string.IsNullOrWhiteSpace(value.Source)) prefix += " - " + value.Source;
                    GUILayout.Label(prefix, _hintStyle);
                    GUILayout.Label(value.Text, _bodyStyle);
                    GUILayout.Space(6f);
                }
            }
            GUILayout.EndScrollView();
        }

        private void DrawResizeGrip()
        {
            Rect grip = new Rect(Mathf.Max(0f, _currentRect.width - 22f), Mathf.Max(0f, _currentRect.height - 20f), 18f, 16f);
            GUI.Label(grip, "//", _resizeGripStyle);
            Event current = Event.current;
            if (current == null) return;

            if (!_resizing && current.type == EventType.MouseDown && current.button == 0 && grip.Contains(current.mousePosition))
            {
                _resizing = true;
                current.Use();
                return;
            }

            if (_resizing && current.type == EventType.MouseDrag && current.button == 0)
            {
                _resizeDelta += current.delta;
                current.Use();
                return;
            }

            if (_resizing && current.type == EventType.MouseUp && current.button == 0)
            {
                _resizing = false;
                current.Use();
            }
        }

        private void EnsureStyles()
        {
            if (_windowStyle != null) return;
            Color cyan = new Color(0.03f, 0.67f, 0.86f, 0.95f);
            Color soft = new Color(0.13f, 0.55f, 0.68f, 0.90f);
            _panelTexture = Framed(new Color(0.015f, 0.09f, 0.125f, 0.92f), cyan);
            _buttonTexture = Framed(new Color(0.035f, 0.17f, 0.22f, 0.90f), soft);
            _buttonHoverTexture = Framed(new Color(0.12f, 0.38f, 0.48f, 0.95f), cyan);
            _selectedTexture = Framed(new Color(0.08f, 0.30f, 0.36f, 0.96f), cyan);
            _dangerTexture = Framed(new Color(0.19f, 0.15f, 0.09f, 0.90f), new Color(0.65f, 0.49f, 0.27f, 0.92f));

            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _panelTexture;
            _windowStyle.border = new RectOffset(1, 1, 1, 1);
            _windowStyle.padding = new RectOffset(12, 12, 8, 10);

            _titleStyle = new GUIStyle(GUI.skin.label);
            _titleStyle.fontSize = 15;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.normal.textColor = new Color(0.56f, 0.88f, 1f, 1f);

            _sectionStyle = new GUIStyle(GUI.skin.label);
            _sectionStyle.fontSize = 11;
            _sectionStyle.fontStyle = FontStyle.Bold;
            _sectionStyle.normal.textColor = new Color(0.56f, 0.78f, 0.88f, 1f);

            _bodyStyle = new GUIStyle(GUI.skin.label);
            _bodyStyle.fontSize = 12;
            _bodyStyle.wordWrap = true;
            _bodyStyle.normal.textColor = new Color(0.92f, 0.94f, 0.92f, 1f);

            _hintStyle = new GUIStyle(GUI.skin.label);
            _hintStyle.fontSize = 10;
            _hintStyle.wordWrap = true;
            _hintStyle.normal.textColor = new Color(0.63f, 0.76f, 0.80f, 1f);

            _buttonStyle = Button(_buttonTexture, _buttonHoverTexture, Color.white);
            _selectedButtonStyle = Button(_selectedTexture, _buttonHoverTexture, new Color(0.88f, 1f, 0.98f, 1f));
            _selectedButtonStyle.fontStyle = FontStyle.Bold;
            _dangerButtonStyle = Button(_dangerTexture, _buttonHoverTexture, new Color(1f, 0.94f, 0.74f, 1f));
            _closeButtonStyle = Button(_buttonTexture, _buttonHoverTexture, new Color(0.84f, 0.94f, 1f, 1f));

            _resizeGripStyle = new GUIStyle(GUI.skin.label);
            _resizeGripStyle.fontSize = 11;
            _resizeGripStyle.alignment = TextAnchor.MiddleCenter;
            _resizeGripStyle.normal.textColor = new Color(0.56f, 0.88f, 1f, 0.90f);
        }

        private static GUIStyle Button(Texture2D normal, Texture2D hover, Color text)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = hover;
            style.normal.textColor = text;
            style.hover.textColor = Color.white;
            style.active.textColor = Color.white;
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
            UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
