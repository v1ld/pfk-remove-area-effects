using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
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
  static class DismissAreaEffect
    {
        internal static void Load()
        {
            EventBus.Subscribe(new AreaEffectDismissal());
        }
    }

    // Provides an option to dismiss dismissible area effects.
    class AreaEffectDismissal : ISceneHandler, IPartyHandler
    {
        readonly BlueprintAbility dismiss;

        public AreaEffectDismissal()
        {
            dismiss = Helpers.CreateAbility("DismissAreaEffect", "Dismiss Area Effect",
                "Dismiss an area effect when you are out of combat. You must be within range of the effect. Dismissing an area effect is a standard action that does not provoke attacks of opportunity.\n" +
                "(If this ability is enabled, it means there is a currently active area effect that can be dismissed.)",
                "0346A8E1E7DB985F9D789CF46778B147",
                Helpers.GetIcon("95f7cdcec94e293489a85afdf5af1fd7"), // dismissal
                AbilityType.Extraordinary, // (Ex) so it doesn't provoke an attack of opportunity, and works w/ antimagic field.
                CommandType.Standard,
                AbilityRange.Long, // Note: should be the spell range, but this should be good enough.
                "", "",
                Helpers.Create<DismissAreaEffectLogic>(),
                Helpers.CreateRunActions(Helpers.Create<DismissAreaEffectAction>()));
            dismiss.CanTargetPoint = true;
        }

        private void addDismissAbility(UnitEntityData unit)
        {
            if (!unit.Descriptor.IsPet && !unit.Descriptor.HasFact(dismiss))
            {
                unit.Descriptor.AddFact(dismiss);
            }
        }

        void ISceneHandler.OnAreaDidLoad()
        {
            foreach (var character in Game.Instance.Player.PartyCharacters)
            {
                addDismissAbility(character);
            }
        }

        void ISceneHandler.OnAreaBeginUnloading() { }

        void IPartyHandler.HandleAddCompanion(UnitEntityData unit)
        {
            addDismissAbility(unit);
        }

        void IPartyHandler.HandleCompanionActivated(UnitEntityData unit) { }

        void IPartyHandler.HandleCompanionRemoved(UnitEntityData unit) { }
  }

    public class DismissAreaEffectLogic : GameLogicComponent, IAbilityTargetChecker, IAbilityAvailabilityProvider
    {
        public bool CanTarget(UnitEntityData caster, TargetWrapper target) =>
            GetTargetAreaEffect(caster, target) != null;

        public bool IsAvailableFor(AbilityData ability) =>
            Game.Instance.State.AreaEffects.Any(
              area => !ability.Caster.Unit.IsInCombat && IsAreaEffectSpell(area) && CanDismiss(ability.Caster.Unit, area));

        public string GetReason() =>
            Game.Instance.Player.MainCharacter.Value.IsInCombat ? "Cannot dismiss area effects in combat." : "No area effects to dismiss.";

        internal static void EndTargetAreaEffect(UnitEntityData caster, TargetWrapper target)
        {
            var area = GetTargetAreaEffect(caster, target);
            if (area == null) return;

            area.ForceEnd();
        }

        internal static AreaEffectEntityData GetTargetAreaEffect(UnitEntityData caster, TargetWrapper target) =>
            Game.Instance.State.AreaEffects.FirstOrDefault(
              area => IsAreaEffectSpell(area) && CanDismiss(caster, area) && area.View.Shape.Contains(target.Point));

        internal static bool IsAreaEffectSpell(AreaEffectEntityData area) =>
            area.Blueprint.AffectEnemies && area.Context.SourceAbility?.Type == AbilityType.Spell;

        internal static bool CanDismiss(UnitEntityData caster, AreaEffectEntityData area) =>
            !caster.IsInCombat || dismissibleAreas.Contains(area.Blueprint.AssetGuid);

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
    }

    public class DismissAreaEffectAction : ContextAction
    {
        public override string GetCaption() => "Dismiss caster's area effect spell near target";

        public override void RunAction()
        {
            var context = Context.SourceAbilityContext;
            if (context == null) return;

            DismissAreaEffectLogic.EndTargetAreaEffect(context.Caster, Target);
        }
    }
}