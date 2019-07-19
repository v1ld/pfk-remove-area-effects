// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.GameModes;
using Kingmaker.Utility;
using UnityEngine;
using UnityModManagerNet;

namespace DismissAreaEffect
{
    public class Main
    {
        [Harmony12.HarmonyPatch(typeof(LibraryScriptableObject), "LoadDictionary", new Type[0])]
        static class LibraryScriptableObject_LoadDictionary_Patch
        {
            static void Postfix(LibraryScriptableObject __instance)
            {
                var self = __instance;
                if (Main.library != null) return;
                Main.library = self;

                EnableGameLogging();

#if DEBUG
                // Perform extra sanity checks in debug builds.
                SafeLoad(CheckPatchingSuccess, "Check that all patches are used, and were loaded");
                SafeLoad(SaveCompatibility.CheckCompat, "Check save game compatibility");
                Log.Write("Load finished.");
#endif
            }
        }

        [Harmony12.HarmonyPatch(typeof(UnityModManager.UI), "Update")]
        internal static class UnityModManager_UI_Update_Patch
        {
            private static void Postfix(UnityModManager.UI __instance)
            {
                try
                {
                    if (Game.Instance.CurrentMode == GameModeType.Default || Game.Instance.CurrentMode == GameModeType.Pause)
                    {
                        if (Input.GetKeyUp("l"))
                        {
                            AreaEffectDismissal.DismissAreaEffects();
                        }
                    }
                }
                catch (Exception e)
                {
                  Log.Write($"Key read: {e}");
                }
            }
        }

        internal static LibraryScriptableObject library;

        public static bool enabled;

        public static UnityModManager.ModEntry.ModLogger logger;

        internal static Settings settings;

        static string testedGameVersion = "2.0.5c";

        static Harmony12.HarmonyInstance harmonyInstance;

        static readonly Dictionary<Type, bool> typesPatched = new Dictionary<Type, bool>();
        static readonly List<String> failedPatches = new List<String>();
        static readonly List<String> failedLoading = new List<String>();

        [System.Diagnostics.Conditional("DEBUG")]
        static void EnableGameLogging()
        {
            if (UberLogger.Logger.Enabled) return;

            // Code taken from GameStarter.Awake(). PF:K logging can be enabled with command line flags,
            // but when developing the mod it's easier to force it on.
            var dataPath = ApplicationPaths.persistentDataPath;
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            UberLogger.Logger.Enabled = true;
            var text = Path.Combine(dataPath, "GameLog.txt");
            if (File.Exists(text))
            {
                File.Copy(text, Path.Combine(dataPath, "GameLogPrev.txt"), overwrite: true);
                File.Delete(text);
            }
            UberLogger.Logger.AddLogger(new UberLoggerFile("GameLogFull.txt", dataPath));
            UberLogger.Logger.AddLogger(new UberLoggerFilter(new UberLoggerFile("GameLog.txt", dataPath), UberLogger.LogSeverity.Warning, "MatchLight"));

            UberLogger.Logger.Enabled = true;
        }

        // We don't want one patch failure to take down the entire mod, so they're applied individually.
        //
        // Also, in general the return value should be ignored. If a patch fails, we still want to create
        // blueprints, otherwise the save won't load. Better to have something be non-functional.
        internal static bool ApplyPatch(Type type, String featureName)
        {
            try
            {
                if (typesPatched.ContainsKey(type)) return typesPatched[type];

                var patchInfo = Harmony12.HarmonyMethodExtensions.GetHarmonyMethods(type);
                if (patchInfo == null || patchInfo.Count() == 0)
                {
                    Log.Error($"Failed to apply patch {type}: could not find Harmony attributes");
                    failedPatches.Add(featureName);
                    typesPatched.Add(type, false);
                    return false;
                }
                var processor = new Harmony12.PatchProcessor(harmonyInstance, type, Harmony12.HarmonyMethod.Merge(patchInfo));
                var patch = processor.Patch().FirstOrDefault();
                if (patch == null)
                {
                    Log.Error($"Failed to apply patch {type}: no dynamic method generated");
                    failedPatches.Add(featureName);
                    typesPatched.Add(type, false);
                    return false;
                }
                typesPatched.Add(type, true);
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"Failed to apply patch {type}: {e}");
                failedPatches.Add(featureName);
                typesPatched.Add(type, false);
                return false;
            }
        }

        static void CheckPatchingSuccess()
        {
            // Check to make sure we didn't forget to patch something.
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                var infos = Harmony12.HarmonyMethodExtensions.GetHarmonyMethods(type);
                if (infos != null && infos.Count() > 0 && !typesPatched.ContainsKey(type))
                {
                    Log.Write($"Did not apply patch for {type}");
                }
            }
        }

        static bool Load(UnityModManager.ModEntry modEntry)
        {
            logger = modEntry.Logger;
#if false
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
#endif
            settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            harmonyInstance = Harmony12.HarmonyInstance.Create(modEntry.Info.Id);
            if (!ApplyPatch(typeof(LibraryScriptableObject_LoadDictionary_Patch), "All mod features"))
            {
                // If we can't patch this, nothing will work, so want the mod to turn red in UMM.
                throw Error("Failed to patch LibraryScriptableObject.LoadDictionary(), cannot load mod");
            }
            if (!ApplyPatch(typeof(UnityModManager_UI_Update_Patch), "Key bindings"))
            {
                // If we can't patch this, nothing will work, so want the mod to turn red in UMM.
                throw Error("Failed to patch UnityModManager.UI.Update(), cannot load mod");
            }
            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;
            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (!enabled) return;

            var fixedWidth = new GUILayoutOption[1] { GUILayout.ExpandWidth(false) };
            if (testedGameVersion != GameVersion.GetVersion())
            {
                GUILayout.Label($"<b>This mod was tested against game version {testedGameVersion}, but you are running {GameVersion.GetVersion()}.</b>", fixedWidth);
            }
            if (failedPatches.Count > 0)
            {
                GUILayout.BeginVertical();
                GUILayout.Label("<b>Error: Some patches failed to apply. These features may not work:</b>", fixedWidth);
                foreach (var featureName in failedPatches)
                {
                    GUILayout.Label($"  • <b>{featureName}</b>", fixedWidth);
                }
                GUILayout.EndVertical();
            }
            if (failedLoading.Count > 0)
            {
                GUILayout.BeginVertical();
                GUILayout.Label("<b>Error: Some assets failed to load. Saves using these features won't work:</b>", fixedWidth);
                foreach (var featureName in failedLoading)
                {
                    GUILayout.Label($"  • <b>{featureName}</b>", fixedWidth);
                }
                GUILayout.EndVertical();
            }
        }

        static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        internal static void SafeLoad(Action load, String name)
        {
            try
            {
                load();
            }
            catch (Exception e)
            {
                failedLoading.Add(name);
                Log.Error(e);
            }
        }

        internal static T SafeLoad<T>(Func<T> load, String name)
        {
            try
            {
                return load();
            }
            catch (Exception e)
            {
                failedLoading.Add(name);
                Log.Error(e);
                return default(T);
            }
        }

        internal static Exception Error(String message)
        {
            logger?.Log(message);
            return new InvalidOperationException(message);
        }
    }


    public class Settings : UnityModManager.ModSettings
    {
        public override void Save(UnityModManager.ModEntry modEntry)
        {
            UnityModManager.ModSettings.Save<Settings>(this, modEntry);
        }
    }
}
