using System;
using System.Collections.Generic;
using UnityEngine;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Utility;
using DaggerfallConnect;
using ProjectLegacy.Core;

namespace ProjectLegacy.UI.Panels
{
    /// <summary>
    /// Mobile-optimized character sheet presented as a scrollable single-column layout.
    /// Displays player stats, skills grouped by governing attribute, level/experience info,
    /// and active magical effects. Each section is collapsible via tap on its header.
    /// Reads all data from <see cref="GameManager.Instance"/> and the player entity.
    /// </summary>
    public class MobileCharacterSheet : MonoBehaviour
    {
        private const string LogPrefix = "[ProjectLegacy]";
        private const float SectionAnimSpeed = 8f;
        private const float HeaderHeight = 36f;
        private const float RowHeight = 24f;
        private const float SectionGap = 6f;

        #region Serialized Fields

        [Header("Layout")]
        [SerializeField]
        [Tooltip("Horizontal content padding in dp.")]
        private float _contentPaddingDp = 12f;

        [SerializeField]
        [Tooltip("Title bar height in dp.")]
        private float _titleBarHeightDp = 48f;

        [Header("Colors")]
        [SerializeField]
        [Tooltip("Background overlay color.")]
        private Color _overlayColor = new Color(0.04f, 0.04f, 0.07f, 0.94f);

        [SerializeField]
        [Tooltip("Section header background color.")]
        private Color _headerColor = new Color(0.12f, 0.12f, 0.16f, 0.95f);

        [SerializeField]
        [Tooltip("Section header text color.")]
        private Color _headerTextColor = new Color(0.85f, 0.72f, 0.35f, 1f);

        [SerializeField]
        [Tooltip("Row background color (alternating A).")]
        private Color _rowColorA = new Color(0.1f, 0.1f, 0.13f, 0.85f);

        [SerializeField]
        [Tooltip("Row background color (alternating B).")]
        private Color _rowColorB = new Color(0.13f, 0.13f, 0.16f, 0.85f);

        [SerializeField]
        [Tooltip("Label text color.")]
        private Color _labelColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        [SerializeField]
        [Tooltip("Value text color.")]
        private Color _valueColor = Color.white;

        [SerializeField]
        [Tooltip("Bar fill color for experience progress.")]
        private Color _xpBarColor = new Color(0.3f, 0.55f, 0.85f, 1f);

        [SerializeField]
        [Tooltip("Positive effect text color.")]
        private Color _buffColor = new Color(0.3f, 0.8f, 0.3f, 1f);

        [SerializeField]
        [Tooltip("Negative effect text color.")]
        private Color _debuffColor = new Color(0.85f, 0.3f, 0.3f, 1f);

        #endregion

        #region Section Tracking

        /// <summary>
        /// Identifies each collapsible section of the character sheet.
        /// </summary>
        private enum Section
        {
            LevelInfo,
            PrimaryStats,
            MajorSkills,
            MinorSkills,
            MiscSkills,
            ActiveEffects
        }

        private readonly Dictionary<Section, bool> _sectionExpanded = new Dictionary<Section, bool>();
        private readonly Dictionary<Section, float> _sectionAnimT = new Dictionary<Section, float>();

        private static readonly string[] SectionLabels =
        {
            "Level & Experience",
            "Primary Attributes",
            "Major Skills",
            "Minor Skills",
            "Miscellaneous Skills",
            "Active Effects"
        };

        #endregion

        #region Public Properties

        /// <summary>Whether the character sheet is currently visible.</summary>
        public bool IsVisible { get; private set; }

        /// <summary>Fired when the panel is shown.</summary>
        public event Action OnPanelShown;

        /// <summary>Fired when the panel is hidden.</summary>
        public event Action OnPanelHidden;

        #endregion

        #region Private State

        private PlayerEntity _playerEntity;
        private Vector2 _scrollPosition;

        private Texture2D _pixelTex;
        private GUIStyle _titleStyle;
        private GUIStyle _sectionHeaderStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _effectStyle;
        private GUIStyle _closeBtnStyle;
        private bool _stylesInitialized;

        private Rect _safeArea;
        private float _contentPadding;
        private float _titleBarHeight;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Initialize all sections as expanded
            foreach (Section s in Enum.GetValues(typeof(Section)))
            {
                _sectionExpanded[s] = true;
                _sectionAnimT[s] = 1f;
            }
        }

        private void Start()
        {
            _pixelTex = new Texture2D(1, 1);
            _pixelTex.SetPixel(0, 0, Color.white);
            _pixelTex.Apply();
        }

        private void Update()
        {
            if (!IsVisible)
                return;

            CachePlayerEntity();
            AnimateSections();
        }

        private void OnDestroy()
        {
            if (_pixelTex != null)
                Destroy(_pixelTex);
        }

        private void OnGUI()
        {
            if (!IsVisible || _pixelTex == null)
                return;

            EnsureStyles();
            RecalculateLayoutIfNeeded();
            CachePlayerEntity();

            if (_playerEntity == null)
                return;

            DrawBackground();
            DrawTitleBar();
            DrawScrollableContent();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Shows the character sheet panel, pausing game input.
        /// </summary>
        public void Show()
        {
            if (IsVisible)
                return;

            CachePlayerEntity();
            if (_playerEntity == null)
            {
                Debug.LogWarning($"{LogPrefix} Cannot show character sheet — player entity not available.");
                return;
            }

            IsVisible = true;
            _scrollPosition = Vector2.zero;

            if (GameManager.Instance != null)
            {
                InputManager.Instance.IsPaused = true;
            }

            OnPanelShown?.Invoke();
            Debug.Log($"{LogPrefix} MobileCharacterSheet shown for {_playerEntity.Name}, Level {_playerEntity.Level}");
        }

        /// <summary>
        /// Hides the character sheet panel and restores game input.
        /// </summary>
        public void Hide()
        {
            if (!IsVisible)
                return;

            IsVisible = false;

            if (GameManager.Instance != null)
            {
                InputManager.Instance.IsPaused = false;
            }

            OnPanelHidden?.Invoke();
            Debug.Log($"{LogPrefix} MobileCharacterSheet hidden.");
        }

        /// <summary>
        /// Toggles the expanded/collapsed state of a section.
        /// </summary>
        /// <param name="section">The section to toggle.</param>
        public void ToggleSection(Section section)
        {
            if (_sectionExpanded.ContainsKey(section))
            {
                _sectionExpanded[section] = !_sectionExpanded[section];
                Debug.Log($"{LogPrefix} Section '{section}' {(_sectionExpanded[section] ? "expanded" : "collapsed")}");
            }
        }

        #endregion

        #region Drawing

        private void DrawBackground()
        {
            GUI.color = _overlayColor;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _pixelTex);
            GUI.color = Color.white;
        }

        private void DrawTitleBar()
        {
            Rect titleRect = new Rect(_safeArea.x, _safeArea.y, _safeArea.width, _titleBarHeight);

            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.98f);
            GUI.DrawTexture(titleRect, _pixelTex);

            GUI.color = Color.white;
            string title = _playerEntity != null
                ? $"{_playerEntity.Name}  —  Level {_playerEntity.Level} {_playerEntity.Career.Name}"
                : "Character Sheet";
            GUI.Label(
                new Rect(titleRect.x + _contentPadding, titleRect.y, titleRect.width - 60f, titleRect.height),
                title,
                _titleStyle
            );

            // Close button
            Rect closeRect = new Rect(titleRect.xMax - 50f, titleRect.y, 50f, titleRect.height);
            GUI.Label(closeRect, "X", _closeBtnStyle);
            if (Event.current.type == EventType.MouseDown && closeRect.Contains(Event.current.mousePosition))
            {
                Hide();
                Event.current.Use();
            }
        }

        private void DrawScrollableContent()
        {
            float scrollTop = _safeArea.y + _titleBarHeight;
            float scrollHeight = _safeArea.height - _titleBarHeight;
            Rect viewport = new Rect(_safeArea.x, scrollTop, _safeArea.width, scrollHeight);

            float totalHeight = CalculateTotalContentHeight();
            Rect contentRect = new Rect(0, 0, viewport.width - 16f, totalHeight);

            _scrollPosition = GUI.BeginScrollView(viewport, _scrollPosition, contentRect);

            float yOffset = 0f;
            float contentWidth = contentRect.width - _contentPadding * 2f;

            yOffset = DrawLevelInfoSection(yOffset, contentWidth);
            yOffset = DrawPrimaryStatsSection(yOffset, contentWidth);
            yOffset = DrawSkillsSection(Section.MajorSkills, "Major Skills", yOffset, contentWidth);
            yOffset = DrawSkillsSection(Section.MinorSkills, "Minor Skills", yOffset, contentWidth);
            yOffset = DrawSkillsSection(Section.MiscSkills, "Miscellaneous Skills", yOffset, contentWidth);
            yOffset = DrawActiveEffectsSection(yOffset, contentWidth);

            GUI.EndScrollView();
        }

        private float DrawSectionHeader(Section section, string label, float yOffset, float contentWidth)
        {
            Rect headerRect = new Rect(_contentPadding, yOffset, contentWidth, HeaderHeight);

            GUI.color = _headerColor;
            GUI.DrawTexture(headerRect, _pixelTex);

            // Expand/collapse indicator
            string indicator = _sectionExpanded[section] ? "v" : ">";
            GUI.color = _headerTextColor;
            GUI.Label(
                new Rect(headerRect.x + 8f, headerRect.y, headerRect.width - 16f, headerRect.height),
                $"{indicator}  {label}",
                _sectionHeaderStyle
            );

            // Tap to toggle
            if (Event.current.type == EventType.MouseDown && headerRect.Contains(Event.current.mousePosition))
            {
                ToggleSection(section);
                Event.current.Use();
            }

            GUI.color = Color.white;
            return yOffset + HeaderHeight + 2f;
        }

        private float DrawRow(string label, string value, float yOffset, float contentWidth, int rowIndex)
        {
            return DrawRow(label, value, yOffset, contentWidth, rowIndex, _valueColor);
        }

        private float DrawRow(string label, string value, float yOffset, float contentWidth, int rowIndex, Color valColor)
        {
            Rect rowRect = new Rect(_contentPadding, yOffset, contentWidth, RowHeight);

            GUI.color = rowIndex % 2 == 0 ? _rowColorA : _rowColorB;
            GUI.DrawTexture(rowRect, _pixelTex);

            float halfWidth = contentWidth * 0.5f;

            GUI.color = _labelColor;
            GUI.Label(
                new Rect(rowRect.x + 12f, rowRect.y, halfWidth - 16f, RowHeight),
                label,
                _labelStyle
            );

            GUI.color = valColor;
            GUI.Label(
                new Rect(rowRect.x + halfWidth, rowRect.y, halfWidth - 12f, RowHeight),
                value,
                _valueStyle
            );

            GUI.color = Color.white;
            return yOffset + RowHeight;
        }

        private float DrawLevelInfoSection(float yOffset, float contentWidth)
        {
            Section section = Section.LevelInfo;
            yOffset = DrawSectionHeader(section, SectionLabels[(int)section], yOffset, contentWidth);

            float t = _sectionAnimT[section];
            if (t <= 0.01f)
                return yOffset + SectionGap;

            int rowIndex = 0;

            yOffset = DrawRow("Level", _playerEntity.Level.ToString(), yOffset, contentWidth, rowIndex++);
            yOffset = DrawRow("Class", _playerEntity.Career.Name ?? "Unknown", yOffset, contentWidth, rowIndex++);

            // Experience progress bar
            int currentXp = _playerEntity.CurrentLevelUpSkillSum;
            int neededXp = _playerEntity.Career.AdvancementMultiplier;
            float xpRatio = neededXp > 0 ? Mathf.Clamp01((float)currentXp / neededXp) : 0f;

            yOffset = DrawRow("Skill Advances", $"{currentXp} / {neededXp}", yOffset, contentWidth, rowIndex++);

            // XP bar
            Rect barBgRect = new Rect(_contentPadding + 12f, yOffset + 2f, contentWidth - 24f, 10f);
            GUI.color = new Color(0.12f, 0.12f, 0.15f, 0.9f);
            GUI.DrawTexture(barBgRect, _pixelTex);
            GUI.color = _xpBarColor;
            GUI.DrawTexture(new Rect(barBgRect.x, barBgRect.y, barBgRect.width * xpRatio, barBgRect.height), _pixelTex);
            GUI.color = Color.white;
            yOffset += 16f;

            yOffset = DrawRow("Current Health", $"{_playerEntity.CurrentHealth} / {_playerEntity.MaxHealth}", yOffset, contentWidth, rowIndex++);
            yOffset = DrawRow("Current Magicka", $"{_playerEntity.CurrentMagicka} / {_playerEntity.MaxMagicka}", yOffset, contentWidth, rowIndex++);

            int fatigue = (int)(_playerEntity.CurrentFatigue / 64);
            int maxFatigue = (int)(_playerEntity.MaxFatigue / 64);
            yOffset = DrawRow("Current Fatigue", $"{fatigue} / {maxFatigue}", yOffset, contentWidth, rowIndex++);
            yOffset = DrawRow("Gold Carried", _playerEntity.GoldPieces.ToString("N0"), yOffset, contentWidth, rowIndex++);

            return yOffset + SectionGap;
        }

        private float DrawPrimaryStatsSection(float yOffset, float contentWidth)
        {
            Section section = Section.PrimaryStats;
            yOffset = DrawSectionHeader(section, SectionLabels[(int)section], yOffset, contentWidth);

            float t = _sectionAnimT[section];
            if (t <= 0.01f)
                return yOffset + SectionGap;

            DaggerfallStats stats = _playerEntity.Stats;
            int rowIndex = 0;

            yOffset = DrawStatRow("Strength", stats.LiveStrength, stats.PermanentStrength, yOffset, contentWidth, rowIndex++);
            yOffset = DrawStatRow("Intelligence", stats.LiveIntelligence, stats.PermanentIntelligence, yOffset, contentWidth, rowIndex++);
            yOffset = DrawStatRow("Willpower", stats.LiveWillpower, stats.PermanentWillpower, yOffset, contentWidth, rowIndex++);
            yOffset = DrawStatRow("Agility", stats.LiveAgility, stats.PermanentAgility, yOffset, contentWidth, rowIndex++);
            yOffset = DrawStatRow("Endurance", stats.LiveEndurance, stats.PermanentEndurance, yOffset, contentWidth, rowIndex++);
            yOffset = DrawStatRow("Personality", stats.LivePersonality, stats.PermanentPersonality, yOffset, contentWidth, rowIndex++);
            yOffset = DrawStatRow("Speed", stats.LiveSpeed, stats.PermanentSpeed, yOffset, contentWidth, rowIndex++);
            yOffset = DrawStatRow("Luck", stats.LiveLuck, stats.PermanentLuck, yOffset, contentWidth, rowIndex++);

            return yOffset + SectionGap;
        }

        private float DrawStatRow(string name, int live, int permanent, float yOffset, float contentWidth, int rowIndex)
        {
            string valueStr;
            Color valColor = _valueColor;

            if (live > permanent)
            {
                valueStr = $"{live} (+{live - permanent})";
                valColor = _buffColor;
            }
            else if (live < permanent)
            {
                valueStr = $"{live} ({live - permanent})";
                valColor = _debuffColor;
            }
            else
            {
                valueStr = live.ToString();
            }

            return DrawRow(name, valueStr, yOffset, contentWidth, rowIndex, valColor);
        }

        private float DrawSkillsSection(Section section, string label, float yOffset, float contentWidth)
        {
            yOffset = DrawSectionHeader(section, label, yOffset, contentWidth);

            float t = _sectionAnimT[section];
            if (t <= 0.01f)
                return yOffset + SectionGap;

            DaggerfallSkills skills = _playerEntity.Skills;
            List<DFCareer.Skills> skillList = GetSkillsForSection(section);
            int rowIndex = 0;

            for (int i = 0; i < skillList.Count; i++)
            {
                DFCareer.Skills skill = skillList[i];
                int liveValue = skills.GetLiveSkillValue(skill);
                int permValue = skills.GetPermanentSkillValue(skill);
                string skillName = DaggerfallUnity.Instance != null
                    ? DaggerfallUnity.Instance.TextProvider.GetSkillName(skill)
                    : skill.ToString();

                Color valColor = _valueColor;
                string valStr = liveValue.ToString();

                if (liveValue > permValue)
                {
                    valStr = $"{liveValue} (+{liveValue - permValue})";
                    valColor = _buffColor;
                }
                else if (liveValue < permValue)
                {
                    valStr = $"{liveValue} ({liveValue - permValue})";
                    valColor = _debuffColor;
                }

                yOffset = DrawRow(skillName, valStr, yOffset, contentWidth, rowIndex++, valColor);
            }

            return yOffset + SectionGap;
        }

        private float DrawActiveEffectsSection(float yOffset, float contentWidth)
        {
            Section section = Section.ActiveEffects;
            yOffset = DrawSectionHeader(section, SectionLabels[(int)section], yOffset, contentWidth);

            float t = _sectionAnimT[section];
            if (t <= 0.01f)
                return yOffset + SectionGap;

            // Read active effects from the entity effect manager
            EntityEffectManager effectManager = null;
            if (GameManager.Instance != null && GameManager.Instance.PlayerEntityBehaviour != null)
            {
                effectManager = GameManager.Instance.PlayerEntityBehaviour.GetComponent<EntityEffectManager>();
            }

            if (effectManager == null)
            {
                Rect noEffectsRect = new Rect(_contentPadding + 12f, yOffset, contentWidth - 24f, RowHeight);
                GUI.color = _labelColor;
                GUI.Label(noEffectsRect, "No active effects.", _labelStyle);
                GUI.color = Color.white;
                return yOffset + RowHeight + SectionGap;
            }

            int rowIndex = 0;
            EntityEffectBundle[] bundles = effectManager.EffectBundles;
            if (bundles == null || bundles.Length == 0)
            {
                Rect noEffectsRect = new Rect(_contentPadding + 12f, yOffset, contentWidth - 24f, RowHeight);
                GUI.color = _labelColor;
                GUI.Label(noEffectsRect, "No active effects.", _labelStyle);
                GUI.color = Color.white;
                return yOffset + RowHeight + SectionGap;
            }

            for (int b = 0; b < bundles.Length; b++)
            {
                EntityEffectBundle bundle = bundles[b];
                if (bundle == null)
                    continue;

                string bundleName = bundle.Name ?? "Unknown Effect";
                int roundsRemaining = bundle.RoundsRemaining;
                string timeStr = roundsRemaining > 0 ? $"{roundsRemaining} rnds" : "Permanent";

                bool isHarmful = bundle.Settings.TargetType == TargetTypes.CasterOnly
                    ? false
                    : true; // Simplified heuristic

                Color effectColor = isHarmful ? _debuffColor : _buffColor;
                yOffset = DrawRow(bundleName, timeStr, yOffset, contentWidth, rowIndex++, effectColor);
            }

            return yOffset + SectionGap;
        }

        #endregion

        #region Helpers

        private List<DFCareer.Skills> GetSkillsForSection(Section section)
        {
            List<DFCareer.Skills> result = new List<DFCareer.Skills>();
            if (_playerEntity == null || _playerEntity.Career == null)
                return result;

            DFCareer career = _playerEntity.Career;

            switch (section)
            {
                case Section.MajorSkills:
                    AddCareerSkills(result, career.MajorSkill1, career.MajorSkill2, career.MajorSkill3);
                    break;
                case Section.MinorSkills:
                    AddCareerSkills(result, career.MinorSkill1, career.MinorSkill2, career.MinorSkill3);
                    break;
                case Section.MiscSkills:
                    // All skills not in major or minor
                    HashSet<DFCareer.Skills> majorMinor = new HashSet<DFCareer.Skills>
                    {
                        career.MajorSkill1, career.MajorSkill2, career.MajorSkill3,
                        career.MinorSkill1, career.MinorSkill2, career.MinorSkill3
                    };
                    foreach (DFCareer.Skills skill in Enum.GetValues(typeof(DFCareer.Skills)))
                    {
                        if (skill == DFCareer.Skills.None)
                            continue;
                        if (!majorMinor.Contains(skill))
                            result.Add(skill);
                    }
                    break;
            }

            return result;
        }

        private void AddCareerSkills(List<DFCareer.Skills> list, params DFCareer.Skills[] skills)
        {
            for (int i = 0; i < skills.Length; i++)
            {
                if (skills[i] != DFCareer.Skills.None)
                    list.Add(skills[i]);
            }
        }

        private float CalculateTotalContentHeight()
        {
            // Rough estimate for scroll view sizing
            float total = 0f;
            int sectionCount = Enum.GetValues(typeof(Section)).Length;

            for (int i = 0; i < sectionCount; i++)
            {
                total += HeaderHeight + 2f; // header
                total += SectionGap;

                Section section = (Section)i;
                if (!_sectionExpanded.ContainsKey(section) || !_sectionExpanded[section])
                    continue;

                switch (section)
                {
                    case Section.LevelInfo:
                        total += RowHeight * 7 + 16f; // 7 rows + xp bar
                        break;
                    case Section.PrimaryStats:
                        total += RowHeight * 8;
                        break;
                    case Section.MajorSkills:
                        total += RowHeight * 3;
                        break;
                    case Section.MinorSkills:
                        total += RowHeight * 3;
                        break;
                    case Section.MiscSkills:
                        total += RowHeight * 29; // remaining skills
                        break;
                    case Section.ActiveEffects:
                        total += RowHeight * 5; // estimate
                        break;
                }
            }

            return total + 100f; // padding
        }

        private void AnimateSections()
        {
            foreach (Section s in Enum.GetValues(typeof(Section)))
            {
                float target = _sectionExpanded[s] ? 1f : 0f;
                float current = _sectionAnimT[s];
                _sectionAnimT[s] = Mathf.MoveTowards(current, target, Time.deltaTime * SectionAnimSpeed);
            }
        }

        private void CachePlayerEntity()
        {
            if (_playerEntity == null && GameManager.Instance != null)
            {
                _playerEntity = GameManager.Instance.PlayerEntity;
            }
        }

        private void RecalculateLayoutIfNeeded()
        {
            if (_lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height)
                return;

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            ScreenLayoutResolver layout = LegacyBootstrapper.Instance != null
                ? LegacyBootstrapper.Instance.ScreenLayout
                : null;

            _safeArea = layout != null ? layout.SafeArea : Screen.safeArea;

            float dpScale = layout != null ? layout.DpToPixels(1f) : 1f;
            _contentPadding = _contentPaddingDp * dpScale;
            _titleBarHeight = _titleBarHeightDp * dpScale;
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized)
                return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white }
            };

            _sectionHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _headerTextColor }
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = _labelColor }
            };

            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = _valueColor }
            };

            _effectStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = _labelColor }
            };

            _closeBtnStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.4f, 0.4f, 1f) }
            };

            _stylesInitialized = true;
        }

        #endregion
    }
}
