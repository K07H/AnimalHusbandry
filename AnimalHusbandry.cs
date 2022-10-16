using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;

namespace AnimalHusbandry
{
    /// <summary>AnimalHusbandry is a mod for Green Hell that allows you to tweak animal husbandry settings.</summary>
    public class AnimalHusbandry : MonoBehaviour
    {
        #region Enums

        public enum MessageType
        {
            Info,
            Warning,
            Error
        }

        public enum Animals
        {
            None,
            Peccary,
            Capybara,
            Tapir
        }

        #endregion

        #region Constructors/Destructor

        public AnimalHusbandry()
        {
            Instance = this;
        }

        private static AnimalHusbandry Instance;

        public static AnimalHusbandry Get() => AnimalHusbandry.Instance;

        #endregion

        #region Attributes

        /// <summary>The name of this mod.</summary>
        private static readonly string ModName = nameof(AnimalHusbandry);

        /// <summary>Path to ModAPI runtime configuration file (contains game shortcuts).</summary>
        private static readonly string RuntimeConfigurationFile = Path.Combine(Application.dataPath.Replace("GH_Data", "Mods"), "RuntimeConfiguration.xml");

        /// <summary>Path to AnimalHusbandry presets folder.</summary>
        private static readonly string PresetsFolder = Path.Combine(Application.dataPath.Replace("GH_Data", "Mods"), "AnimalHusbandryPresets").Replace('\\', '/');

        /// <summary>Default shortcut to display AnimalHusbandry settings.</summary>
        private static readonly KeyCode DefaultModKeybindingId = KeyCode.Keypad6;

        private static KeyCode ModKeybindingId { get; set; } = DefaultModKeybindingId;

        private static HUDManager LocalHUDManager = null;
        private static Player LocalPlayer = null;

        private static readonly float ModScreenTotalWidth = 850f;
        private static readonly float ModScreenTotalHeight = 500f;
        private static readonly float ModScreenMinWidth = 800f;
        private static readonly float ModScreenMaxWidth = 850f;
        private static readonly float ModScreenMinHeight = 50f;
        private static readonly float ModScreenMaxHeight = 550f;

        private static float ModScreenStartPositionX { get; set; } = Screen.width / 7f;
        private static float ModScreenStartPositionY { get; set; } = Screen.height / 7f;
        private static bool IsMinimized { get; set; } = false;

        public static Rect AnimalHusbandryScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);

        public static Vector2 AnimalSettingsScrollViewPosition;

        public static string SelectedFilterName;
        public static int SelectedFilterIndex;
        public static Animals SelectedFilter = Animals.None;

        public static int SelectedPresetIndex;
        public static string SelectedPresetName;

        public static string PresetNameFieldValue = string.Empty;

        private Color DefaultGuiColor = GUI.color;
        private bool ShowUI = false;

        private List<Preset> DefaultAnimalSettings = null;

        #endregion

        #region Static functions

        public static string HUDBigInfoMessage(string message, MessageType messageType, Color? headcolor = null) => $"<color=#{ (headcolor != null ? ColorUtility.ToHtmlStringRGBA(headcolor.Value) : ColorUtility.ToHtmlStringRGBA(Color.red))  }>{messageType}</color>\n{message}";

        private static void ShowHUDBigInfo(string text, float duration = 2f)
        {
            string header = ModName + " Info";
            string textureName = HUDInfoLogTextureType.Reputation.ToString();
            HUDBigInfo obj = (HUDBigInfo)LocalHUDManager.GetHUD(typeof(HUDBigInfo));
            HUDBigInfoData.s_Duration = duration;
            HUDBigInfoData data = new HUDBigInfoData
            {
                m_Header = header,
                m_Text = text,
                m_TextureName = textureName,
                m_ShowTime = Time.time
            };
            obj.AddInfo(data);
            obj.Show(show: true);
        }

        private static KeyCode GetConfigurableKey()
        {
            if (File.Exists(RuntimeConfigurationFile))
            {
                string[] lines = null;
                try
                {
                    lines = File.ReadAllLines(RuntimeConfigurationFile);
                }
                catch (Exception ex)
                {
                    ModAPI.Log.Write($"[{ModName}:GetConfigurableKey] Exception caught while reading shortcuts configuration: [{ex.ToString()}].");
                }
                if (lines != null && lines.Length > 0)
                {
                    string sttDelim = "<Button ID=\"" + ModName + "\">";
                    string endDelim = "</Button>";
                    foreach (string line in lines)
                    {
                        if (line.Contains(sttDelim) && line.Contains(endDelim))
                        {
                            int stt = line.IndexOf(sttDelim);
                            if ((stt >= 0) && (line.Length > (stt + sttDelim.Length)))
                            {
                                string split = line.Substring(stt + sttDelim.Length);
                                if (split != null && split.Contains(endDelim))
                                {
                                    int end = split.IndexOf(endDelim);
                                    if ((end > 0) && (split.Length > end))
                                    {
                                        string parsed = split.Substring(0, end);
                                        if (!string.IsNullOrEmpty(parsed))
                                        {
                                            parsed = parsed.Replace("NumPad", "Keypad").Replace("Oem", "");
                                            if (!string.IsNullOrEmpty(parsed) && Enum.TryParse<KeyCode>(parsed, true, out KeyCode parsedKey))
                                            {
                                                ModAPI.Log.Write($"[{ModName}:GetConfigurableKey] \"Show settings\" shortcut has been parsed ({parsed}).");
                                                return parsedKey;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            ModAPI.Log.Write($"[{ModName}:GetConfigurableKey] Could not parse \"Show settings\" shortcut. Using default value ({DefaultModKeybindingId.ToString()}).");
            return DefaultModKeybindingId;
        }

        public static AIs.AI.AIID GetAIID(Animals animal) => (Enum.TryParse<AIs.AI.AIID>(animal.ToString(), out AIs.AI.AIID retval) ? retval : AIs.AI.AIID.None);

        #endregion

        #region Methods

        private void InitWindow()
        {
            int wid = GetHashCode();
            AnimalHusbandryScreen = GUILayout.Window(wid,
                AnimalHusbandryScreen,
                InitAnimalHusbandryScreen,
                ModName,
                GUI.skin.window,
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(ModScreenMinWidth),
                GUILayout.MaxWidth(ModScreenMaxWidth),
                GUILayout.ExpandHeight(true),
                GUILayout.MinHeight(ModScreenMinHeight),
                GUILayout.MaxHeight(ModScreenMaxHeight));
        }

        private void InitData()
        {
            AnimalHusbandry.LocalHUDManager = HUDManager.Get();
            AnimalHusbandry.LocalPlayer = Player.Get();
        }

        private void InitSkinUI()
        {
            GUI.skin = ModAPI.Interface.Skin;
        }

        private void CollapseWindow()
        {
            if (!IsMinimized)
            {
                AnimalHusbandryScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenMinHeight);
                IsMinimized = true;
            }
            else
            {
                AnimalHusbandryScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);
                IsMinimized = false;
            }
            InitWindow();
        }

        private void CloseWindow()
        {
            ShowUI = false;
            EnableCursor(false);
        }

        private void EnableCursor(bool blockPlayer = false)
        {
            CursorManager.Get().ShowCursor(blockPlayer, false);

            if (blockPlayer)
            {
                LocalPlayer.BlockMoves();
                LocalPlayer.BlockRotation();
                LocalPlayer.BlockInspection();
            }
            else
            {
                LocalPlayer.UnblockMoves();
                LocalPlayer.UnblockRotation();
                LocalPlayer.UnblockInspection();
            }
        }

        private void ToggleShowUI()
        {
            ShowUI = !ShowUI;
        }

        private List<Preset> CurrentSettingsToPreset()
        {
            Preset peccary = new Preset();
            peccary.Name = "Peccary";
            Preset capybara = new Preset();
            capybara.Name = "Capybara";
            Preset tapir = new Preset();
            tapir.Name = "Tapir";

            AIs.AIManager manager = AIs.AIManager.Get();
            if (manager != null && manager.m_FarmAnimalParamsMap != null &&
                manager.m_FarmAnimalParamsMap.ContainsKey((int)AIs.AI.AIID.Peccary) &&
                manager.m_FarmAnimalParamsMap.ContainsKey((int)AIs.AI.AIID.Capybara) &&
                manager.m_FarmAnimalParamsMap.ContainsKey((int)AIs.AI.AIID.Tapir))
            {
                peccary.m_FoodCapacity = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_FoodCapacity;
                peccary.m_WaterCapacity = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_WaterCapacity;
                peccary.m_SleepTime = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_SleepTime;
                peccary.m_DecreaseFoodLevelPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_DecreaseFoodLevelPerSec;
                peccary.m_DecreaseWaterLevelPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_DecreaseWaterLevelPerSec;
                peccary.m_DecreasePoisonLevelPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_DecreasePoisonLevelPerSec;
                peccary.m_WaterLevelToDrink = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_WaterLevelToDrink;
                peccary.m_FoodLevelToEat = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_FoodLevelToEat;
                peccary.m_DecreaseTrustPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_DecreaseTrustPerSec;
                peccary.m_IncreaseTrustPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_IncreaseTrustPerSec;
                peccary.m_TrustDecreaseOnHitMe = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_TrustDecreaseOnHitMe;
                peccary.m_TrustDecreaseOnHitOther = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_TrustDecreaseOnHitOther;
                peccary.m_OutsideFarmDecreaseTrustPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_OutsideFarmDecreaseTrustPerSec;
                peccary.m_TrustLevelToRunAway = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_TrustLevelToRunAway;
                peccary.m_PregnantCooldown = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_PregnantCooldown;
                peccary.m_PregnantDuration = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_PregnantDuration;
                peccary.m_MaturationPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_MaturationPerSec;
                peccary.m_DecreaseHealthPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_DecreaseHealthPerSec;
                peccary.m_IncreaseHealthPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_IncreaseHealthPerSec;
                peccary.m_MinFoodToGainTrust = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_MinFoodToGainTrust;
                peccary.m_MinWaterToGainTrust = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_MinWaterToGainTrust;
                peccary.m_NoTrustDistanceToPlayer = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_NoTrustDistanceToPlayer;
                peccary.m_FollowWhistlerDuration = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_FollowWhistlerDuration;
                peccary.m_ShitInterval = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_ShitInterval;
                peccary.m_DurationOfBeingTied = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_DurationOfBeingTied;
                peccary.m_PoisonFromShitPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_PoisonFromShitPerSec;
                peccary.m_ShitPoisonLimit = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_ShitPoisonLimit;
                peccary.m_MinTrustToWhistle = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_MinTrustToWhistle;
                peccary.m_MinTrustToPet = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_MinTrustToPet;
                peccary.m_MinTrustToSetName = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Peccary].m_MinTrustToSetName;

                capybara.m_FoodCapacity = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_FoodCapacity;
                capybara.m_WaterCapacity = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_WaterCapacity;
                capybara.m_SleepTime = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_SleepTime;
                capybara.m_DecreaseFoodLevelPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_DecreaseFoodLevelPerSec;
                capybara.m_DecreaseWaterLevelPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_DecreaseWaterLevelPerSec;
                capybara.m_DecreasePoisonLevelPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_DecreasePoisonLevelPerSec;
                capybara.m_WaterLevelToDrink = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_WaterLevelToDrink;
                capybara.m_FoodLevelToEat = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_FoodLevelToEat;
                capybara.m_DecreaseTrustPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_DecreaseTrustPerSec;
                capybara.m_IncreaseTrustPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_IncreaseTrustPerSec;
                capybara.m_TrustDecreaseOnHitMe = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_TrustDecreaseOnHitMe;
                capybara.m_TrustDecreaseOnHitOther = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_TrustDecreaseOnHitOther;
                capybara.m_OutsideFarmDecreaseTrustPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_OutsideFarmDecreaseTrustPerSec;
                capybara.m_TrustLevelToRunAway = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_TrustLevelToRunAway;
                capybara.m_PregnantCooldown = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_PregnantCooldown;
                capybara.m_PregnantDuration = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_PregnantDuration;
                capybara.m_MaturationPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_MaturationPerSec;
                capybara.m_DecreaseHealthPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_DecreaseHealthPerSec;
                capybara.m_IncreaseHealthPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_IncreaseHealthPerSec;
                capybara.m_MinFoodToGainTrust = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_MinFoodToGainTrust;
                capybara.m_MinWaterToGainTrust = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_MinWaterToGainTrust;
                capybara.m_NoTrustDistanceToPlayer = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_NoTrustDistanceToPlayer;
                capybara.m_FollowWhistlerDuration = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_FollowWhistlerDuration;
                capybara.m_ShitInterval = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_ShitInterval;
                capybara.m_DurationOfBeingTied = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_DurationOfBeingTied;
                capybara.m_PoisonFromShitPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_PoisonFromShitPerSec;
                capybara.m_ShitPoisonLimit = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_ShitPoisonLimit;
                capybara.m_MinTrustToWhistle = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_MinTrustToWhistle;
                capybara.m_MinTrustToPet = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_MinTrustToPet;
                capybara.m_MinTrustToSetName = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Capybara].m_MinTrustToSetName;

                tapir.m_FoodCapacity = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_FoodCapacity;
                tapir.m_WaterCapacity = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_WaterCapacity;
                tapir.m_SleepTime = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_SleepTime;
                tapir.m_DecreaseFoodLevelPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_DecreaseFoodLevelPerSec;
                tapir.m_DecreaseWaterLevelPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_DecreaseWaterLevelPerSec;
                tapir.m_DecreasePoisonLevelPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_DecreasePoisonLevelPerSec;
                tapir.m_WaterLevelToDrink = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_WaterLevelToDrink;
                tapir.m_FoodLevelToEat = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_FoodLevelToEat;
                tapir.m_DecreaseTrustPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_DecreaseTrustPerSec;
                tapir.m_IncreaseTrustPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_IncreaseTrustPerSec;
                tapir.m_TrustDecreaseOnHitMe = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_TrustDecreaseOnHitMe;
                tapir.m_TrustDecreaseOnHitOther = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_TrustDecreaseOnHitOther;
                tapir.m_OutsideFarmDecreaseTrustPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_OutsideFarmDecreaseTrustPerSec;
                tapir.m_TrustLevelToRunAway = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_TrustLevelToRunAway;
                tapir.m_PregnantCooldown = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_PregnantCooldown;
                tapir.m_PregnantDuration = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_PregnantDuration;
                tapir.m_MaturationPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_MaturationPerSec;
                tapir.m_DecreaseHealthPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_DecreaseHealthPerSec;
                tapir.m_IncreaseHealthPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_IncreaseHealthPerSec;
                tapir.m_MinFoodToGainTrust = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_MinFoodToGainTrust;
                tapir.m_MinWaterToGainTrust = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_MinWaterToGainTrust;
                tapir.m_NoTrustDistanceToPlayer = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_NoTrustDistanceToPlayer;
                tapir.m_FollowWhistlerDuration = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_FollowWhistlerDuration;
                tapir.m_ShitInterval = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_ShitInterval;
                tapir.m_DurationOfBeingTied = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_DurationOfBeingTied;
                tapir.m_PoisonFromShitPerSec = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_PoisonFromShitPerSec;
                tapir.m_ShitPoisonLimit = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_ShitPoisonLimit;
                tapir.m_MinTrustToWhistle = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_MinTrustToWhistle;
                tapir.m_MinTrustToPet = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_MinTrustToPet;
                tapir.m_MinTrustToSetName = manager.m_FarmAnimalParamsMap[(int)AIs.AI.AIID.Tapir].m_MinTrustToSetName;
            }

            return new List<Preset> {
                peccary,
                capybara,
                tapir
            };
        }

        private void LoadPreset(List<Preset> settings, string successMessage)
        {
            if (string.IsNullOrWhiteSpace(successMessage))
                successMessage = "Preset has been applied successfully.";
            if (settings != null && settings.Count == 3)
            {
                AIs.AIManager manager = AIs.AIManager.Get();
                if (manager != null && manager.m_FarmAnimalParamsMap != null &&
                    manager.m_FarmAnimalParamsMap.ContainsKey((int)AIs.AI.AIID.Peccary) &&
                    manager.m_FarmAnimalParamsMap.ContainsKey((int)AIs.AI.AIID.Capybara) &&
                    manager.m_FarmAnimalParamsMap.ContainsKey((int)AIs.AI.AIID.Tapir))
                {
                    foreach (Preset preset in settings)
                    {
                        int key = -1;
                        if (preset.Name == "Peccary")
                            key = (int)AIs.AI.AIID.Peccary;
                        else if (preset.Name == "Capybara")
                            key = (int)AIs.AI.AIID.Capybara;
                        else if (preset.Name == "Tapir")
                            key = (int)AIs.AI.AIID.Tapir;
                        else
                            key = -1;

                        if (key >= 0)
                        {
                            manager.m_FarmAnimalParamsMap[key].m_FoodCapacity = preset.m_FoodCapacity;
                            manager.m_FarmAnimalParamsMap[key].m_WaterCapacity = preset.m_WaterCapacity;
                            manager.m_FarmAnimalParamsMap[key].m_SleepTime = preset.m_SleepTime;
                            manager.m_FarmAnimalParamsMap[key].m_DecreaseFoodLevelPerSec = preset.m_DecreaseFoodLevelPerSec;
                            manager.m_FarmAnimalParamsMap[key].m_DecreaseWaterLevelPerSec = preset.m_DecreaseWaterLevelPerSec;
                            manager.m_FarmAnimalParamsMap[key].m_DecreasePoisonLevelPerSec = preset.m_DecreasePoisonLevelPerSec;
                            manager.m_FarmAnimalParamsMap[key].m_WaterLevelToDrink = preset.m_WaterLevelToDrink;
                            manager.m_FarmAnimalParamsMap[key].m_FoodLevelToEat = preset.m_FoodLevelToEat;
                            manager.m_FarmAnimalParamsMap[key].m_DecreaseTrustPerSec = preset.m_DecreaseTrustPerSec;
                            manager.m_FarmAnimalParamsMap[key].m_IncreaseTrustPerSec = preset.m_IncreaseTrustPerSec;
                            manager.m_FarmAnimalParamsMap[key].m_TrustDecreaseOnHitMe = preset.m_TrustDecreaseOnHitMe;
                            manager.m_FarmAnimalParamsMap[key].m_TrustDecreaseOnHitOther = preset.m_TrustDecreaseOnHitOther;
                            manager.m_FarmAnimalParamsMap[key].m_OutsideFarmDecreaseTrustPerSec = preset.m_OutsideFarmDecreaseTrustPerSec;
                            manager.m_FarmAnimalParamsMap[key].m_TrustLevelToRunAway = preset.m_TrustLevelToRunAway;
                            manager.m_FarmAnimalParamsMap[key].m_PregnantCooldown = preset.m_PregnantCooldown;
                            manager.m_FarmAnimalParamsMap[key].m_PregnantDuration = preset.m_PregnantDuration;
                            manager.m_FarmAnimalParamsMap[key].m_MaturationPerSec = preset.m_MaturationPerSec;
                            manager.m_FarmAnimalParamsMap[key].m_DecreaseHealthPerSec = preset.m_DecreaseHealthPerSec;
                            manager.m_FarmAnimalParamsMap[key].m_IncreaseHealthPerSec = preset.m_IncreaseHealthPerSec;
                            manager.m_FarmAnimalParamsMap[key].m_MinFoodToGainTrust = preset.m_MinFoodToGainTrust;
                            manager.m_FarmAnimalParamsMap[key].m_MinWaterToGainTrust = preset.m_MinWaterToGainTrust;
                            manager.m_FarmAnimalParamsMap[key].m_NoTrustDistanceToPlayer = preset.m_NoTrustDistanceToPlayer;
                            manager.m_FarmAnimalParamsMap[key].m_FollowWhistlerDuration = preset.m_FollowWhistlerDuration;
                            manager.m_FarmAnimalParamsMap[key].m_ShitInterval = preset.m_ShitInterval;
                            manager.m_FarmAnimalParamsMap[key].m_DurationOfBeingTied = preset.m_DurationOfBeingTied;
                            manager.m_FarmAnimalParamsMap[key].m_PoisonFromShitPerSec = preset.m_PoisonFromShitPerSec;
                            manager.m_FarmAnimalParamsMap[key].m_ShitPoisonLimit = preset.m_ShitPoisonLimit;
                            manager.m_FarmAnimalParamsMap[key].m_MinTrustToWhistle = preset.m_MinTrustToWhistle;
                            manager.m_FarmAnimalParamsMap[key].m_MinTrustToPet = preset.m_MinTrustToPet;
                            manager.m_FarmAnimalParamsMap[key].m_MinTrustToSetName = preset.m_MinTrustToSetName;
                        }
                    }
                    ShowHUDBigInfo(HUDBigInfoMessage(successMessage, MessageType.Info, Color.green), 5f);
                    ModAPI.Log.Write("[AnimalHusbandry:LoadPreset] " + successMessage);
                }
            }
        }

        private string[] GetPresets()
        {
            List<string> allFiles = new List<string>();
            allFiles.Add("None");
            try
            {
                if (Directory.Exists(PresetsFolder))
                {
                    DirectoryInfo d = new DirectoryInfo(PresetsFolder);
                    if (d != null)
                    {
                        FileInfo[] files = d.GetFiles("*.txt");
                        if (files != null && files.Length > 0)
                        {
                            foreach (FileInfo file in files)
                                allFiles.Add(file.Name.EndsWith(".txt") && file.Name.Length > 4 ? file.Name.Substring(0, file.Name.Length - 4) : file.Name);
                            return allFiles.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModAPI.Log.Write("[AnimalHusbandry:SearchPresets] Exception caught: [" + ex.ToString() + "].");
            }
            return allFiles.ToArray();
        }

        private string[] GetAnimals()
        {
            string[] animals = Enum.GetNames(typeof(Animals));
            if (animals != null && animals.Length > 0)
            {
                int len = animals.Length;
                for (int i = 0; i < len; i++)
                    animals[i] = animals[i].Replace('_', ' ');
            }
            return animals;
        }

        protected void serializer_UnknownNode(object sender, XmlNodeEventArgs e)
        {
#if DEBUG
            ModAPI.Log.Write("[AnimalHusbandry:serializer_UnknownNode] Unknown Node:" + e.Name + "\t" + e.Text);
#endif
        }

        protected void serializer_UnknownAttribute(object sender, XmlAttributeEventArgs e)
        {
#if DEBUG
            System.Xml.XmlAttribute attr = e.Attr;
            ModAPI.Log.Write("[AnimalHusbandry:serializer_UnknownAttribute] Unknown attribute " + attr.Name + "='" + attr.Value + "'");
#endif
        }

        #endregion

        #region Main UI methods

        private void InitAnimalHusbandryScreen(int windowID)
        {
            ModScreenStartPositionX = AnimalHusbandryScreen.x;
            ModScreenStartPositionY = AnimalHusbandryScreen.y;

            using (var modContentScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                ScreenMenuBox();
                if (P2PSession.Instance.GetGameVisibility() == P2PGameVisibility.Singleplayer || ReplTools.AmIMaster())
                {
                    if (!IsMinimized)
                    {
                        AnimalsFilterBox();
                        if (SelectedFilter != Animals.None)
                            AnimalSettingsScrollViewBox();
                        PresetsBox();
                    }
                }
                else
                {
                    GUI.color = Color.red;
                    GUILayout.Label("This mod only works if you are the host or in singleplayer mode.", GUI.skin.label);
                    GUI.color = DefaultGuiColor;
                }
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void ScreenMenuBox()
        {
            if (GUI.Button(new Rect(AnimalHusbandryScreen.width - 40f, 0f, 20f, 20f), "-", GUI.skin.button))
                CollapseWindow();
            if (GUI.Button(new Rect(AnimalHusbandryScreen.width - 20f, 0f, 20f, 20f), "X", GUI.skin.button))
                CloseWindow();
        }

        private void AnimalsFilterBox()
        {
            using (var filterScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                string[] filters = GetAnimals();
                if (filters != null)
                {
                    int filtersCount = filters.Length;
                    GUI.color = Color.cyan;
                    GUILayout.Label("Choose an animal then click on the \"Settings\" button.", GUI.skin.label);

                    GUI.color = DefaultGuiColor;
                    SelectedFilterIndex = GUILayout.SelectionGrid(SelectedFilterIndex, filters, filtersCount, GUI.skin.button);
                    GUILayout.Space(5f);
                    if (GUILayout.Button("Settings", GUI.skin.button))
                        OnClickApplyFilterButton();
                }
            }
        }

        private void AnimalSettingsScrollViewBox()
        {
            GUILayout.Space(10.0f);
            using (var animalSettingsViewScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                GUI.color = Color.cyan;
                GUILayout.Label($"Selected animal: {SelectedFilter.ToString().Replace('_', ' ')}", GUI.skin.label);

                GUI.color = DefaultGuiColor;
                GUILayout.Label("Settings: ", GUI.skin.label);
                GUILayout.Space(10f);

                AnimalSettingsScrollViewPosition = GUILayout.BeginScrollView(AnimalSettingsScrollViewPosition, GUI.skin.scrollView, GUILayout.MinHeight(300f));

                int animalKey = (int)AnimalHusbandry.GetAIID(SelectedFilter);
                AIs.AIManager manager = AIs.AIManager.Get();
                if (manager != null && manager.m_FarmAnimalParamsMap != null && manager.m_FarmAnimalParamsMap.ContainsKey(animalKey))
                {
                    FarmAnimalParams animalParams = manager.m_FarmAnimalParamsMap[animalKey];
                    if (animalParams != null)
                    {
                        var ItalicLabelStyle = new GUIStyle(GUI.skin.label);
                        ItalicLabelStyle.fontStyle = FontStyle.Italic;

                        GUI.color = Color.yellow;
                        GUILayout.Label("Food: ", GUI.skin.label);
                        GUI.color = DefaultGuiColor;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Food capacity: " + animalParams.m_FoodCapacity.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_FoodCapacity = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_FoodCapacity, 1.0f, 1000.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The amount of food the animal can contain.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Food level to eat: " + animalParams.m_FoodLevelToEat.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_FoodLevelToEat = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_FoodLevelToEat, 0.01f, 1.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("If the food level of the animal is below this value it will try to eat (higher value means it will try to eat more often).", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Food level decrease per second: " + animalParams.m_DecreaseFoodLevelPerSec.ToString("F6", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_DecreaseFoodLevelPerSec = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_DecreaseFoodLevelPerSec, 0.000001f, 0.005f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The rate at which the animal is loosing food (higher value means it will eat more often).", ItalicLabelStyle);
                        GUILayout.Space(25f);
                        GUI.color = Color.yellow;
                        GUILayout.Label("Water: ", GUI.skin.label);
                        GUI.color = DefaultGuiColor;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Water capacity: " + animalParams.m_WaterCapacity.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_WaterCapacity = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_WaterCapacity, 1.0f, 1000.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The amount of water the animal can contain.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Water level to drink: " + animalParams.m_WaterLevelToDrink.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_WaterLevelToDrink = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_WaterLevelToDrink, 0.01f, 1.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("If the water level of the animal is below this value it will try to drink (higher value means it will try to drink more often).", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Water level decrease per second: " + animalParams.m_DecreaseWaterLevelPerSec.ToString("F6", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_DecreaseWaterLevelPerSec = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_DecreaseWaterLevelPerSec, 0.000001f, 0.005f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The rate at which the animal is losing water (higher value means it will drink more often).", ItalicLabelStyle);
                        GUILayout.Space(25f);
                        GUI.color = Color.yellow;
                        GUILayout.Label("Shit poisoning: ", GUI.skin.label);
                        GUI.color = DefaultGuiColor;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Shit interval: " + animalParams.m_ShitInterval.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_ShitInterval = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_ShitInterval, 10.0f, 10000.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The rate at which the animal defecates.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Shit poison limit: " + animalParams.m_ShitPoisonLimit.ToString(CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_ShitPoisonLimit = (int)GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_ShitPoisonLimit, 0.0f, 20.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The maximum amount of droppings taken into account when calculating poison level (the more droppings taken into account the faster poison level will increase). Setting this to 0 will disable poison level increase.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Poison level from shit per second: " + animalParams.m_PoisonFromShitPerSec.ToString("F6", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_PoisonFromShitPerSec = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_PoisonFromShitPerSec, 0.000001f, 0.005f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The rate at which the animal gets poisoned (if there are droppings nearby).", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Poison level decrease per second: " + animalParams.m_DecreasePoisonLevelPerSec.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_DecreasePoisonLevelPerSec = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_DecreasePoisonLevelPerSec, 0.01f, 1.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The rate at which the animal is eliminating the poison from its body.", ItalicLabelStyle);
                        GUILayout.Space(25f);
                        GUI.color = Color.yellow;
                        GUILayout.Label("Health: ", GUI.skin.label);
                        GUI.color = DefaultGuiColor;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Health level decrease per second: " + animalParams.m_DecreaseHealthPerSec.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_DecreaseHealthPerSec = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_DecreaseHealthPerSec, 0.0001f, 0.5f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The amount of health that is lost every second (if food or water levels are not ok, or if the animal is poisoned or bleeding).", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Health level increase per second: " + animalParams.m_IncreaseHealthPerSec.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_IncreaseHealthPerSec = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_IncreaseHealthPerSec, 0.0001f, 0.5f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The amount of health that is gained every second (if food and water levels are ok, and if the animal is not poisoned or bleeding).", ItalicLabelStyle);
                        GUILayout.Space(25f);
                        GUI.color = Color.yellow;
                        GUILayout.Label("Pregnancy: ", GUI.skin.label);
                        GUI.color = DefaultGuiColor;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Pregnancy duration: " + animalParams.m_PregnantDuration.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_PregnantDuration = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_PregnantDuration, 100.0f, 50000.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The amount of time to wait before a baby is born.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Pregnancy cooldown: " + animalParams.m_PregnantCooldown.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_PregnantCooldown = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_PregnantCooldown, 100.0f, 10000.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The amount of time to wait before the animal can be pregnant again.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Maturation level per second: " + animalParams.m_MaturationPerSec.ToString("F6", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_MaturationPerSec = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_MaturationPerSec, 0.000001f, 0.005f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The rate at which the animal grows.", ItalicLabelStyle);
                        GUILayout.Space(25f);
                        GUI.color = Color.yellow;
                        GUILayout.Label("Trust: ", GUI.skin.label);
                        GUI.color = DefaultGuiColor;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Min food level to gain trust: " + animalParams.m_MinFoodToGainTrust.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_MinFoodToGainTrust = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_MinFoodToGainTrust, 0.01f, 1.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The minimum amount of food that is required for the animal to gain trust.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Min water level to gain trust: " + animalParams.m_MinWaterToGainTrust.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_MinWaterToGainTrust = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_MinWaterToGainTrust, 0.01f, 1.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The minimum amount of water that is required for the animal to gain trust.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Trust increase per second: " + animalParams.m_IncreaseTrustPerSec.ToString("F6", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_IncreaseTrustPerSec = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_IncreaseTrustPerSec, 0.000001f, 0.005f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The amount of trust that is gained every second.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Trust decrease per second: " + animalParams.m_DecreaseTrustPerSec.ToString("F6", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_DecreaseTrustPerSec = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_DecreaseTrustPerSec, 0.000001f, 0.005f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The amount of trust that is lost every second.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Outside farm trust decrease per second: " + animalParams.m_OutsideFarmDecreaseTrustPerSec.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_OutsideFarmDecreaseTrustPerSec = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_OutsideFarmDecreaseTrustPerSec, 0.0001f, 0.3f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The amount of trust that is lost every second if the animal is outside the pen.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Trust decrease on hit me: " + animalParams.m_TrustDecreaseOnHitMe.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_TrustDecreaseOnHitMe = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_TrustDecreaseOnHitMe, 0.01f, 1.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The amount of trust that is lost by the animal when it gets hit.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Trust decrease on hit other: " + animalParams.m_TrustDecreaseOnHitOther.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_TrustDecreaseOnHitOther = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_TrustDecreaseOnHitOther, 0.01f, 1.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The amount of trust that is lost by other animals around when an animal is hit.", ItalicLabelStyle);
                        GUILayout.Space(25f);
                        GUI.color = Color.yellow;
                        GUILayout.Label("Other trust settings: ", GUI.skin.label);
                        GUI.color = DefaultGuiColor;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Min trust to pet: " + animalParams.m_MinTrustToPet.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_MinTrustToPet = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_MinTrustToPet, 0.01f, 1.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The minimum level of trust required to pet the animal.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Min trust to give name: " + animalParams.m_MinTrustToSetName.ToString(CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_MinTrustToSetName = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_MinTrustToSetName, 0.0f, 1.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The minimum level of trust required to give the animal a name (requires to pet the animal first).", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Min trust to whistle: " + animalParams.m_MinTrustToWhistle.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_MinTrustToWhistle = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_MinTrustToWhistle, 0.01f, 1.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The minimum level of trust required to whistle the animal.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("No trust distance from player: " + animalParams.m_NoTrustDistanceToPlayer.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_NoTrustDistanceToPlayer = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_NoTrustDistanceToPlayer, 0.1f, 50.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The minimum distance between the player and the animal for it to stop fleeing (higher value means the animal will run further).", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Trust level to run away: " + animalParams.m_TrustLevelToRunAway.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_TrustLevelToRunAway = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_TrustLevelToRunAway, 0.01f, 1.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The minimum level of trust required for the animal to stop fleeing.", ItalicLabelStyle);
                        GUILayout.Space(25f);
                        GUI.color = Color.yellow;
                        GUILayout.Label("Other AI settings: ", GUI.skin.label);
                        GUI.color = DefaultGuiColor;
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Tied duration: " + animalParams.m_DurationOfBeingTied.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_DurationOfBeingTied = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_DurationOfBeingTied, 1.0f, 10000.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The length of time the animal remains tied up.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Follow whistle duration: " + animalParams.m_FollowWhistlerDuration.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_FollowWhistlerDuration = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_FollowWhistlerDuration, 1.0f, 1000.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The length of time the animal follows the player.", ItalicLabelStyle);
                        GUILayout.Space(8f);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Sleep duration: " + animalParams.m_SleepTime.ToString("F3", CultureInfo.InvariantCulture), GUI.skin.label);
                        GUILayout.FlexibleSpace();
                        manager.m_FarmAnimalParamsMap[animalKey].m_SleepTime = GUILayout.HorizontalSlider(manager.m_FarmAnimalParamsMap[animalKey].m_SleepTime, 1.0f, 5000.0f, GUILayout.Width(400.0f));
                        GUILayout.EndHorizontal();
                        GUILayout.Label("The length of time the animal stays idling when sleeping.", ItalicLabelStyle);
                    }
                    else
                        GUILayout.Label("No settings were found for this animal.", GUI.skin.label);
                }
                else
                    GUILayout.Label("No settings were found for this animal.", GUI.skin.label);

                GUILayout.EndScrollView();
            }
        }

        private void PresetsBox()
        {
            GUILayout.Space(10.0f);
            using (var mainViewScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                var ItalicLabelStyle = new GUIStyle(GUI.skin.label);
                ItalicLabelStyle.fontStyle = FontStyle.Italic;
                ItalicLabelStyle.fontSize = ItalicLabelStyle.fontSize - 2;
                GUILayout.Label("Presets folder location: " + PresetsFolder, ItalicLabelStyle);
                GUILayout.Space(5f);
                GUI.color = Color.cyan;
                GUILayout.Label("Save preset (allows you to save currents settings for all animals into a preset): ", GUI.skin.label);
                GUI.color = DefaultGuiColor;
                using (var actionScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label("Preset name: ", GUI.skin.label);
                    PresetNameFieldValue = GUILayout.TextField(PresetNameFieldValue, 100, GUI.skin.textField, GUILayout.MinWidth(150.0f), GUILayout.MaxWidth(300.0f));
                    if (GUILayout.Button("Save preset", GUI.skin.button, GUILayout.MaxWidth(200f)))
                        OnClickSavePresetButton();
                    GUILayout.FlexibleSpace();
                }
                GUILayout.Space(5f);
                GUI.color = Color.cyan;
                GUILayout.Label("Load preset (allows you to load settings for all animals from a preset): ", GUI.skin.label);
                GUI.color = DefaultGuiColor;
                string[] foundPresets = GetPresets();
                if (foundPresets.Length > 0)
                {
                    using (var presetsListScope = new GUILayout.VerticalScope(GUI.skin.box))
                    {
                        SelectedPresetIndex = GUILayout.SelectionGrid(SelectedPresetIndex, foundPresets, 3, GUI.skin.button);
                    }
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Load preset", GUI.skin.button, GUILayout.MaxWidth(200f)))
                        OnClickLoadPresetButton();
                    if (GUILayout.Button("Delete preset", GUI.skin.button, GUILayout.MaxWidth(200f)))
                        OnClickDeletePresetButton();
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Restore default settings", GUI.skin.button, GUILayout.MaxWidth(200f)))
                        OnClickRestoreDefaultsButton();
                    GUILayout.EndHorizontal();
                }
                else
                    GUILayout.Label("No presets were found.", GUI.skin.label);
            }
        }

        #endregion

        #region Click events

        private void OnClickRestoreDefaultsButton()
        {
            LoadPreset(DefaultAnimalSettings, "Default settings were restored successfully.");
        }

        private void OnClickDeletePresetButton()
        {
            if (SelectedPresetIndex <= 0)
            {
                ShowHUDBigInfo(HUDBigInfoMessage("Please select a preset first.", MessageType.Error, Color.red), 5f);
                return;
            }
            string[] presets = GetPresets();
            if (presets != null && presets.Length > SelectedPresetIndex)
            {
                try
                {
                    SelectedPresetName = presets[SelectedPresetIndex];
                    if (!string.IsNullOrWhiteSpace(SelectedPresetName))
                    {
                        string filePath = Path.Combine(PresetsFolder, SelectedPresetName + ".txt").Replace('\\', '/');
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                            ShowHUDBigInfo(HUDBigInfoMessage("Preset \"" + SelectedPresetName + "\" has been deleted successfully.", MessageType.Info, Color.green), 5f);
                            ModAPI.Log.Write("[AnimalHusbandry:OnClickDeletePresetButton] Preset \"" + SelectedPresetName + "\" has been deleted successfully.");
                            SelectedPresetIndex = 0;
                            SelectedPresetName = "";
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ModAPI.Log.Write("[AnimalHusbandry:OnClickDeletePresetButton] Exception caught: [" + ex.ToString() + "].");
                }
                ShowHUDBigInfo(HUDBigInfoMessage("Could not delete preset from folder \"" + PresetsFolder + "\", check logs.", MessageType.Error, Color.red), 5f);
            }
            else
            {
                ShowHUDBigInfo(HUDBigInfoMessage("Could not find any preset to delete inside folder \"" + PresetsFolder + "\".", MessageType.Error, Color.red), 5f);
                ModAPI.Log.Write("[AnimalHusbandry:OnClickDeletePresetButton] Could not find any preset to delete inside folder \"" + PresetsFolder + "\".");
            }
        }

        private void OnClickLoadPresetButton()
        {
            if (SelectedPresetIndex <= 0)
            {
                ShowHUDBigInfo(HUDBigInfoMessage("Please select a preset first.", MessageType.Error, Color.red), 5f);
                return;
            }
            string[] presets = GetPresets();
            if (presets != null && presets.Length > SelectedPresetIndex)
            {
                try
                {
                    SelectedPresetName = presets[SelectedPresetIndex];
                    if (!string.IsNullOrWhiteSpace(SelectedPresetName))
                    {
                        string filePath = Path.Combine(PresetsFolder, SelectedPresetName + ".txt").Replace('\\', '/');
                        if (File.Exists(filePath))
                        {
                            List<Preset> settings = null;
                            XmlSerializer serializer = new XmlSerializer(typeof(List<Preset>));
                            serializer.UnknownNode += new XmlNodeEventHandler(serializer_UnknownNode);
                            serializer.UnknownAttribute += new XmlAttributeEventHandler(serializer_UnknownAttribute);
                            using (FileStream fs = new FileStream(filePath, FileMode.Open))
                            {
                                settings = (List<Preset>)serializer.Deserialize(fs);
                            }
                            LoadPreset(settings, "Preset \"" + SelectedPresetName + "\" has been applied successfully.");
                        }
                    }
                    ModAPI.Log.Write("[AnimalHusbandry:OnClickLoadPresetButton] Could not load preset from folder \"" + PresetsFolder + "\".");
                }
                catch (Exception ex)
                {
                    ModAPI.Log.Write("[AnimalHusbandry:OnClickLoadPresetButton] Exception caught: [" + ex.ToString() + "].");
                }
                ShowHUDBigInfo(HUDBigInfoMessage("Could not load preset from folder \"" + PresetsFolder + "\", check logs.", MessageType.Error, Color.red), 5f);
            }
            else
            {
                ShowHUDBigInfo(HUDBigInfoMessage("Could not find any preset inside folder \"" + PresetsFolder + "\".", MessageType.Error, Color.red), 5f);
                ModAPI.Log.Write("[AnimalHusbandry:OnClickLoadPresetButton] Could not find any preset inside folder \"" + PresetsFolder + "\".");
            }
        }

        private void OnClickApplyFilterButton()
        {
            string[] filters = GetAnimals();
            if (filters != null)
            {
                SelectedFilterName = filters[SelectedFilterIndex];
                SelectedFilter = (Animals)Enum.Parse(typeof(Animals), SelectedFilterName.Replace(' ', '_'));
            }
            if (SelectedFilterIndex == 0)
                ShowHUDBigInfo(HUDBigInfoMessage("Please select an animal first.", MessageType.Error, Color.red), 5f);
        }

        private void OnClickSavePresetButton()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(PresetNameFieldValue))
                {
                    ShowHUDBigInfo(HUDBigInfoMessage("Cannot save preset (preset name is missing).", MessageType.Error, Color.red), 5f);
                    return;
                }
                if (!PresetNameFieldValue.All(x => char.IsLetterOrDigit(x) || x == ' ' || x == '-' || x == '_'))
                {
                    ShowHUDBigInfo(HUDBigInfoMessage("Cannot save preset (preset name contains invalid characters: only letters, digits, spaces, dashes and underscores are allowed).", MessageType.Error, Color.red), 5f);
                    return;
                }

                if (!Directory.Exists(PresetsFolder))
                    Directory.CreateDirectory(PresetsFolder);
                
                string filePath = Path.Combine(PresetsFolder, PresetNameFieldValue + ".txt").Replace('\\', '/');
                if (File.Exists(filePath))
                {
                    ShowHUDBigInfo(HUDBigInfoMessage("Cannot save preset (a preset with the same name already exists).", MessageType.Error, Color.red), 5f);
                    return;
                }

                List<Preset> presets = CurrentSettingsToPreset();
                XmlSerializer serializer = new XmlSerializer(typeof(List<Preset>));
                TextWriter writer = new StreamWriter(filePath);
                serializer.Serialize(writer, presets);
                writer.Close();

                ShowHUDBigInfo(HUDBigInfoMessage("Preset \"" + PresetNameFieldValue + "\" has been saved to \"" + filePath + "\".", MessageType.Info, Color.green), 5f);
                ModAPI.Log.Write("[AnimalHusbandry:OnClickSavePresetButton] Preset \"" + PresetNameFieldValue + "\" has been saved to \"" + filePath + "\".");
            }
            catch (Exception ex)
            {
                ShowHUDBigInfo(HUDBigInfoMessage("Cannot save preset (" + ex.Message + ").", MessageType.Error, Color.red), 5f);
                ModAPI.Log.Write("[AnimalHusbandry:OnClickSavePresetButton] Exception caught: [" + ex.ToString() + "].");
            }
        }

        #endregion

        #region Unity methods

        private void OnGUI()
        {
            if (ShowUI)
            {
                InitData();
                InitSkinUI();
                InitWindow();
            }
        }

        private void Start()
        {
            ModAPI.Log.Write($"[{ModName}:Start] Initializing {ModName}...");

            // Grab HUD manager and player.
            InitData();
            // Load "Show settings" shortcut.
            ModKeybindingId = GetConfigurableKey();
            // Save default settings.
            DefaultAnimalSettings = CurrentSettingsToPreset();

            ModAPI.Log.Write($"[{ModName}:Start] {ModName} initialized.");
        }

        private void Update()
        {
            if (Input.GetKeyDown(ModKeybindingId))
            {
                if (!ShowUI)
                {
                    InitData();
                    EnableCursor(true);
                }
                ToggleShowUI();
                if (!ShowUI)
                    EnableCursor(false);
            }
        }

        #endregion
    }
}
