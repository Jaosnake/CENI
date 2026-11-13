using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RoR2;
using UnityEngine;

namespace ProperSave
{
    // The portrait is an optional sidecar, never part of the save binary.
    internal static class PortraitCache
    {
        private static readonly Dictionary<string, Texture2D> missingTextures = new Dictionary<string, Texture2D>();

        internal static void RememberName(SaveFileMetadata save)
        {
            try
            {
                var user = save?.Header?.Users?.Length > 0 ? save.Header.Users[0] : null;
                if (user == null || user.MissingBody || string.IsNullOrEmpty(save.FileName)) return;
                var body = BodyCatalog.GetBodyPrefab(user.Body);
                var survivor = SurvivorCatalog.FindSurvivorDefFromBody(body);
                if (survivor == null || string.IsNullOrEmpty(survivor.displayNameToken)) return;
                var name = Language.GetString(survivor.displayNameToken);
                if (string.IsNullOrWhiteSpace(name) || name == survivor.displayNameToken) return;
                var path = ProperSavePlugin.SavesPath / (save.FileName + ".name.txt");
                var bytes = Encoding.UTF8.GetBytes(name);
                if (bytes.Length > 1024) return;
                using var stream = ProperSavePlugin.SavesFileSystem.OpenFile(path, FileMode.Create, FileAccess.Write);
                stream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("[C.E.N.I] Could not cache character name: " + ex.Message);
            }
        }

        internal static string GetSavedName(SaveFileMetadata save)
        {
            if (save == null || string.IsNullOrEmpty(save.FileName)) return null;
            try
            {
                var path = ProperSavePlugin.SavesPath / (save.FileName + ".name.txt");
                if (!ProperSavePlugin.SavesFileSystem.FileExists(path)) return null;
                using var stream = ProperSavePlugin.SavesFileSystem.OpenFile(path, FileMode.Open, FileAccess.Read);
                if (stream.Length > 1024) return null;
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var name = reader.ReadToEnd();
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
            catch (Exception ex)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("[C.E.N.I] Could not read cached character name: " + ex.Message);
                return null;
            }
        }

        internal static void Remember(SaveFileMetadata save, Texture portrait)
        {
            if (save == null || string.IsNullOrEmpty(save.FileName) || !portrait) return;
            var path = ProperSavePlugin.SavesPath / (save.FileName + ".portrait.png");
            if (ProperSavePlugin.SavesFileSystem.FileExists(path)) return;
            RenderTexture previous = RenderTexture.active;
            RenderTexture render = null;
            Texture2D copy = null;
            try
            {
                render = RenderTexture.GetTemporary(64, 64, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(portrait, render);
                RenderTexture.active = render;
                copy = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                copy.Apply();
                var bytes = copy.EncodeToPNG();
                using var stream = ProperSavePlugin.SavesFileSystem.OpenFile(path, FileMode.Create, FileAccess.Write);
                stream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("[C.E.N.I] Could not cache save portrait: " + ex.Message);
            }
            finally
            {
                RenderTexture.active = previous;
                if (render != null) RenderTexture.ReleaseTemporary(render);
                if (copy) UnityEngine.Object.Destroy(copy);
            }
        }

        internal static Texture GetMissing(SaveFileMetadata save)
        {
            if (save == null || string.IsNullOrEmpty(save.FileName)) return null;
            if (missingTextures.TryGetValue(save.FileName, out var cached)) return cached;
            var path = ProperSavePlugin.SavesPath / (save.FileName + ".portrait.png");
            try
            {
                if (!ProperSavePlugin.SavesFileSystem.FileExists(path)) return null;
                using var stream = ProperSavePlugin.SavesFileSystem.OpenFile(path, FileMode.Open, FileAccess.Read);
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                if (memory.Length > 1024 * 1024) return null;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(memory.ToArray())) { UnityEngine.Object.Destroy(texture); return null; }
                var pixels = texture.GetPixels32();
                for (var i = 0; i < pixels.Length; i++)
                {
                    var gray = (byte)((pixels[i].r * 299 + pixels[i].g * 587 + pixels[i].b * 114) / 1000);
                    pixels[i].r = pixels[i].g = pixels[i].b = gray;
                }
                texture.SetPixels32(pixels);
                texture.Apply();
                missingTextures[save.FileName] = texture;
                return texture;
            }
            catch (Exception ex)
            {
                ProperSavePlugin.InstanceLogger.LogWarning("[C.E.N.I] Could not read cached portrait: " + ex.Message);
                return null;
            }
        }
    }
}
