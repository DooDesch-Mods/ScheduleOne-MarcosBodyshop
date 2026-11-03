using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MelonLoader;
using MelonLoader.Utils;
using Sideload.Api;

namespace MarcosBodyshop.Shop
{
    /// <summary>
    /// The Chassis version the menu needs. MelonLoader only checks that Chassis is there; an older one loads, and the
    /// menu then opens with no categories because the calls it makes into Chassis do not exist yet.
    /// </summary>
    internal static class Requirements
    {
        // Same as the Chassis dependency in thunderstore/manifest.json.
        internal const string MinChassis = "1.8.0";
        // Same as the Sideload dependency in thunderstore/manifest.json.
        internal const string MinSideload = "1.35.0";

        private static bool _checked;
        private static string _problem;

        /// <summary>A player line when the installed Chassis is too old, otherwise null.</summary>
        internal static string ChassisProblem()
        {
            if (_checked) return _problem;
            _checked = true;
            string installed = null;
            foreach (MelonMod m in MelonMod.RegisteredMelons)
            {
                if (m?.Info != null && string.Equals(m.Info.Name, "Chassis", StringComparison.Ordinal)) { installed = m.Info.Version; break; }
            }
            if (installed == null)
            {
                Core.Log.Warning("[Menu] Chassis is not in the loaded mods - its version is not checked.");
                return _problem = null;
            }
            if (!TryParse(installed, out Version have))
            {
                Core.Log.Warning("[Menu] the Chassis version '" + installed + "' is unreadable - not checked.");
                return _problem = null;
            }
            // A build without a release tag reports 0.0.0-dev; it is built from current source, not an old release.
            if (have == new Version(0, 0, 0))
            {
                Core.Log.Msg("[Menu] Chassis " + installed + " is a development build - its version is not checked.");
                return _problem = null;
            }
            if (have < Version.Parse(MinChassis))
            {
                Core.Log.Error("[Menu] Chassis " + installed + " is too old: the menu needs " + MinChassis + " or newer.");
                return _problem = DooDesch.Localization.L10n.T("Chassis 1.8.0 or newer is missing.");
            }
            Core.Log.Msg("[Menu] Chassis " + installed + " meets the minimum " + MinChassis + ".");
            return _problem = null;
        }

        /// <summary>
        /// A player line when the menu cannot draw, otherwise null. The Sideload shim only answers "bound or not";
        /// this tells the install mistakes apart and logs every copy and file it finds, so the player's line and log
        /// name the cause:
        /// <list type="bullet">
        /// <item>no Sideload file anywhere;</item>
        /// <item>Sideload.dll in a subfolder of Mods (a zip's <c>mods/</c> folder extracted into Mods), which
        /// MelonLoader does not load;</item>
        /// <item>a Sideload assembly loaded from UserLibs or Plugins: it is no mod there and never starts, and an old
        /// one in UserLibs also keeps the one in Mods from loading;</item>
        /// <item>a Sideload older than <see cref="MinSideload"/>, or one whose bridge failed.</item>
        /// </list>
        /// </summary>
        internal static string SideloadProblem()
        {
            bool bound = Surfaces.Available;
            string melonVersion = null, melonPath = null;
            foreach (MelonBase m in MelonBase.RegisteredMelons)
            {
                if (m?.Info == null || !string.Equals(m.Info.Name, "Sideload", StringComparison.Ordinal)) continue;
                melonVersion = m.Info.Version;
                melonPath = m.MelonAssembly?.Location;
                break;
            }
            if (bound && melonVersion != null) return null;

            var copies = new List<string>();
            string strayCopy = null;
            Type bridge = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                AssemblyName name;
                try { name = asm.GetName(); }
                catch (Exception e) { Core.Log.Warning("[Menu] an assembly without a readable name was skipped: " + e.Message); continue; }
                if (!string.Equals(name.Name, "Sideload", StringComparison.OrdinalIgnoreCase)) continue;
                string where;
                try { where = asm.IsDynamic || string.IsNullOrEmpty(asm.Location) ? null : asm.Location; }
                catch (Exception e) { Core.Log.Warning("[Menu] a Sideload assembly has no readable location: " + e.Message); where = null; }
                copies.Add(name.Version + " at " + (where ?? "(no file)"));
                if (where != null && strayCopy == null && !IsStraightInMods(where)) strayCopy = where;
                if (bridge == null)
                {
                    try { bridge = asm.GetType("Sideload.Bridge.SideloadBridge", false); }
                    catch (Exception e) { Core.Log.Warning("[Menu] Sideload at " + (where ?? "(no file)") + " could not be searched for its bridge: " + e.Message); }
                }
            }

            Core.Log.Error("[Menu] Sideload is not usable (bridge " + (bound ? "bound" : "not bound") + "). Loaded mod: "
                + (melonVersion == null ? "none named Sideload" : melonVersion + " from " + (melonPath ?? "an unknown file"))
                + ". Assemblies named Sideload: " + (copies.Count == 0 ? "none" : string.Join("; ", copies)) + ".");
            string file = LogSideloadFiles();

            if (melonVersion == null)
            {
                if (strayCopy != null)
                {
                    Core.Log.Error("[Menu] " + strayCopy + " is loaded as a library, not as a mod: Sideload never starts from there, and a copy in Mods cannot load beside it. Remove it; Sideload.dll belongs straight in Mods.");
                    return DooDesch.Localization.L10n.T("Sideload is loaded from {0}, not from the Mods folder.", Relative(strayCopy));
                }
                if (file != null)
                {
                    Core.Log.Error("[Menu] " + file + " is not loaded: MelonLoader loads mods only straight from the Mods folder.");
                    return DooDesch.Localization.L10n.T("Sideload is not loaded. Move {0} straight into the Mods folder.", Relative(file));
                }
                return DooDesch.Localization.L10n.T("Sideload is not installed.");
            }

            if (TryParse(melonVersion, out Version have) && have != new Version(0, 0, 0) && have < Version.Parse(MinSideload))
            {
                Core.Log.Error("[Menu] Sideload " + melonVersion + " is too old: the menu needs " + MinSideload + " or newer.");
                return DooDesch.Localization.L10n.T("Sideload {0} is too old. The menu needs 1.35.0 or newer.", melonVersion);
            }

            if (copies.Count > 1)
                Core.Log.Error("[Menu] " + copies.Count + " copies of Sideload are loaded - keep only Mods/Sideload.dll.");
            LogBridgeFailure(bridge);
            return DooDesch.Localization.L10n.T("Sideload {0} did not start. Details are in MelonLoader/Latest.log.", melonVersion);
        }

        private static bool IsStraightInMods(string path)
        {
            try
            {
                string dir = Path.GetFullPath(Path.GetDirectoryName(path) ?? "").TrimEnd(Path.DirectorySeparatorChar);
                string mods = Path.GetFullPath(MelonEnvironment.ModsDirectory).TrimEnd(Path.DirectorySeparatorChar);
                return string.Equals(dir, mods, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                Core.Log.Warning("[Menu] the folder of " + path + " could not be compared with Mods: " + e.Message);
                return true;
            }
        }

        /// <summary>A path below the game folder as the player sees it there ("Mods/mods/Sideload.dll").</summary>
        private static string Relative(string path)
        {
            try
            {
                string root = Path.GetFullPath(MelonEnvironment.GameRootDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(path);
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) full = full.Substring(root.Length);
                return full.Replace(Path.DirectorySeparatorChar, '/');
            }
            catch (Exception e)
            {
                Core.Log.Warning("[Menu] " + path + " could not be shortened: " + e.Message);
                return path;
            }
        }

        /// <summary>
        /// Logs every Sideload file under the game's mod folders, wherever a mod manager or a player put it, and
        /// returns the first one that is not straight in Mods (else the first one at all), or null when there is none.
        /// </summary>
        private static string LogSideloadFiles()
        {
            string[] folders = { MelonEnvironment.ModsDirectory, MelonEnvironment.PluginsDirectory, MelonEnvironment.UserLibsDirectory };
            string first = null, stray = null;
            foreach (string folder in folders)
            {
                try
                {
                    if (!Directory.Exists(folder)) continue;
                    foreach (string file in Directory.GetFiles(folder, "Sideload*.dll", SearchOption.AllDirectories))
                    {
                        Core.Log.Msg("[Menu] Sideload file on disk: " + file + " (" + new FileInfo(file).Length + " bytes).");
                        first ??= file;
                        if (stray == null && !IsStraightInMods(file)) stray = file;
                    }
                }
                catch (Exception e) { Core.Log.Warning("[Menu] " + folder + " could not be searched for Sideload: " + e.Message); }
            }
            if (first == null) Core.Log.Error("[Menu] no Sideload file in Mods, Plugins or UserLibs.");
            return stray ?? first;
        }

        /// <summary>The shim swallows the reason its handshake failed; read the bridge once more to log it.</summary>
        private static void LogBridgeFailure(Type bridge)
        {
            if (bridge == null)
            {
                Core.Log.Error("[Menu] the loaded Sideload has no Sideload.Bridge.SideloadBridge.");
                return;
            }
            foreach (string field in new[] { "RegisterApp", "MountSurface" })
            {
                try
                {
                    object v = bridge.GetField(field, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    Core.Log.Error("[Menu] Sideload's bridge field " + field + ": " + (v == null ? "missing" : v.GetType().FullName) + ".");
                }
                catch (Exception e) { Core.Log.Error("[Menu] reading Sideload's bridge field " + field + " failed: " + (e.InnerException ?? e)); }
            }
        }

        // "1.8.1", "1.8.1-beta.2" and "1.8.1.0" all read as 1.8.1.
        private static bool TryParse(string text, out Version version)
        {
            int cut = text.IndexOfAny(new[] { '-', '+', ' ' });
            if (cut >= 0) text = text.Substring(0, cut);
            return Version.TryParse(text, out version);
        }
    }
}
