using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root.Strings.GameLog;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace DismissAreaEffect
{
    class AreaEffectDismissal
    {
        public static void DismissAreaEffects()
        {
            if (Game.Instance.Player.IsInCombat)
            {
                NotifyPlayer("Cannot dismiss area effects in combat.", true);
                return;
            }

            var areaEffects = Game.Instance.State.AreaEffects.Where(area => IsAreaEffectSpell(area) && CanDismiss(area));
            if (areaEffects.Count() == 0)
            {
                NotifyPlayer("No area effects to dismiss.", true);
                return;
            }

            int count = 0;
            foreach (var area in areaEffects)
            {
                area.ForceEnd();
                count++;
            }
            NotifyPlayer($"Dismissed {count} area effect" + (count == 1 ? "" : "s") + ".");
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
    }
}
