using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using MelonLoader.Utils;
using ScheduleOne.UI;
using ScheduleOne.UI.Phone;
using ScheduleOne.DevUtilities;
using ScheduleOne.Storage;
using ScheduleOne.Money;
using ScheduleOne.PlayerScripts;
using HarmonyLib;
using ScheduleOne.ItemFramework;
using ScheduleOne.Persistence;

namespace EasyUpgrades
{
    public class EasyUpgradesMain : MelonMod
    {
        // Static fields to track upgrades
        private static bool originalStackLimitsCaptured = false;
        public static int CurrentStackLimitUpgrades { get; private set; } = 0;
        public static int MaxStackLimitUpgrades { get; } = 4;

        public static int CurrentMaxWorkersUpgrade { get; private set; } = 0;
        public static int MaxWorkersUpgradeLimit { get; } = 3;
        private const string PREF_MAX_WORKERS = "MaxWorkersUpgrade";

        // We'll no longer use a global CurrentStackLimit.
        private const int UpgradeIncrement = 10;

        private const int ROWS = 4;
        private const int COLUMNS = 3;
        private const KeyCode TOGGLE_KEY = KeyCode.B;
        private StorageEntity backpackEntity;
        private bool backpackInitialized;
        private int backpackLevel = 0;

        // This field stores the current save identifier (derived from the save folder name)
        private string currentSaveOrganisation = "";

        // Constants for preference keys (do not include the category here)
        private const string PREF_BACKPACK_LEVEL = "BackpackLevel";
        private const string PREF_SAVE_ORGANISATION = "SaveOrganisation";

        // A default category to use before any save is loaded
        private const string DEFAULT_PREFS_CATEGORY = "EasyUpgrades_Default";

        // Static instance for access
        public static EasyUpgradesMain Instance { get; private set; }

        // This dictionary will record each item’s original stack limit.
        public static Dictionary<ItemDefinition, int> OriginalStackLimits = new Dictionary<ItemDefinition, int>();

        // Helper to get the preferences category for the current save.
        private string GetCategoryForCurrentSave()
        {
            if (!string.IsNullOrEmpty(currentSaveOrganisation))
                return $"EasyUpgrades_{currentSaveOrganisation}";
            return DEFAULT_PREFS_CATEGORY;
        }

        // Provide a static helper so other classes (like UpgradesApp) can retrieve the current category.
        public static string GetCurrentCategory()
        {
            return Instance?.GetCategoryForCurrentSave() ?? DEFAULT_PREFS_CATEGORY;
        }

        // Flag to indicate that per-save preferences have been loaded.
        private bool preferencesLoaded = false;

        public override void OnInitializeMelon()
        {
            Instance = this;
            MelonLogger.Msg("EasyUpgrades Mod loaded.");

            // Create a default category as fallback
            MelonPreferences.CreateCategory(DEFAULT_PREFS_CATEGORY);
            MelonPreferences.CreateEntry(DEFAULT_PREFS_CATEGORY, PREF_BACKPACK_LEVEL, 0, "Backpack upgrade level");
            MelonPreferences.CreateEntry(DEFAULT_PREFS_CATEGORY, "StackLimitUpgrades", 0, "Number of stack limit upgrades");
            MelonPreferences.CreateEntry(DEFAULT_PREFS_CATEGORY, PREF_MAX_WORKERS, 0, "Max Workers Upgrade Level");
            MelonPreferences.CreateEntry(DEFAULT_PREFS_CATEGORY, PREF_SAVE_ORGANISATION, "", "Current save organisation name");

            // Load default values from the default category (will be replaced once a save is loaded)
            backpackLevel = MelonPreferences.GetEntryValue<int>(DEFAULT_PREFS_CATEGORY, PREF_BACKPACK_LEVEL);
            CurrentStackLimitUpgrades = MelonPreferences.GetEntryValue<int>(DEFAULT_PREFS_CATEGORY, "StackLimitUpgrades");
            CurrentMaxWorkersUpgrade = MelonPreferences.GetEntryValue<int>(DEFAULT_PREFS_CATEGORY, PREF_MAX_WORKERS);

            MelonLogger.Msg($"Loaded default preferences - Backpack Level: {backpackLevel}, Stack Limit Upgrades: {CurrentStackLimitUpgrades}, Employee Level: {CurrentMaxWorkersUpgrade}");
            MelonLogger.Msg("EasyUpgrades initialized.");
            MelonLogger.Error("Items in this backpack will be LOST when you exit the game or the save!");
        }

        public override void OnApplicationStart()
        {
            // Removed initial app instantiation to prevent duplicates.
            MelonCoroutines.Start(CheckCurrentSave());
        }

        private IEnumerator CheckCurrentSave()
        {
            // Wait until the LoadManager is ready and the game is loaded.
            yield return new WaitUntil(() => Singleton<LoadManager>.Instance != null && Singleton<LoadManager>.Instance.IsGameLoaded);
            // Wait until the LoadedGameFolderPath is non-empty.
            yield return new WaitUntil(() => !string.IsNullOrEmpty(Singleton<LoadManager>.Instance.LoadedGameFolderPath));

            // Use the loaded game folder path as the unique identifier.
            string savePath = Singleton<LoadManager>.Instance.LoadedGameFolderPath;
            string newOrg = Path.GetFileName(savePath);
            if (!string.IsNullOrEmpty(newOrg))
            {
                // Only update if the save has changed.
                if (newOrg != currentSaveOrganisation)
                {
                    currentSaveOrganisation = newOrg;
                    string category = GetCategoryForCurrentSave();

                    // Create the per-save category if it does not already exist.
                    MelonPreferences.CreateCategory(category);
                    if (!MelonPreferences.GetCategory(category).Entries.Any(e => e.Identifier == PREF_BACKPACK_LEVEL))
                    {
                        MelonPreferences.CreateEntry(category, PREF_BACKPACK_LEVEL, 0, "Backpack upgrade level");
                        MelonPreferences.CreateEntry(category, "StackLimitUpgrades", 0, "Number of stack limit upgrades");
                        MelonPreferences.CreateEntry(category, PREF_MAX_WORKERS, 0, "Max Workers Upgrade Level");
                    }

                    // Load upgrade settings for this save.
                    backpackLevel = MelonPreferences.GetEntryValue<int>(category, PREF_BACKPACK_LEVEL);
                    CurrentStackLimitUpgrades = MelonPreferences.GetEntryValue<int>(category, "StackLimitUpgrades");
                    CurrentMaxWorkersUpgrade = MelonPreferences.GetEntryValue<int>(category, PREF_MAX_WORKERS);

                    MelonLogger.Msg($"Loaded preferences for save '{newOrg}' - Backpack Level: {backpackLevel}, " +
                                      $"Stack Limit Upgrades: {CurrentStackLimitUpgrades}, Employee Level: {CurrentMaxWorkersUpgrade}");

                    // Immediately re-run the stack limit patch so that item definitions update to the new value.
                    MelonCoroutines.Start(RetryStackLimitPatch());

                    // Force a UI refresh: if an UpgradesApp already exists, destroy it and re-instantiate it.
                    GameObject existingApp = GameObject.Find("UpgradesApp");
                    if (existingApp != null)
                    {
                        UnityEngine.Object.Destroy(existingApp);
                        MelonCoroutines.Start(SetupUpgradesApp());
                    }
                }
            }
            else
            {
                MelonLogger.Warning("Could not determine save organisation from the loaded game folder path.");
            }
            preferencesLoaded = true;
        }


        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            MelonLogger.Msg($"Scene initialized: {sceneName}");

            if (sceneName != ScheduleOne.Persistence.SaveManager.MENU_SCENE_NAME)
            {
                MelonCoroutines.Start(CheckCurrentSave());
                if (sceneName != "Main")
                {
                    MelonLogger.Msg("Non-Main scene detected, reinitializing Upgrades App.");
                    MelonCoroutines.Start(SetupUpgradesApp());
                }
            }
            else
            {
                MelonLogger.Msg("Main menu scene detected, skipping save check and app setup.");
            }

            if (sceneName == "Main")
            {
                MelonCoroutines.Start(MainSceneInit());
            }
        }


        private IEnumerator MainSceneInit()
        {
            yield return new WaitUntil(() => preferencesLoaded);

            yield return RetryStackLimitPatch();
            yield return UpdateEmployeeCapacitiesWhenReady();

            if (backpackLevel > 0)
            {
                MelonLogger.Msg($"Player has backpack level {backpackLevel}, setting up backpack...");
                yield return SetupBackpack();
            }
            else
            {
                MelonLogger.Msg("Player hasn't purchased backpack yet");
            }

            // Instantiate the Upgrades App (only once per scene load)
            MelonCoroutines.Start(SetupUpgradesApp());
        }

        private IEnumerator UpdateEmployeeCapacitiesWhenReady()
        {
            while (PlayerSingleton<AppsCanvas>.Instance == null)
                yield return null;
            UpdateEmployeeCapacities();
            yield break;
        }

        private IEnumerator RetryStackLimitPatch()
        {
            while (PlayerSingleton<AppsCanvas>.Instance == null)
                yield return null;

            var itemDefinitions = Resources.FindObjectsOfTypeAll<ItemDefinition>();
            if (itemDefinitions.Length == 0)
            {
                MelonLogger.Error("No item definitions found!");
                yield break;
            }

            foreach (var def in itemDefinitions)
            {
                try
                {
                    var stackLimitField = typeof(ItemDefinition).GetField("StackLimit",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (stackLimitField != null)
                    {
                        // Capture the base (original) value only once.
                        if (!originalStackLimitsCaptured && !OriginalStackLimits.ContainsKey(def))
                        {
                            int orig = (int)stackLimitField.GetValue(def);
                            OriginalStackLimits[def] = orig;
                        }
                        int baseLimit = OriginalStackLimits.ContainsKey(def) ? OriginalStackLimits[def] : (int)stackLimitField.GetValue(def);
                        int newLimit = baseLimit + (CurrentStackLimitUpgrades * UpgradeIncrement);
                        stackLimitField.SetValue(def, newLimit);
                        MelonLogger.Msg($"Updated {def.Name} stack limit to {newLimit}");
                    }
                    else
                    {
                        MelonLogger.Error($"Could not find StackLimit field for {def.Name}");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error updating stack limit for {def.Name}: {ex.Message}");
                }
            }
            originalStackLimitsCaptured = true;

            // Patch the getter as a backup.
            var methods = typeof(ItemDefinition).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            var originalMethod = methods.FirstOrDefault(m => m.Name == "get_StackLimit" && m.ReturnType == typeof(int));
            if (originalMethod != null)
            {
                HarmonyInstance.Patch(
                    originalMethod,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(EasyUpgradesMain), nameof(StackLimitPatch)))
                );
                MelonLogger.Msg($"Successfully patched method: {originalMethod.Name}");
            }
            else
            {
                MelonLogger.Error("Could not find StackLimit method after direct modification");
            }
            yield break;
        }


        private static void StackLimitPatch(ItemDefinition __instance, ref int __result)
        {
            if (OriginalStackLimits.TryGetValue(__instance, out int baseLimit))
            {
                __result = baseLimit + (CurrentStackLimitUpgrades * UpgradeIncrement);
            }
        }

        public void UpgradeStackLimit()
        {
            string category = GetCategoryForCurrentSave();
            if (CurrentStackLimitUpgrades < MaxStackLimitUpgrades)
            {
                CurrentStackLimitUpgrades++;
                MelonLogger.Msg($"Stack Limit Upgraded. New upgrade level: {CurrentStackLimitUpgrades}/{MaxStackLimitUpgrades}");
                MelonPreferences.SetEntryValue(category, "StackLimitUpgrades", CurrentStackLimitUpgrades);
                MelonPreferences.Save();
                MelonCoroutines.Start(RetryStackLimitPatch());
            }
            else
            {
                MelonLogger.Msg("Maximum stack limit upgrades reached!");
            }
        }

        public void UpdateEmployeeCapacities()
        {
            int bonus = CurrentMaxWorkersUpgrade * 3;
            // (Update employee capacities for properties as needed)
        }

        public void UpgradeMaxWorkers()
        {
            string category = GetCategoryForCurrentSave();
            if (CurrentMaxWorkersUpgrade < MaxWorkersUpgradeLimit)
            {
                CurrentMaxWorkersUpgrade++;
                MelonLogger.Msg($"Max Workers Upgrade increased to {CurrentMaxWorkersUpgrade}/{MaxWorkersUpgradeLimit}");
                MelonPreferences.SetEntryValue(category, PREF_MAX_WORKERS, CurrentMaxWorkersUpgrade);
                MelonPreferences.Save();
                UpdateEmployeeCapacities();
            }
            else
            {
                MelonLogger.Msg("Max Workers Upgrade already at maximum!");
            }
        }

        private IEnumerator SetupBackpack()
        {
            MelonLogger.Msg("Setting up backpack...");
            // Wait until the AppsCanvas singleton is available.
            while (PlayerSingleton<AppsCanvas>.Instance == null)
                yield return null;

            // Once AppsCanvas is available, assume the player is fully loaded.
            GameObject playerObj = GameObject.Find("Player");
            if (playerObj == null)
            {
                MelonLogger.Error("Could not find Player GameObject!");
                yield break;
            }

            MelonLogger.Msg("Found Player, looking for existing storage entities to clone...");
            StorageEntity[] existingEntities = UnityEngine.Object.FindObjectsOfType<StorageEntity>();
            if (existingEntities.Length == 0)
            {
                MelonLogger.Error("No existing StorageEntity objects found in the scene!");
                yield break;
            }

            MelonLogger.Msg($"Found {existingEntities.Length} StorageEntity objects");
            StorageEntity templateEntity = existingEntities.FirstOrDefault(entity => entity.name.Contains("Shitbox_Police"));
            if (templateEntity == null)
            {
                templateEntity = existingEntities.FirstOrDefault(entity =>
                    entity.ItemSlots != null && entity.ItemSlots.Count > 0 &&
                    entity.GetComponent<FishNet.Object.NetworkObject>() != null);
                MelonLogger.Msg($"Using template entity: {templateEntity?.name}");
            }

            if (templateEntity == null)
            {
                MelonLogger.Error("Could not find a suitable template entity!");
                yield break;
            }

            GameObject backpackObj = UnityEngine.Object.Instantiate(templateEntity.gameObject, playerObj.transform);
            backpackObj.name = "PlayerBackpack";
            backpackObj.transform.localPosition = Vector3.zero;
            backpackEntity = backpackObj.GetComponent<StorageEntity>();

            if (backpackEntity != null)
            {
                backpackEntity.StorageEntityName = "Backpack";
                backpackEntity.StorageEntitySubtitle = "Items will be LOST when you log out, if left in backpack!";
                backpackEntity.MaxAccessDistance = 0f;
                backpackEntity.EmptyOnSleep = true;
                backpackEntity.ClearContents();

                MelonLogger.Msg($"Backpack created with {backpackEntity.ItemSlots.Count} slots (level {backpackLevel})");
                backpackInitialized = true;
                MelonLogger.Msg("Backpack setup complete!");
            }
            else
            {
                MelonLogger.Error("Failed to get StorageEntity component from the cloned object!");
                UnityEngine.Object.Destroy(backpackObj);
            }
        }
        public override void OnUpdate()
        {
            if (backpackLevel > 0 && backpackInitialized && backpackEntity != null)
            {
                if (Input.GetKeyDown(TOGGLE_KEY))
                {
                    MelonLogger.Msg("B key pressed, toggling backpack...");
                    try
                    {
                        var storageMenu = UnityEngine.Object.FindObjectOfType<ScheduleOne.UI.StorageMenu>();
                        if (storageMenu != null)
                        {
                            if (backpackEntity.IsOpened)
                            {
                                MelonLogger.Msg("Closing backpack via StorageMenu");
                                storageMenu.CloseMenu();
                            }
                            else
                            {
                                MelonLogger.Msg("Opening backpack via StorageMenu");
                                storageMenu.Open(backpackEntity);
                            }
                        }
                        else
                        {
                            MelonLogger.Error("StorageMenu not found, cannot toggle backpack");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error toggling backpack: {ex.Message}");
                        MelonLogger.Error(ex.StackTrace);
                    }
                }
            }
        }

        public void UpgradeBackpack(int level)
        {
            MelonLogger.Msg($"Upgrade requested to level {level}");
            backpackLevel = level;
            string category = GetCategoryForCurrentSave();
            MelonPreferences.SetEntryValue(category, PREF_BACKPACK_LEVEL, backpackLevel);
            MelonPreferences.Save();
            MelonLogger.Msg($"Saved backpack level {backpackLevel} to preferences");
            if (level == 1 && (backpackEntity == null || !backpackInitialized))
            {
                MelonLogger.Msg("First backpack upgrade - creating backpack");
                MelonCoroutines.Start(SetupBackpack());
            }
        }

        private IEnumerator SetupUpgradesApp()
        {
            // Wait until HomeScreen and preferences are ready.
            while (PlayerSingleton<HomeScreen>.Instance == null)
                yield return null;
            while (!preferencesLoaded)
                yield return null;

            // Always destroy the existing UpgradesApp UI if it exists.
            GameObject existingApp = GameObject.Find("UpgradesApp");
            if (existingApp != null)
            {
                MelonLogger.Msg("Destroying existing UpgradesApp.");
                UnityEngine.Object.Destroy(existingApp);
                yield return null; // wait one frame for proper destruction
            }

            // Also remove the associated icon.
            // Option 1: If you have renamed your icon (e.g. "EasyUpgradesAppIcon"), use that:
            GameObject existingIcon = GameObject.Find("EasyUpgradesAppIcon");
            if (existingIcon != null)
            {
                MelonLogger.Msg("Destroying existing EasyUpgradesAppIcon.");
                UnityEngine.Object.Destroy(existingIcon);
                yield return null;
            }
            else
            {
                // Option 2: If your icon is always at a specific index in the AppIcons container, remove that.
                GameObject appIconsContainer = GameObject.Find("Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/HomeScreen/AppIcons");
                if (appIconsContainer != null && appIconsContainer.transform.childCount > 7)
                {
                    Transform iconToRemove = appIconsContainer.transform.GetChild(7);
                    if (iconToRemove != null)
                    {
                        MelonLogger.Msg("Removing the icon at index 7 from AppIcons container.");
                        UnityEngine.Object.Destroy(iconToRemove.gameObject);
                        yield return null;
                    }
                }
            }

            GameObject appsCanvas = GameObject.Find("Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/AppsCanvas");
            if (appsCanvas == null)
            {
                MelonLogger.Error("AppsCanvas not found!");
                yield break;
            }
            GameObject upgradesAppGO = new GameObject("UpgradesApp");
            upgradesAppGO.transform.SetParent(appsCanvas.transform, false);
            RectTransform upgradesRT = upgradesAppGO.AddComponent<RectTransform>();
            upgradesRT.anchorMin = new Vector2(0, 0);
            upgradesRT.anchorMax = new Vector2(1, 1);
            upgradesRT.offsetMin = Vector2.zero;
            upgradesRT.offsetMax = Vector2.zero;
            upgradesAppGO.AddComponent<UpgradesApp>();
            MelonLogger.Msg("Upgrades App instantiated.");
            yield break;
        }
    }

    public class UpgradeItem
    {
        public string Name;
        public int Price;
        public Color ColorTint; // Placeholder

        public UpgradeItem(string name, int price, Color color)
        {
            Name = name;
            Price = price;
            ColorTint = color;
        }
    }

    public class UpgradesApp : App<UpgradesApp>
    {
        private List<UpgradeItem> upgrades = new List<UpgradeItem>()
        {
            new UpgradeItem("BackPack (0/1)", 100, Color.green),
            new UpgradeItem("Stack Size (0/4)", 100, Color.yellow),
            new UpgradeItem("Vehicle Storage (0/2)", 100, Color.magenta),
            new UpgradeItem("Test", 100, Color.cyan),
            new UpgradeItem("Max Workers", 100, Color.red),
            new UpgradeItem("Test", 100, new Color(1f, 0.65f, 0f, 1f)),
            new UpgradeItem("Test", 100, new Color(0.3f, 0.8f, 0.3f, 1f)),
            new UpgradeItem("Test", 100, Color.white),
            new UpgradeItem("Test", 100, new Color(0.2f, 0.6f, 1f, 1f)),
            new UpgradeItem("Test", 100, new Color(0.7f, 0.4f, 0.1f, 1f))
        };

        private RectTransform _containerRT;

        private bool TrySpendMoney(int amount)
        {
            try
            {
                var moneyManager = NetworkSingleton<MoneyManager>.Instance;
                float currentOnlineBalance = moneyManager.sync___get_value_onlineBalance();
                if (currentOnlineBalance >= amount)
                {
                    moneyManager.CreateOnlineTransaction("Upgrade Purchase", -amount, 1, "Upgrade Purchase");
                    return true;
                }
                var playerInventory = PlayerSingleton<PlayerInventory>.Instance;
                if (playerInventory.cashInstance.Balance >= amount)
                {
                    moneyManager.ChangeCashBalance(-amount);
                    return true;
                }
                MelonLogger.Msg("Not enough money for purchase!");
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking/spending money: {ex.Message}");
                return false;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            GameObject container = new GameObject("Container");
            container.transform.SetParent(this.transform, false);
            _containerRT = container.AddComponent<RectTransform>();
            _containerRT.anchorMin = new Vector2(0, 0);
            _containerRT.anchorMax = new Vector2(1, 1);
            _containerRT.offsetMin = Vector2.zero;
            _containerRT.offsetMax = Vector2.zero;
            container.AddComponent<CanvasGroup>();

            Image bg = container.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            GameObject topbar = new GameObject("Topbar");
            topbar.transform.SetParent(container.transform, false);
            RectTransform topbarRT = topbar.AddComponent<RectTransform>();
            topbarRT.anchorMin = new Vector2(0, 1);
            topbarRT.anchorMax = new Vector2(1, 1);
            topbarRT.pivot = new Vector2(0.5f, 1);
            topbarRT.sizeDelta = new Vector2(0, 60);
            Image topbarImage = topbar.AddComponent<Image>();
            topbarImage.color = new Color(0.4f, 0f, 0.4f, 1f);

            GameObject title = new GameObject("Title");
            title.transform.SetParent(topbar.transform, false);
            RectTransform titleRT = title.AddComponent<RectTransform>();
            titleRT.anchorMin = Vector2.zero;
            titleRT.anchorMax = Vector2.one;
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;
            TextMeshProUGUI titleText = title.AddComponent<TextMeshProUGUI>();
            titleText.text = "Upgrades";
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontSize = 42;
            titleText.color = Color.white;

            container.SetActive(false);
            this.appContainer = _containerRT;
        }

        protected override void Start()
        {
            base.Start();
            bool wasActive = this.appContainer.gameObject.activeSelf;
            this.appContainer.gameObject.SetActive(true);

            // Use per-save preferences category.
            string category = EasyUpgradesMain.GetCurrentCategory();
            int backpackLevel = MelonPreferences.GetEntryValue<int>(category, "BackpackLevel");
            int stackLimitUpgrades = MelonPreferences.GetEntryValue<int>(category, "StackLimitUpgrades");
            if (backpackLevel > 0 && upgrades.Count > 0)
            {
                upgrades[0].Name = $"BackPack ({backpackLevel}/1)";
            }
            upgrades[1].Name = $"Stack Size ({stackLimitUpgrades}/4)";

            int workersUpgrades = MelonPreferences.GetEntryValue<int>(category, "MaxWorkersUpgrade");
            if (workersUpgrades > 0 && upgrades.Count > 4)
            {
                upgrades[4].Name = $"Max Workers";
            }

            GameObject gridContainer = new GameObject("GridContainer");
            gridContainer.transform.SetParent(this.appContainer, false);
            RectTransform gridContainerRT = gridContainer.AddComponent<RectTransform>();
            gridContainerRT.anchorMin = new Vector2(0, 0);
            gridContainerRT.anchorMax = new Vector2(1, 0.9f);
            gridContainerRT.offsetMin = new Vector2(20, 20);
            gridContainerRT.offsetMax = new Vector2(-20, -20);

            GridLayoutGroup grid = gridContainer.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(170, 180);
            grid.spacing = new Vector2(15, 30);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;

            int maxItems = Math.Min(10, upgrades.Count);
            for (int i = 0; i < maxItems; i++)
            {
                CreateItemCard(upgrades[i], gridContainer.transform);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(gridContainerRT);
            MelonLogger.Msg($"[Start] Container size: {this.appContainer.rect.size}");
            this.appContainer.gameObject.SetActive(wasActive);
        }

        public override void OnStartClient(bool IsOwner)
        {
            if (!IsOwner) return;
            AppName = "Upgrades";
            IconLabel = "Upgrades";
            Orientation = EOrientation.Horizontal;

            string modFolder = Path.Combine(MelonEnvironment.ModsDirectory, "EasyUpgrades");
            string filePath = Path.Combine(modFolder, "appicon_upgrades.png");
            if (File.Exists(filePath))
            {
                try
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    Texture2D tex = new Texture2D(2, 2);
                    if (ImageConversion.LoadImage(tex, fileData))
                    {
                        AppIcon = Sprite.Create(
                            tex,
                            new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f)
                        );
                    }
                    else
                    {
                        MelonLogger.Warning("Failed to load image data for custom app icon.");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error("Exception loading custom app icon: " + ex);
                }
            }
            else
            {
                MelonLogger.Warning("Custom app icon file not found at: " + filePath);
            }
            base.OnStartClient(IsOwner);
        }

        private void CreateItemCard(UpgradeItem item, Transform parent)
        {
            GameObject card = new GameObject(item.Name + "Card");
            card.transform.SetParent(parent, false);
            Image cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);

            VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 15f;
            vlg.padding = new RectOffset(20, 20, 20, 20);

            GameObject iconContainer = new GameObject("IconContainer");
            iconContainer.transform.SetParent(card.transform, false);
            RectTransform iconContainerRT = iconContainer.AddComponent<RectTransform>();
            iconContainerRT.sizeDelta = new Vector2(120, 140);

            Image iconImg = iconContainer.AddComponent<Image>();
            iconImg.sprite = CreateCustomSprite(item.Name, item.ColorTint);
            iconImg.preserveAspect = true;

            GameObject nameGO = new GameObject("Name");
            nameGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.text = item.Name;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontSize = 18;
            nameText.color = Color.white;

            GameObject priceGO = new GameObject("Price");
            priceGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI priceText = priceGO.AddComponent<TextMeshProUGUI>();
            priceText.text = "$" + item.Price;
            priceText.alignment = TextAlignmentOptions.Center;
            priceText.fontSize = 18;
            priceText.color = new Color(0.2f, 0.8f, 0.2f, 1f);
            priceText.fontStyle = FontStyles.Bold;

            GameObject buyBtnContainer = new GameObject("BuyButtonContainer");
            buyBtnContainer.transform.SetParent(card.transform, false);
            RectTransform buyBtnContainerRT = buyBtnContainer.AddComponent<RectTransform>();
            buyBtnContainerRT.sizeDelta = new Vector2(70, 20);
            LayoutElement containerElement = buyBtnContainer.AddComponent<LayoutElement>();
            containerElement.preferredHeight = 25;

            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(card.transform, false);
            spacer.transform.SetSiblingIndex(buyBtnContainer.transform.GetSiblingIndex());
            LayoutElement spacerElement = spacer.AddComponent<LayoutElement>();
            spacerElement.preferredHeight = 10;

            GameObject buyBtnGO = new GameObject("BuyButton");
            buyBtnGO.transform.SetParent(buyBtnContainer.transform, false);
            RectTransform buyBtnRT = buyBtnGO.AddComponent<RectTransform>();
            buyBtnRT.anchorMin = new Vector2(0, 0);
            buyBtnRT.anchorMax = new Vector2(1, 1);
            buyBtnRT.offsetMin = Vector2.zero;
            buyBtnRT.offsetMax = Vector2.zero;
            buyBtnRT.sizeDelta = new Vector2(40, 20);

            Image btnImg = buyBtnGO.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.8f, 0.2f, 1f);
            btnImg.sprite = CreateRoundedRectSprite(12);

            Button buyBtn = buyBtnGO.AddComponent<Button>();
            ColorBlock colors = buyBtn.colors;
            colors.normalColor = new Color(0.2f, 0.8f, 0.2f, 1f);
            colors.highlightedColor = new Color(0.3f, 0.9f, 0.3f, 1f);
            colors.pressedColor = new Color(0.1f, 0.7f, 0.1f, 1f);
            colors.disabledColor = new Color(0.2f, 0.8f, 0.2f, 1f);
            buyBtn.colors = colors;

            GameObject btnTextGO = new GameObject("Text");
            btnTextGO.transform.SetParent(buyBtnGO.transform, false);
            RectTransform btnTextRT = btnTextGO.AddComponent<RectTransform>();
            btnTextRT.anchorMin = Vector2.zero;
            btnTextRT.anchorMax = Vector2.one;
            btnTextRT.offsetMin = Vector2.zero;
            btnTextRT.offsetMax = Vector2.zero;
            TextMeshProUGUI btnText = btnTextGO.AddComponent<TextMeshProUGUI>();
            btnText.text = "Buy";
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.fontSize = 12;
            btnText.color = Color.white;
            btnText.fontStyle = FontStyles.Bold;

            buyBtn.onClick.AddListener(() =>
            {
                if (item.Name.StartsWith("BackPack"))
                {
                    HandleBackpackUpgrade(item, nameText, btnImg, btnText, buyBtn);
                }
                else if (item.Name.StartsWith("Stack Size"))
                {
                    HandleStackSizeUpgrade(item, nameText, btnImg, btnText, buyBtn);
                }
                else if (item.Name.StartsWith("Max Workers"))
                {
                    int currentWorkersUpgrade = EasyUpgradesMain.CurrentMaxWorkersUpgrade;
                    if (currentWorkersUpgrade < EasyUpgradesMain.MaxWorkersUpgradeLimit)
                    {
                        if (TrySpendMoney(item.Price))
                        {
                            EasyUpgradesMain.Instance.UpgradeMaxWorkers();
                            nameText.text = $"Max Workers";
                            if (currentWorkersUpgrade + 1 >= EasyUpgradesMain.MaxWorkersUpgradeLimit)
                            {
                                btnImg.color = Color.red;
                                btnText.text = "Max";
                                buyBtn.interactable = false;
                            }
                        }
                        else
                        {
                            MelonLogger.Msg("Cannot afford Max Workers upgrade!");
                        }
                    }
                    else
                    {
                        MelonLogger.Msg("Max Workers upgrade already at max!");
                        btnImg.color = Color.red;
                        btnText.text = "Max";
                        buyBtn.interactable = false;
                    }
                }
                else
                {
                    HandleDefaultUpgrade(item);
                }
            });

            ApplyMaxUpgradeState(item, nameText, btnImg, btnText, buyBtn);
            MelonLogger.Msg("Created card for item: " + item.Name);
        }

        private void ApplyMaxUpgradeState(UpgradeItem item, TextMeshProUGUI nameText, Image btnImg, TextMeshProUGUI btnText, Button buyBtn)
        {
            string category = EasyUpgradesMain.GetCurrentCategory();
            if (item.Name.StartsWith("BackPack"))
            {
                int backpackLevel = MelonPreferences.GetEntryValue<int>(category, "BackpackLevel");
                if (backpackLevel >= 1)
                {
                    nameText.text = "BackPack (1/1)";
                    btnImg.color = Color.red;
                    btnText.text = "Max";
                    buyBtn.interactable = false;
                }
            }
            else if (item.Name.StartsWith("Stack Size"))
            {
                int stackLimitUpgrades = MelonPreferences.GetEntryValue<int>(category, "StackLimitUpgrades");
                if (stackLimitUpgrades >= 4)
                {
                    nameText.text = $"Stack Size ({stackLimitUpgrades}/4)";
                    btnImg.color = Color.red;
                    btnText.text = "Max";
                    buyBtn.interactable = false;
                }
            }
            else if (item.Name.StartsWith("Max Workers"))
            {
                int workersUpgrades = MelonPreferences.GetEntryValue<int>(category, "MaxWorkersUpgrade");
                if (workersUpgrades >= EasyUpgradesMain.MaxWorkersUpgradeLimit)
                {
                    nameText.text = $"Max Workers";
                    btnImg.color = Color.red;
                    btnText.text = "Max";
                    buyBtn.interactable = false;
                }
            }
        }

        private void HandleBackpackUpgrade(UpgradeItem item, TextMeshProUGUI nameText, Image btnImg, TextMeshProUGUI btnText, Button buyBtn)
        {
            string levelStr = item.Name.Substring(item.Name.IndexOf("(") + 1, 1);
            int currentLevel;
            if (int.TryParse(levelStr, out currentLevel))
            {
                if (currentLevel < 1)
                {
                    if (TrySpendMoney(item.Price))
                    {
                        EasyUpgradesMain.Instance.UpgradeBackpack(1);
                        nameText.text = "BackPack (1/1)";
                        btnImg.color = Color.red;
                        btnText.text = "Max";
                        buyBtn.interactable = false;
                    }
                    else
                    {
                        MelonLogger.Msg("Cannot afford backpack upgrade!");
                    }
                }
                else
                {
                    MelonLogger.Msg("Backpack already at max level!");
                    btnImg.color = Color.red;
                    btnText.text = "Max";
                    buyBtn.interactable = false;
                }
            }
            else
            {
                MelonLogger.Error("Failed to parse backpack level from name!");
            }
        }

        private void HandleStackSizeUpgrade(UpgradeItem item, TextMeshProUGUI nameText, Image btnImg, TextMeshProUGUI btnText, Button buyBtn)
        {
            int currentUpgrades = EasyUpgradesMain.CurrentStackLimitUpgrades;
            if (currentUpgrades < EasyUpgradesMain.MaxStackLimitUpgrades)
            {
                if (TrySpendMoney(item.Price))
                {
                    EasyUpgradesMain.Instance.UpgradeStackLimit();
                    nameText.text = $"Stack Size ({currentUpgrades + 1}/{EasyUpgradesMain.MaxStackLimitUpgrades})";
                    if (currentUpgrades + 1 >= EasyUpgradesMain.MaxStackLimitUpgrades)
                    {
                        btnImg.color = Color.red;
                        btnText.text = "Max";
                        buyBtn.interactable = false;
                    }
                }
                else
                {
                    MelonLogger.Msg("Cannot afford stack size upgrade!");
                }
            }
            else
            {
                MelonLogger.Msg("Stack size already at max level!");
                btnImg.color = Color.red;
                btnText.text = "Max";
                buyBtn.interactable = false;
            }
        }

        private void HandleDefaultUpgrade(UpgradeItem item)
        {
            if (TrySpendMoney(item.Price))
            {
                MelonLogger.Msg($"Bought {item.Name} for ${item.Price}");
            }
            else
            {
                MelonLogger.Msg($"Cannot afford {item.Name}!");
            }
        }

        private Sprite CreateCustomSprite(string itemName, Color fallbackColor)
        {
            try
            {
                string modFolder = Path.Combine(MelonEnvironment.ModsDirectory, "EasyUpgrades");
                string filePath = "";
                if (itemName.StartsWith("BackPack"))
                {
                    filePath = Path.Combine(modFolder, "upgrade_backpack.png");
                }
                else if (itemName.StartsWith("Stack Size"))
                {
                    filePath = Path.Combine(modFolder, "upgrade_stacksize.png");
                }
                else if (itemName.StartsWith("Max Workers"))
                {
                    filePath = Path.Combine(modFolder, "upgrade_employee.png");
                }
                if (File.Exists(filePath))
                {
                    byte[] fileData = File.ReadAllBytes(filePath);
                    Texture2D tex = new Texture2D(2, 2);
                    if (ImageConversion.LoadImage(tex, fileData))
                    {
                        return Sprite.Create(
                            tex,
                            new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f)
                        );
                    }
                    else
                    {
                        MelonLogger.Warning($"Failed to load image data for {itemName} icon.");
                    }
                }
                else
                {
                    MelonLogger.Warning($"Custom icon file not found at: {filePath}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Exception loading custom {itemName} icon: {ex}");
            }
            return CreatePlaceholderSprite(fallbackColor);
        }

        private Sprite CreatePlaceholderSprite(Color color)
        {
            int size = 96;
            Texture2D tex = new Texture2D(size, size);
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    tex.SetPixel(x, y, color);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private Sprite CreateRoundedRectSprite(float radius)
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size);
            Color[] colors = new Color[size * size];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.clear;
            }
            tex.SetPixels(colors);
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float nx = (float)x / size;
                    float ny = (float)y / size;
                    if (IsInsideRoundedRect(nx, ny, radius / size))
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        private bool IsInsideRoundedRect(float x, float y, float cornerRadius)
        {
            float cx = Math.Abs(x - 0.5f) * 2;
            float cy = Math.Abs(y - 0.5f) * 2;
            if (cx <= 1 - 2 * cornerRadius && cy <= 1)
                return true;
            if (cy <= 1 - 2 * cornerRadius && cx <= 1)
                return true;
            if (cx > 1 - 2 * cornerRadius && cy > 1 - 2 * cornerRadius)
            {
                float dx = cx - (1 - 2 * cornerRadius);
                float dy = cy - (1 - 2 * cornerRadius);
                return (dx * dx + dy * dy) <= 4 * cornerRadius * cornerRadius;
            }
            return false;
        }
    }
}
