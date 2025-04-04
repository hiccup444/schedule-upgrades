using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MelonLoader;
using MelonLoader.Utils;
using ScheduleOne.UI;           // Contains App<T>
using ScheduleOne.UI.Phone;     // Contains Phone, HomeScreen, etc
using ScheduleOne.DevUtilities; // Contains PlayerSingleton

namespace EasyUpgrades
{
    public class EasyUpgradesMain : MelonMod
    {
        public override void OnApplicationStart()
        {
            MelonLogger.Msg("EasyUpgrades Mod loaded.");
            MelonCoroutines.Start(SetupUpgradesApp());
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
            yield break;
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
            new UpgradeItem("BackPack (0/2)", 100, Color.green),
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

            buyBtn.onClick.AddListener(() =>
            {
                MelonLogger.Msg($"Bought {item.Name} for ${item.Price}");
            });

            MelonLogger.Msg("Created card for item: " + item.Name);
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
