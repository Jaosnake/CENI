using RoR2;
using RoR2.UI;
using RoR2.UI.MainMenu;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ProperSave
{
    internal static class MainMenuUI
    {
        private const string ButtonName = "[CENI] Main Menu Load";
        private const string DefaultGameModeName = "ClassicRun";

        private static HGButton mainMenuButton;
        private static string pendingGameModeName;
        private static bool loadInProgress;

        public static void RegisterHooks()
        {
            On.RoR2.UI.MainMenu.MainMenuController.Start += MainMenuControllerStart;
            On.RoR2.ConsoleFunctions.SubmitCmd += ConsoleFunctionsSubmitCmd;
        }

        public static void UnregisterHooks()
        {
            On.RoR2.UI.MainMenu.MainMenuController.Start -= MainMenuControllerStart;
            On.RoR2.ConsoleFunctions.SubmitCmd -= ConsoleFunctionsSubmitCmd;
        }

        internal static void RefreshButton()
        {
            if (!mainMenuButton)
            {
                return;
            }

            var shouldBeInteractable = !loadInProgress && !Loading.IsLoading && SaveFileMetadata.GetMainMenuSaveMetadatas().Count > 0;
            if (mainMenuButton.interactable != shouldBeInteractable)
            {
                mainMenuButton.interactable = shouldBeInteractable;
            }
        }

        private static void MainMenuControllerStart(On.RoR2.UI.MainMenu.MainMenuController.orig_Start orig, MainMenuController self)
        {
            orig(self);

            try
            {
                if (MainMenuController.instance == self)
                {
                    AddMainMenuButton(self);
                }
            }
            catch (Exception e)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("Failed while adding main menu load button");
                ProperSavePlugin.InstanceLogger.LogError(e);
            }
        }

        private static void ConsoleFunctionsSubmitCmd(On.RoR2.ConsoleFunctions.orig_SubmitCmd orig, ConsoleFunctions self, string cmd)
        {
            if (pendingGameModeName != null && cmd != null)
            {
                var token = "gamemode " + DefaultGameModeName;
                if (cmd.Contains(token))
                {
                    cmd = cmd.Replace(token, "gamemode " + pendingGameModeName);
                    pendingGameModeName = null;
                }
            }

            orig(self, cmd);
        }

        private static void AddMainMenuButton(MainMenuController menu)
        {
            var anchor = menu.onlineMultiplayerButtonTransition;
            var template = menu.singlePlayerButton;
            if (!anchor || !template)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("Main menu buttons not found, load button was not added");
                return;
            }

            var parent = anchor.transform.parent;
            var existing = parent.Find(ButtonName);
            if (existing)
            {
                UnityEngine.Object.Destroy(existing.gameObject);
            }

            var buttonObject = UnityEngine.Object.Instantiate(template.gameObject, parent);
            buttonObject.name = ButtonName;
            buttonObject.SetActive(true);
            buttonObject.transform.SetSiblingIndex(anchor.transform.GetSiblingIndex() + 1);

            var button = buttonObject.GetComponent<HGButton>();
            button.hoverToken = LanguageConsts.PROPER_SAVE_TITLE_CONTINUE_DESC;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(OnMainMenuButtonClicked);

            foreach (var languageController in buttonObject.GetComponentsInChildren<LanguageTextMeshController>(true))
            {
                languageController.token = LanguageConsts.PROPER_SAVE_MAIN_MENU_LOAD;
            }

            mainMenuButton = button;
            WireExplicitNavigation(button, anchor);
            buttonObject.AddComponent<MainMenuLoadButtonRefresher>();
            RefreshButton();
        }

        // If the menu uses explicit navigation, insert the new button between Multiplayer and the next entry.
        private static void WireExplicitNavigation(HGButton button, Selectable previous)
        {
            var previousNavigation = previous.navigation;
            if (previousNavigation.mode != Navigation.Mode.Explicit)
            {
                return;
            }

            var next = previousNavigation.selectOnDown;

            var previousNav = previousNavigation;
            previousNav.selectOnDown = button;
            previous.navigation = previousNav;

            var buttonNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = previous, selectOnDown = next };
            button.navigation = buttonNav;

            if (next)
            {
                var nextNav = next.navigation;
                if (nextNav.mode == Navigation.Mode.Explicit)
                {
                    nextNav.selectOnUp = button;
                    next.navigation = nextNav;
                }
            }
        }

        private static void OnMainMenuButtonClicked()
        {
            if (Run.instance != null || Loading.IsLoading || loadInProgress)
            {
                return;
            }

            LobbyUI.ShowMainMenuSaveDialog();
        }

        internal static void StartLoadFromMainMenu(SaveFileMetadata save)
        {
            if (loadInProgress || save?.Header == null)
            {
                return;
            }

            var title = UnityEngine.Object.FindObjectOfType<TitleMenuController>(true);
            if (!title)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("TitleMenuController not found, can't load from main menu");
                return;
            }

            var gameModeName = GameModeCatalog.GetGameModeName(save.Header.GameMode);
            if (string.IsNullOrEmpty(gameModeName))
            {
                ProperSavePlugin.InstanceLogger.LogWarning($"Game mode of save {save.FileName} is not available");
                return;
            }

            // Reuse the native "Single Player" transition so the fade, lobby creation and menu locking behave exactly like the vanilla button.
            pendingGameModeName = gameModeName;
            title.OnSinglePlayerButtonPressed();
            if (pendingGameModeName != null)
            {
                pendingGameModeName = null;
                ProperSavePlugin.InstanceLogger.LogInfo("Main menu is not ready to start a game right now");
                return;
            }

            loadInProgress = true;
            ProperSavePlugin.Instance.StartCoroutine(LoadWhenLobbyReady(save));
        }

        private static IEnumerator LoadWhenLobbyReady(SaveFileMetadata save)
        {
            try
            {
                var timeout = 60f;
                while (timeout > 0f && (PreGameController.instance == null || NetworkUser.readOnlyInstancesList.Count == 0))
                {
                    timeout -= Time.unscaledDeltaTime;
                    yield return null;
                }

                if (PreGameController.instance == null || NetworkUser.readOnlyInstancesList.Count == 0)
                {
                    ProperSavePlugin.InstanceLogger.LogWarning("Timed out waiting for the lobby to start the loaded run");
                    yield break;
                }

                var settle = 1f;
                while (settle > 0f)
                {
                    settle -= Time.unscaledDeltaTime;
                    yield return null;
                }

                ProperSavePlugin.InstanceLogger.LogInfo($"Loading save {save.FileName} from the main menu");
                yield return Loading.LoadLobby(save);
            }
            finally
            {
                loadInProgress = false;
            }
        }

        private class MainMenuLoadButtonRefresher : MonoBehaviour
        {
            private float timer;

            private void OnEnable()
            {
                timer = 0f;
                RefreshButton();
            }

            private void Update()
            {
                timer -= Time.unscaledDeltaTime;
                if (timer > 0f)
                {
                    return;
                }

                timer = 0.75f;
                RefreshButton();
            }
        }
    }
}
