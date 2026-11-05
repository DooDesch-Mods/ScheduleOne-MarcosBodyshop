using System;
using System.Reflection;
using DooDesch.Requirements;
using Sideload.Api;

namespace MarcosBodyshop.Shop
{
    /// <summary>
    /// The mods the menu needs. The minimums are the versions in thunderstore/manifest.json, and the checks are the
    /// shared <see cref="Require"/> (Workspace/requirements): they tell the install mistakes apart, name the file and
    /// log the details. MelonLoader only checks that Chassis is there; an older one loads, and the menu then opens
    /// with no categories because the calls it makes into Chassis do not exist yet.
    /// </summary>
    internal static class Requirements
    {
        /// <summary>
        /// A player line when the menu cannot open, otherwise null. Sideload counts as ready once the Sideload shim
        /// bound to the running Sideload; the shim only answers "bound or not", so the shared check finds the cause.
        /// </summary>
        internal static string Problem() =>
            Require.Check(new Need("Sideload") { Ready = () => Surfaces.Available, Explain = LogBridgeFailure });

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
