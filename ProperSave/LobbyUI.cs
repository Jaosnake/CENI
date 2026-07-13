using ProperSave.Components;
using RoR2;
using RoR2.UI;
using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProperSave
{
    internal static class LobbyUI
    {
        private static GameObject lobbyButton;
        private static GameObject lobbySubmenuLegend;
        private static GameObject lobbyGlyphAndDescription;
        private static TooltipProvider tooltipProvider;
        private static GamepadTooltipProvider gamepadTooltipProvider;
        private static SaveFileMetadata lastFileMetadata;
        private static TooltipContent lastTooltipContent;

        private const float DialogWidth = 640f;
        private const float SaveButtonHeight = 86f;
        private const float CompactButtonHeight = 52f;
        private const float LegendHeight = 42f;
        private const float ConfirmRowHeight = 56f;
        private const float ConfirmButtonWidth = 230f;
        private const float ConfirmButtonHeight = 48f;
        private const float LegendItemWidth = 260f;
        private const float LegendItemHeight = 30f;

        #region Buttons
        public static void RegisterHooks()
        {
            On.RoR2.UI.CharacterSelectController.Awake += CharacterSelectControllerAwake;
            NetworkUser.onPostNetworkUserStart += NetworkUserOnPostNetworkUserStart;
            NetworkUser.onNetworkUserLost += NetworkUserOnNetworkUserLost;
        }

        public static void UnregisterHooks()
        {
            On.RoR2.UI.CharacterSelectController.Awake -= CharacterSelectControllerAwake;
            NetworkUser.onPostNetworkUserStart -= NetworkUserOnPostNetworkUserStart;
            NetworkUser.onNetworkUserLost -= NetworkUserOnNetworkUserLost;
        }

        private static void NetworkUserOnNetworkUserLost(NetworkUser networkUser)
        {
            UpdateLobbyControls(networkUser);
        }

        private static void NetworkUserOnPostNetworkUserStart(NetworkUser networkUser)
        {
            UpdateLobbyControls();
        }

        private static void CharacterSelectControllerAwake(On.RoR2.UI.CharacterSelectController.orig_Awake orig, CharacterSelectController self)
        {
            try
            {
                #region LoadButton
                var quitButton = self.transform.GetChild(2).GetChild(4).GetChild(0).gameObject;
                lobbyButton = GameObject.Instantiate(quitButton, quitButton.transform.parent);

                foreach (var filter in self.GetComponents<InputSourceFilter>())
                {
                    if (filter.requiredInputSource == MPEventSystem.InputSource.MouseAndKeyboard)
                    {
                        Array.Resize(ref filter.objectsToFilter, filter.objectsToFilter.Length + 1);
                        filter.objectsToFilter[filter.objectsToFilter.Length - 1] = lobbyButton;
                        break;
                    }
                }

                lobbyButton.name = "[CENI] Load";

                tooltipProvider = lobbyButton.AddComponent<TooltipProvider>();

                var rectTransform = lobbyButton.GetComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(1F, 1.5F);
                rectTransform.anchorMax = new Vector2(1F, 1.5F);

                var buttonComponent = lobbyButton.GetComponent<HGButton>();
                buttonComponent.hoverToken = LanguageConsts.PROPER_SAVE_TITLE_CONTINUE_DESC;

                var languageComponent = lobbyButton.GetComponent<LanguageTextMeshController>();
                languageComponent.token = LanguageConsts.PROPER_SAVE_TITLE_LOAD;

                buttonComponent.onClick = new Button.ButtonClickedEvent();
                buttonComponent.onClick.AddListener(LoadOnInputEvent);
                #endregion

                #region Load GlypAndDescription
                var submenuLegend = self.transform.GetChild(2).GetChild(4).GetChild(1).gameObject;
                lobbySubmenuLegend = GameObject.Instantiate(submenuLegend, submenuLegend.transform.parent);

                foreach (var filter in self.GetComponents<InputSourceFilter>())
                {
                    if (filter.requiredInputSource == MPEventSystem.InputSource.Gamepad)
                    {
                        Array.Resize(ref filter.objectsToFilter, filter.objectsToFilter.Length + 1);
                        filter.objectsToFilter[filter.objectsToFilter.Length - 1] = lobbySubmenuLegend;
                        break;
                    }
                }

                lobbySubmenuLegend.name = "[CENI] SubmenuLegend";

                var uiJuiceComponent = lobbySubmenuLegend.GetComponent<UIJuice>();
                var enableEventComponent = lobbySubmenuLegend.GetComponent<OnEnableEvent>();

                enableEventComponent.action.RemoveAllListeners();
                enableEventComponent.action.AddListener(uiJuiceComponent.TransitionPanFromTop);
                enableEventComponent.action.AddListener(uiJuiceComponent.TransitionAlphaFadeIn);

                var rectTransformComponent = lobbySubmenuLegend.GetComponent<RectTransform>();
                rectTransformComponent.anchorMin = new Vector2(1, 1);
                rectTransformComponent.anchorMax = new Vector2(1, 2);

                lobbyGlyphAndDescription = lobbySubmenuLegend.transform.GetChild(0).gameObject;
                lobbyGlyphAndDescription.SetActive(true);

                var glyph = lobbyGlyphAndDescription.transform.GetChild(0).GetComponent<InputBindingDisplayController>();
                glyph.actionName = "UISubmitTertiary";

                var glyph2 = lobbyGlyphAndDescription.transform.GetChild(1);
                var description = lobbyGlyphAndDescription.transform.GetChild(2).GetComponent<LanguageTextMeshController>();
                GameObject.Destroy(glyph2.gameObject);

                description.token = LanguageConsts.PROPER_SAVE_TITLE_LOAD;

                for (var i = 1; i < lobbySubmenuLegend.transform.childCount; i++)
                {
                    GameObject.Destroy(lobbySubmenuLegend.transform.GetChild(i).gameObject);
                }
                #endregion

                var gamepadInputEvent = self.gameObject.AddComponent<HoldGamepadInputEvent>();
                gamepadInputEvent.actionName = "UISubmitTertiary";
                gamepadInputEvent.enabledObjectsIfActive = Array.Empty<GameObject>();

                gamepadInputEvent.actionEvent = new UnityEngine.Events.UnityEvent();
                gamepadInputEvent.actionEvent.AddListener(LoadOnInputEvent);

                gamepadTooltipProvider = glyph.gameObject.AddComponent<GamepadTooltipProvider>();
                gamepadTooltipProvider.inputEvent = gamepadInputEvent;

                UpdateLobbyControls();
            }
            catch (Exception e)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("Failed while adding lobby buttons");
                ProperSavePlugin.InstanceLogger.LogError(e);
            }
            orig(self);
        }

        private static void LoadOnInputEvent()
        {
            if (Run.instance != null)
            {
                ProperSavePlugin.InstanceLogger.LogInfo("Can't load while run is active");
                return;
            }
            if (Loading.IsLoading)
            {
                ProperSavePlugin.InstanceLogger.LogInfo("Already loading");
                return;
            }

            var saves = SaveFileMetadata.GetCurrentLobbySaveMetadatas();
            if (saves.Count == 0)
            {
                ProperSavePlugin.InstanceLogger.LogInfo("Save file for current users is not found");
                return;
            }

            ShowSaveSelectionDialog(saves);
        }

        private static void UpdateLobbyControls(NetworkUser exceptUser = null)
        {
            var metadata = SaveFileMetadata.GetCurrentLobbySaveMetadata(exceptUser);
            var interactable =
                PlatformSystems.lobbyManager.isInLobby == PlatformSystems.lobbyManager.ownsLobby &&
                metadata != null &&
                metadata.FilePath.HasValue &&
                ProperSavePlugin.SavesFileSystem.FileExists(metadata.FilePath.Value);

            if (metadata != lastFileMetadata)
            {
                lastFileMetadata = metadata;
                try
                {
                    if (metadata != null)
                    {
                        lastTooltipContent = new TooltipContent
                        {
                            titleToken = LanguageConsts.PROPER_SAVE_TOOLTIP_LOAD_TITLE,
                            overrideBodyText = GetSaveDescription(metadata),
                            titleColor = Color.black,
                            disableBodyRichText = false
                        };
                    }
                    else
                    {
                        lastTooltipContent = default;
                    }
                }
                catch (Exception ex)
                {
                    ProperSavePlugin.InstanceLogger.LogWarning("Failed to get information about save file");
                    ProperSavePlugin.InstanceLogger.LogError(ex);
                    interactable = false;
                }
            }

            try
            {
                if (lobbyButton)
                {
                    var component = lobbyButton.GetComponent<HGButton>();
                    if (component)
                    {
                        component.interactable = interactable;
                    }
                }
                if (tooltipProvider)
                {
                    tooltipProvider.SetContent(lastTooltipContent);
                }
            }
            catch { }
            try
            {
                if (lobbyGlyphAndDescription)
                {
                    var color = interactable ? Color.white : new Color(0.3F, 0.3F, 0.3F);

                    var glyphText = lobbyGlyphAndDescription.transform.GetChild(0).GetComponent<HGTextMeshProUGUI>();
                    glyphText.color = color;

                    var descriptionText = lobbyGlyphAndDescription.transform.GetChild(1).GetComponent<HGTextMeshProUGUI>();
                    descriptionText.color = color;
                }
                if (gamepadTooltipProvider)
                {
                    gamepadTooltipProvider.SetContent(lastTooltipContent);
                }
            }
            catch { }
        }

        private static string GetSaveDescription(SaveFileMetadata saveMetadata)
        {
            var header = saveMetadata.Header;
            var builder = new StringBuilder();
            foreach (var userData in header.Users)
            {
                var networkUser = NetworkUser.readOnlyInstancesList.FirstOrDefault(user => userData.UserId.Load().Equals(user.id));
                var body = BodyCatalog.GetBodyPrefab(userData.Body);
                var survivor = SurvivorCatalog.FindSurvivorDefFromBody(body);
                builder.Append(Language.GetStringFormatted(LanguageConsts.PROPER_SAVE_TOOLTIP_LOAD_DESCRIPTION_CHARACTER, networkUser?.userName, survivor != null ? Language.GetString(survivor.displayNameToken) : ""));
            }

            var stage = SceneCatalog.GetSceneDefFromSceneName(header.SceneName);
            var difficulty = DifficultyCatalog.GetDifficultyDef(header.Difficulty);
            var time = header.Time;
            var stageClearCount = header.StageClearCount;

            return Language.GetStringFormatted(
                LanguageConsts.PROPER_SAVE_TOOLTIP_LOAD_DESCRIPTION_BODY,
                builder.ToString(),
                stage ? Language.GetString(stage.nameToken) : "",
                (stageClearCount + 1).ToString(),
                $"{(time / 60):00}:{(time % 60):00}",
                difficulty != null ? Language.GetString(difficulty.nameToken) : "",
                header.ContentHash != ProperSavePlugin.ContentHash ? Language.GetString(LanguageConsts.PROPER_SAVE_TOOLTIP_LOAD_CONTENT_MISMATCH) : "");
        }

        private static void ShowSaveSelectionDialog(System.Collections.Generic.List<SaveFileMetadata> saves)
        {
            var dialog = SimpleDialogBox.Create(EventSystem.current as MPEventSystem);
            if (dialog == null)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("Failed to create save selection dialog");
                return;
            }

            dialog.headerToken = new SimpleDialogBox.TokenParamsPair { token = LanguageConsts.PROPER_SAVE_SELECT_SAVE_TITLE };
            dialog.descriptionToken = new SimpleDialogBox.TokenParamsPair { token = LanguageConsts.PROPER_SAVE_SELECT_SAVE_DESCRIPTION };

            foreach (var save in saves)
            {
                var captured = save;
                dialog.AddActionButton(() =>
                {
                    ProperSavePlugin.Instance.StartCoroutine(Loading.LoadLobby(captured));
                }, LanguageConsts.PROPER_SAVE_TITLE_LOAD, destroyDialog: true);

                SetLastButtonContent(dialog, GetSaveButtonDescription(save), GetSavePortrait(save));
            }

            dialog.AddActionButton(() =>
            {
                ShowDeleteSaveDialog(saves);
            }, LanguageConsts.PROPER_SAVE_DELETE_SAVE, destroyDialog: true);
            MarkLastButtonCompact(dialog);

            dialog.AddActionButton(() => { }, LanguageConsts.PROPER_SAVE_SELECT_SAVE_CANCEL, destroyDialog: true);
            MarkLastButtonCompact(dialog);

            AddCloseWatcher(dialog);

            ProperSavePlugin.Instance.StartCoroutine(DelayedSaveDialogLayout(dialog));
        }

        private static void ShowDeleteSaveDialog(System.Collections.Generic.List<SaveFileMetadata> saves)
        {
            var dialog = SimpleDialogBox.Create(EventSystem.current as MPEventSystem);
            if (dialog == null)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("Failed to create delete save dialog");
                return;
            }

            dialog.headerToken = new SimpleDialogBox.TokenParamsPair { token = LanguageConsts.PROPER_SAVE_DELETE_SAVE_TITLE };
            dialog.descriptionToken = new SimpleDialogBox.TokenParamsPair { token = LanguageConsts.PROPER_SAVE_DELETE_SAVE_DESCRIPTION };

            foreach (var save in saves)
            {
                var captured = save;
                dialog.AddActionButton(() =>
                {
                    ShowDeleteConfirmDialog(captured);
                }, LanguageConsts.PROPER_SAVE_DELETE_SAVE, destroyDialog: true);

                SetLastButtonContent(dialog, GetSaveButtonDescription(save), GetSavePortrait(save));
            }

            dialog.AddActionButton(() => { }, LanguageConsts.PROPER_SAVE_SELECT_SAVE_CANCEL, destroyDialog: true);
            MarkLastButtonCompact(dialog);

            AddCloseWatcher(dialog);

            ProperSavePlugin.Instance.StartCoroutine(DelayedSaveDialogLayout(dialog));
        }

        private static void ShowDeleteConfirmDialog(SaveFileMetadata save)
        {
            var dialog = SimpleDialogBox.Create(EventSystem.current as MPEventSystem);
            if (dialog == null)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("Failed to create delete confirmation dialog");
                return;
            }

            dialog.headerToken = new SimpleDialogBox.TokenParamsPair { token = LanguageConsts.PROPER_SAVE_DELETE_CONFIRM_TITLE };
            dialog.descriptionToken = new SimpleDialogBox.TokenParamsPair { token = LanguageConsts.PROPER_SAVE_DELETE_CONFIRM_DESCRIPTION };

            dialog.AddActionButton(() =>
            {
                SaveFileMetadata.DeleteSave(save);
                UpdateLobbyControls();
            }, LanguageConsts.PROPER_SAVE_DELETE_CONFIRM_YES, destroyDialog: true);

            dialog.AddActionButton(() =>
            {
                ShowSaveSelectionDialog(SaveFileMetadata.GetCurrentLobbySaveMetadatas());
            }, LanguageConsts.PROPER_SAVE_SELECT_SAVE_CANCEL, destroyDialog: true);

            AddCloseWatcher(dialog, () => ShowSaveSelectionDialog(SaveFileMetadata.GetCurrentLobbySaveMetadatas()));

            ProperSavePlugin.Instance.StartCoroutine(DelayedSaveDialogLayout(dialog, confirmLayout: true));
        }

        private static string GetSaveButtonDescription(SaveFileMetadata saveMetadata)
        {
            var header = saveMetadata.Header;
            var character = "";
            if (header.Users.Length > 0)
            {
                var body = BodyCatalog.GetBodyPrefab(header.Users[0].Body);
                var survivor = SurvivorCatalog.FindSurvivorDefFromBody(body);
                character = survivor != null ? Language.GetString(survivor.displayNameToken) : "";
            }

            var stage = SceneCatalog.GetSceneDefFromSceneName(header.SceneName);
            var difficulty = DifficultyCatalog.GetDifficultyDef(header.Difficulty);
            var time = header.Time;

            return Language.GetStringFormatted(
                LanguageConsts.PROPER_SAVE_SELECT_SAVE_BUTTON,
                character,
                stage ? Language.GetString(stage.nameToken) : header.SceneName,
                (header.StageClearCount + 1).ToString(),
                $"{(time / 60):00}:{(time % 60):00}",
                difficulty != null ? Language.GetString(difficulty.nameToken) : "",
                "");
        }

        private static Texture GetSavePortrait(SaveFileMetadata saveMetadata)
        {
            try
            {
                var header = saveMetadata.Header;
                if (header.Users.Length == 0)
                {
                    return null;
                }

                var body = BodyCatalog.GetBodyPrefab(header.Users[0].Body);
                var characterBody = body ? body.GetComponent<CharacterBody>() : null;
                return characterBody ? characterBody.portraitIcon : null;
            }
            catch
            {
                return null;
            }
        }

        private static void SetLastButtonContent(SimpleDialogBox dialog, string text, Texture portrait)
        {
            try
            {
                var container = dialog.buttonContainer;
                if (container == null || container.childCount == 0)
                {
                    return;
                }

                var button = container.GetChild(container.childCount - 1);
                if (portrait != null)
                {
                    AddPortraitToButton(button, portrait);
                }

                foreach (var languageController in button.GetComponentsInChildren<LanguageTextMeshController>())
                {
                    languageController.enabled = false;
                }

                var label = button.GetComponentInChildren<HGTextMeshProUGUI>();
                if (label != null)
                {
                    label.text = text;
                    label.enableWordWrapping = true;
                    label.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
                    label.fontSize = 18f;

                    var rect = label.GetComponent<RectTransform>();
                    if (rect != null && portrait != null)
                    {
                        rect.offsetMin = new Vector2(rect.offsetMin.x + 78f, rect.offsetMin.y);
                    }
                }
            }
            catch (Exception ex)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("Failed to set save button content");
                ProperSavePlugin.InstanceLogger.LogError(ex);
            }
        }

        private static void AddPortraitToButton(Transform button, Texture portrait)
        {
            var portraitObject = new GameObject("[CENI] Portrait", typeof(RectTransform), typeof(RawImage));
            var rect = portraitObject.GetComponent<RectTransform>();
            rect.SetParent(button, false);
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(12f, 0f);
            rect.sizeDelta = new Vector2(64f, 64f);

            var image = portraitObject.GetComponent<RawImage>();
            image.texture = portrait;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private static IEnumerator DelayedSaveDialogLayout(SimpleDialogBox dialog, bool confirmLayout = false)
        {
            yield return null;
            if (confirmLayout)
            {
                ApplyConfirmDialogLayout(dialog);
            }
            else
            {
                ApplySaveDialogLayout(dialog);
            }
        }

        private static void ApplySaveDialogLayout(SimpleDialogBox dialog)
        {
            try
            {
                var container = dialog.buttonContainer;
                if (container == null)
                {
                    return;
                }

                AddInputLegend(dialog);

                foreach (var layout in container.GetComponents<LayoutGroup>())
                {
                    layout.enabled = false;
                    UnityEngine.Object.DestroyImmediate(layout, true);
                }

                var vertical = container.gameObject.AddComponent<VerticalLayoutGroup>();
                vertical.spacing = 8f;
                vertical.childAlignment = TextAnchor.UpperCenter;
                vertical.childControlWidth = false;
                vertical.childControlHeight = false;
                vertical.childForceExpandWidth = false;
                vertical.childForceExpandHeight = false;

                for (var i = 0; i < container.childCount; i++)
                {
                    var child = container.GetChild(i);
                    if (child.GetComponent<SaveDialogInputLegendController>() != null)
                    {
                        ConfigureInputLegendLayout(child);
                        continue;
                    }

                    var isCompactButton = child.GetComponent<CompactSaveDialogButton>() != null;
                    var buttonHeight = isCompactButton ? CompactButtonHeight : SaveButtonHeight;
                    var rect = child.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.sizeDelta = new Vector2(DialogWidth, buttonHeight);
                    }

                    SetLayoutElement(child.gameObject, DialogWidth, buttonHeight);
                }

                var fitter = container.GetComponent<ContentSizeFitter>() ?? container.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

                WrapSaveDialogNavigation(container);
                LayoutRebuilder.ForceRebuildLayoutImmediate(container);
            }
            catch (Exception ex)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("Failed to apply save selection layout");
                ProperSavePlugin.InstanceLogger.LogError(ex);
            }
        }

        private static void ApplyConfirmDialogLayout(SimpleDialogBox dialog)
        {
            try
            {
                var container = dialog.buttonContainer;
                if (container == null)
                {
                    return;
                }

                AddInputLegend(dialog);

                foreach (var layout in container.GetComponents<LayoutGroup>())
                {
                    layout.enabled = false;
                    UnityEngine.Object.DestroyImmediate(layout, true);
                }

                var buttons = container.GetComponentsInChildren<MPButton>(includeInactive: true)
                    .Where(button => button.GetComponentInParent<SaveDialogInputLegendController>() == null)
                    .Select(button => button.transform)
                    .Distinct()
                    .ToList();

                var row = container.Find("[CENI] Confirm Button Row") as RectTransform;
                if (row == null)
                {
                    var rowObject = new GameObject("[CENI] Confirm Button Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                    row = rowObject.GetComponent<RectTransform>();
                    row.SetParent(container, false);
                    row.SetSiblingIndex(0);
                }
                SetCenteredRect(row, DialogWidth, ConfirmRowHeight);

                foreach (var button in buttons)
                {
                    button.SetParent(row, false);
                }

                var rowLayout = row.GetComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 96f;
                rowLayout.childAlignment = TextAnchor.MiddleCenter;
                rowLayout.childControlWidth = false;
                rowLayout.childControlHeight = false;
                rowLayout.childForceExpandWidth = false;
                rowLayout.childForceExpandHeight = false;

                SetLayoutElement(row.gameObject, DialogWidth, ConfirmRowHeight);

                foreach (var button in buttons)
                {
                    var rect = button.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        SetCenteredRect(rect, ConfirmButtonWidth, ConfirmButtonHeight);
                    }

                    SetLayoutElement(button.gameObject, ConfirmButtonWidth, ConfirmButtonHeight);
                }

                var vertical = container.gameObject.AddComponent<VerticalLayoutGroup>();
                vertical.spacing = 8f;
                vertical.childAlignment = TextAnchor.UpperCenter;
                vertical.childControlWidth = true;
                vertical.childControlHeight = false;
                vertical.childForceExpandWidth = false;
                vertical.childForceExpandHeight = false;

                ConfigureInputLegendLayout(container.Find("[CENI] Save Input Legend"));

                var fitter = container.GetComponent<ContentSizeFitter>() ?? container.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

                WrapSaveDialogNavigation(container);
                LayoutRebuilder.ForceRebuildLayoutImmediate(container);
            }
            catch (Exception ex)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("Failed to apply delete confirmation layout");
                ProperSavePlugin.InstanceLogger.LogError(ex);
            }
        }

        private static void WrapSaveDialogNavigation(RectTransform container)
        {
            var buttons = container.GetComponentsInChildren<MPButton>()
                .Where(button => button.GetComponentInParent<SaveDialogInputLegendController>() == null)
                .ToList();
            if (buttons.Count == 0)
            {
                return;
            }

            for (var i = 0; i < buttons.Count; i++)
            {
                var navigation = new Navigation { mode = Navigation.Mode.Explicit };
                navigation.selectOnUp = buttons[(i - 1 + buttons.Count) % buttons.Count];
                navigation.selectOnDown = buttons[(i + 1) % buttons.Count];
                navigation.selectOnLeft = navigation.selectOnUp;
                navigation.selectOnRight = navigation.selectOnDown;
                buttons[i].navigation = navigation;
            }
        }

        private static void AddInputLegend(SimpleDialogBox dialog)
        {
            var container = dialog?.buttonContainer;
            if (container == null || container.Find("[CENI] Save Input Legend") != null)
            {
                return;
            }

            var legend = new GameObject("[CENI] Save Input Legend", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(SaveDialogInputLegendController));
            var legendRect = legend.GetComponent<RectTransform>();
            legendRect.SetParent(container, false);
            legendRect.sizeDelta = new Vector2(DialogWidth, LegendHeight);

            ConfigureInputLegendLayout(legend.transform);

            var layout = legend.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 74f;
            layout.padding = new RectOffset(10, 0, 8, 0);
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            var controller = legend.GetComponent<SaveDialogInputLegendController>();
            controller.eventSystem = EventSystem.current as MPEventSystem;
            controller.selectGlyph = AddLegendItem(legendRect, "UISubmit", LanguageConsts.PROPER_SAVE_MOUSE_SELECT_HINT, LanguageConsts.PROPER_SAVE_SELECT_HINT);
            controller.backGlyph = AddLegendItem(legendRect, "UICancel", LanguageConsts.PROPER_SAVE_ESC_HINT, LanguageConsts.PROPER_SAVE_BACK_HINT);
        }

        private static void ConfigureInputLegendLayout(Transform legend)
        {
            if (legend == null)
            {
                return;
            }

            var rect = legend.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(DialogWidth, LegendHeight);
            }

            SetLayoutElement(legend.gameObject, DialogWidth, LegendHeight);
        }

        private static InputBindingDisplayController AddLegendItem(RectTransform parent, string actionName, string keyToken, string textToken)
        {
            var item = new GameObject(actionName + " Legend", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var itemRect = item.GetComponent<RectTransform>();
            itemRect.SetParent(parent, false);
            itemRect.sizeDelta = new Vector2(LegendItemWidth, LegendItemHeight);

            var itemLayout = item.GetComponent<HorizontalLayoutGroup>();
            itemLayout.spacing = 7f;
            itemLayout.childForceExpandHeight = false;
            itemLayout.childForceExpandWidth = false;

            var layoutElement = item.GetComponent<LayoutElement>();
            ConfigureLayoutElement(layoutElement, LegendItemWidth, LegendItemHeight);

            var glyphObject = new GameObject("Glyph", typeof(RectTransform));
            glyphObject.SetActive(false);
            var glyphRect = glyphObject.GetComponent<RectTransform>();
            glyphRect.SetParent(itemRect, false);
            glyphRect.sizeDelta = new Vector2(28f, 28f);

            var glyphText = glyphObject.AddComponent<HGTextMeshProUGUI>();
            glyphText.fontSize = 18f;
            glyphText.alignment = TMPro.TextAlignmentOptions.Center;

            var glyph = glyphObject.AddComponent<InputBindingDisplayController>();
            glyph.actionName = actionName;
            glyph.useExplicitInputSource = true;
            glyph.explicitInputSource = MPEventSystem.InputSource.Gamepad;
            glyphObject.SetActive(true);

            var keyObject = new GameObject("KeyFallback", typeof(RectTransform));
            keyObject.SetActive(false);
            var keyRect = keyObject.GetComponent<RectTransform>();
            keyRect.SetParent(itemRect, false);
            keyRect.sizeDelta = new Vector2(160f, 28f);

            var keyText = keyObject.AddComponent<HGTextMeshProUGUI>();
            keyText.fontSize = 14f;
            keyText.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            keyText.enableWordWrapping = false;

            var keyLang = keyObject.AddComponent<LanguageTextMeshController>();
            keyLang.token = keyToken;
            keyObject.SetActive(true);

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.SetActive(false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(itemRect, false);
            labelRect.sizeDelta = new Vector2(90f, 28f);

            var labelText = labelObject.AddComponent<HGTextMeshProUGUI>();
            labelText.fontSize = 14f;
            labelText.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            labelText.enableWordWrapping = false;

            var lang = labelObject.AddComponent<LanguageTextMeshController>();
            lang.token = textToken;
            labelObject.SetActive(true);

            return glyph;
        }

        private static SaveDialogCloseWatcher AddCloseWatcher(SimpleDialogBox dialog, Action onCancel = null)
        {
            var watcher = GetDialogRoot(dialog).AddComponent<SaveDialogCloseWatcher>();
            watcher.dialog = dialog;
            watcher.onCancel = onCancel;
            return watcher;
        }

        private static GameObject GetDialogRoot(SimpleDialogBox dialog)
        {
            return dialog.rootObject != null ? dialog.rootObject : dialog.gameObject;
        }

        private static void SetCenteredRect(RectTransform rect, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetLayoutElement(GameObject target, float width, float height)
        {
            var element = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            ConfigureLayoutElement(element, width, height);
        }

        private static void ConfigureLayoutElement(LayoutElement element, float width, float height)
        {
            element.minWidth = width;
            element.preferredWidth = width;
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;
        }

        private class SaveDialogCloseWatcher : MonoBehaviour
        {
            public SimpleDialogBox dialog;
            public Action onCancel;
            private float delay = 0.4f;
            private bool closing;

            private void Update()
            {
                if (closing)
                {
                    return;
                }

                delay -= Time.unscaledDeltaTime;
                if (delay > 0f)
                {
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton1))
                {
                    closing = true;
                    var target = dialog.rootObject != null ? dialog.rootObject : dialog.gameObject;
                    onCancel?.Invoke();
                    UnityEngine.Object.Destroy(target);
                }
            }
        }

        private static void MarkLastButtonCompact(SimpleDialogBox dialog)
        {
            var container = dialog?.buttonContainer;
            if (container == null || container.childCount == 0)
            {
                return;
            }

            var child = container.GetChild(container.childCount - 1);
            if (child.GetComponent<CompactSaveDialogButton>() == null)
            {
                child.gameObject.AddComponent<CompactSaveDialogButton>();
            }
        }

        private class CompactSaveDialogButton : MonoBehaviour
        {
        }

        private class SaveDialogInputLegendController : MonoBehaviour
        {
            public MPEventSystem eventSystem;
            public InputBindingDisplayController selectGlyph;
            public InputBindingDisplayController backGlyph;

            private void Update()
            {
                var gamepad = eventSystem != null && eventSystem.currentInputSource == MPEventSystem.InputSource.Gamepad;
                SetGlyphVisible(selectGlyph, gamepad);
                SetGlyphVisible(backGlyph, gamepad);
            }

            private static void SetGlyphVisible(InputBindingDisplayController glyph, bool visible)
            {
                if (glyph == null)
                {
                    return;
                }

                var glyphText = glyph.GetComponent<HGTextMeshProUGUI>();
                if (glyphText != null)
                {
                    glyphText.enabled = visible;
                }

                var fallback = glyph.transform.parent.Find("KeyFallback");
                if (fallback != null)
                {
                    fallback.gameObject.SetActive(!visible);
                }
            }
        }
        #endregion
    }
}
