#if DEBUG
using System;
using HarmonyLib;
using UnityEngine;

namespace MarcosBodyshop.Mock
{
    /// <summary>
    /// The 'bodyshop' console word. Both SubmitCommand overloads are patched, and the string body calls the list body,
    /// so one submission arrives twice; the frame + signature guard runs it once.
    /// </summary>
    internal static class ConsolePatch
    {
        private static int _lastFrame = -1;
        private static string _lastSignature = "";

        internal static bool TryHandle(string[] parts)
        {
            if (parts.Length == 0 || !parts[0].Equals("bodyshop", StringComparison.OrdinalIgnoreCase)) return false;

            string signature = string.Join(" ", parts);
            if (Time.frameCount == _lastFrame && signature == _lastSignature) return true;
            _lastFrame = Time.frameCount;
            _lastSignature = signature;

            try { GarageMock.Command(parts); }
            catch (Exception e) { Core.Log.Error("[Mock] '" + signature + "' failed: " + e); }
            return true;
        }
    }

    [HarmonyPatch(typeof(Il2CppScheduleOne.Console), nameof(Il2CppScheduleOne.Console.SubmitCommand), new Type[] { typeof(string) })]
    internal static class Console_SubmitCommand_String_Patch
    {
        private static bool Prefix(string args)
        {
            if (string.IsNullOrWhiteSpace(args)) return true;
            return !ConsolePatch.TryHandle(args.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }

    [HarmonyPatch(typeof(Il2CppScheduleOne.Console), nameof(Il2CppScheduleOne.Console.SubmitCommand), new Type[] { typeof(Il2CppSystem.Collections.Generic.List<string>) })]
    internal static class Console_SubmitCommand_List_Patch
    {
        private static bool Prefix(Il2CppSystem.Collections.Generic.List<string> args)
        {
            if (args == null || args.Count == 0) return true;
            var parts = new string[args.Count];
            for (int i = 0; i < args.Count; i++) parts[i] = args[i];
            return !ConsolePatch.TryHandle(parts);
        }
    }
}
#endif
