using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using Zio;
using System.IO;
using ProperSave.Data;

namespace ProperSave
{
    public class SaveFileMetadata
    {
        public string FileName { get; internal set; }
        public bool ForceLoad { get; internal set; }
        public SaveFileHeader Header { get; internal set; }
        public long BodyOffset { get; internal set; }
        public SaveFile Body { get; internal set; }

        public UPath? FilePath
        {
            get
            {
                return string.IsNullOrEmpty(FileName) ? null : ProperSavePlugin.SavesPath / $"{FileName}.bin";
            }
        }

        internal static List<SaveFileMetadata> SavesMetadata { get; } = new List<SaveFileMetadata>();

        internal void FillMetadataForCurrentLobby()
        {
            ForceLoad = false;
            Header = new SaveFileHeader();
            Body = new SaveFile();
            BodyOffset = 0;

            Header.FillFromCurrentRun();
            Body.FillFromCurrentRun();

            if (FileName is null)
            {
                do
                {
                    FileName = Guid.NewGuid().ToString();
                }
                while (ProperSavePlugin.SavesFileSystem.FileExists(FilePath.Value));
            }
        }

        internal static SaveFileMetadata GetCurrentLobbySaveMetadata(NetworkUser exceptUser = null)
        {
            return GetCurrentLobbySaveMetadatas(exceptUser).FirstOrDefault();
        }

        internal static List<SaveFileMetadata> GetCurrentLobbySaveMetadatas(NetworkUser exceptUser = null)
        {
            try
            {
                if (!TryGetCurrentLobbyIdentity(exceptUser, out var users, out var bodies, out var profile, out var gameMode))
                {
                    return new List<SaveFileMetadata>();
                }

                return SavesMetadata
                    .Where(el => IsSameSaveGroup(el, users, bodies, profile, gameMode))
                    .OrderByDescending(el => el.Header.StageClearCount)
                    .ThenByDescending(el => el.Header.Time)
                    .ThenByDescending(el => el.Header.SaveDate)
                    .Take(ProperSavePlugin.MaxSaveSlotsPerCharacter)
                    .ToList();
            }
            catch (Exception ex)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("Couldn't get save metadata for current lobby");
                ProperSavePlugin.InstanceLogger.LogError(ex.ToString());
                return new List<SaveFileMetadata>();
            }
        }

        internal static void PruneCurrentSaveGroup(SaveFileMetadata keepMetadata)
        {
            if (keepMetadata?.Header == null)
            {
                return;
            }

            var keep = SavesMetadata
                .Where(el => IsSameSaveGroup(el, keepMetadata))
                .OrderByDescending(el => el.Header.SaveDate)
                .Take(ProperSavePlugin.MaxSaveSlotsPerCharacter)
                .ToHashSet();

            var toDelete = SavesMetadata
                .Where(el => IsSameSaveGroup(el, keepMetadata) && !keep.Contains(el))
                .ToList();

            foreach (var metadata in toDelete)
            {
                try
                {
                    if (metadata.FilePath.HasValue && ProperSavePlugin.SavesFileSystem.FileExists(metadata.FilePath.Value))
                    {
                        ProperSavePlugin.SavesFileSystem.DeleteFile(metadata.FilePath.Value);
                    }
                }
                catch (Exception ex)
                {
                    ProperSavePlugin.InstanceLogger.LogWarning($"Failed to delete old save file \"{metadata.FileName}\"");
                    ProperSavePlugin.InstanceLogger.LogError(ex);
                }

                SavesMetadata.Remove(metadata);
            }
        }

        internal static bool DeleteSave(SaveFileMetadata metadata)
        {
            if (metadata == null)
            {
                return false;
            }

            try
            {
                if (metadata.FilePath.HasValue && ProperSavePlugin.SavesFileSystem.FileExists(metadata.FilePath.Value))
                {
                    ProperSavePlugin.SavesFileSystem.DeleteFile(metadata.FilePath.Value);
                }

                SavesMetadata.Remove(metadata);
                if (ProperSavePlugin.CurrentSave == metadata)
                {
                    ProperSavePlugin.CurrentSave = null;
                }

                return true;
            }
            catch (Exception ex)
            {
                ProperSavePlugin.InstanceLogger.LogWarning($"Failed to delete save file \"{metadata.FileName}\"");
                ProperSavePlugin.InstanceLogger.LogError(ex);
                return false;
            }
        }

        private static bool TryGetCurrentLobbyIdentity(NetworkUser exceptUser, out List<NetworkUserId> users, out List<BodyIndex> bodies, out string profile, out GameModeIndex gameMode)
        {
            users = NetworkUser.readOnlyInstancesList.Select(el => el.id).ToList();
            bodies = PlayerCharacterMasterController.instances
                .Select(el => el.master?.bodyPrefab ? el.master.bodyPrefab.GetComponent<CharacterBody>()?.bodyIndex ?? BodyIndex.None : BodyIndex.None)
                .Where(el => el != BodyIndex.None)
                .ToList();
            profile = null;
            gameMode = PreGameController.instance ? PreGameController.instance.gameModeIndex : Run.instance ? Run.instance.gameModeIndex : GameModeIndex.Invalid;

            if (exceptUser != null)
            {
                users.Remove(exceptUser.id);
            }

            if (users.Count == 0 || gameMode == GameModeIndex.Invalid)
            {
                return false;
            }

            if (users.Count == 1 && LocalUserManager.readOnlyLocalUsersList.Count > 0)
            {
                profile = System.IO.Path.GetFileNameWithoutExtension(LocalUserManager.readOnlyLocalUsersList[0].userProfile.fileName);
            }

            return true;
        }

        private static bool IsSameSaveGroup(SaveFileMetadata metadata, List<NetworkUserId> users, List<BodyIndex> bodies, string profile, GameModeIndex gameMode)
        {
            if (metadata?.Header == null || metadata.Header.Users == null)
            {
                return false;
            }
            if (metadata.Header.Users.Length != users.Count || metadata.Header.GameMode != gameMode)
            {
                return false;
            }

            if (users.Count == 1)
            {
                return metadata.Header.UserProfileId == profile &&
                       metadata.Header.Users.Length == 1 &&
                       (bodies.Count == 0 || metadata.Header.Users[0].Body == bodies[0]);
            }

            return users.DifferenceCount(metadata.Header.Users.Select(e => e?.UserId?.Load() ?? default)) == 0 &&
                   (bodies.Count == 0 || bodies.DifferenceCount(metadata.Header.Users.Select(e => e?.Body ?? BodyIndex.None)) == 0);
        }

        private static bool IsSameSaveGroup(SaveFileMetadata left, SaveFileMetadata right)
        {
            if (left?.Header == null || right?.Header == null)
            {
                return false;
            }
            if (left.Header.GameMode != right.Header.GameMode || left.Header.Users.Length != right.Header.Users.Length)
            {
                return false;
            }
            if (left.Header.Users.Length == 1)
            {
                return left.Header.UserProfileId == right.Header.UserProfileId &&
                       left.Header.Users[0].Body == right.Header.Users[0].Body;
            }

            return left.Header.Users.Select(e => e?.UserId?.Load() ?? default)
                       .DifferenceCount(right.Header.Users.Select(e => e?.UserId?.Load() ?? default)) == 0 &&
                   left.Header.Users.Select(e => e?.Body ?? BodyIndex.None)
                       .DifferenceCount(right.Header.Users.Select(e => e?.Body ?? BodyIndex.None)) == 0;
        }

        internal static void PopulateSavesMetadata()
        {
            if (!ProperSavePlugin.SavesFileSystem.DirectoryExists(ProperSavePlugin.SavesPath))
            {
                ProperSavePlugin.SavesFileSystem.CreateDirectory(ProperSavePlugin.SavesPath);
                return;
            }

            SavesMetadata.Clear();
            var metadatas = new List<SaveFileMetadata>();
            foreach (var filePath in ProperSavePlugin.SavesFileSystem.EnumerateFiles(ProperSavePlugin.SavesPath, "*.bin"))
            {
                try
                {
                    var metadata = new SaveFileMetadata();
                    metadata.FileName = filePath.GetNameWithoutExtension();
                    metadata.ReadHeader();

                    metadatas.Add(metadata);
                }
                catch (Exception e)
                {
                    ProperSavePlugin.InstanceLogger.LogWarning($"Failed to load save file \"{filePath.GetName()}\"");
                    ProperSavePlugin.InstanceLogger.LogError(e);
                }
            }
            SavesMetadata.AddRange(metadatas
                .OrderByDescending(el => el.Header.ContentHash == ProperSavePlugin.ContentHash)
                .ThenByDescending(el => el.Header.SaveDate));
        }

        internal static void Replace(SaveFileMetadata metadata, SaveFileMetadata oldMetadata)
        {
            if (oldMetadata != null)
            {
                SavesMetadata.Remove(oldMetadata);
            }

            SavesMetadata.Insert(0, metadata);
        }

        internal void Write(bool resilient)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new BinaryWriter(memoryStream);

            Header.Write(writer, resilient);
            Body.Write(writer, resilient);

            //Write to file from MemoryStream so that in case of an exception during saving, the old file will be intact
            using var fileStream = ProperSavePlugin.SavesFileSystem.OpenFile(FilePath.Value, FileMode.Create, FileAccess.Write);
            memoryStream.Seek(0, SeekOrigin.Begin);
            memoryStream.CopyTo(fileStream);
        }

        internal void ReadBody()
        {
            if (Body != null)
            {
                return;
            }

            using var fileStream = ProperSavePlugin.SavesFileSystem.OpenFile(FilePath.Value, FileMode.Open, FileAccess.Read);
            fileStream.Seek(BodyOffset, SeekOrigin.Begin);
            using var reader = new BinaryReader(fileStream);

            Body = SaveFile.Read(reader);
        }

        internal void ReadHeader()
        {
            if (Header != null)
            {
                return;
            }

            using var fileStream = ProperSavePlugin.SavesFileSystem.OpenFile(FilePath.Value, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fileStream);

            Header = SaveFileHeader.Read(reader);
            BodyOffset = fileStream.Position;
            ForceLoad = false;
        }

        internal void ReadForce(string path)
        {
            using var fileStream = File.Open(path, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(fileStream);

            Header = SaveFileHeader.Read(reader);
            BodyOffset = fileStream.Position;
            Body = SaveFile.Read(reader);
            ForceLoad = true;
        }
    }
}
