using System;
using MelonLoader;

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

        // "1.8.1", "1.8.1-beta.2" and "1.8.1.0" all read as 1.8.1.
        private static bool TryParse(string text, out Version version)
        {
            int cut = text.IndexOfAny(new[] { '-', '+', ' ' });
            if (cut >= 0) text = text.Substring(0, cut);
            return Version.TryParse(text, out version);
        }
    }
}
