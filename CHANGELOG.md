**1.0.5**

- Saves whose survivor is unavailable stay visible and show a warning instead of starting with a missing character. Restore the character mod to load the save.
- Caches the displayed survivor name and a small portrait beside the save when available; an unavailable character is displayed in grayscale with a red X when a cached portrait exists. Older saves receive a cached portrait when viewed while the character is installed.
- Excludes saves with missing characters or changed content from automatic cleanup. Fixed an error path in forced loading.
- Keeps the existing save format. Item mods are not checked or blocked; changes to the mod list can still affect restored content.

**1.0.4**

- Added a **Load Save** button to the main menu, right below Multiplayer. It opens the same save list as the lobby Load option (character, stage, number, time, difficulty, portrait and delete) and loads the chosen save straight into the run, without going through the lobby first. It lists the most recent saves of your profile from any character and stays disabled when there are no saves. The Load option in the Singleplayer lobby still works as before.
- Fixed items, equipment, money and loadout not being restored when loading a save that has allied minions without `AIOwnership` or `BaseAI` (such as some drones and turrets). The error aborted the load before the player's own data was applied. These minions now load safely, and any minion that still fails is skipped and logged without affecting the player.
- Added `Load Save` / `Carregar Save` localization (English and Brazilian Portuguese) and screenshots of the new menu to the README.

**1.0.3**

- Fixed `SaveFileHeader.SaveDate` not being populated in memory when creating a new save during the current session, which previously prevented older saves from being pruned properly.
- Improved save pruning and ordering for multi-scene Hidden Realms and DLC4 events so sub-stages with paused timers do not overwrite normal stage saves, and the most recent save is always listed first.
- Normalized `UserProfileId` comparisons across newly created and on-disk saves.

**1.0.2**

- Fixed game-breaking save failure on stage transition in Risk of Rain 2 v1.5.0 caused by the removal of `Inventory._lastExtraEquipmentCount`.
- Fixed potential load exception in v1.5.0 caused by changes to `Inventory.activeEquipmentSet`.
- Fixed gamepad Load legend display issue (`[SEM VÍNC. CONTROLE]` overlap) in the character select lobby by resetting the gamepad axis range and using the dedicated Equipment binding action.
- Fixed an issue causing allied minions (drones, lemurians) to be dead or despawned after loading a run (aligned with upstream ProperSave 3.1.2).
- Restored run rulebook and seed reliably on load via `NetworkSession.BeginRun`.
- Prevented spawning duplicate save-state picker dialogs when pressing the controller Load action rapidly.

**1.0.1**

- Corrected English spelling and improved the wording of two Brazilian Portuguese save descriptions.
- Normalized the English translation file to UTF-8 without BOM.

**1.0.0**

- Initial C.E.N.I release.
- Added selectable save-state UI from the lobby Load action.
- Added up to 5 save states per character/lobby group.
- Added survivor portraits in the save picker when available.
- Added Brazilian Portuguese localization.
- English is used as fallback for all non-pt-BR languages.
- Changed save ordering to highest stage first.
- Preserves save history after game over.
