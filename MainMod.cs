using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
using System.Linq;

namespace EasyUpgrades
{
    public class EasyUpgradesMain : MelonMod
    {

        // Static fields to track stack limit upgrades
        public static int CurrentStackLimit { get; private set; } = 20; // Starting value
        public static int MaxStackLimitUpgrades { get; } = 3; // Maximum number of upgrades
        public static int CurrentStackLimitUpgrades { get; private set; } = 0;

        // Backpack constants and variables
        private const int ROWS = 4;
        private const int COLUMNS = 3;
        private const KeyCode TOGGLE_KEY = KeyCode.B;
        private StorageEntity backpackEntity;
        private bool backpackInitialized;
        private int backpackLevel = 0;
        private string currentSaveOrganisation = "";

        // MelonPrefs categories and entries
        private const string PREFS_CATEGORY = "EasyUpgrades";
        private const string PREF_BACKPACK_LEVEL = "BackpackLevel";
        private const string PREF_SAVE_ORGANISATION = "SaveOrganisation";

        // Static instance for other methods to access
        public static EasyUpgradesMain Instance { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;
            MelonLogger.Msg("EasyUpgrades Mod loaded.");

            // Register MelonPrefs
            MelonPreferences.CreateCategory(PREFS_CATEGORY);
            MelonPreferences.CreateEntry(PREFS_CATEGORY, PREF_BACKPACK_LEVEL, 0, "Backpack upgrade level");
            // Add a new entry for stack limit upgrades
            MelonPreferences.CreateEntry(PREFS_CATEGORY, "StackLimitUpgrades", 0, "Number of stack limit upgrades");
            MelonPreferences.CreateEntry(PREFS_CATEGORY, PREF_SAVE_ORGANISATION, "", "Current save organisation name");

            // Load preferences
            backpackLevel = MelonPreferences.GetEntryValue<int>(PREFS_CATEGORY, PREF_BACKPACK_LEVEL);
            currentSaveOrganisation = MelonPreferences.GetEntryValue<string>(PREFS_CATEGORY, PREF_SAVE_ORGANISATION);

            // Load stack limit upgrades
            CurrentStackLimitUpgrades = MelonPreferences.GetEntryValue<int>(PREFS_CATEGORY, "StackLimitUpgrades");

            // Set the current stack limit based on saved upgrades
            CurrentStackLimit = 20 + (CurrentStackLimitUpgrades * 10);

            MelonLogger.Msg($"Loaded preferences - Backpack Level: {backpackLevel}, Stack Limit Upgrades: {CurrentStackLimitUpgrades}, Save: {currentSaveOrganisation}");
            MelonLogger.Msg("BackpackMod initialized. Thank you to Tugakit for the code reference.");
            MelonLogger.Error("Items in this backpack will be LOST when you exit the game or the save!");
        }

        public override void OnApplicationStart()
        {
            MelonCoroutines.Start(SetupUpgradesApp());

            // Find current save organization
            MelonCoroutines.Start(CheckCurrentSave());
        }

        private IEnumerator CheckCurrentSave()
        {
            // Wait a bit for the game to load
            yield return new WaitForSeconds(5f);

            GameObject saveInfoObj = GameObject.Find("SaveInfo");
            if (saveInfoObj != null)
            {

                var component = saveInfoObj.GetComponent<MonoBehaviour>();
                if (component != null)
                {

                    var field = component.GetType().GetField("OrganisationName");
                    if (field != null)
                    {
                        string newOrg = (string)field.GetValue(component);
                        MelonLogger.Msg($"Current save organisation: {newOrg}");

                        // Update current organization
                        currentSaveOrganisation = newOrg;
                        MelonPreferences.SetEntryValue(PREFS_CATEGORY, PREF_SAVE_ORGANISATION, currentSaveOrganisation);
                        MelonPreferences.Save();
                    }
                }
            }
            else
            {
                MelonLogger.Warning("Couldn't find SaveInfo to detect current save");
            }
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            MelonLogger.Msg($"Scene initialized: {sceneName}");

            if (sceneName == "Main")
            {
                try
                {
                    // Start a coroutine to attempt patching after a delay
                    MelonCoroutines.Start(RetryStackLimitPatch());
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"Error initiating stack limit patch: {ex.Message}");
                }

                // Existing backpack setup code
                if (backpackLevel > 0)
                {
                    MelonLogger.Msg($"Player has backpack level {backpackLevel}, setting up backpack...");
                    MelonCoroutines.Start(SetupBackpack());
                }
                else
                {
                    MelonLogger.Msg("Player hasn't purchased backpack yet");
                }
            }
        }

        private IEnumerator RetryStackLimitPatch()
        {
            // Wait 10 seconds before attempting to patch
            yield return new WaitForSeconds(10f);

            try
            {
                // Find all item definitions
                var itemDefinitions = Resources.FindObjectsOfTypeAll<ItemDefinition>();

                if (itemDefinitions.Length == 0)
                {
                    MelonLogger.Error("No item definitions found!");
                    yield break;
                }

                // Modify stack limit for all item definitions
                foreach (var def in itemDefinitions)
                {
                    try
                    {
                        // Use reflection to modify the StackLimit field
                        var stackLimitField = typeof(ItemDefinition).GetField("StackLimit",
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance);

                        if (stackLimitField != null)
                        {
                            stackLimitField.SetValue(def, CurrentStackLimit);
                            MelonLogger.Msg($"Updated {def.Name} stack limit to {CurrentStackLimit}");
                        }
                        else
                        {
                            MelonLogger.Error($"Could not find StackLimit field for {def.Name}");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"Error updating stack limit for {def?.Name ?? "Unknown"}: {ex.Message}");
                    }
                }

                // Attempt to patch the method as a backup
                var methods = typeof(ItemDefinition).GetMethods(
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Static
                );

                var originalMethod = methods.FirstOrDefault(m =>
                    m.Name == "get_StackLimit" &&
                    m.ReturnType == typeof(int)
                );

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
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in stack limit patch: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static void StackLimitPatch(ItemDefinition __instance, ref int __result)
        {
            // Ensure the result is set to the current stack limit
            __result = CurrentStackLimit;
        }

        public void UpgradeStackLimit()
        {
            if (CurrentStackLimitUpgrades < MaxStackLimitUpgrades)
            {
                CurrentStackLimit += 10;
                CurrentStackLimitUpgrades++;

                MelonLogger.Msg($"Stack Limit Upgraded to {CurrentStackLimit}. Upgrade level: {CurrentStackLimitUpgrades}/{MaxStackLimitUpgrades}");

                // Attempt to modify all existing item definitions
                var itemDefinitions = Resources.FindObjectsOfTypeAll<ItemDefinition>();
                foreach (var def in itemDefinitions)
                {
                    try
                    {
                        // Use reflection to modify the private field
                        var stackLimitField = typeof(ItemDefinition).GetField("StackLimit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

                        if (stackLimitField != null)
                        {
                            stackLimitField.SetValue(def, CurrentStackLimit);
                            MelonLogger.Msg($"Updated {def.Name} stack limit to {CurrentStackLimit}");
                            MelonPreferences.SetEntryValue(PREFS_CATEGORY, "StackLimitUpgrades", CurrentStackLimitUpgrades);
                            MelonPreferences.Save();
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
            }
            else
            {
                MelonLogger.Msg("Maximum stack limit upgrades reached!");
            }
        }

        private IEnumerator SetupBackpack()
        {
            MelonLogger.Msg("Setting up backpack...");

            // Wait for game to initialize
            yield return new WaitForSeconds(3f);

            // Find player
            GameObject playerObj = GameObject.Find("Player");
            if (playerObj == null)
            {
                MelonLogger.Error("Could not find Player GameObject!");
                yield break;
            }

            MelonLogger.Msg("Found Player, looking for existing storage entities to clone...");

            // Find existing storage entities
            StorageEntity[] existingEntities = UnityEngine.Object.FindObjectsOfType<StorageEntity>();
            if (existingEntities.Length == 0)
            {
                MelonLogger.Error("No existing StorageEntity objects found in the scene!");
                yield break;
            }

            MelonLogger.Msg($"Found {existingEntities.Length} StorageEntity objects");

            // Find a good template entity (like the Shitbox_Police)
            StorageEntity templateEntity = null;
            foreach (var entity in existingEntities)
            {
                if (entity.name.Contains("Shitbox_Police"))
                {
                    templateEntity = entity;
                    MelonLogger.Msg($"Found ideal template entity: {entity.name}");
                    break;
                }
            }

            // If we didn't find the specific one, use any working entity
            if (templateEntity == null)
            {
                foreach (var entity in existingEntities)
                {
                    if (entity.ItemSlots != null && entity.ItemSlots.Count > 0 &&
                        entity.GetComponent<FishNet.Object.NetworkObject>() != null)
                    {
                        templateEntity = entity;
                        MelonLogger.Msg($"Using template entity: {entity.name}");
                        break;
                    }
                }
            }

            if (templateEntity == null)
            {
                MelonLogger.Error("Could not find a suitable template entity!");
                yield break;
            }

            // Create our backpack by instantiating a copy of the template
            GameObject backpackObj = UnityEngine.Object.Instantiate(templateEntity.gameObject, playerObj.transform);
            backpackObj.name = "PlayerBackpack";
            backpackObj.transform.localPosition = Vector3.zero;

            // Get the storage entity component
            backpackEntity = backpackObj.GetComponent<StorageEntity>();

            // Configure it
            if (backpackEntity != null)
            {
                // Set properties
                backpackEntity.StorageEntityName = "Backpack";
                backpackEntity.StorageEntitySubtitle = "Items will be LOST when you log out, if left in backpack!";
                backpackEntity.MaxAccessDistance = 0f;
                backpackEntity.EmptyOnSleep = true;

                // Clear any contents
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
            // Only handle backpack toggling if the player has purchased it
            if (backpackLevel > 0 && backpackInitialized && backpackEntity != null)
            {
                if (Input.GetKeyDown(TOGGLE_KEY))
                {
                    MelonLogger.Msg("B key pressed, toggling backpack...");

                    try
                    {
                        // Use the storage menu directly
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

        // Method for upgrading backpack
        public void UpgradeBackpack(int level)
        {
            MelonLogger.Msg($"Upgrade requested to level {level}");

            // Update backpack level
            backpackLevel = level;

            // Save to preferences
            MelonPreferences.SetEntryValue(PREFS_CATEGORY, PREF_BACKPACK_LEVEL, backpackLevel);
            MelonPreferences.Save();

            MelonLogger.Msg($"Saved backpack level {backpackLevel} to preferences");

            // If this is the first upgrade, we need to create the backpack
            if (level == 1 && (backpackEntity == null || !backpackInitialized))
            {
                MelonLogger.Msg("First backpack upgrade - creating backpack");
                MelonCoroutines.Start(SetupBackpack());
            }
        }

        private IEnumerator SetupUpgradesApp()
        {
            // Wait until the HomeScreen is available.
            while (PlayerSingleton<HomeScreen>.Instance == null)
                yield return null;

            // Find the AppsCanvas
            GameObject appsCanvas = GameObject.Find("Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/AppsCanvas");
            if (appsCanvas == null)
            {
                MelonLogger.Error("AppsCanvas not found!");
                yield break;
            }

            // Create the UpgradesApp object as a child of AppsCanvas
            GameObject upgradesAppGO = new GameObject("UpgradesApp");
            upgradesAppGO.transform.SetParent(appsCanvas.transform, false);
            // Add a RectTransform and stretch it.
            RectTransform upgradesRT = upgradesAppGO.AddComponent<RectTransform>();
            upgradesRT.anchorMin = new Vector2(0, 0);
            upgradesRT.anchorMax = new Vector2(1, 1);
            upgradesRT.offsetMin = Vector2.zero;
            upgradesRT.offsetMax = Vector2.zero;
            upgradesAppGO.AddComponent<UpgradesApp>();

            MelonLogger.Msg("Upgrades App instantiated.");
        }
    }

    // Data container for an upgrade item.
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
            new UpgradeItem("Stack Size (0/2)", 100, Color.yellow),
            new UpgradeItem("Vehicle Storage (0/2)", 100, Color.magenta),
            new UpgradeItem("Sprint Speed (0/3)", 100, Color.cyan),
            new UpgradeItem("Max Workers (0/3)", 100, Color.red),
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
                // Get the MoneyManager instance
                var moneyManager = NetworkSingleton<MoneyManager>.Instance;

                // Check if the player has enough online balance
                float currentOnlineBalance = moneyManager.sync___get_value_onlineBalance();
                if (currentOnlineBalance >= amount)
                {
                    // Deduct the money via the game's transaction system
                    moneyManager.CreateOnlineTransaction("Upgrade Purchase", -amount, 1, "Upgrade Purchase");
                    return true;
                }

                // Optional: Check cash balance if online balance is insufficient
                var playerInventory = PlayerSingleton<PlayerInventory>.Instance;
                if (playerInventory.cashInstance.Balance >= amount)
                {
                    // Deduct from cash balance
                    moneyManager.ChangeCashBalance(-amount);
                    return true;
                }

                // Not enough money
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

            // Create the Container as a child of UpgradesApp.
            GameObject container = new GameObject("Container");
            container.transform.SetParent(this.transform, false);
            _containerRT = container.AddComponent<RectTransform>();
            _containerRT.anchorMin = new Vector2(0, 0);
            _containerRT.anchorMax = new Vector2(1, 1);
            _containerRT.offsetMin = Vector2.zero;
            _containerRT.offsetMax = Vector2.zero;

            container.AddComponent<CanvasGroup>();

            // Add a dark grey background.
            Image bg = container.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // Create Topbar.
            GameObject topbar = new GameObject("Topbar");
            topbar.transform.SetParent(container.transform, false);
            RectTransform topbarRT = topbar.AddComponent<RectTransform>();
            topbarRT.anchorMin = new Vector2(0, 1);
            topbarRT.anchorMax = new Vector2(1, 1);
            topbarRT.pivot = new Vector2(0.5f, 1);
            topbarRT.sizeDelta = new Vector2(0, 60);
            Image topbarImage = topbar.AddComponent<Image>();
            topbarImage.color = new Color(0.4f, 0f, 0.4f, 1f);

            // Create Topbar Title.
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

            // Assign container to the inherited appContainer field.
            this.appContainer = _containerRT;
        }

        protected override void Start()
        {
            base.Start();
            bool wasActive = this.appContainer.gameObject.activeSelf;
            this.appContainer.gameObject.SetActive(true);

            // Update backpack upgrade level based on saved data
            int backpackLevel = MelonPreferences.GetEntryValue<int>("EasyUpgrades", "BackpackLevel");
            int stackLimitUpgrades = MelonPreferences.GetEntryValue<int>("EasyUpgrades", "StackLimitUpgrades");
            if (backpackLevel > 0 && upgrades.Count > 0)
            {
                // Update the displayed backpack level
                upgrades[0].Name = $"BackPack ({backpackLevel}/1)";
            }

            if (stackLimitUpgrades >= 3 && upgrades.Count > 1)
            {
                // Update the displayed stack size level
                upgrades[1].Name = $"Stack Size ({stackLimitUpgrades}/3)";
            }

            // Create a Grid Layout
            GameObject gridContainer = new GameObject("GridContainer");
            gridContainer.transform.SetParent(this.appContainer, false);
            RectTransform gridContainerRT = gridContainer.AddComponent<RectTransform>();
            gridContainerRT.anchorMin = new Vector2(0, 0);
            gridContainerRT.anchorMax = new Vector2(1, 0.9f);
            gridContainerRT.offsetMin = new Vector2(20, 20);
            gridContainerRT.offsetMax = new Vector2(-20, -20);

            // Add a GridLayoutGroup to organize items into a grid
            GridLayoutGroup grid = gridContainer.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(170, 180);
            grid.spacing = new Vector2(15, 30);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5; // row count

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
            // Main card container
            GameObject card = new GameObject(item.Name + "Card");
            card.transform.SetParent(parent, false);

            // Add a background image with dark grey color
            Image cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.15f, 0.15f, 0.15f, 1f); // Darker than the app background

            // Use a vertical layout group to organize elements
            VerticalLayoutGroup vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(20, 20, 10, 20);

            GameObject iconContainer = new GameObject("IconContainer");
            iconContainer.transform.SetParent(card.transform, false);
            RectTransform iconContainerRT = iconContainer.AddComponent<RectTransform>();
            iconContainerRT.sizeDelta = new Vector2(120, 120);

            Image iconImg = iconContainer.AddComponent<Image>();
            iconImg.sprite = CreatePlaceholderSprite(item.ColorTint);
            iconImg.preserveAspect = true;

            GameObject nameGO = new GameObject("Name");
            nameGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.text = item.Name;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.fontSize = 18;
            nameText.color = Color.white; // White text on dark background

            GameObject priceGO = new GameObject("Price");
            priceGO.transform.SetParent(card.transform, false);
            TextMeshProUGUI priceText = priceGO.AddComponent<TextMeshProUGUI>();
            priceText.text = "$" + item.Price;
            priceText.alignment = TextAlignmentOptions.Center;
            priceText.fontSize = 18;
            priceText.color = new Color(0.2f, 0.8f, 0.2f, 1f); // green color
            priceText.fontStyle = FontStyles.Bold; // Make price bold

            GameObject buyBtnContainer = new GameObject("BuyButtonContainer");
            buyBtnContainer.transform.SetParent(card.transform, false);
            RectTransform buyBtnContainerRT = buyBtnContainer.AddComponent<RectTransform>();
            buyBtnContainerRT.sizeDelta = new Vector2(70, 20); // Smaller container

            LayoutElement containerElement = buyBtnContainer.AddComponent<LayoutElement>();
            containerElement.preferredHeight = 25;

            // Add a spacer before the button to push it down
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(card.transform, false);
            spacer.transform.SetSiblingIndex(buyBtnContainer.transform.GetSiblingIndex());
            LayoutElement spacerElement = spacer.AddComponent<LayoutElement>();
            spacerElement.preferredHeight = 10; // Add extra space between price and button

            // Create the actual button
            GameObject buyBtnGO = new GameObject("BuyButton");
            buyBtnGO.transform.SetParent(buyBtnContainer.transform, false);
            RectTransform buyBtnRT = buyBtnGO.AddComponent<RectTransform>();
            buyBtnRT.anchorMin = new Vector2(0, 0);
            buyBtnRT.anchorMax = new Vector2(1, 1);
            buyBtnRT.offsetMin = Vector2.zero;
            buyBtnRT.offsetMax = Vector2.zero;
            buyBtnRT.sizeDelta = new Vector2(40, 20);

            // Button background
            Image btnImg = buyBtnGO.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.8f, 0.2f, 1f); // Green button

            // Rounded corners
            btnImg.sprite = CreateRoundedRectSprite(12);

            // Add button component
            Button buyBtn = buyBtnGO.AddComponent<Button>();

            // Button's color transitions
            ColorBlock colors = buyBtn.colors;
            colors.normalColor = new Color(0.2f, 0.8f, 0.2f, 1f); // Green
            colors.highlightedColor = new Color(0.3f, 0.9f, 0.3f, 1f); // Lighter green when highlighted
            colors.pressedColor = new Color(0.1f, 0.7f, 0.1f, 1f); // Darker green when pressed
            colors.disabledColor = new Color(0.2f, 0.8f, 0.2f, 1f);
            buyBtn.colors = colors;

            // Button text
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

            // Add click handler for the button
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
                else
                {
                    // Default handler for other items
                    HandleDefaultUpgrade(item);
                }
            });

            // Check and apply max upgrade state on initialization
            ApplyMaxUpgradeState(item, nameText, btnImg, btnText, buyBtn);

            MelonLogger.Msg("Created card for item: " + item.Name);
        }

        private void ApplyMaxUpgradeState(UpgradeItem item, TextMeshProUGUI nameText, Image btnImg, TextMeshProUGUI btnText, Button buyBtn)
        {
            if (item.Name.StartsWith("BackPack"))
            {
                int backpackLevel = MelonPreferences.GetEntryValue<int>("EasyUpgrades", "BackpackLevel");
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
                int stackLimitUpgrades = MelonPreferences.GetEntryValue<int>("EasyUpgrades", "StackLimitUpgrades");
                if (stackLimitUpgrades >= 3)
                {
                    nameText.text = $"Stack Size ({stackLimitUpgrades}/3)";
                    btnImg.color = Color.red;
                    btnText.text = "Max";
                    buyBtn.interactable = false;
                }
            }
        }

        private void HandleBackpackUpgrade(UpgradeItem item, TextMeshProUGUI nameText, Image btnImg, TextMeshProUGUI btnText, Button buyBtn)
        {
            // Parse current level from item name
            string levelStr = item.Name.Substring(item.Name.IndexOf("(") + 1, 1);
            int currentLevel;
            if (int.TryParse(levelStr, out currentLevel))
            {
                // Check if we can upgrade further
                if (currentLevel < 1) // Max level is 1
                {
                    // Try to spend money first
                    if (TrySpendMoney(item.Price))
                    {
                        // Upgrade the backpack
                        EasyUpgradesMain.Instance.UpgradeBackpack(1);

                        // Update the display name
                        nameText.text = "BackPack (1/1)";

                        // Disable the button
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
                    // Disable the button
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
            // Stack Size upgrade logic
            int currentUpgrades = EasyUpgradesMain.CurrentStackLimitUpgrades;

            if (currentUpgrades < 3)
            {
                // Try to spend money first
                if (TrySpendMoney(item.Price))
                {
                    // Upgrade the stack limit
                    EasyUpgradesMain.Instance.UpgradeStackLimit();

                    // Update the display name
                    nameText.text = $"Stack Size ({currentUpgrades + 1}/3)";

                    // Check if max upgrades reached
                    if (currentUpgrades + 1 >= 3)
                    {
                        // Disable the button
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
                // Disable the button
                btnImg.color = Color.red;
                btnText.text = "Max";
                buyBtn.interactable = false;
            }
        }

        private void HandleDefaultUpgrade(UpgradeItem item)
        {
            // Default handler for other items
            if (TrySpendMoney(item.Price))
            {
                MelonLogger.Msg($"Bought {item.Name} for ${item.Price}");
                // Add specific upgrade logic for other items
            }
            else
            {
                MelonLogger.Msg($"Cannot afford {item.Name}!");
            }
        }

        // Simple placeholder sprite
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

        // Create a rounded rectangle sprite for buttons
        private Sprite CreateRoundedRectSprite(float radius)
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size);

            // Fill with transparent pixels initially
            Color[] colors = new Color[size * size];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.clear;
            }
            tex.SetPixels(colors);

            // Draw a rounded rectangle
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
