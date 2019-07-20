using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints.Root.Strings.GameLog;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Parts;

namespace RemoveAreaEffects
{
    class RemoveAreaEffects
    {
        public static void Run()
        {
            if (!(Main.settings.DismissAllowedInCombat && Main.settings.DismissInsteadOfWait))
            {
                if (Game.Instance.Player.ControllableCharacters.Any(unit => unit.IsInCombat))
                {
                    NotifyPlayer("Cannot remove area effects in combat.", true);
                    return;
                }
            }

            var areaEffects = Game.Instance.State.AreaEffects.Where(area => IsAreaEffectSpell(area) && CanDismiss(area));
            if (areaEffects.Count() == 0)
            {
                NotifyPlayer("No area effects to remove.", true);
                return;
            }

            if (Main.settings.DismissInsteadOfWait)
            {
                DoDismissAreaEffects(areaEffects);
            }
            else
            {
                DoWaitOutAreaEffects(areaEffects);
            }

            var effectsCount = (from effect in areaEffects
                                group effect by SpellNameForAreaEffect(effect.Blueprint.ToString()) into g
                                select new { Effect = g.Key, Count = g.Count() }).ToDictionary(g => g.Effect, g => g.Count);
            NotifyPlayer($"Dismissed {SummarizeCountDictionary(effectsCount)} area effect" + (areaEffects.Count() == 1 ? "" : "s") + ".");
        }

        private static void DoDismissAreaEffects(IEnumerable<AreaEffectEntityData> areaEffects)
        {
            foreach (var area in areaEffects)
            {
                area.ForceEnd();
            }
        }

        private static void DoWaitOutAreaEffects(IEnumerable<AreaEffectEntityData> areaEffects)
        {
            TimeSpan lastEndingTime = areaEffects.Max(area => Helpers.GetField<TimeSpan>(area, "m_CreationTime") + Helpers.GetField<TimeSpan>(area, "m_Duration"));
            TimeSpan waitTime = lastEndingTime - Game.Instance.Player.GameTime;

            Game.Instance.AdvanceGameTime(waitTime);

            if (Main.settings.WaitingIgnoresFatigue)
            {
                int hours = waitTime.Hours + 1;
                foreach (var character in Game.Instance.Player.Party)
                {
                    character.Ensure<UnitPartWeariness>().AddWearinessHours((float)(-hours));
                }
            }
        }

        static bool IsAreaEffectSpell(AreaEffectEntityData area) =>
            area.Blueprint.AffectEnemies && area.Context.SourceAbility?.Type == AbilityType.Spell;

        internal static bool CanDismiss(AreaEffectEntityData area) =>
            dismissibleAreas.Contains(area.Blueprint.AssetGuid);

        static readonly HashSet<string> dismissibleAreas = new HashSet<string> {
            "cae4347a512809e4388fb3949dc0bc67", // Blade Barrier
            "6c116b31887c6284fbd41c070f6422f6", // Cloak of Dreams
            "bcb6329cefc66da41b011299a43cc681", // Entangle
            "d46313be45054b248a1f1656ddb38614", // Grease
            "4c695315962bf9a4ea7fc7e2bb3e2f60", // Ice Storm
            "6b2b1ba6ec6487f46b8c76b603abba6b", // Ice Storm (shadow)
            "e09010a73354a794293ebc7b33c2d130", // Obscuring Mist
            "d64b08ae01012e34cbc55b3a27ea29b7", // Obsidian Flow
            "b21bc337e2beaa74b8823570cd45d6dd", // Sirocco
            "bb87c7513a16b9a44b4948a4e932a81b", // Sirocco (shadow)
            "16e0e4c6a16f68c49832340b93706499", // Spike Growth
            "1d649d8859b25024888966ba1cc291d1", // Volcanic Storm
            "1f45c8b0a735097439a9dac04f5b0161", // Volcanic Storm (shadow)
            "fd323c05f76390749a8555b13156813d", // Web
        };

        internal static void NotifyPlayer(string message, bool warning = false)
        {
            if (warning)
            {
                EventBus.RaiseEvent<IWarningNotificationUIHandler>((IWarningNotificationUIHandler h) => h.HandleWarning(message, true));
            }
            else
            {
                Game.Instance.UI.BattleLogManager.LogView.AddLogEntry(message, GameLogStrings.Instance.DefaultColor);
            }
        }

        // "1 Web, 2 Obsidian Flow and 1 Grease"
        private static string SummarizeCountDictionary(Dictionary<string,int> counts)
        {
            string[] keys = counts.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = $"{counts[keys[i]]} {keys[i]}";
            }
            int n = keys.Length;
            return n == 1 ? keys[0] : (string.Join(", ", keys, 0, n-1) + " and " + keys[n-1]);
        }

        // Put spaces before capitals and drop the last word
        // "ObsidianFlowArea" => "Obsidian Flow"
        private static string SpellNameForAreaEffect(string name)
        {
            char[] spaced = new char[name.Length * 2];
            int last = 0, lastSpace = 0;
            for (int i = 0; i < name.Length; ++i)
            {
                if (char.IsUpper(name[i]) && i > 0)
                {
                    lastSpace = last;
                    spaced[last++] = ' ';
                }
                spaced[last++] = name[i];
            }
            return new string(spaced, 0, lastSpace);
        }
    }
}
