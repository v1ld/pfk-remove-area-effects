// Copyright (c) 2019 v1ld.git@gmail.com
// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

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
            if (Game.Instance.Player.ControllableCharacters.Any(unit => unit.IsInCombat))
            {
                NotifyPlayer("Cannot remove area effects in combat.", true);
                return;
            }

            var areaEffects = Game.Instance.State.AreaEffects.Where(area => CanDismiss(area));
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
            NotifyPlayer($"Removed {SummarizeCountDictionary(effectsCount)} area effect" + (areaEffects.Count() == 1 ? "" : "s") + ".");
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
            "f4dc3f53090627945b83f16ebf3146a6", // Acid Fog
            "e122151e93e44e0488521aed9e51b617", // Acid Pit
            "cae4347a512809e4388fb3949dc0bc67", // Blade Barrier
            "6c116b31887c6284fbd41c070f6422f6", // Cloak of Dreams
            "6df1ac314d4e6e9418e34470b79f90d8", // Cloud Kill
            "cf742a1d377378e4c8799f6a3afff1ba", // Create Pit
            "bcb6329cefc66da41b011299a43cc681", // Entangle
            "d46313be45054b248a1f1656ddb38614", // Grease
            "d086b1aeb367a5b43808d34c321955d1", // Hungry Pit
            "4c695315962bf9a4ea7fc7e2bb3e2f60", // Ice Storm
            "6b2b1ba6ec6487f46b8c76b603abba6b", // Ice Storm (shadow)
            "e09010a73354a794293ebc7b33c2d130", // Obscuring Mist
            "d64b08ae01012e34cbc55b3a27ea29b7", // Obsidian Flow
            "72328360f1eeeb94d8a43d51db96eccb", // Sickening Entanglement
            "b21bc337e2beaa74b8823570cd45d6dd", // Sirocco
            "bb87c7513a16b9a44b4948a4e932a81b", // Sirocco (shadow)
            "16e0e4c6a16f68c49832340b93706499", // Spike Growth
            "beccc33f543b1f8469c018982c23ac06", // Spiked Pit
            "aa2e0a0fe89693f4e9205fd52c5ba3e5", // Stinking Cloud
            "1d649d8859b25024888966ba1cc291d1", // Volcanic Storm
            "1f45c8b0a735097439a9dac04f5b0161", // Volcanic Storm (shadow)
            "fd323c05f76390749a8555b13156813d", // Web

            // Kineticist infusions
            "6ea87a0ff5df41c459d641326f9973d5", // CloudBlizzardBlastArea
            "48aa66d1a15515e40b07bc1f5fb80f64", // CloudSandstormBlastArea
            "35a62ad81dd5ae3478956c61d6cd2d2e", // CloudSteamBlastArea
            "3659ce23ae102ca47a7bf3a30dd98609", // CloudThunderstormBlastArea
            "4b19dd893a4b80a49905903bcd56b9e2", // DeadlyEarthEarthBlastArea
            "c26aa67475bdb64449b0e0be6a9ea823", // DeadlyEarthMagmaBlastArea
            "38a2979db34ad0f45a449e5eb174729f", // DeadlyEarthMetalBlastArea
            "267f19ba174b21e4d9baf30afd589068", // DeadlyEarthMetalBlastAreaRare
            "0af604484b5fcbb41b328750797e3948", // DeadlyEarthMudBlastArea
            "2a90aa7f771677b4e9624fa77697fdc6", // WallAirBlastArea
            "d12f759590ac61b40870a0725b92a985", // WallBlizzardBlastArea
            "f3b3f32b7f9f35b4cb4114d633b6de6d", // WallBlueFlameBlastArea
            "a4d33389f2b7b824889169d227cab729", // WallBlueFlameBlastAreaPure
            "724d174829a1c1949a4a7ba99cfb06a0", // WallChargedWaterBlastArea
            "2414e5c126976604584ebcee90395eee", // WallColdBlastArea
            "af830491079fea141ad5f46e2dcf93cf", // WallEarthBlastArea
            "740b3ba212b5bb448becf202a97cdbf4", // WallElectricBlastArea
            "edb2896d49015434bbbe401ee27338c3", // WallFireBlastArea
            "3b65f77ec33ab764592803685fe6891e", // WallIceBlastArea
            "f92cdd3b43a744f4f8abeacb913c92fb", // WallMagmaBlastArea
            "c6b4fc6e73c25de4f83378c959144dc8", // WallMetalBlastArea
            "9a9895cbb91a15d48a0368ee8d0f650e", // WallMetalBlastAreaRare
            "2cad16fcffefe3240b2d6dc3d33ff580", // WallMudBlastArea
            "182de1c07ecb56d448cd6d3237ae4b81", // WallPlasmaBlastArea
            "2eef9ca9e79968547a01d06d3828e17f", // WallSandstormBlastArea
            "6a64cc20d5820dc4cb3907b36ce6ac13", // WallSteamBlastArea
            "757b40456bbe27a46bbf18a57d64f31b", // WallThunderstormBlastArea
            "bb4ddd5e7d64a4a49ba71fe8275d1553", // WallWaterBlastArea
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
