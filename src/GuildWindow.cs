using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ErenshorGuildLife
{
    internal sealed class GuildWindow
    {
        internal const int CanvasSortOrder = 522;
        internal const float MinimumWidth = 440f;
        internal const float MinimumHeight = 320f;
        private sealed class MemberRowUi
        {
            internal TextMeshProUGUI Level;
            internal TextMeshProUGUI Zone;
        }

        private const int TabRoster = 0;
        private const int TabBulletin = 1;
        private int _tab;

        private GameObject _root;
        private RectTransform _panel;
        private RectTransform _bodyRoot;
        private RectTransform _collapseChevron;
        private GameObject _resizeGripRoot;
        private bool _collapsed;
        private float _expandedHeight;
        private RectTransform _rosterRoot;
        private RectTransform _bulletinRoot;
        private RectTransform _rosterContent;
        private RectTransform _bulletinContent;
        private TextMeshProUGUI _rosterHeading;
        private TextMeshProUGUI _rosterHint;
        private TextMeshProUGUI _bulletinHeading;
        private Button _rosterTab;
        private Button _bulletinTab;
        private Button _clearButton;
        private TextMeshProUGUI _clearLabel;
        private RetainedPosition _position;
        private Action _clearBulletin;
        private GuildSnapshot _snapshot;
        private GuildLifeDocument _document;
        private string _rosterSignature = string.Empty;
        private readonly Dictionary<string, MemberRowUi> _memberRows =
            new Dictionary<string, MemberRowUi>(StringComparer.OrdinalIgnoreCase);
        private int _bulletinCount = -1;
        private float _clearArmedUntil;

        internal void Initialize(float storedX, float storedY, float width, float height,
            Action<float, float> persist, Action<float, float> persistSize, Action close, Action reset)
        {
            Dispose();
            float maxWidth = Mathf.Max(1f, Screen.width - 20f);
            float maxHeight = Mathf.Max(1f, Screen.height - 20f);
            width = Mathf.Clamp(width, Mathf.Min(MinimumWidth, maxWidth), maxWidth);
            height = Mathf.Clamp(height, Mathf.Min(MinimumHeight, maxHeight), maxHeight);
            _root = RetainedUiKit.CreateCanvas("ErenshorGuildLifeCanvas", CanvasSortOrder);
            RectTransform canvas = _root.GetComponent<RectTransform>();
            _panel = RetainedUiKit.CreateRect("GuildLifePanel", canvas);
            RetainedUiKit.AnchorBottomLeft(_panel, 0f, 0f, width, height);
            RetainedUiKit.AddImage(_panel, RetainedUiKit.Panel);
            _panel.gameObject.AddComponent<CanvasGroup>();
            _bodyRoot = RetainedUiKit.CreateRect("Body", _panel);
            RetainedUiKit.Stretch(_bodyRoot, 0f, 0f, 0f, 0f);
            _expandedHeight = height;
            _collapsed = false;
            BuildHeader(close, reset);
            BuildTabs();
            BuildRoster();
            BuildBulletin();
            _position = new RetainedPosition(storedX, storedY, 0.5f, 0.5f, persist);
            _position.Resolve(_panel);
            SuiteResizeHandler resize = RetainedUiKit.AddResizeGrip("ResizeGrip", _panel, _panel, 16f, new Vector2(MinimumWidth, MinimumHeight),
                delegate(float w, float h)
                {
                    _expandedHeight = Mathf.Max(MinimumHeight, h);
                    if (persistSize != null) persistSize(w, h);
                });
            _resizeGripRoot = resize == null ? null : resize.gameObject;
            RetainedUiKit.AddFrame(_panel, 1f);
            UpdateCollapseVisual();
            _root.SetActive(false);
        }

        private void BuildHeader(Action close, Action reset)
        {
            RectTransform header = RetainedUiKit.CreateRect("Header", _panel);
            RetainedUiKit.AnchorTopStretch(header, 0f, 0f, 0f, SuiteWindowChromePolicy.HeaderHeight);
            RetainedUiKit.AddImage(header, RetainedUiKit.Header);
            AddCollapseButton(header);
            TextMeshProUGUI title = RetainedUiKit.AddLabel("Title", header, "ERENSHOR GUILD LIFE", 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            RetainedUiKit.Stretch(title.rectTransform, 40f, 0f, 72f, 0f);
            AddHeaderButton(header, "Reset", "R", -38f, reset);
            AddHeaderButton(header, "Close", "X", -6f, close);
            RetainedUiKit.AddDragSurface("DragSurface", header, _panel, 36f, 72f,
                delegate
                {
                    if (_position == null) return;
                    if (_collapsed) _position.Clamp(_panel);
                    else _position.DragCompleted(_panel);
                });
        }

        private void BuildTabs()
        {
            RectTransform row = RetainedUiKit.CreateRect("Tabs", _bodyRoot);
            row.anchorMin = new Vector2(0f, 1f); row.anchorMax = new Vector2(1f, 1f); row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(10f, -66f); row.offsetMax = new Vector2(-10f, -35f);
            _rosterTab = AddAbsoluteButton(row, "Roster", "Roster", 0f, 92f, delegate { SetTab(TabRoster); });
            _bulletinTab = AddAbsoluteButton(row, "Bulletin", "Bulletin", 98f, 92f, delegate { SetTab(TabBulletin); });
        }

        private void BuildRoster()
        {
            _rosterRoot = RetainedUiKit.CreateRect("RosterView", _bodyRoot);
            _rosterRoot.anchorMin = Vector2.zero; _rosterRoot.anchorMax = Vector2.one;
            _rosterRoot.offsetMin = new Vector2(10f, 10f); _rosterRoot.offsetMax = new Vector2(-10f, -70f);

            _rosterHeading = RetainedUiKit.AddLabel("Heading", _rosterRoot, "", 13f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _rosterHeading.rectTransform.anchorMin = new Vector2(0f, 1f); _rosterHeading.rectTransform.anchorMax = new Vector2(1f, 1f);
            _rosterHeading.rectTransform.pivot = new Vector2(0.5f, 1f); _rosterHeading.rectTransform.offsetMin = new Vector2(0f, -28f); _rosterHeading.rectTransform.offsetMax = Vector2.zero;

            _rosterHint = RetainedUiKit.AddLabel("Hint", _rosterRoot, "", 10f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            _rosterHint.color = RetainedUiKit.Muted;
            _rosterHint.rectTransform.anchorMin = new Vector2(0f, 1f); _rosterHint.rectTransform.anchorMax = new Vector2(1f, 1f);
            _rosterHint.rectTransform.pivot = new Vector2(0.5f, 1f); _rosterHint.rectTransform.offsetMin = new Vector2(0f, -58f); _rosterHint.rectTransform.offsetMax = new Vector2(0f, -30f);

            RectTransform viewport; RectTransform raw;
            ScrollRect scroll = RetainedUiKit.AddScrollRect("RosterScroll", _rosterRoot, false, true, out viewport, out raw);
            RectTransform sr = scroll.GetComponent<RectTransform>();
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = Vector2.zero; sr.offsetMax = new Vector2(0f, -62f);
            _rosterContent = RetainedUiKit.AddVerticalContent("RosterRows", viewport, 3f, 2);
            scroll.content = _rosterContent;
        }

        private void BuildBulletin()
        {
            _bulletinRoot = RetainedUiKit.CreateRect("BulletinView", _bodyRoot);
            _bulletinRoot.anchorMin = Vector2.zero; _bulletinRoot.anchorMax = Vector2.one;
            _bulletinRoot.offsetMin = new Vector2(10f, 10f); _bulletinRoot.offsetMax = new Vector2(-10f, -70f);

            RectTransform top = RetainedUiKit.CreateRect("Top", _bulletinRoot);
            RetainedUiKit.AnchorTopStretch(top, 0f, 0f, 0f, 30f);
            _bulletinHeading = RetainedUiKit.AddLabel("Heading", top, "GUILD BULLETIN", 12f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            _bulletinHeading.rectTransform.anchorMin = Vector2.zero; _bulletinHeading.rectTransform.anchorMax = Vector2.one;
            _bulletinHeading.rectTransform.offsetMin = Vector2.zero; _bulletinHeading.rectTransform.offsetMax = new Vector2(-64f, 0f);
            _clearButton = RetainedUiKit.AddButton("Clear", top, "Clear", ClearBulletin, 58f, 24f, true);
            _clearLabel = _clearButton.GetComponentInChildren<TextMeshProUGUI>();
            RectTransform cr = _clearButton.GetComponent<RectTransform>(); RemoveLayout(cr);
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 0.5f); cr.pivot = new Vector2(1f, 0.5f); cr.anchoredPosition = Vector2.zero; cr.sizeDelta = new Vector2(58f, 24f);

            TextMeshProUGUI hint = RetainedUiKit.AddLabel("Hint", _bulletinRoot,
                "Confirmed roster changes and events shared by compatible mods appear here.", 10f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            hint.color = RetainedUiKit.Muted;
            hint.rectTransform.anchorMin = new Vector2(0f, 1f); hint.rectTransform.anchorMax = new Vector2(1f, 1f);
            hint.rectTransform.pivot = new Vector2(0.5f, 1f); hint.rectTransform.offsetMin = new Vector2(0f, -58f); hint.rectTransform.offsetMax = new Vector2(0f, -32f);

            RectTransform viewport; RectTransform raw;
            ScrollRect scroll = RetainedUiKit.AddScrollRect("BulletinScroll", _bulletinRoot, false, true, out viewport, out raw);
            RectTransform sr = scroll.GetComponent<RectTransform>();
            sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = Vector2.zero; sr.offsetMax = new Vector2(0f, -62f);
            _bulletinContent = RetainedUiKit.AddVerticalContent("BulletinRows", viewport, 7f, 2);
            scroll.content = _bulletinContent;
        }

        internal void Tick(bool visible, GuildSnapshot snapshot, GuildLifeDocument document, Action clearBulletin)
        {
            if (_root == null) return;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible) return;
            bool fitted = RetainedUiKit.FitToScreen(_panel, 10f);
            if (_position != null)
            {
                if (_collapsed) _position.Clamp(_panel);
                else
                {
                    _position.Resolve(_panel);
                    if (fitted) _position.Clamp(_panel);
                }
            }
            _snapshot = snapshot; _document = document; _clearBulletin = clearBulletin;
            if (_collapsed) return;

            string sig = BuildRosterSignature();
            if (SuiteWindowChromePolicy.ShouldRebuildStructure(_rosterSignature, sig))
            {
                _rosterSignature = sig;
                RebuildRosterRows();
            }
            UpdateRosterDynamicValues();
            int count = _document == null ? 0 : _document.Bulletin.Count;
            if (count != _bulletinCount)
            {
                _bulletinCount = count;
                RebuildBulletinRows();
            }
            bool canClear = count > 0;
            if (_clearButton != null) _clearButton.interactable = canClear;
            if (!canClear) _clearArmedUntil = 0f;
            if (_clearLabel != null) _clearLabel.text = Time.unscaledTime < _clearArmedUntil ? "Confirm" : "Clear";
            UpdateTabAppearance();
        }

        internal void ResetTransientState()
        {
            _tab = TabRoster;
            _rosterSignature = string.Empty;
            _memberRows.Clear();
            _bulletinCount = -1;
            _clearArmedUntil = 0f;
        }

        internal void ResetPosition() { if (_position != null) _position.Reset(_panel); }

        internal void Dispose()
        {
            SuiteDragHandler.ForceReleaseIfOwned();
            RetainedUiKit.DestroyRoot(ref _root);
            _panel = null; _bodyRoot = null; _collapseChevron = null; _resizeGripRoot = null;
            _collapsed = false; _expandedHeight = 0f;
            _rosterRoot = null; _bulletinRoot = null; _rosterContent = null; _bulletinContent = null;
            _position = null; _snapshot = null; _document = null; _clearBulletin = null;
            _clearButton = null; _clearLabel = null;
            _rosterSignature = string.Empty; _memberRows.Clear(); _bulletinCount = -1; _clearArmedUntil = 0f;
        }

        private void SetTab(int tab)
        {
            int next = tab == TabBulletin ? TabBulletin : TabRoster;
            if (next != _tab) _clearArmedUntil = 0f;
            _tab = next;
            UpdateTabAppearance();
        }

        private void UpdateTabAppearance()
        {
            if (_rosterRoot != null) _rosterRoot.gameObject.SetActive(_tab == TabRoster);
            if (_bulletinRoot != null) _bulletinRoot.gameObject.SetActive(_tab == TabBulletin);
            SetSelected(_rosterTab, _tab == TabRoster);
            SetSelected(_bulletinTab, _tab == TabBulletin);
        }

        private string BuildRosterSignature()
        {
            if (_snapshot == null) return "null";
            StringBuilder sb = new StringBuilder();
            sb.Append(_snapshot.RuntimeAvailable).Append('|').Append(_snapshot.InGuild).Append('|')
              .Append(_snapshot.GuildId).Append('|').Append(_snapshot.GuildName);
            for (int i = 0; i < _snapshot.Members.Count; i++)
            {
                GuildMemberSnapshot m = _snapshot.Members[i];
                if (m != null) sb.Append('|').Append(m.Name);
            }
            return sb.ToString();
        }

        private void RebuildRosterRows()
        {
            RetainedUiKit.ClearChildren(_rosterContent);
            _memberRows.Clear();
            if (_snapshot == null || !_snapshot.RuntimeAvailable)
            {
                _rosterHeading.text = "GUILD ROSTER UNAVAILABLE";
                _rosterHint.text = "Guild information is not available right now. It will refresh automatically.";
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rosterContent);
                return;
            }
            if (!_snapshot.InGuild)
            {
                _rosterHeading.text = "NO GUILD FOUND";
                _rosterHint.text = "This character is not currently shown in a guild roster.";
                LayoutRebuilder.ForceRebuildLayoutImmediate(_rosterContent);
                return;
            }
            string guildName = string.IsNullOrWhiteSpace(_snapshot.GuildName) ? "GUILD ROSTER" : _snapshot.GuildName;
            _rosterHeading.text = guildName + "  —  " + _snapshot.Members.Count.ToString() + " members";
            _rosterHint.text = "Guild roster is view-only here. Use Erenshor's Guild Manager for guild actions.";

            for (int i = 0; i < _snapshot.Members.Count; i++)
            {
                GuildMemberSnapshot member = _snapshot.Members[i];
                if (member == null) continue;
                RectTransform row = RetainedUiKit.AddHorizontalRow("Member", _rosterContent, 25f, 6f);
                TextMeshProUGUI name = RetainedUiKit.AddLabel("Name", row, member.Name ?? string.Empty, 11f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                LayoutElement nl = name.gameObject.AddComponent<LayoutElement>(); nl.flexibleWidth = 1f; nl.preferredHeight = 25f;
                TextMeshProUGUI level = RetainedUiKit.AddLabel("Level", row, member.Level > 0 ? "Lv " + member.Level.ToString() : "", 10f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
                LayoutElement ll = level.gameObject.AddComponent<LayoutElement>(); ll.preferredWidth = 52f; ll.preferredHeight = 25f;
                TextMeshProUGUI zone = RetainedUiKit.AddLabel("Zone", row,
                    string.IsNullOrWhiteSpace(member.Zone) ? "location unknown" : member.Zone, 10f, FontStyles.Normal, TextAlignmentOptions.MidlineRight);
                zone.color = RetainedUiKit.Muted;
                LayoutElement zl = zone.gameObject.AddComponent<LayoutElement>(); zl.preferredWidth = 170f; zl.preferredHeight = 25f;
                if (!string.IsNullOrWhiteSpace(member.Name))
                    _memberRows[member.Name] = new MemberRowUi { Level = level, Zone = zone };
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rosterContent);
        }

        private void UpdateRosterDynamicValues()
        {
            if (_snapshot == null || !_snapshot.RuntimeAvailable || !_snapshot.InGuild) return;
            for (int i = 0; i < _snapshot.Members.Count; i++)
            {
                GuildMemberSnapshot member = _snapshot.Members[i];
                if (member == null || string.IsNullOrWhiteSpace(member.Name)) continue;
                MemberRowUi row;
                if (!_memberRows.TryGetValue(member.Name, out row) || row == null) continue;
                string level = member.Level > 0 ? "Lv " + member.Level.ToString() : string.Empty;
                string zone = string.IsNullOrWhiteSpace(member.Zone) ? "location unknown" : member.Zone;
                if (row.Level != null && !string.Equals(row.Level.text, level, StringComparison.Ordinal)) row.Level.text = level;
                if (row.Zone != null && !string.Equals(row.Zone.text, zone, StringComparison.Ordinal)) row.Zone.text = zone;
            }
        }

        private void RebuildBulletinRows()
        {
            RetainedUiKit.ClearChildren(_bulletinContent);
            int count = _document == null ? 0 : _document.Bulletin.Count;
            _bulletinHeading.text = "GUILD BULLETIN  —  " + count.ToString() + " entries";
            if (count == 0)
            {
                AddBulletinLabel("No guild news has been recorded yet.", true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(_bulletinContent);
                return;
            }
            int start = Math.Max(0, count - 200);
            for (int i = start; i < count; i++)
            {
                GuildBulletinEntry value = _document.Bulletin[i];
                if (value == null) continue;
                DateTime local = value.TimestampUtc.Kind == DateTimeKind.Utc ? value.TimestampUtc.ToLocalTime() : value.TimestampUtc;
                string prefix = local.ToString("yyyy-MM-dd HH:mm");
                if (!string.IsNullOrWhiteSpace(value.Category)) prefix += " [" + value.Category + "]";
                if (!string.IsNullOrWhiteSpace(value.Actor)) prefix += " " + value.Actor;
                if (!string.IsNullOrWhiteSpace(value.Source)) prefix += " - " + value.Source;
                AddBulletinLabel(prefix + Environment.NewLine + (value.Text ?? string.Empty), false);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bulletinContent);
        }

        private void ClearBulletin()
        {
            if (_document == null || _document.Bulletin.Count == 0 || _clearBulletin == null) return;
            if (Time.unscaledTime >= _clearArmedUntil)
            {
                _clearArmedUntil = Time.unscaledTime + 4f;
                return;
            }
            _clearArmedUntil = 0f;
            _clearBulletin();
        }

        private void AddBulletinLabel(string value, bool muted)
        {
            TextMeshProUGUI label = RetainedUiKit.AddLabel("Entry", _bulletinContent, value, 10.5f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            if (muted) label.color = RetainedUiKit.Muted;
            LayoutElement le = label.gameObject.AddComponent<LayoutElement>(); le.minHeight = 28f; le.preferredHeight = Mathf.Max(28f, label.preferredHeight + 7f);
        }

        private static Button AddAbsoluteButton(RectTransform parent, string name, string label, float x, float width, Action action)
        {
            Button b = RetainedUiKit.AddButton(name, parent, label, action, width, 26f, false);
            RectTransform r = b.GetComponent<RectTransform>(); RemoveLayout(r);
            r.anchorMin = r.anchorMax = new Vector2(0f, 0.5f); r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(x, 0f); r.sizeDelta = new Vector2(width, 26f);
            return b;
        }

        private void AddCollapseButton(RectTransform header)
        {
            Button button = RetainedUiKit.AddButton("Collapse", header, "", ToggleCollapsed, 28f, 24f, false);
            RectTransform rect = button.GetComponent<RectTransform>();
            RemoveLayout(rect);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(4f, 0f);
            rect.sizeDelta = new Vector2(28f, 24f);
            _collapseChevron = button.GetComponent<RectTransform>();
            RetainedUiKit.AddVerticalChevron(_collapseChevron, true);
        }

        private void ToggleCollapsed()
        {
            SetCollapsed(!_collapsed);
        }

        private void SetCollapsed(bool collapsed)
        {
            if (_panel == null || _collapsed == collapsed) return;
            float oldHeight = _panel.rect.height;
            if (collapsed && _expandedHeight < MinimumHeight) _expandedHeight = Mathf.Max(MinimumHeight, oldHeight);
            _collapsed = collapsed;

            float desired = SuiteWindowChromePolicy.ResolveDisplayHeight(_collapsed, _expandedHeight, MinimumHeight);
            if (!_collapsed) desired = Mathf.Min(desired, Mathf.Max(SuiteWindowChromePolicy.CollapsedHeight, Screen.height - 20f));
            _panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, desired);

            Vector2 position = _panel.anchoredPosition;
            position.y = SuiteWindowChromePolicy.PreserveTopBottomY(position.y, oldHeight, desired);
            _panel.anchoredPosition = position;

            if (_bodyRoot != null) _bodyRoot.gameObject.SetActive(!_collapsed);
            if (_resizeGripRoot != null) _resizeGripRoot.SetActive(!_collapsed);
            UpdateCollapseVisual();

            if (_position != null)
            {
                _position.Clamp(_panel);
                if (!_collapsed) _position.DragCompleted(_panel);
            }
        }

        private void UpdateCollapseVisual()
        {
            if (_collapseChevron == null) return;
            for (int i = _collapseChevron.childCount - 1; i >= 0; i--)
                if (_collapseChevron.GetChild(i).name == "Chevron") UnityEngine.Object.Destroy(_collapseChevron.GetChild(i).gameObject);
            // Expanded means click to collapse upward; collapsed means click to expand down.
            RetainedUiKit.AddVerticalChevron(_collapseChevron, !_collapsed);
        }

        private static void AddHeaderButton(RectTransform header, string name, string label, float right, Action action)
        {
            Button b = RetainedUiKit.AddButton(name, header, label, action, 28f, 24f, false);
            RectTransform r = b.GetComponent<RectTransform>(); RemoveLayout(r);
            r.anchorMin = r.anchorMax = new Vector2(1f, 0.5f); r.pivot = new Vector2(1f, 0.5f);
            r.anchoredPosition = new Vector2(right, 0f); r.sizeDelta = new Vector2(28f, 24f);
        }

        private static void SetSelected(Button button, bool selected)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = selected ? RetainedUiKit.Selected : RetainedUiKit.Button;
        }

        private static void RemoveLayout(RectTransform r)
        {
            LayoutElement le = r.GetComponent<LayoutElement>();
            if (le != null) UnityEngine.Object.DestroyImmediate(le);
        }
    }
}
