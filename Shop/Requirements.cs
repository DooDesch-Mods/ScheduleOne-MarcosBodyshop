using System;
using System.Reflection;
using DooDesch.Requirements;
using Sideload.Api;

namespace MarcosBodyshop.Shop
{
    /// <summary>
    /// The mods the menu needs and the versions it needs them in. The checks themselves are the shared
    /// <see cref="Require"/> (Workspace/requirements): they tell the install mistakes apart, name the file and log
    /// the details. MelonLoader only checks that Chassis is there; an older one loads, and the menu then opens with
    /// no categories because the calls it makes into Chassis do not exist yet.
    /// </summary>
    internal static class Requirements
    {
        // Same as the Chassis dependency in thunderstore/manifest.json.
        internal const string MinChassis = "1.8.0";
        // Same as the Sideload dependency in thunderstore/manifest.json.
        internal const string MinSideload = "1.35.0";

        /// <summary>A player line when the installed Chassis is too old, otherwise null.</summary>
        internal static string ChassisProblem() => Require.Version("Chassis", MinChassis);

        /// <summary>
        /// A player line when the menu cannot draw, otherwise null. Ready means the Sideload shim bound to the
        /// running Sideload; the shim only answers "bound or not", so the shared check finds the cause.
        /// </summary>
        internal static string SideloadProblem() =>
            Require.Installed("Sideload", MinSideload, () => Surfaces.Available, LogBridgeFailure);

        /// <summary>The shim swallows the reason its handshake failed; read the bridge once more to log it.</summary>
        private static void LogBridgeFailure(Assembly sideload)
        {
            Type bridge = sideload?.GetType("Sideload.Bridge.SideloadBridge", false);
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
    }
}
