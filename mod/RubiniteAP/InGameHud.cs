using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

namespace RubiniteAP
{
    public sealed class InGameHud : MonoBehaviour
    {
        private const int MaxVisibleToasts = 5;
        private const float ToastWidth = 480f;
        private const float ToastHeight = 76f;
        private const float ToastSpacing = 6f;
        private const float ToastFadeTime = 0.35f;
        public ToastDurationMode DurationMode = ToastDurationMode.Timed;
        public float HoldTime = 5f;

        private const float IconSize = 48f;
        private const float TextLeftWithIcon = 68f;
        private const float TextLeftNoIcon = 18f;

        public enum ToastDurationMode { Timed, Permanent }

        private GameBridge _bridge;
        private RectTransform _container;
        private Canvas _canvas;
        private TMP_FontAsset _gameFont;
        private Sprite _panelSprite;

        private int _lastReceivedCount = -1;
        private int _lastSentCount = -1;
        private bool _userEnabled = true;

        private GameObject _pausePanel;
        private RectTransform _pauseListContent;
        private int _lastPauseHistoryCount = -1; 

        private GameObject _trackerPanel;
        private GameObject _tooltipPanel;
        private TextMeshProUGUI _tooltipText;
        
        private readonly Dictionary<RectTransform, BossInfo> _hoverTargets = new Dictionary<RectTransform, BossInfo>();

        public bool IsUserEnabled => _userEnabled;
        public bool IsActuallyVisible { get; private set; }

        private sealed class ToastEntry
        {
            public GameObject Root;
            public CanvasGroup Group;
            public RectTransform Rect;
            public float BornAt;
            public float TargetY;
            public float StartX = 40f;
        }

        private readonly List<ToastEntry> _toasts = new List<ToastEntry>();

        public void Initialize(GameBridge bridge)
        {
            _bridge = bridge;

            var s = RubiniteAPPlugin.Instance?.Settings;
            if (s != null)
            {
                if (s.HudDurationMode != null &&
                    System.Enum.TryParse<ToastDurationMode>(s.HudDurationMode.Value, true, out var mode))
                    DurationMode = mode;

                if (s.HudHoldTime != null)
                    HoldTime = s.HudHoldTime.Value;
            }

            var tmps = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
            if (tmps != null && tmps.Length > 0)
                _gameFont = tmps[0].font;

            EnsureEventSystem();
            CreateCanvas();
            GeneratePanelSprite();
            CreatePausePanel();
            CreateSigilTracker();
            CreateTooltip();

            _lastReceivedCount = bridge.GetReceivedHistory().Count;
            _lastSentCount = bridge.GetSentHistory().Count();

            UpdateVisibility();
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("AP_EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
            }
        }

        public void Toggle()
        {
            _userEnabled = !_userEnabled;
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            bool shouldShow = _userEnabled && ShouldShowInCurrentContext();
            if (_canvas != null) _canvas.enabled = shouldShow;
            IsActuallyVisible = shouldShow;
        }

        private bool ShouldShowInCurrentContext()
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "TitleMenu") return false;
            return true;
        }

        private void CreateCanvas()
        {
            var go = new GameObject("AP_InGameHud");
            DontDestroyOnLoad(go);

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999;

            go.AddComponent<GraphicRaycaster>();

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var containerGo = new GameObject("ToastContainer");
            containerGo.transform.SetParent(go.transform, false);
            _container = containerGo.AddComponent<RectTransform>();
            
            _container.anchorMin = new Vector2(1, 1);
            _container.anchorMax = new Vector2(1, 1);
            _container.pivot = new Vector2(1, 1);
            _container.anchoredPosition = new Vector2(-30f, -30f);
            
            _container.sizeDelta = new Vector2(ToastWidth, 0);
        }

        private void GeneratePanelSprite()
        {
            const int w = 32, h = 32, r = 8;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[w * h];

            var fill   = new Color32(38, 24, 20, 225);
            var border = new Color32(95, 65, 45, 245);
            var clear  = new Color32(0, 0, 0, 0);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int cx = Mathf.Clamp(x, r, w - r - 1);
                    int cy = Mathf.Clamp(y, r, h - r - 1);
                    float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if ((x < r || x >= w - r) && (y < r || y >= h - r) && dist > r - 0.5f)
                    {
                        pixels[y * w + x] = clear;
                        continue;
                    }
                    bool isBorder = x < 2 || x >= w - 2 || y < 2 || y >= h - 2;
                    pixels[y * w + x] = isBorder ? border : fill;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            _panelSprite = Sprite.Create(
                tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        private void CreatePausePanel()
        {
            _pausePanel = new GameObject("AP_PauseHistoryPanel");
            _pausePanel.transform.SetParent(_canvas.transform, false);
            
            var bgImg = _pausePanel.AddComponent<Image>();
            
            var nativeBg = _bridge.FindSpriteByName("UI_SkillBook_Frame_Info_Paper_main");
            if (nativeBg != null)
            {
                bgImg.sprite = nativeBg;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.sprite = _panelSprite;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = new Color(1f, 1f, 1f, 0.95f);
            }

            var bgRt = _pausePanel.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(1, 1);
            bgRt.anchorMax = new Vector2(1, 1);
            bgRt.pivot = new Vector2(1, 1);
            bgRt.anchoredPosition = new Vector2(-30f, -120f); 
            bgRt.sizeDelta = new Vector2(550f, 850f);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(_pausePanel.transform, false);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            if (_gameFont != null) titleTmp.font = _gameFont;
            titleTmp.text = "RECENT CHECKS";
            titleTmp.fontSize = 24;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color32(80, 50, 30, 255); 
            titleTmp.alignment = TextAlignmentOptions.Center;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.anchoredPosition = new Vector2(0, -15f);
            titleRt.sizeDelta = new Vector2(0, 40f);

            var scrollGo = new GameObject("ScrollView");
            scrollGo.transform.SetParent(_pausePanel.transform, false);
            var scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0, 0);
            scrollRt.anchorMax = new Vector2(1, 1);
            scrollRt.offsetMin = new Vector2(20f, 20f);
            scrollRt.offsetMax = new Vector2(-20f, -60f);

            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;

            var viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportImg = viewportGo.AddComponent<Image>();
            viewportImg.color = new Color(0,0,0,0.05f);
            viewportGo.AddComponent<RectMask2D>();
            
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            scrollRect.viewport = viewportRt;

            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            _pauseListContent = contentGo.AddComponent<RectTransform>();
            _pauseListContent.anchorMin = new Vector2(0, 1);
            _pauseListContent.anchorMax = new Vector2(1, 1);
            _pauseListContent.pivot = new Vector2(0.5f, 1);
            _pauseListContent.sizeDelta = new Vector2(0, 0);

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 6f;
            layout.padding = new RectOffset(10, 10, 10, 10);

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            scrollRect.content = _pauseListContent;

            _pausePanel.SetActive(false);
        }

        private void CreateSigilTracker()
        {
            _trackerPanel = new GameObject("AP_SigilTrackerPanel");
            _trackerPanel.transform.SetParent(_canvas.transform, false);
            
            var bgImg = _trackerPanel.AddComponent<Image>();
            
            var nativeBg = _bridge.FindSpriteByName("UI_SkillBook_Frame_Info_Paper_main");
            if (nativeBg != null)
            {
                bgImg.sprite = nativeBg;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.sprite = _panelSprite;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = new Color(1f, 1f, 1f, 0.95f);
            }

            var bgRt = _trackerPanel.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(0, 0.5f);
            bgRt.pivot = new Vector2(0, 0.5f);
            bgRt.anchoredPosition = new Vector2(30f, 0f);
            
            bgRt.sizeDelta = new Vector2(320f, 480f); 

            var grid = _trackerPanel.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(80f, 80f); 
            grid.spacing = new Vector2(10f, 10f);
            grid.padding = new RectOffset(20, 20, 20, 20);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.MiddleCenter;

            _trackerPanel.SetActive(false);
        }

        private void CreateTooltip()
        {
            _tooltipPanel = new GameObject("AP_TooltipPanel");
            _tooltipPanel.transform.SetParent(_canvas.transform, false);
            
            var bgImg = _tooltipPanel.AddComponent<Image>();
            
            var nativeBg = _bridge.FindSpriteByName("UI_SkillBook_Frame_Info_Paper_main");
            if (nativeBg != null)
            {
                bgImg.sprite = nativeBg;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = Color.white;
            }
            else
            {
                bgImg.sprite = _panelSprite;
                bgImg.type = Image.Type.Sliced;
                bgImg.color = new Color(1f, 1f, 1f, 0.95f);
            }

            var bgRt = _tooltipPanel.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(0, 0.5f);
            bgRt.pivot = new Vector2(0, 0.5f);
            bgRt.anchoredPosition = new Vector2(360f, 0f); 
            
            bgRt.sizeDelta = new Vector2(320f, 0f); 

            var fitter = _tooltipPanel.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var layout = _tooltipPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 20);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 2f;

            var textGo = new GameObject("HeaderText");
            textGo.transform.SetParent(_tooltipPanel.transform, false);
            _tooltipText = textGo.AddComponent<TextMeshProUGUI>();
            if (_gameFont != null) _tooltipText.font = _gameFont;
            _tooltipText.fontSize = 20;
            _tooltipText.color = new Color32(50, 30, 20, 255); 
            _tooltipText.alignment = TextAlignmentOptions.TopLeft;
            _tooltipText.enableWordWrapping = true; 
            _tooltipText.overflowMode = TextOverflowModes.Overflow;

            _tooltipPanel.SetActive(false);
        }

        private void Update()
        {
            if (_bridge == null) return;

            UpdateVisibility();

            bool isPaused = IsPauseMenuOpen();
            
            _pausePanel.SetActive(isPaused);
            _trackerPanel.SetActive(isPaused);

            if (isPaused)
            {
                int currentHistoryCount = _bridge.GetReceivedHistory().Count;
                if (_lastPauseHistoryCount != currentHistoryCount)
                {
                    _lastPauseHistoryCount = currentHistoryCount;
                    UpdatePausePanel();
                    UpdateSigilTracker();
                }
                
                _container.gameObject.SetActive(false); 
                UpdateHoverState();
            }
            else
            {
                _container.gameObject.SetActive(true);
                HideTooltip(); 
                _lastPauseHistoryCount = -1;

                var received = _bridge.GetReceivedHistory();
                var receivedSourceIds = new HashSet<long>();
                foreach (var r in received)
                    if (r.SourceLocationId > 0) receivedSourceIds.Add(r.SourceLocationId);

                if (received.Count > _lastReceivedCount)
                {
                    for (int i = _lastReceivedCount; i < received.Count; i++)
                    {
                        var r = received[i];
                        var senderName = _bridge.GetSlotName(r.SenderSlot);
                        var icon = _bridge.PickIconForItem(r.ItemName);
                        AddToast("Received", r.ItemName, "from " + senderName, icon, r.Flags);
                    }
                    _lastReceivedCount = received.Count;
                }

                var sent = _bridge.GetSentHistory().ToList();
                if (sent.Count > _lastSentCount)
                {
                    for (int i = _lastSentCount; i < sent.Count; i++)
                    {
                        var s = sent[i];
                        if (s.RecipientSlot != _bridge.MySlot) continue;
                        if (receivedSourceIds.Contains(s.LocationId)) continue;

                        var icon = _bridge.PickIconForItem(s.ItemName);
                        var locName = s.LocationDisplayName ?? "a check";
                        AddToast("Sent", s.ItemName, locName, icon, s.Flags);
                    }
                    _lastSentCount = sent.Count;
                }
                
                UpdateToasts();
            }
        }

        private void UpdatePausePanel()
        {
            foreach (Transform child in _pauseListContent)
                Destroy(child.gameObject);

            var history = _bridge.GetReceivedHistory();
            int startIndex = Mathf.Max(0, history.Count - 20);
            
            for (int i = history.Count - 1; i >= startIndex; i--)
            {
                var record = history[i];
                var itemGo = new GameObject("HistoryItem");
                itemGo.transform.SetParent(_pauseListContent, false);
                
                var layoutElement = itemGo.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = 40f;

                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(itemGo.transform, false);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = _bridge.PickIconForItem(record.ItemName);
                iconImg.preserveAspect = true;
                
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0, 0.5f);
                iconRt.anchorMax = new Vector2(0, 0.5f);
                iconRt.pivot = new Vector2(0, 0.5f);
                iconRt.anchoredPosition = new Vector2(5, 0);
                iconRt.sizeDelta = new Vector2(32, 32);

                var textGo = new GameObject("Text");
                textGo.transform.SetParent(itemGo.transform, false);
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                if (_gameFont != null) tmp.font = _gameFont;
                
                string sender = _bridge.GetSlotName(record.SenderSlot);
                tmp.text = $"<b>{record.ItemName}</b> <color=#6b503b>from {sender}</color>";
                
                tmp.fontSize = 20;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 12;
                tmp.fontSizeMax = 20;
                
                tmp.color = new Color32(50, 30, 20, 255);
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.enableWordWrapping = false;
                tmp.overflowMode = TextOverflowModes.Ellipsis;
                
                var textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(45, 0);
                textRt.offsetMax = new Vector2(-10, 0);
            }
        }

        private void UpdateSigilTracker()
        {
            int activeBosses = 0;
            foreach (var boss in GameData.Bosses)
                if (_bridge.IsBossActive(boss.Key)) activeBosses++;

            if (_trackerPanel.transform.childCount == activeBosses && _hoverTargets.Count == activeBosses)
                return; 

            foreach (Transform child in _trackerPanel.transform)
                Destroy(child.gameObject);
            _hoverTargets.Clear();

            var frameBgSprite = _bridge.FindSpriteByName("SkillsTreeIcon_2_2");

            foreach (var boss in GameData.Bosses)
            {
                if (!_bridge.IsBossActive(boss.Key)) continue;

                bool unlocked = _bridge.IsBossUnlockedPublic(boss);
                
                var nodeGo = new GameObject(boss.Key);
                nodeGo.transform.SetParent(_trackerPanel.transform, false);

                var bgImg = nodeGo.AddComponent<Image>();
                if (frameBgSprite != null)
                {
                    bgImg.sprite = frameBgSprite;
                    bgImg.color = unlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f, 1f);
                }
                else
                {
                    bgImg.sprite = _panelSprite;
                    bgImg.color = unlocked ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.8f);
                }

                var rt = nodeGo.GetComponent<RectTransform>();
                _hoverTargets.Add(rt, boss);

                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(nodeGo.transform, false);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = _bridge.PickIconForItem(boss.Sigil ?? boss.Key + " Sigil");
                iconImg.preserveAspect = true;
                iconImg.color = unlocked ? Color.white : new Color(0.2f, 0.2f, 0.2f, 1f);

                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 0.5f);
                iconRt.anchorMax = new Vector2(0.5f, 0.5f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                iconRt.sizeDelta = new Vector2(50f, 50f);
            }
        }

        private void UpdateHoverState()
        {
            bool hovering = false;
            Vector2 mousePos = Input.mousePosition;

            foreach (var kvp in _hoverTargets)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(kvp.Key, mousePos, null))
                {
                    ShowTooltip(kvp.Value);
                    hovering = true;
                    break;
                }
            }

            if (!hovering)
            {
                HideTooltip();
            }
        }

        private void ShowTooltip(BossInfo boss)
        {
            if (_tooltipPanel == null) return;

            for (int i = _tooltipPanel.transform.childCount - 1; i >= 0; i--)
            {
                var child = _tooltipPanel.transform.GetChild(i).gameObject;
                if (child.name == "Row") Destroy(child);
            }

            bool portalUnlocked = _bridge.IsBossUnlockedPublic(boss);
            
            Color32 colorGreen = new Color32(74, 110, 62, 255);
            Color32 colorRed = new Color32(139, 54, 54, 255);

            string colorGreenHex = "#" + ColorUtility.ToHtmlStringRGB(colorGreen);
            string colorRedHex = "#" + ColorUtility.ToHtmlStringRGB(colorRed);
            
            string status = portalUnlocked 
                ? $"<color={colorGreenHex}>Unlocked</color>" 
                : $"<color={colorRedHex}>Locked</color> (Needs {boss.Sigil ?? "Progression"})";
            
            string bossName = boss.Name;
            if (boss.Key == _bridge.GoalBossKey)
            {
                bossName += $" <color={colorRedHex}>(Goal Boss)</color>";
            }

            string headerText = $"<align=center><b>{bossName}</b></align>\n\n";
            
            headerText += $"{status}\n";

            if (boss.Key == _bridge.GoalBossKey && _bridge.GoalBossKillsRequired > 0)
            {
                int kills = _bridge.CurrentBossKillCount;
                int req = _bridge.GoalBossKillsRequired;
                
                if (kills >= req)
                {
                    headerText += $"<color={colorGreenHex}>Goal Unlocked (Kills: {kills}/{req})</color>\n";
                }
                else
                {
                    headerText += $"<color={colorRedHex}>Goal Locked (Kills: {kills}/{req})</color>\n";
                }
            }
            
            headerText += "\n"; 
            _tooltipText.text = headerText;

            string[] checkNames = { "First Clear", "No-Hit", "No-Talisman", "Flawless", "Rank" };
            for (int i = 0; i < 5; i++)
            {
                if (!_bridge.IsCheckEnabled(boss.Level, i)) continue;

                bool hasCheck = _bridge.State.HasChecked(boss.LocationBase + i);
                
                var rowGo = new GameObject("Row");
                rowGo.transform.SetParent(_tooltipPanel.transform, false);
                var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
                rowLayout.childControlHeight = true;
                rowLayout.childControlWidth = true;
                rowLayout.childForceExpandHeight = false;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childAlignment = TextAnchor.MiddleLeft;
                rowLayout.spacing = 8f;

                var checkGo = new GameObject("Checkbox");
                checkGo.transform.SetParent(rowGo.transform, false);
                var checkImg = checkGo.AddComponent<Image>();
                
                checkImg.color = hasCheck ? colorGreen : colorRed;
                
                var checkRt = checkGo.GetComponent<RectTransform>();
                checkRt.sizeDelta = new Vector2(12, 12);

                var layoutElement = checkGo.AddComponent<LayoutElement>();
                layoutElement.preferredHeight = 12;
                layoutElement.preferredWidth = 12;

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(rowGo.transform, false);
                var labelTmp = labelGo.AddComponent<TextMeshProUGUI>();
                if (_gameFont != null) labelTmp.font = _gameFont;
                labelTmp.text = checkNames[i];
                labelTmp.fontSize = 20;
                labelTmp.color = new Color32(50, 30, 20, 255);
                labelTmp.alignment = TextAlignmentOptions.Left;
            }

            _tooltipPanel.SetActive(true);
        }

        private void HideTooltip()
        {
            if (_tooltipPanel != null && _tooltipPanel.activeSelf)
                _tooltipPanel.SetActive(false);
        }

        private static (string label, Color32 color) GetImportance(int flags)
        {
            if ((flags & 1) != 0) return ("PROGRESSION", new Color32(255, 210, 100, 255));
            if ((flags & 2) != 0) return ("USEFUL",     new Color32(150, 200, 255, 255));
            if ((flags & 4) != 0) return ("TRAP",       new Color32(255, 100, 100, 255));
            return                       ("FILLER",     new Color32(150, 150, 150, 255));
        }

        private void AddToast(string verb, string itemName, string sub, Sprite icon, int flags)
        {
            var (importance, importanceColor) = GetImportance(flags);

            var root = new GameObject("Toast", typeof(RectTransform));
            root.transform.SetParent(_container, false);

            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            rt.sizeDelta = new Vector2(ToastWidth, ToastHeight);

            var group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var bg = new GameObject("Bg");
            bg.transform.SetParent(root.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = _panelSprite;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Color.white;
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            var stripe = new GameObject("Stripe");
            stripe.transform.SetParent(root.transform, false);
            var stripeImg = stripe.AddComponent<Image>();
            stripeImg.color = importanceColor;
            var stripeRt = stripe.GetComponent<RectTransform>();
            stripeRt.anchorMin = new Vector2(0, 0);
            stripeRt.anchorMax = new Vector2(0, 1);
            stripeRt.pivot = new Vector2(0, 0.5f);
            stripeRt.anchoredPosition = new Vector2(4, 0);
            stripeRt.sizeDelta = new Vector2(4, -14);

            float textLeft = TextLeftNoIcon;
            if (icon != null)
            {
                textLeft = TextLeftWithIcon;
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(root.transform, false);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0, 0.5f);
                iconRt.anchorMax = new Vector2(0, 0.5f);
                iconRt.pivot = new Vector2(0, 0.5f);
                iconRt.anchoredPosition = new Vector2(12, 0);
                iconRt.sizeDelta = new Vector2(IconSize, IconSize);
            }

            var verbGo = new GameObject("Verb");
            verbGo.transform.SetParent(root.transform, false);
            var verbTmp = verbGo.AddComponent<TextMeshProUGUI>();
            if (_gameFont != null) verbTmp.font = _gameFont;
            verbTmp.text = importance + "  ·  " + verb.ToUpperInvariant();
            verbTmp.fontSize = 11;
            verbTmp.fontStyle = FontStyles.Bold;
            verbTmp.color = importanceColor;
            verbTmp.alignment = TextAlignmentOptions.Left;
            verbTmp.enableWordWrapping = false;
            var verbRt = verbGo.GetComponent<RectTransform>();
            verbRt.anchorMin = new Vector2(0, 1);
            verbRt.anchorMax = new Vector2(1, 1);
            verbRt.pivot = new Vector2(0, 1);
            verbRt.offsetMin = new Vector2(textLeft, -22);
            verbRt.offsetMax = new Vector2(-8, -6);

            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(root.transform, false);
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            if (_gameFont != null) titleTmp.font = _gameFont;
            titleTmp.text = itemName;
            titleTmp.fontSize = 18;
            titleTmp.enableAutoSizing = true;
            titleTmp.fontSizeMin = 10;
            titleTmp.fontSizeMax = 18;
            
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = new Color32(240, 230, 210, 255);
            titleTmp.alignment = TextAlignmentOptions.Left;
            titleTmp.enableWordWrapping = false;
            titleTmp.overflowMode = TextOverflowModes.Ellipsis;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 0);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.offsetMin = new Vector2(textLeft, 24);
            titleRt.offsetMax = new Vector2(-8, -22);

            var subGo = new GameObject("Sub");
            subGo.transform.SetParent(root.transform, false);
            var subTmp = subGo.AddComponent<TextMeshProUGUI>();
            if (_gameFont != null) subTmp.font = _gameFont;
            subTmp.text = sub;
            subTmp.fontSize = 12;
            subTmp.color = new Color32(175, 160, 140, 255);
            subTmp.alignment = TextAlignmentOptions.Left;
            subTmp.enableWordWrapping = false;
            subTmp.overflowMode = TextOverflowModes.Ellipsis;
            var subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0, 0);
            subRt.anchorMax = new Vector2(1, 0);
            subRt.pivot = new Vector2(0, 0);
            subRt.anchoredPosition = new Vector2(textLeft, 6);
            subRt.sizeDelta = new Vector2(-8 - textLeft, 16);

            var entry = new ToastEntry
            {
                Root = root,
                Group = group,
                Rect = rt,
                BornAt = Time.unscaledTime,
            };
            rt.anchoredPosition = new Vector2(ToastWidth, 0);
            _toasts.Insert(0, entry);

            while (_toasts.Count > MaxVisibleToasts)
            {
                var oldest = _toasts[_toasts.Count - 1];
                _toasts.RemoveAt(_toasts.Count - 1);
                if (oldest.Root != null) Destroy(oldest.Root);
            }

            for (int i = 0; i < _toasts.Count; i++)
                _toasts[i].TargetY = -i * (ToastHeight + ToastSpacing);
        }

        private void UpdateToasts()
        {
            float now = Time.unscaledTime;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var t = _toasts[i];
                if (t.Root == null) { _toasts.RemoveAt(i); continue; }

                float age = now - t.BornAt;

                if (DurationMode == ToastDurationMode.Permanent)
                {
                    t.Group.alpha = age < ToastFadeTime ? age / ToastFadeTime : 1f;
                }
                else
                {
                    float hold = HoldTime;
                    if (age < ToastFadeTime)
                        t.Group.alpha = age / ToastFadeTime;
                    else if (age < hold - ToastFadeTime)
                        t.Group.alpha = 1f;
                    else if (age < hold)
                        t.Group.alpha = 1f - (age - (hold - ToastFadeTime)) / ToastFadeTime;
                    else
                    {
                        Destroy(t.Root);
                        _toasts.RemoveAt(i);
                        continue;
                    }
                }

                float slideT = Mathf.Clamp01(age / 0.3f);
                slideT = 1f - Mathf.Pow(1f - slideT, 3f);
                float x = Mathf.Lerp(t.StartX, 0f, slideT);

                float newY = Mathf.Lerp(t.Rect.anchoredPosition.y, t.TargetY, Time.unscaledDeltaTime * 12f);
                t.Rect.anchoredPosition = new Vector2(x, newY);
            }
        }

        private bool IsPauseMenuOpen()
        {
            var menuCore = MenuCore.Instance;
            if (menuCore == null || menuCore.currentMenu == null) return false;
            
            return menuCore.currentMenu.GetType().Name == "Option";
        }
    }
}