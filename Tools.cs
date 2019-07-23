// Copyright (c) v1ld.git@gmail.com 2019
// This code is licensed under MIT license (see LICENSE for details)

using Kingmaker.UnitLogic.Abilities.Blueprints;

namespace RemoveAreaEffects
{
    internal static class Tools
    {
        internal static void dumpAreaEffects()
        {
            foreach (var blueprint in Main.library.GetAllBlueprints())
            {
                if (blueprint is BlueprintAbilityAreaEffect)
                {
                    Log.Write(blueprint);
                }
            }
        }
    }
}
