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
using ScheduleOne.UI.Phone;     // Contains Phone, HomeScreen, etc.
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
            while (PlayerSingleton<HomeScreen>.Instance == null)
                yield return null;

            GameObject upgradesAppGO = new GameObject("UpgradesApp");
            upgradesAppGO.AddComponent<UpgradesApp>();
            MelonLogger.Msg("Upgrades App instantiated.");
            yield break;
        }
    }

    public class UpgradesApp : App<UpgradesApp>
    {
        private RectTransform _containerRT;
        private RectTransform _viewportRT;
        private RectTransform _contentRT;

        protected override void Awake()
        {
            base.Awake();

            // Find the AppsCanvas (where app panels reside).
            GameObject appsCanvas = GameObject.Find("Player_Local/CameraContainer/Camera/OverlayCamera/GameplayMenu/Phone/phone/AppsCanvas");
            if (appsCanvas == null)
            {
                MelonLogger.Error("AppsCanvas not found. Upgrades app container not created.");
                return;
            }

            // Create container.
            GameObject container = new GameObject("Container");
            container.transform.SetParent(appsCanvas.transform, false);
            _containerRT = container.AddComponent<RectTransform>();
            _containerRT.anchorMin = Vector2.zero;
            _containerRT.anchorMax = Vector2.one;
            _containerRT.offsetMin = Vector2.zero;
            _containerRT.offsetMax = Vector2.zero;
            container.AddComponent<CanvasGroup>(); // Optional, for visibility control.
            Image bg = container.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark grey background.

            // Create topbar.
            GameObject topbar = new GameObject("Topbar");
            topbar.transform.SetParent(container.transform, false);
            RectTransform topbarRT = topbar.AddComponent<RectTransform>();
            topbarRT.anchorMin = new Vector2(0, 1);
            topbarRT.anchorMax = new Vector2(1, 1);
            topbarRT.pivot = new Vector2(0.5f, 1);
            topbarRT.sizeDelta = new Vector2(0, 50);
            Image topbarImage = topbar.AddComponent<Image>();
            topbarImage.color = new Color(0.4f, 0f, 0.4f, 1f); // Dark purple.
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
            titleText.fontSize = 36;
            titleText.color = Color.white;

            // Create Scroll View.
            GameObject scrollView = new GameObject("Scroll View");
            scrollView.transform.SetParent(container.transform, false);
            RectTransform svRT = scrollView.AddComponent<RectTransform>();
            svRT.anchorMin = new Vector2(0, 0);
            svRT.anchorMax = new Vector2(1, 1);
            svRT.offsetMin = new Vector2(0, 0);
            svRT.offsetMax = new Vector2(0, -50); // Leave space for topbar.
            ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
            Image svImage = scrollView.AddComponent<Image>();
            svImage.color = new Color(0, 0, 0, 0); // Transparent.

            // Create Viewport.
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform, false);
            _viewportRT = viewport.AddComponent<RectTransform>();
            _viewportRT.anchorMin = Vector2.zero;
            _viewportRT.anchorMax = Vector2.one;
            _viewportRT.offsetMin = Vector2.zero;
            _viewportRT.offsetMax = Vector2.zero;
            Image vpImage = viewport.AddComponent<Image>();
            vpImage.color = new Color(1, 1, 1, 0); // Transparent.
            Mask vpMask = viewport.AddComponent<Mask>();
            vpMask.showMaskGraphic = false;
            scrollRect.viewport = _viewportRT;

            // Create Scrollbar Vertical.
            GameObject scrollbarGO = new GameObject("Scrollbar Vertical");
            scrollbarGO.transform.SetParent(scrollView.transform, false);
            RectTransform sbRT = scrollbarGO.AddComponent<RectTransform>();
            sbRT.anchorMin = new Vector2(1, 0);
            sbRT.anchorMax = new Vector2(1, 1);
            sbRT.pivot = new Vector2(1, 0.5f);
            sbRT.sizeDelta = new Vector2(20, 0);
            Image sbImage = scrollbarGO.AddComponent<Image>();
            sbImage.color = new Color(0.5f, 0.5f, 0.5f, 1f); // Gray.
            Scrollbar scrollbar = scrollbarGO.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = sbImage;
            // Create Sliding Area and Handle.
            GameObject slidingArea = new GameObject("Sliding Area");
            slidingArea.transform.SetParent(scrollbarGO.transform, false);
            RectTransform saRT = slidingArea.AddComponent<RectTransform>();
            saRT.anchorMin = new Vector2(0, 0);
            saRT.anchorMax = new Vector2(1, 1);
            saRT.offsetMin = Vector2.zero;
            saRT.offsetMax = Vector2.zero;
            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(slidingArea.transform, false);
            RectTransform handleRT = handle.AddComponent<RectTransform>();
            handleRT.anchorMin = new Vector2(0, 0);
            handleRT.anchorMax = new Vector2(1, 1);
            handleRT.offsetMin = Vector2.zero;
            handleRT.offsetMax = Vector2.zero;
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.8f, 0.8f, 0.8f, 1f); // Lighter gray.
            scrollbar.handleRect = handleRT;
            scrollbar.value = 1;
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            scrollRect.verticalScrollbarSpacing = -3;

            // Assign our container to the inherited appContainer field.
            this.appContainer = _containerRT;

            // Hide container by default.
            container.SetActive(false);
        }

        protected override void Start()
        {
            base.Start();
            bool wasActive = this.appContainer.gameObject.activeSelf;
            this.appContainer.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentRT);
            MelonLogger.Msg($"[Start] Content size: {_contentRT.rect.size}, container size: {_containerRT.rect.size}");
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
                        AppIcon = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
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
    }
}
