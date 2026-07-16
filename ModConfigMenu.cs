using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ModConfigMenu
{
    [BepInPlugin("zzzz.mcm.config", "Mod Config Menu", "1.0.0")]
    public class ModConfigMenuPlugin : BaseUnityPlugin
    {
        internal static ModConfigMenuPlugin Instance;

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("<", "<\u200B");
        }

        internal static void Log(string msg)
        {
            try { if (Instance != null && Instance.Logger != null) Instance.Logger.LogInfo(msg); }
            catch { Debug.Log("[MCM] " + msg); }
        }

        internal static ConfigEntry<KeyboardShortcut> toggleKey;
        private static ConfigEntry<bool> showKeybindWarnings;

        private static List<ModInfo> mods = new List<ModInfo>();
        private static int selectedModIndex = -1;
        private static bool showingModList = true;
        internal static GameObject currentPanel;
        private static TextMeshProUGUI panelTitle;
        private static GameObject panelBackBtn;
        private static GameObject panelActionBar;
        private static Transform contentArea;
        private static TMP_FontAsset uiFont;
        private static bool fontLoaded;

        internal static bool capturingKey;
        internal static EntryInfo keyCaptureEntry;
        internal static TextMeshProUGUI keyCaptureDisplay;
        internal static float keyCaptureTimeout;
        private static Image _captureBtnImg;

        private static Sprite panelSprite;
        private static Sprite innerPanelSprite;
        private static Sprite buttonSprite;
        private static Sprite inputFieldSprite;
        private static Sprite toggleOnSprite;
        private static Sprite toggleOffSprite;
        private static Sprite scrollbarSprite;
        private static Sprite handleSprite;
        private static bool spritesLoaded;


        private static readonly Color ColText      = new Color(0.15f, 0.15f, 0.18f);
        private static readonly Color ColTextLight = new Color(0.85f, 0.85f, 0.88f);
        private static readonly Color ColDimText   = new Color(0.40f, 0.40f, 0.45f);
        private static readonly Color ColBackdrop  = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color ColAccent    = new Color(0.30f, 0.40f, 0.55f);

        private void Awake()
        {
            Instance = this;

            toggleKey = Config.Bind("General", "ToggleKey", new KeyboardShortcut(KeyCode.F5),
                "Keyboard shortcut to open/close the Mods panel");
            showKeybindWarnings = Config.Bind("General", "KeybindWarnings", true,
                "Shows a warning for mods sharing keybinds");
            showKeybindWarnings.SettingChanged += (sender, args) =>
            {
                DetectKeybindConflicts();
                if (contentArea != null)
                {
                    foreach (Transform child in contentArea) Destroy(child.gameObject);
                    if (showingModList) BuildModList(contentArea);
                    else BuildContent(contentArea);
                }
            };

            var captureObj = new GameObject("MCM_CaptureRunner");
            DontDestroyOnLoad(captureObj);
            captureObj.hideFlags = HideFlags.HideAndDontSave;
            captureObj.AddComponent<KeyCaptureRunner>();

            var pollerObj = new GameObject("MCM_Poller");
            DontDestroyOnLoad(pollerObj);
            pollerObj.hideFlags = HideFlags.HideAndDontSave;
            pollerObj.AddComponent<TitleScreenPoller>();

            try { Harmony.CreateAndPatchAll(typeof(QuitToTitlePatch)); }
            catch (Exception ex) { Logger.LogInfo("MCM: QuitToTitle patch failed - " + ex.Message); }

            try { Harmony.CreateAndPatchAll(typeof(UIConfigButtonPatch)); }
            catch (Exception ex) { Logger.LogInfo("MCM: Harmony patch failed - " + ex.Message); }

            ScanAllConfigs();
        }

        private void Update()
        {
            // Panel toggle and key capture are handled by KeyCaptureRunner
        }

        //  Sprite & font loading 

        private static void LoadAtlasSprites()
        {
            // Lazy font init - TMP assets not loaded during Awake
            if (!fontLoaded)
            {
                fontLoaded = true;
                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fonts != null)
                {
                    foreach (var f in fonts)
                    {
                        if (f != null && f.name == "Saira Condensed SemiBold Static")
                        { uiFont = f; break; }
                    }
                }
                if (uiFont == null) uiFont = TMP_Settings.defaultFontAsset;
            }

            if (spritesLoaded) return;

            // Load sprites from the game's UI_Atlas texture
            Texture2D atlas = null;
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            if (textures != null)
            {
                foreach (var t in textures)
                {
                    if (t != null && t.name == "UI_Atlas") { atlas = t; break; }
                }
            }

            if (atlas != null)
            {
                const float ppu = 48f;
                panelSprite = Sprite.Create(atlas, new Rect(672f, 1016f, 32f, 32f), new Vector2(16f, 16f), ppu, 0u, SpriteMeshType.Tight, new Vector4(7f, 10f, 7f, 8f));
                innerPanelSprite = Sprite.Create(atlas, new Rect(671f, 935f, 34f, 34f), new Vector2(17f, 17f), ppu, 0u, SpriteMeshType.Tight, new Vector4(14f, 14f, 14f, 15f));
                buttonSprite = Sprite.Create(atlas, new Rect(128f, 1521f, 37f, 31f), new Vector2(0.5f, 0.5f), ppu, 0u, SpriteMeshType.Tight, new Vector4(11f, 15f, 11f, 14f));
                inputFieldSprite = Sprite.Create(atlas, new Rect(912f, 859f, 32f, 13f), new Vector2(16f, 16f), ppu, 0u, SpriteMeshType.Tight, new Vector4(3f, 3f, 3f, 3f));
                toggleOnSprite = Sprite.Create(atlas, new Rect(849f, 793f, 30f, 16f), new Vector2(15f, 8f), ppu, 0u, SpriteMeshType.Tight, Vector4.zero);
                toggleOffSprite = Sprite.Create(atlas, new Rect(849f, 776f, 30f, 16f), new Vector2(15f, 8f), ppu, 0u, SpriteMeshType.Tight, Vector4.zero);
                scrollbarSprite = Sprite.Create(atlas, new Rect(658f, 848f, 8f, 32f), new Vector2(4f, 16f), ppu, 0u, SpriteMeshType.Tight, new Vector4(0f, 2f, 0f, 2f));
                handleSprite = Sprite.Create(atlas, new Rect(656f, 888f, 8f, 40f), new Vector2(4f, 20f), ppu, 0u, SpriteMeshType.Tight, new Vector4(2f, 20f, 2f, 19f));
                spritesLoaded = true;
                return;
            }

            // Fallback: use sprite references from active game UI
            HarvestSpritesFromScene();
            if (panelSprite != null || buttonSprite != null)
                spritesLoaded = true;
        }

        private static void HarvestSpritesFromScene()
        {
            foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (img == null || img.sprite == null) continue;
                var s = img.sprite;

                if (panelSprite == null && s.border.sqrMagnitude > 50f)
                    { panelSprite = s; innerPanelSprite = s; }
                if (buttonSprite == null && s.border.sqrMagnitude > 10f
                    && img.GetComponent<Button>() != null)
                    buttonSprite = s;
                if (toggleOnSprite == null && s.border.sqrMagnitude < 1f
                    && s.rect.width > 20f && s.rect.width < 60f)
                    { toggleOnSprite = s; toggleOffSprite = s; }
                if (inputFieldSprite == null && s.border.sqrMagnitude > 0f
                    && img.GetComponent<TMP_InputField>() != null)
                    inputFieldSprite = s;
                if (scrollbarSprite == null && s.border.sqrMagnitude > 0f
                    && img.GetComponent<Scrollbar>() != null)
                    scrollbarSprite = s;
            }
        }

        /// <summary>Apply a 9-sliced sprite to an Image, or fall back to a flat colour.</summary>
        private static void ApplySpriteOrColor(Image img, Sprite sprite, Color fallbackColor)
        {
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = fallbackColor;
            }
        }

        //  Config discovery 

        private static void ScanAllConfigs()
        {
            mods.Clear();

            var plugins = new List<BaseUnityPlugin>();

            // Source 1: Chainloader.PluginInfos
            foreach (var kvp in Chainloader.PluginInfos)
            {
                var p = kvp.Value.Instance as BaseUnityPlugin;
                if (p != null && !plugins.Contains(p)) plugins.Add(p);
            }

            // Source 2: FindObjectsOfType
            var found = UnityEngine.Object.FindObjectsOfType(typeof(BaseUnityPlugin));
            if (found != null)
            {
                foreach (var obj in found)
                {
                    var p = obj as BaseUnityPlugin;
                    if (p != null && !plugins.Contains(p)) plugins.Add(p);
                }
            }

            foreach (var plugin in plugins)
            {
                var meta = plugin.Info.Metadata;
                string guid = meta != null ? meta.GUID : plugin.GetType().FullName;
                string name = meta != null ? meta.Name : plugin.GetType().Name;

                var mod = new ModInfo
                {
                    Guid = guid,
                    Name = name,
                    ConfigFile = plugin.Config
                };

                string lastSection = null;

                foreach (var kv in plugin.Config)
                {
                    var entry = kv.Value;
                    if (entry == null) continue;

                    var info = new EntryInfo
                    {
                        Def = kv.Key,
                        Entry = entry,
                        Tooltip = (entry.Description != null && entry.Description.Description != null)
                            ? entry.Description.Description : ""
                    };

                    // Determine control type from the setting type
                    Type st = entry.SettingType;

                    if (st == typeof(bool))
                    {
                        info.ControlType = EntryControlType.Bool;
                    }
                    else if (st == typeof(int))
                    {
                        TryReadRange(entry, info);
                        if (info.HasRange)
                            info.ControlType = EntryControlType.Int;
                        else
                            info.ControlType = EntryControlType.Textbox; // no range > text input
                    }
                    else if (st == typeof(float))
                    {
                        TryReadRange(entry, info);
                        if (info.HasRange)
                        {
                            info.ControlType = EntryControlType.Float;
                        }
                        else
                        {
                            info.ControlType = EntryControlType.Textbox;
                        }
                    }
                    else if (st == typeof(string))
                    {
                        info.ControlType = EntryControlType.Textbox;
                        TryReadList(entry, info);
                    }
                    else if (st == typeof(KeyboardShortcut) || st == typeof(KeyCode))
                    {
                        info.ControlType = EntryControlType.Keybind;
                    }
                    else
                    {
                        continue; // unsupported type > skip
                    }

                    string section = info.Def.Section ?? "General";
                    if (section != lastSection)
                    {
                        lastSection = section;
                        mod.Sections.Add(section);
                    }

                    mod.Entries.Add(info);
                }

                mods.Add(mod);
            }

            DetectKeybindConflicts();
        }

        internal static void DetectKeybindConflicts()
        {
            foreach (var m in mods) { m.HasKeybindConflicts = false; foreach (var e in m.Entries) e.ConflictWarning = null; }
            if (!showKeybindWarnings.Value) return;
            var keyMap = new Dictionary<string, List<string>>();
            foreach (var m in mods)
                foreach (var e in m.Entries)
                {
                    if (e.ControlType != EntryControlType.Keybind) continue;
                    string key = KeybindDisplayText(e);
                    if (key == "None") continue;
                    if (!keyMap.ContainsKey(key)) keyMap[key] = new List<string>();
                    keyMap[key].Add(Sanitize(m.Name ?? m.Guid));
                }
            foreach (var m in mods)
                foreach (var e in m.Entries)
                {
                    if (e.ControlType != EntryControlType.Keybind) continue;
                    string key = KeybindDisplayText(e);
                    if (key == "None") continue;
                    List<string> users;
                    if (keyMap.TryGetValue(key, out users) && users.Count > 1)
                    {
                        var others = new List<string>();
                        var myName = Sanitize(m.Name ?? m.Guid);
                        foreach (var u in users) if (u != myName) others.Add(u);
                        if (others.Count == 0) others.Add("this mod");
                        if (others.Count > 0)
                        {
                            e.ConflictWarning = "Shared with " + string.Join(", ", others.ToArray());
                            m.HasKeybindConflicts = true;
                        }
                    }
                }
        }

        private static void TryReadRange(ConfigEntryBase entry, EntryInfo info)
        {
            var av = entry.Description != null ? entry.Description.AcceptableValues : null;
            if (av == null) return;

            Type t = av.GetType();
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(AcceptableValueRange<>))
                return;

            try
            {
                var minProp = t.GetProperty("MinValue");
                var maxProp = t.GetProperty("MaxValue");
                if (minProp != null) info.MinValue = minProp.GetValue(av, null);
                if (maxProp != null) info.MaxValue = maxProp.GetValue(av, null);
                info.HasRange = (minProp != null && maxProp != null);
            }
            catch { /* HasRange stays false */ }
        }

        private static void TryReadList(ConfigEntryBase entry, EntryInfo info)
        {
            var av = entry.Description != null ? entry.Description.AcceptableValues : null;
            if (av == null) return;

            Type t = av.GetType();
            if (!t.IsGenericType || t.GetGenericTypeDefinition() != typeof(AcceptableValueList<>))
                return;

            try
            {
                var prop = t.GetProperty("AcceptableValues");
                if (prop == null) return;

                var arr = prop.GetValue(av, null) as Array;
                if (arr != null && arr.Length > 0)
                {
                    info.Choices = new string[arr.Length];
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var item = arr.GetValue(i);
                        info.Choices[i] = (item != null) ? item.ToString() : "";
                    }
                    info.ControlType = EntryControlType.Dropdown;
                }
            }
            catch { /* stays a textbox */ }
        }

        //  Panel open/close

        internal static void OpenMenu()
        {
            try
            {
                if (currentPanel != null) DoCloseMenu();
                if (mods.Count == 0) return;
                showingModList = true;
                LoadAtlasSprites();
                currentPanel = BuildPanel();
            }
            catch (Exception ex)
            {
                Log("MCM: OpenMenu error - " + ex.Message);
            }
        }

        // Snapshot for revert-on-close without save
        private static Dictionary<ConfigEntryBase, object> _snapshot = new Dictionary<ConfigEntryBase, object>();
        private static bool _didSave;
        private static GameObject _confirmOverlay;
        private static bool _confirmGoBack;

        private static bool HasUnsavedChanges()
        {
            if (_didSave) return false;
            if (_snapshot.Count == 0) return false;
            foreach (var kv in _snapshot)
            {
                if (kv.Key.BoxedValue == null && kv.Value == null) continue;
                if (kv.Key.BoxedValue == null || !kv.Key.BoxedValue.Equals(kv.Value))
                    return true;
            }
            return false;
        }

        private static void SnapshotCurrentMod()
        {
            _snapshot.Clear();
            _didSave = false;
            if (selectedModIndex >= 0 && selectedModIndex < mods.Count)
                foreach (var e in mods[selectedModIndex].Entries)
                    _snapshot[e.Entry] = e.Entry.BoxedValue;
        }

        internal static void TryCloseMenu()
        {
            _confirmGoBack = false;
            if (HasUnsavedChanges())
            {
                ShowConfirmOverlay();
            }
            else
            {
                DoCloseMenu();
            }
        }

        private static void ShowConfirmOverlay()
        {
            if (_confirmOverlay != null || currentPanel == null) return;
            var innerPanel = currentPanel.transform.Find("Panel/Inner");
            if (innerPanel == null) return;

            _confirmOverlay = new GameObject("ConfirmOverlay", typeof(RectTransform), typeof(Image));
            _confirmOverlay.transform.SetParent(innerPanel, false);
            _confirmOverlay.GetComponent<RectTransform>().FullStretch();
            _confirmOverlay.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

            var msg = MakeText(_confirmOverlay.transform, "Save Changes?",
                24, ColTextLight, TextAlignmentOptions.Center);
            var mr = msg.GetRect();
            mr.anchorMin = new Vector2(0.5f, 0.55f);
            mr.anchorMax = new Vector2(0.5f, 0.55f);
            mr.sizeDelta = new Vector2(300f, 36f);

            var saveBtn = MakeButton(_confirmOverlay.transform, "Save", new Vector2(140f, 42f), () =>
            {
                if (selectedModIndex >= 0 && selectedModIndex < mods.Count)
                    mods[selectedModIndex].ConfigFile.Save();
                _didSave = true;
                Destroy(_confirmOverlay);
                _confirmOverlay = null;
                if (_confirmGoBack) NavigateToModList(true);
                else DoCloseMenu();
            });
            saveBtn.GetRect().SetAnchors(new Vector2(0.35f, 0.4f), new Vector2(0.35f, 0.4f));

            var exitBtn = MakeButton(_confirmOverlay.transform, "Exit", new Vector2(140f, 42f), () =>
            {
                Destroy(_confirmOverlay);
                _confirmOverlay = null;
                if (_confirmGoBack) NavigateToModList(true);
                else DoCloseMenu();
            });
            exitBtn.GetRect().SetAnchors(new Vector2(0.65f, 0.4f), new Vector2(0.65f, 0.4f));
        }

        private static void DoCloseMenu()
        {
            // Revert unsaved changes
            if (!_didSave)
            {
                foreach (var kv in _snapshot)
                    kv.Key.BoxedValue = kv.Value;
            }
            _snapshot.Clear();
            if (_confirmOverlay != null) { Destroy(_confirmOverlay); _confirmOverlay = null; }

            ReenableBackdrop();
            capturingKey = false;
            keyCaptureEntry = null;
            keyCaptureDisplay = null;
            ResetCaptureButtonColor();

            if (currentPanel != null)
            {
                Destroy(currentPanel);
                currentPanel = null;
                contentArea = null;
                panelTitle = null;
                panelBackBtn = null;
                panelActionBar = null;
            }
        }

        /// <summary>Switch to the mod list view without destroying the panel.</summary>
        private static void NavigateToModList(bool skipConfirm = false)
        {
            if (capturingKey) CancelKeyCapture();
            if (!skipConfirm)
            {
                _confirmGoBack = true;
                if (HasUnsavedChanges()) { ShowConfirmOverlay(); return; }
            }
            if (!_didSave)
                foreach (var kv in _snapshot) kv.Key.BoxedValue = kv.Value;
            _snapshot.Clear();
            showingModList = true;
            if (contentArea != null)
            {
                foreach (Transform child in contentArea)
                    Destroy(child.gameObject);
                var vp = contentArea.parent;
                if (vp != null) { var sv = vp.parent; if (sv != null) { var sr = sv.GetComponent<ScrollRect>(); if (sr != null) sr.verticalNormalizedPosition = 1f; } }
                BuildModList(contentArea);
            }
            if (panelBackBtn != null) panelBackBtn.SetActive(false);
            if (panelActionBar != null) panelActionBar.SetActive(false);
            if (panelTitle != null) panelTitle.text = "Mods";
        }

        /// <summary>Switch to a specific mod's config editor view in-place.</summary>
        private static void NavigateToConfigEditor(int modIndex, bool snapshot = true)
        {
            if (capturingKey) CancelKeyCapture();
            if (modIndex < 0 || modIndex >= mods.Count) return;
            showingModList = false;
            selectedModIndex = modIndex;
            if (contentArea != null)
            {
                foreach (Transform child in contentArea)
                    Destroy(child.gameObject);
                var vp = contentArea.parent;
                if (vp != null) { var sv = vp.parent; if (sv != null) { var sr = sv.GetComponent<ScrollRect>(); if (sr != null) sr.verticalNormalizedPosition = 1f; } }
                BuildContent(contentArea);
            }
            if (snapshot) SnapshotCurrentMod();
            if (panelBackBtn != null) panelBackBtn.SetActive(true);
            if (panelActionBar != null) panelActionBar.SetActive(true);
            if (panelTitle != null)
                panelTitle.text = Sanitize(mods[selectedModIndex].Name ?? mods[selectedModIndex].Guid);
        }

        //  Panel construction

        private static GameObject BuildPanel()
        {
            if (mods.Count == 0) return null;

            var root = new GameObject("MCM_Root", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdrop.transform.SetParent(root.transform, false);
            FullRect(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().color = ColBackdrop;
            var bb = backdrop.GetComponent<Button>();
            bb.transition = Selectable.Transition.None;
            bb.onClick.AddListener(() => TryCloseMenu());

            const int panelW = 860;
            const int panelH = 580;

            var outerPanel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            outerPanel.transform.SetParent(root.transform, false);
            var opr = outerPanel.GetComponent<RectTransform>();
            opr.anchorMin = new Vector2(0.5f, 0.5f);
            opr.anchorMax = new Vector2(0.5f, 0.5f);
            opr.anchoredPosition = Vector2.zero;
            opr.sizeDelta = new Vector2(panelW, panelH);
            ApplySpriteOrColor(outerPanel.GetComponent<Image>(), panelSprite,
                new Color(0.08f, 0.08f, 0.08f));

            var innerPanel = new GameObject("Inner", typeof(RectTransform), typeof(Image));
            innerPanel.transform.SetParent(outerPanel.transform, false);
            var ipr = innerPanel.GetComponent<RectTransform>();
            ipr.anchorMin = Vector2.zero;
            ipr.anchorMax = Vector2.one;
            ipr.offsetMin = new Vector2(24f, 30f);
            ipr.offsetMax = new Vector2(-24f, -24f);
            ApplySpriteOrColor(innerPanel.GetComponent<Image>(), innerPanelSprite,
                new Color(0.12f, 0.12f, 0.14f));

            panelTitle = MakeText(innerPanel.transform, "Mods", 33, ColText,
                TextAlignmentOptions.Center);
            var titleLabel = panelTitle;
            var ttr = titleLabel.GetRect();
            ttr.anchorMin = new Vector2(0f, 1f);
            ttr.anchorMax = new Vector2(1f, 1f);
            ttr.pivot = new Vector2(0.5f, 1f);
            ttr.anchoredPosition = new Vector2(0f, -27f);
            ttr.sizeDelta = new Vector2(0f, 45f);

            panelBackBtn = MakeButton(innerPanel.transform, "Back", new Vector2(90f, 42f), () =>
            {
                NavigateToModList();
            });
            var backBtn = panelBackBtn;
            var bbr = backBtn.GetRect();
            bbr.anchorMin = new Vector2(0f, 1f);
            bbr.anchorMax = new Vector2(0f, 1f);
            bbr.pivot = new Vector2(0f, 1f);
            bbr.anchoredPosition = new Vector2(6f, -15f);
            backBtn.SetActive(false);

            // Close button cloned from the game's exit dialog
            GameObject closeBtn = null;
            var allYn = Resources.FindObjectsOfTypeAll<YesNoDialogueUI>();
            YesNoDialogueUI yn = (allYn != null && allYn.Length > 0) ? allYn[0] : null;
            if (yn != null && yn.noButton != null)
            {
                closeBtn = UnityEngine.Object.Instantiate(yn.noButton.gameObject, root.transform);
                closeBtn.name = "CloseBtn";
                // Strip localisation
                foreach (var comp in closeBtn.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    var tn = comp.GetType().Name;
                    if (tn.Contains("Local") || tn.Contains("Translate"))
                        UnityEngine.Object.DestroyImmediate(comp);
                }
                foreach (var tmp in closeBtn.GetComponentsInChildren<TextMeshProUGUI>(true))
                    tmp.text = "Close";
                // Click catcher for our handler
                var catcher = new GameObject("ClickCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
                catcher.transform.SetParent(closeBtn.transform, false);
                var cr2 = catcher.GetComponent<RectTransform>();
                cr2.anchorMin = Vector2.zero; cr2.anchorMax = Vector2.one; cr2.sizeDelta = Vector2.zero;
                catcher.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
                catcher.GetComponent<Image>().raycastTarget = true;
                var cb = catcher.GetComponent<Button>();
                cb.transition = Selectable.Transition.None;
                cb.onClick.AddListener(() => TryCloseMenu());
            }
            else
            {
                closeBtn = MakeButton(root.transform, "Close", new Vector2(120f, 42f), TryCloseMenu);
            }
            // Replace the original button's click handler
            var origBtn = closeBtn.GetComponent<Button>();
            if (origBtn != null)
            {
                origBtn.onClick.RemoveAllListeners();
                origBtn.onClick.AddListener(() => TryCloseMenu());
            }

            var cbr = closeBtn.GetRect();
            cbr.anchorMin = new Vector2(0.5f, 0.5f);
            cbr.anchorMax = new Vector2(0.5f, 0.5f);
            cbr.pivot = new Vector2(1f, 1f);
            cbr.anchoredPosition = new Vector2(panelW / 2f, -panelH / 2f - 2f);
            cbr.localScale = new Vector3(2f, 2f, 1f);

            float contentTop = -69f;

            var scrollView = new GameObject("ScrollView", typeof(RectTransform),
                typeof(ScrollRect), typeof(Image));
            scrollView.transform.SetParent(innerPanel.transform, false);
            var svr = scrollView.GetComponent<RectTransform>();
            svr.anchorMin = Vector2.zero;
            svr.anchorMax = Vector2.one;
            svr.offsetMin = new Vector2(6f, 75f);
            svr.offsetMax = new Vector2(-33f, contentTop);
            scrollView.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            var scrollRect = scrollView.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;

            var viewport = new GameObject("Viewport", typeof(RectTransform),
                typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollView.transform, false);
            var vpr = viewport.GetRect();
            FullRect(vpr);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            scrollRect.viewport = vpr;

            var content = new GameObject("Content", typeof(RectTransform),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var cr = content.GetRect();
            cr.anchorMin = new Vector2(0f, 1f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.pivot = new Vector2(0.5f, 1f);
            cr.sizeDelta = new Vector2(0f, 0f);

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 2f;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = content.GetComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = cr;
            contentArea = content.transform;

            BuildScrollbar(scrollView.transform, scrollRect);

            panelActionBar = new GameObject("ActionBar", typeof(RectTransform), typeof(Image),
                typeof(HorizontalLayoutGroup));
            panelActionBar.transform.SetParent(innerPanel.transform, false);
            var abr = panelActionBar.GetRect();
            abr.anchorMin = new Vector2(0f, 0f);
            abr.anchorMax = new Vector2(1f, 0f);
            abr.pivot = new Vector2(0.5f, 0f);
            abr.sizeDelta = new Vector2(0f, 66f);
            ApplySpriteOrColor(panelActionBar.GetComponent<Image>(), innerPanelSprite,
                new Color(0.08f, 0.08f, 0.10f));

            var abLayout = panelActionBar.GetComponent<HorizontalLayoutGroup>();
            abLayout.spacing = 15f;
            abLayout.padding = new RectOffset(21, 21, 9, 9);
            abLayout.childAlignment = TextAnchor.MiddleCenter;
            abLayout.childForceExpandWidth = false;
            abLayout.childForceExpandHeight = false;
            abLayout.childControlWidth = false;
            abLayout.childControlHeight = false;

            MakeButton(panelActionBar.transform, "Restore Defaults", new Vector2(280f, 45f), () =>
            {
                if (selectedModIndex < 0 || selectedModIndex >= mods.Count) return;
                var mod = mods[selectedModIndex];
                foreach (var e in mod.Entries)
                    e.Entry.BoxedValue = e.Entry.DefaultValue;
                DetectKeybindConflicts();
                NavigateToConfigEditor(selectedModIndex, false);
            });

            MakeButton(panelActionBar.transform, "Save", new Vector2(120f, 45f), () =>
            {
                if (selectedModIndex < 0 || selectedModIndex >= mods.Count) return;
                mods[selectedModIndex].ConfigFile.Save();
                _didSave = true;
            });

            MakeButton(panelActionBar.transform, "Save & Close", new Vector2(220f, 45f), () =>
            {
                if (selectedModIndex >= 0 && selectedModIndex < mods.Count)
                    mods[selectedModIndex].ConfigFile.Save();
                _didSave = true;
                DoCloseMenu();
            });

            panelActionBar.SetActive(false);

            if (showingModList || selectedModIndex < 0 || selectedModIndex >= mods.Count)
            {
                showingModList = true;
                backBtn.SetActive(false);
                panelActionBar.SetActive(false);
                titleLabel.text = "Mods";
                BuildModList(content.transform);
            }
            else
            {
                backBtn.SetActive(true);
                panelActionBar.SetActive(true);
                titleLabel.text = Sanitize(mods[selectedModIndex].Name ?? mods[selectedModIndex].Guid);
                BuildContent(content.transform);
                SnapshotCurrentMod();
            }

            return root;
        }

        /// <summary>Build the mod-selection list (first view).</summary>
        private static void BuildModList(Transform contentTransform)
        {
            if (mods.Count == 0)
            {
                MakeText(contentTransform, "No configurable mods found.", 14, ColDimText);
                return;
            }

            foreach (var mod in mods)
            {
                int idx = mods.IndexOf(mod);
                string name = mod.Name ?? mod.Guid;
                int entryCount = mod.Entries.Count;
                bool hasConfig = entryCount > 0;

                var row = new GameObject("ModRow_" + name,
                    typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                row.transform.SetParent(contentTransform, false);

                var le = row.GetComponent<LayoutElement>();
                le.minHeight = 63f;
                le.preferredHeight = 63f;

                var img = row.GetComponent<Image>();
                ApplySpriteOrColor(img, buttonSprite, new Color(0.14f, 0.14f, 0.18f));
                img.raycastTarget = true;

                var btn = row.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.ColorTint;
                btn.colors = ColorBlock.defaultColorBlock;
                var nav = btn.navigation;
                nav.mode = Navigation.Mode.None;
                btn.navigation = nav;

                btn.onClick.AddListener(() =>
                {
                    if (mods[idx].Entries.Count > 0)
                        NavigateToConfigEditor(idx, true);
                });

                // Mod name
                string displayName = Sanitize(name);
                if (mod.HasKeybindConflicts)
                    displayName += "  <size=14><color=#CC2222>⚠ Shared Keybinds</color></size>";
                var nameText = MakeText(row.transform, displayName, 24, ColTextLight,
                    TextAlignmentOptions.MidlineLeft);
                var ntr = nameText.GetRect();
                ntr.anchorMin = new Vector2(0f, 0.5f);
                ntr.anchorMax = new Vector2(1f, 0.5f);
                ntr.anchoredPosition = new Vector2(10f, 10f);
                ntr.sizeDelta = new Vector2(-100f, 24f);

                // Subtitle
                string sub;
                if (hasConfig)
                    sub = string.Format("{0} configurable entr{1}", entryCount,
                        entryCount == 1 ? "y" : "ies");
                else
                    sub = "Not configurable";
                var subText = MakeText(row.transform, sub, 15,
                    hasConfig ? ColTextLight : ColDimText,
                    TextAlignmentOptions.MidlineLeft);
                var str = subText.GetRect();
                str.anchorMin = new Vector2(0f, 0.5f);
                str.anchorMax = new Vector2(1f, 0.5f);
                str.anchoredPosition = new Vector2(10f, -10f);
                str.sizeDelta = new Vector2(-100f, 18f);
            }
        }

        private static void BuildScrollbar(Transform scrollViewTransform, ScrollRect scrollRect)
        {
            var sbGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            sbGo.transform.SetParent(scrollViewTransform, false);
            var sr = sbGo.GetRect();
            sr.anchorMin = new Vector2(1f, 0f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.sizeDelta = new Vector2(16f, 0f);
            sr.anchoredPosition = new Vector2(10f, 0f);
            ApplySpriteOrColor(sbGo.GetComponent<Image>(), scrollbarSprite,
                new Color(0.06f, 0.06f, 0.08f));

            var scrollbar = sbGo.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            var slidingArea = new GameObject("SlidingArea", typeof(RectTransform));
            slidingArea.transform.SetParent(sbGo.transform, false);
            var sa = slidingArea.GetRect();
            FullRect(sa);
            sa.offsetMin = new Vector2(2f, 4f);
            sa.offsetMax = new Vector2(-2f, -4f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            handle.transform.SetParent(slidingArea.transform, false);
            var hr = handle.GetRect();
            FullRect(hr);
            handle.GetComponent<LayoutElement>().minHeight = 30f;
            ApplySpriteOrColor(handle.GetComponent<Image>(), handleSprite,
                new Color(0.30f, 0.30f, 0.35f));

            scrollbar.handleRect = hr;
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollRect.verticalScrollbar = scrollbar;
        }

        //  Content population
        private static void BuildContent(Transform contentTransform)
        {
            if (selectedModIndex < 0 || selectedModIndex >= mods.Count) return;
            var mod = mods[selectedModIndex];

            if (mod.Entries.Count == 0)
            {
                MakeText(contentTransform, "This mod has no configurable entries.", 14, ColDimText);
                return;
            }

            string currentSection = null;

            for (int i = 0; i < mod.Entries.Count; i++)
            {
                var entry = mod.Entries[i];
                string section = entry.Def.Section ?? "General";

                // Section header
                if (section != currentSection)
                {
                    currentSection = section;

                    if (i > 0)
                    {
                        var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
                        spacer.transform.SetParent(contentTransform, false);
                        var le = spacer.GetComponent<LayoutElement>();
                        le.minHeight = 6f;
                        le.preferredHeight = 6f;
                    }

                    var headerGo = MakeText(contentTransform, section, 21, ColText,
                        TextAlignmentOptions.MidlineLeft);
                    headerGo.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
                }

                BuildOptionRow(contentTransform, entry, mod);
            }
        }

        private static void BuildOptionRow(Transform parent, EntryInfo entry, ModInfo mod)
        {
            var row = new GameObject("Row_" + entry.Def.Key,
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(parent, false);

            var le = row.GetComponent<LayoutElement>();
            le.minHeight = 54f;
            le.preferredHeight = 54f;

            if (innerPanelSprite != null)
            {
                row.GetComponent<Image>().sprite = innerPanelSprite;
                row.GetComponent<Image>().type = Image.Type.Sliced;
                row.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.9f);
            }
            else
            {
                row.GetComponent<Image>().color = new Color(0.10f, 0.10f, 0.10f, 0.5f);
            }

            // Left side: key label
            bool hasTooltip = !string.IsNullOrEmpty(entry.Tooltip);

            string labelText = Sanitize(entry.Def.Key);
            if (!string.IsNullOrEmpty(entry.ConflictWarning))
                labelText += "  <size=14><color=#CC2222>⚠ " + entry.ConflictWarning + "</color></size>";

            var keyLabel = MakeText(row.transform, labelText, 20, ColText,
                TextAlignmentOptions.MidlineLeft);
            var klr = keyLabel.GetRect();
            if (hasTooltip)
            {
                klr.anchorMin = new Vector2(0f, 0.5f);
                klr.anchorMax = new Vector2(0.58f, 0.5f);
                klr.anchoredPosition = new Vector2(24f, 8f);
                klr.sizeDelta = new Vector2(-8f, 22f);

                var tip = MakeText(row.transform, Sanitize(entry.Tooltip), 14, ColText,
                    TextAlignmentOptions.MidlineLeft);
                var tlr = tip.GetRect();
                tlr.anchorMin = new Vector2(0f, 0.5f);
                tlr.anchorMax = new Vector2(0.58f, 0.5f);
                tlr.anchoredPosition = new Vector2(24f, -8f);
                tlr.sizeDelta = new Vector2(-8f, 18f);
            }
            else
            {
                klr.anchorMin = new Vector2(0f, 0f);
                klr.anchorMax = new Vector2(0.58f, 1f);
                klr.anchoredPosition = new Vector2(24f, 0f);
                klr.sizeDelta = new Vector2(-8f, 0f);
            }

            //  Right side: interactive control
            var controlArea = new GameObject("Control", typeof(RectTransform));
            controlArea.transform.SetParent(row.transform, false);
            var car = controlArea.GetRect();
            car.anchorMin = new Vector2(0.6f, 0f);
            car.anchorMax = new Vector2(1f, 1f);
            car.anchoredPosition = Vector2.zero;
            car.sizeDelta = new Vector2(-16f, 0f);

            switch (entry.ControlType)
            {
                case EntryControlType.Bool:     BuildBoolControl(controlArea.transform, entry, mod); break;
                case EntryControlType.Int:      BuildIntControl(controlArea.transform, entry, mod); break;
                case EntryControlType.Float:    BuildFloatControl(controlArea.transform, entry, mod); break;
                case EntryControlType.Textbox:  BuildTextboxControl(controlArea.transform, entry, mod); break;
                case EntryControlType.Dropdown: BuildDropdownControl(controlArea.transform, entry, mod); break;
                case EntryControlType.Keybind:  BuildKeybindControl(controlArea.transform, entry, mod); break;
            }
        }

        // Control builders

        private static void BuildBoolControl(Transform parent, EntryInfo entry, ModInfo mod)
        {
            bool val = (entry.Entry.BoxedValue is bool) ? (bool)entry.Entry.BoxedValue : false;

            var toggle = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Button));
            toggle.transform.SetParent(parent, false);
            var tr = toggle.GetRect();
            tr.anchorMin = new Vector2(1f, 0.5f);
            tr.anchorMax = new Vector2(1f, 0.5f);
            tr.pivot = new Vector2(1f, 0.5f);
            tr.anchoredPosition = Vector2.zero;
            tr.sizeDelta = new Vector2(72f, 36f);
            tr.anchoredPosition = new Vector2(-32f, 0f);

            var img = toggle.GetComponent<Image>();
            var btn = toggle.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.SpriteSwap;

            SpriteState ss = btn.spriteState;
            ss.highlightedSprite = val ? toggleOnSprite : toggleOffSprite;
            ss.pressedSprite = val ? toggleOffSprite : toggleOnSprite;
            ss.disabledSprite = val ? toggleOnSprite : toggleOffSprite;
            btn.spriteState = ss;

            ApplyToggleSprite(img, val);

            btn.onClick.AddListener(() =>
            {
                bool newVal = !(bool)entry.Entry.BoxedValue;
                entry.Entry.BoxedValue = newVal;
                ApplyToggleSprite(img, newVal);
                SpriteState nss = btn.spriteState;
                nss.highlightedSprite = newVal ? toggleOnSprite : toggleOffSprite;
                nss.pressedSprite = newVal ? toggleOffSprite : toggleOnSprite;
                nss.disabledSprite = newVal ? toggleOnSprite : toggleOffSprite;
                btn.spriteState = nss;
                _didSave = false;
            });
        }

        private static void ApplyToggleSprite(Image img, bool on)
        {
            if (on && toggleOnSprite != null)
            {
                img.sprite = toggleOnSprite;
                img.color = Color.white;
            }
            else if (!on && toggleOffSprite != null)
            {
                img.sprite = toggleOffSprite;
                img.color = Color.white;
            }
            else
            {
                img.color = on
                    ? new Color(0.35f, 0.55f, 0.35f)
                    : new Color(0.45f, 0.25f, 0.25f);
            }
        }

        //  Int slider

        private static void BuildIntControl(Transform parent, EntryInfo entry, ModInfo mod)
        {
            int min = (entry.MinValue is int) ? (int)entry.MinValue : 0;
            int max = (entry.MaxValue is int) ? (int)entry.MaxValue : 0;
            int val = (entry.Entry.BoxedValue is int) ? (int)entry.Entry.BoxedValue : 0;

            BuildSliderControl(parent, val, min, max, true, (v) =>
            {
                entry.Entry.BoxedValue = Mathf.RoundToInt(v);
            });
        }

        //  Float slider 

        private static void BuildFloatControl(Transform parent, EntryInfo entry, ModInfo mod)
        {
            float min = (entry.MinValue is float) ? (float)entry.MinValue : 0f;
            float max = (entry.MaxValue is float) ? (float)entry.MaxValue : 0f;
            float val = (entry.Entry.BoxedValue is float) ? (float)entry.Entry.BoxedValue : 0f;

            BuildSliderControl(parent, val, min, max, false, (v) =>
            {
                entry.Entry.BoxedValue = v;
            });
        }

        /// <summary>Build a Unity UI Slider with value display.</summary>
        private static void BuildSliderControl(Transform parent, float val, float min, float max,
            bool integer, Action<float> onChanged)
        {
            var row = new GameObject("SliderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var rr = row.GetRect();
            rr.anchorMin = new Vector2(0.5f, 0.5f);
            rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = new Vector2(300f, 39f);
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.spacing = 6f;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childControlWidth = false;
            hl.childControlHeight = false;

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row.transform, false);
            sliderGo.GetRect().sizeDelta = new Vector2(200f, 20f);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(sliderGo.transform, false);
            var bgr = bg.GetRect();
            bgr.anchorMin = new Vector2(0f, 0.5f);
            bgr.anchorMax = new Vector2(1f, 0.5f);
            bgr.pivot = new Vector2(0.5f, 0.5f);
            bgr.sizeDelta = new Vector2(0f, 4f);
            bgr.anchoredPosition = Vector2.zero;
            bg.GetComponent<Image>().color = ColText;

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var far = fillArea.GetRect();
            far.anchorMin = new Vector2(0f, 0.5f);
            far.anchorMax = new Vector2(1f, 0.5f);
            far.pivot = new Vector2(0f, 0.5f);
            far.sizeDelta = new Vector2(0f, 6f);
            far.anchoredPosition = Vector2.zero;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fillArea.transform, false);
            fill.GetRect().FullStretch();
            fill.GetComponent<Image>().color = new Color(0.50f, 0.32f, 0.14f);

            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(sliderGo.transform, false);
            var hndr = handle.GetRect();
            hndr.anchorMin = new Vector2(0.5f, 1f);
            hndr.anchorMax = new Vector2(0.5f, 0f);
            hndr.pivot = new Vector2(0.5f, 0.5f);
            hndr.sizeDelta = new Vector2(8f, 0f);
            hndr.offsetMin = new Vector2(-6f, 0f);
            hndr.offsetMax = new Vector2(6f, 0f);
            var hi = handle.GetComponent<Image>();
            ApplySpriteOrColor(hi, buttonSprite, new Color(0.55f, 0.65f, 0.80f));

            var slider = sliderGo.GetComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = hi;
            slider.transition = Selectable.Transition.ColorTint;
            slider.colors = ColorBlock.defaultColorBlock;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = integer;

            // Clamp value to range and normalise for the slider
            if (val < min || val > max)
            {
                val = Mathf.Clamp(val, min, max);
                onChanged(val);
            }
            float t = (max > min) ? (val - min) / (max - min) : 0f;
            slider.value = Mathf.Lerp(min, max, t);

            // Value display
            string disp = integer ? ((int)val).ToString() : val.ToString("F2");
            var label = MakeText(row.transform, disp, 18, ColText, TextAlignmentOptions.Center);
            label.GetRect().sizeDelta = new Vector2(70f, 39f);

            slider.onValueChanged.AddListener(v =>
            {
                label.text = integer ? ((int)v).ToString() : v.ToString("F2");
                onChanged(v);
            });
        }

        //  Textbox (TMP_InputField) 

        private static void BuildTextboxControl(Transform parent, EntryInfo entry, ModInfo mod)
        {
            string val = ReadBoxedAsString(entry);

            var fieldGo = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            fieldGo.transform.SetParent(parent, false);
            var fieldR = fieldGo.GetRect();
            fieldR.anchorMin = new Vector2(0f, 0.5f);
            fieldR.anchorMax = new Vector2(1f, 0.5f);
            fieldR.pivot = new Vector2(0.5f, 0.5f);
            fieldR.anchoredPosition = Vector2.zero;
            fieldR.sizeDelta = new Vector2(0f, 39f);
            ApplySpriteOrColor(fieldGo.GetComponent<Image>(), inputFieldSprite,
                new Color(0.08f, 0.08f, 0.08f));

            var inputField = fieldGo.GetComponent<TMP_InputField>();

            var textArea = new GameObject("TextArea", typeof(RectTransform));
            textArea.transform.SetParent(fieldGo.transform, false);
            var taR = textArea.GetRect();
            taR.anchorMin = Vector2.zero;
            taR.anchorMax = Vector2.one;
            taR.offsetMin = new Vector2(10f, 2f);
            taR.offsetMax = new Vector2(-10f, -2f);

            // Display text
            var textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(textArea.transform, false);
            textGo.GetRect().FullStretch();
            var tmp = textGo.GetComponent<TextMeshProUGUI>();
            if (uiFont != null) tmp.font = uiFont;
            tmp.text = val;
            tmp.fontSize = 20;
            tmp.color = ColTextLight;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Placeholder
            var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            phGo.transform.SetParent(textArea.transform, false);
            phGo.GetRect().FullStretch();
            var ph = phGo.GetComponent<TextMeshProUGUI>();
            if (uiFont != null) ph.font = uiFont;
            ph.text = "Input value";
            ph.fontSize = 20;
            ph.color = ColDimText;
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            ph.fontStyle = FontStyles.Italic;

            inputField.textViewport = taR;
            inputField.textComponent = tmp;
            inputField.placeholder = ph;
            inputField.text = val;
            if (uiFont != null) inputField.fontAsset = uiFont;

            var st = entry.Entry.SettingType;
            if (st == typeof(float))
                inputField.contentType = TMP_InputField.ContentType.DecimalNumber;
            else if (st == typeof(int))
                inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            else
                inputField.contentType = TMP_InputField.ContentType.Standard;

            string originalVal = val;
            bool userEdited = false;
            bool suppressChange = false;
            inputField.onValueChanged.AddListener(_ => { if (!suppressChange) userEdited = true; });
            inputField.onSelect.AddListener(_ =>
            {
                if (!userEdited)
                {
                    originalVal = inputField.text;
                    suppressChange = true;
                    inputField.text = "";
                    suppressChange = false;
                }
            });
            inputField.onEndEdit.AddListener(newVal =>
            {
                if (!userEdited)
                {
                    inputField.text = originalVal;
                    return;
                }
                if (!SetBoxedValue(entry, newVal ?? ""))
                {
                    inputField.text = originalVal;
                }
                userEdited = false;
            });
        }

        //  Dropdown (arrow cycler)

        private static void BuildDropdownControl(Transform parent, EntryInfo entry, ModInfo mod)
        {
            string[] choices = entry.Choices;
            if (choices == null || choices.Length == 0) return;

            string val = ReadBoxedAsString(entry);
            int idx = Array.IndexOf(choices, val);
            if (idx < 0)
            {
                idx = 0;
                entry.Entry.BoxedValue = choices[0];
            }

            var row = MakeStepperRow(parent);

            // Use a closure-wrapper object to hold the mutable index
            var state = new DropdownState { index = idx };

            var display = MakeText(null, choices[state.index], 20, ColTextLight, TextAlignmentOptions.Center);

            MakeStepperButton(row.transform, "<", () =>
            {
                state.index--;
                if (state.index < 0) state.index = choices.Length - 1;
                entry.Entry.BoxedValue = choices[state.index];
                display.text = choices[state.index];
            });

            display.transform.SetParent(row.transform, false);
            display.GetRect().sizeDelta = new Vector2(165f, 39f);

            MakeStepperButton(row.transform, ">", () =>
            {
                state.index++;
                if (state.index >= choices.Length) state.index = 0;
                entry.Entry.BoxedValue = choices[state.index];
                display.text = choices[state.index];
            });
        }

        private class DropdownState { public int index; }

        //  Keybinds

        private static void BuildKeybindControl(Transform parent, EntryInfo entry, ModInfo mod)
        {
            string display = KeybindDisplayText(entry);

            var btnGo = new GameObject("KeybindBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
            var br = btnGo.GetRect();
            br.anchorMin = new Vector2(0.5f, 0.5f);
            br.anchorMax = new Vector2(0.5f, 0.5f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.sizeDelta = new Vector2(210f, 39f);

            var img = btnGo.GetComponent<Image>();
            ApplySpriteOrColor(img, buttonSprite, new Color(0.18f, 0.18f, 0.22f));

            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = ColorBlock.defaultColorBlock;

            bool hasConflict = !string.IsNullOrEmpty(entry.ConflictWarning);
            var labelColor = hasConflict ? new Color(1f, 0.25f, 0.25f) : ColTextLight;
            var label = MakeText(btnGo.transform, display, 20, labelColor, TextAlignmentOptions.Center);
            label.GetRect().FullStretch();

            btn.onClick.AddListener(() =>
            {
                if (capturingKey)
                {
                    if (keyCaptureDisplay != null)
                        keyCaptureDisplay.text = KeybindDisplayText(keyCaptureEntry);
                    ResetCaptureButtonColor();
                }
                capturingKey = true;
                keyCaptureEntry = entry;
                keyCaptureDisplay = label;
                keyCaptureTimeout = 5f;
                label.text = "Press a key...";
                img.color = ColAccent;
                _captureBtnImg = img;
                if (currentPanel != null)
                {
                    var bd = currentPanel.transform.Find("Backdrop");
                    if (bd != null) bd.GetComponent<Button>().enabled = false;
                }
            });
        }

        private static void ReenableBackdrop()
        {
            if (currentPanel != null)
            {
                var bd = currentPanel.transform.Find("Backdrop");
                if (bd != null) { var b = bd.GetComponent<Button>(); if (b != null) b.enabled = true; }
            }
        }

        private static void ResetCaptureButtonColor()
        {
            if (_captureBtnImg != null)
                ApplySpriteOrColor(_captureBtnImg, buttonSprite, new Color(0.18f, 0.18f, 0.22f));
        }

        /// <summary>Read BoxedValue as string, handling any underlying type.</summary>
        private static string ReadBoxedAsString(EntryInfo entry)
        {
            var val = entry.Entry.BoxedValue;
            if (val == null) return "";
            if (val is string) return (string)val;
            return val.ToString();
        }

        internal static bool IsShortcutDown(KeyboardShortcut ks)
        {
            if (ks.Modifiers != null)
                foreach (var m in ks.Modifiers)
                    if (!Input.GetKey(m)) return false;
            return true;
        }

        private static string KeybindDisplayText(EntryInfo entry)
        {
            if (entry == null || entry.Entry == null || entry.Entry.BoxedValue == null) return "None";
            if (entry.Entry.BoxedValue is KeyboardShortcut)
            {
                var ks = (KeyboardShortcut)entry.Entry.BoxedValue;
                string mods = "";
                if (ks.Modifiers != null)
                {
                    var list = new List<string>();
                    foreach (var m in ks.Modifiers)
                        list.Add(m.ToString());
                    if (list.Count > 0)
                        mods = string.Join("+", list.ToArray()) + "+";
                }
                return mods + ks.MainKey.ToString();
            }
            if (entry.Entry.BoxedValue is KeyCode)
                return ((KeyCode)entry.Entry.BoxedValue).ToString();
            return entry.Entry.BoxedValue.ToString();
        }

        internal static void CancelKeyCapture()
        {
            if (keyCaptureDisplay != null && keyCaptureEntry != null)
                keyCaptureDisplay.text = KeybindDisplayText(keyCaptureEntry);
            ResetCaptureButtonColor();
            ReenableBackdrop();
            capturingKey = false;
            keyCaptureEntry = null;
            keyCaptureDisplay = null;
        }

        internal static void CaptureKeyResult(KeyCode kc)
        {
            if (keyCaptureEntry == null) return;

            if (kc == KeyCode.None || kc == KeyCode.Escape) return;
            if (kc == KeyCode.LeftShift || kc == KeyCode.RightShift ||
                kc == KeyCode.LeftControl || kc == KeyCode.RightControl ||
                kc == KeyCode.LeftAlt || kc == KeyCode.RightAlt ||
                kc == KeyCode.LeftCommand || kc == KeyCode.RightCommand ||
                kc == KeyCode.LeftWindows || kc == KeyCode.RightWindows ||
                kc == KeyCode.LeftApple || kc == KeyCode.RightApple) return;

            var mods = new List<KeyCode>();
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                mods.Add(KeyCode.LeftShift);
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                mods.Add(KeyCode.LeftControl);
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                mods.Add(KeyCode.LeftAlt);

            if (keyCaptureEntry.Entry.SettingType == typeof(KeyboardShortcut))
                keyCaptureEntry.Entry.BoxedValue = new KeyboardShortcut(kc, mods.ToArray());
            else if (keyCaptureEntry.Entry.SettingType == typeof(KeyCode))
                keyCaptureEntry.Entry.BoxedValue = kc;

            if (keyCaptureDisplay != null)
            {
                keyCaptureDisplay.text = KeybindDisplayText(keyCaptureEntry);
                var parent = keyCaptureDisplay.transform.parent;
                if (parent != null)
                {
                    var img = parent.GetComponent<Image>();
                    if (img != null)
                        ApplySpriteOrColor(img, buttonSprite, new Color(0.18f, 0.18f, 0.22f));
                }
            }

            ResetCaptureButtonColor();
            ReenableBackdrop();
            capturingKey = false;
            keyCaptureEntry = null;
            keyCaptureDisplay = null;
            DetectKeybindConflicts();
            if (contentArea != null)
            {
                foreach (Transform child in contentArea) Destroy(child.gameObject);
                BuildContent(contentArea);
            }
        }

        //  Persistence

        /// <summary>Set BoxedValue with proper type conversion.</summary>
        private static bool SetBoxedValue(EntryInfo entry, string text)
        {
            if (entry == null || entry.Entry == null) return false;
            var st = entry.Entry.SettingType;
            try
            {
                if (st == typeof(float))
                {
                    float f;
                    if (float.TryParse(text, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out f))
                    { entry.Entry.BoxedValue = f; return true; }
                    return false;
                }
                else if (st == typeof(int))
                {
                    int i;
                    if (int.TryParse(text, System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out i))
                    { entry.Entry.BoxedValue = i; return true; }
                    return false;
                }
                else if (st == typeof(bool))
                {
                    bool b;
                    if (bool.TryParse(text, out b))
                    { entry.Entry.BoxedValue = b; return true; }
                    return false;
                }
                else
                {
                    entry.Entry.BoxedValue = text;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("MCM: SetBoxedValue failed - {0}", ex.Message));
                return false;
            }
        }


        //  UI helpers 

        private static TextMeshProUGUI MakeText(Transform parent, string text, int fontSize, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var go = new GameObject("TXT", typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (uiFont != null) tmp.font = uiFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static GameObject MakeButton(Transform parent, string text, Vector2 size, Action onClick)
        {
            var go = new GameObject("BTN", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = size;

            var img = go.GetComponent<Image>();
            ApplySpriteOrColor(img, buttonSprite, new Color(0.20f, 0.20f, 0.24f));

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            btn.colors = ColorBlock.defaultColorBlock;
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            var label = MakeText(go.transform, text, 20, ColTextLight, TextAlignmentOptions.Midline);
            label.GetRect().SetAnchors(Vector2.zero, Vector2.one);
            label.raycastTarget = false;

            btn.onClick.AddListener(() => onClick());
            return go;
        }

        private static GameObject MakeStepperButton(Transform parent, string text, Action onClick)
        {
            return MakeButton(parent, text, new Vector2(48f, 42f), onClick);
        }

        private static GameObject MakeStepperRow(Transform parent)
        {
            var row = new GameObject("StepperRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            var rr = row.GetRect();
            rr.anchorMin = new Vector2(0.5f, 0.5f);
            rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.pivot = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = new Vector2(300f, 39f);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            return row;
        }


        private static void FullRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }
    }

    //  RectTransform extensions

    internal static class RectExt
    {
        public static RectTransform GetRect(this Component c) { return c.GetComponent<RectTransform>(); }
        public static RectTransform GetRect(this GameObject go) { return go.GetComponent<RectTransform>(); }

        public static void SetAnchors(this RectTransform rt, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
        }

        public static void FullStretch(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }
    }

    //  Key capture 

    internal class KeyCaptureRunner : MonoBehaviour
    {
        private void Update()
        {
            var tk = ModConfigMenuPlugin.toggleKey;
            if (tk != null && Input.GetKeyDown(tk.Value.MainKey)
                && ModConfigMenuPlugin.IsShortcutDown(tk.Value))
            {
                if (ModConfigMenuPlugin.currentPanel != null)
                    ModConfigMenuPlugin.TryCloseMenu();
                else
                    ModConfigMenuPlugin.OpenMenu();
            }
            if (ModConfigMenuPlugin.currentPanel != null && Input.GetKeyDown(KeyCode.Escape))
                ModConfigMenuPlugin.TryCloseMenu();

            if (!ModConfigMenuPlugin.capturingKey) return;

            ModConfigMenuPlugin.keyCaptureTimeout -= Time.unscaledDeltaTime;
            if (ModConfigMenuPlugin.keyCaptureTimeout <= 0f)
            {
                ModConfigMenuPlugin.CancelKeyCapture();
                return;
            }
            // Check common keys (same approach as cheat menu's Input.GetKeyDown)
            if (Input.GetKeyDown(KeyCode.F2)) { ModConfigMenuPlugin.CaptureKeyResult(KeyCode.F2); return; }
            if (Input.GetKeyDown(KeyCode.Escape)) { ModConfigMenuPlugin.CaptureKeyResult(KeyCode.Escape); return; }
            for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++)
            { if (Input.GetKeyDown(k)) { ModConfigMenuPlugin.CaptureKeyResult(k); return; } }
            for (KeyCode k = KeyCode.F1; k <= KeyCode.F15; k++)
            { if (Input.GetKeyDown(k)) { ModConfigMenuPlugin.CaptureKeyResult(k); return; } }
            for (KeyCode k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++)
            { if (Input.GetKeyDown(k)) { ModConfigMenuPlugin.CaptureKeyResult(k); return; } }
            if (Input.GetKeyDown(KeyCode.Space)) { ModConfigMenuPlugin.CaptureKeyResult(KeyCode.Space); return; }
            if (Input.GetKeyDown(KeyCode.Tab)) { ModConfigMenuPlugin.CaptureKeyResult(KeyCode.Tab); return; }
        }
    }

    //  Title screen button injection 

    internal class TitleScreenPoller : MonoBehaviour
    {
        internal static TitleScreenPoller Instance;
        private bool done;
        private FieldInfo contentParentField;
        private FieldInfo buttonsField;

        private void Awake() { Instance = this; }

        internal void Reset() { done = false; }

        private void Update()
        {
            if (done) return;

            var connectionMenu = FindObjectOfType<OnlineConnectionMenu>();
            if (connectionMenu == null) return;

            if (contentParentField == null)
            {
                contentParentField = typeof(OnlineConnectionMenu).GetField(
                    "mainMenuContentParent",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }
            if (contentParentField == null) return;

            var parent = contentParentField.GetValue(connectionMenu) as GameObject;
            if (parent == null || !parent.activeInHierarchy) return;

            if (parent.transform.Find("ModConfigButton") != null)
            {
                done = true;
                return;
            }

            // Find a button to clone, prefer the Options button
            var allBtns = parent.GetComponentsInChildren<Button>(true);
            if (allBtns == null || allBtns.Length == 0) return;

            Button template = null;
            foreach (var b in allBtns)
            {
                if (b == null || b.gameObject == null || b.transform.parent != parent.transform) continue;
                string txt = "";
                foreach (var tmp in b.GetComponentsInChildren<TextMeshProUGUI>(true))
                { if (tmp != null && !string.IsNullOrEmpty(tmp.text)) { txt = tmp.text; break; } }
                if (txt.IndexOf("Options", StringComparison.OrdinalIgnoreCase) >= 0)
                { template = b; break; }
            }
            if (template == null)
            {
                foreach (var b in allBtns)
                { if (b != null && b.gameObject != null && b.transform.parent == parent.transform)
                    { template = b; break; } }
            }
            if (template == null) template = allBtns[0];

            try
            {
                var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                clone.name = "ModConfigButton";
                clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

                foreach (var comp in clone.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    var tn = comp.GetType().Name;
                    if (tn.Contains("Local") || tn.Contains("Translate"))
                        UnityEngine.Object.DestroyImmediate(comp);
                }

                foreach (var tmp in clone.GetComponentsInChildren<TextMeshProUGUI>(true))
                    tmp.text = "Mods";

                // Transparent click-catcher
                var catcher = new GameObject("ClickCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
                catcher.transform.SetParent(clone.transform, false);
                var cr = catcher.GetComponent<RectTransform>();
                cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.sizeDelta = Vector2.zero;
                catcher.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
                catcher.GetComponent<Image>().raycastTarget = true;
                var cb = catcher.GetComponent<Button>();
                cb.transition = Selectable.Transition.None;
                cb.onClick.AddListener(() => ModConfigMenuPlugin.OpenMenu());

                // Insert into the button array
                if (buttonsField == null)
                {
                    buttonsField = typeof(OnlineConnectionMenu).GetField(
                        "titleScreenButtons",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                }
                if (buttonsField != null)
                {
                    var existing = buttonsField.GetValue(connectionMenu) as Button[];
                    if (existing != null)
                    {
                        var next = new Button[existing.Length + 1];
                        Array.Copy(existing, next, existing.Length);
                        next[next.Length - 1] = cb;
                        buttonsField.SetValue(connectionMenu, next);
                    }
                }
                ModConfigMenuPlugin.DetectKeybindConflicts();
                done = true;
            }
            catch (Exception ex)
            {
                ModConfigMenuPlugin.Log(
                    string.Format("MCM: Main-menu button failed - {0}", ex.Message));
            }
        }
    }

    //  Quit-to-title reset

    [HarmonyPatch]
    internal static class QuitToTitlePatch
    {
        [HarmonyPatch(typeof(PauseMenuUI), "GoToTitleScreen")]
        [HarmonyPrefix]
        private static void ResetFlags()
        {
            if (TitleScreenPoller.Instance != null)
                TitleScreenPoller.Instance.Reset();
            UIConfigButtonPatch.ResetDone();
        }
    }

    //  Pause menu button injection

    [HarmonyPatch]
    internal static class UIConfigButtonPatch
    {
        private static bool done;

        internal static void ResetDone() { done = false; }

        [HarmonyPatch(typeof(PauseMenuUI), "OnContentActivated")]
        [HarmonyPostfix]
        private static void AddModConfigButton(object __instance)
        {
            if (done || __instance == null) return;

            var pauseMenu = __instance as PauseMenuUI;
            if (pauseMenu == null) return;

            try
            {
                var allBtns = pauseMenu.GetComponentsInChildren<Button>(true);
                Button template = null;
                Button insertAfter = null;

                foreach (var b in allBtns)
                {
                    if (b == null || b.gameObject == null || !b.IsInteractable()) continue;

                    string txt = "";
                    foreach (var tmp in b.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (tmp != null && !string.IsNullOrEmpty(tmp.text))
                        {
                            txt = tmp.text;
                            break;
                        }
                    }

                    if (template == null) template = b;
                    if (txt.IndexOf("Options", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        insertAfter = b;
                        template = b;
                        break;
                    }
                }

                if (template == null) return;

                var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                clone.name = "ModConfigButton";

                if (insertAfter != null)
                    clone.transform.SetSiblingIndex(insertAfter.transform.GetSiblingIndex() + 1);
                else
                    clone.transform.SetAsLastSibling();

                var tRT = template.GetComponent<RectTransform>();
                if (tRT != null)
                {
                    var pRT = template.transform.parent.GetComponent<RectTransform>();
                    if (pRT != null)
                        pRT.sizeDelta = new Vector2(pRT.sizeDelta.x,
                            pRT.sizeDelta.y + tRT.rect.height + 10f);
                }

                foreach (var comp in clone.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    var tn = comp.GetType().Name;
                    if (tn.Contains("Local") || tn.Contains("Translate"))
                        UnityEngine.Object.DestroyImmediate(comp);
                }

                foreach (var tmp in clone.GetComponentsInChildren<TextMeshProUGUI>(true))
                    tmp.text = "Mods";

                // Click catcher overlay
                var catcher = new GameObject("ClickCatcher", typeof(RectTransform), typeof(Image), typeof(Button));
                catcher.transform.SetParent(clone.transform, false);
                var cr = catcher.GetComponent<RectTransform>();
                cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.sizeDelta = Vector2.zero;
                catcher.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
                catcher.GetComponent<Image>().raycastTarget = true;
                var cb = catcher.GetComponent<Button>();
                cb.transition = Selectable.Transition.None;
                cb.onClick.AddListener(() => ModConfigMenuPlugin.OpenMenu());

                // Nudge version number text down so it doesn't overlap
                try
                {
                    var vf = typeof(PauseMenuUI).GetField("versionNumberText",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (vf != null)
                    {
                        var vt = vf.GetValue(pauseMenu) as TextMeshProUGUI;
                        if (vt != null)
                        {
                            var vrt = vt.GetComponent<RectTransform>();
                            if (vrt != null)
                                vrt.anchoredPosition = new Vector2(
                                    vrt.anchoredPosition.x,
                                    vrt.anchoredPosition.y - 52f);
                        }
                    }
                }
                catch { /* best-effort */ }

                done = true;
            }
            catch (Exception ex)
            {
                ModConfigMenuPlugin.Log(
                    string.Format("MCM: Pause-menu button failed - {0}", ex.Message));
            }
        }
    }
}
