using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace AdvancedMechUpgrades
{
    public class MechUpgradeCost
    {
        public ThingFilter filter;
        public int count;

        public string Label => filter.Summary;
    }

    public class MechUpgradeOption
    {
        public ThingDef baseDef;
        public ThingDef advancedDef;
        public PawnKindDef advancedKind;
        public RecipeDef recipe;
    }

    internal static class MechUpgradeOptions
    {
        private const string AdvancedDefPrefix = "Mech_Advanced";
        private const string BaseDefPrefix = "Mech_";

        // upgrades cost half of the advanced variant's gestation recipe
        private const float CostFactor = 0.5f;

        private static readonly Dictionary<ThingDef, MechUpgradeOption> ByBaseDef = new Dictionary<ThingDef, MechUpgradeOption>();

        // pairs every Mech_AdvancedX gestation recipe with its Mech_X base. anything that does not
        // follow vanilla's naming gets skipped, which is most mods, and that is fine
        public static void Rebuild()
        {
            ByBaseDef.Clear();
            foreach (RecipeDef recipe in DefDatabase<RecipeDef>.AllDefsListForReading)
            {
                ThingDef advancedDef = recipe.ProducedThingDef;
                if (!IsAdvancedMech(advancedDef))
                {
                    continue;
                }

                ThingDef baseDef = DefDatabase<ThingDef>.GetNamedSilentFail(BaseDefPrefix + advancedDef.defName.Substring(AdvancedDefPrefix.Length));
                if (!IsMech(baseDef) || ByBaseDef.ContainsKey(baseDef))
                {
                    continue;
                }

                PawnKindDef advancedKind = DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(kind => kind.race == advancedDef);
                if (advancedKind == null)
                {
                    continue;
                }

                ByBaseDef.Add(baseDef, new MechUpgradeOption
                {
                    baseDef = baseDef,
                    advancedDef = advancedDef,
                    advancedKind = advancedKind,
                    recipe = recipe
                });
            }
        }

        public static MechUpgradeOption Get(ThingDef baseDef)
        {
            MechUpgradeOption option;
            if (baseDef != null && ByBaseDef.TryGetValue(baseDef, out option))
            {
                return option;
            }
            return null;
        }

        public static int Count => ByBaseDef.Count;

        private static bool IsAdvancedMech(ThingDef def)
        {
            return IsMech(def) && def.defName.StartsWith(AdvancedDefPrefix, StringComparison.Ordinal);
        }

        private static bool IsMech(ThingDef def)
        {
            return def != null && def.race != null && def.race.IsMechanoid;
        }

        // other mods rewrite recipes at startup, so the costs get read fresh every time rather
        // than cached and quietly going stale
        public static List<MechUpgradeCost> BuildCosts(RecipeDef recipe)
        {
            List<MechUpgradeCost> costs = new List<MechUpgradeCost>();
            foreach (IngredientCount ingredient in recipe.ingredients)
            {
                int count = Mathf.CeilToInt(ingredient.GetBaseCount() * CostFactor);
                if (count <= 0)
                {
                    continue;
                }
                costs.Add(new MechUpgradeCost
                {
                    filter = ingredient.filter,
                    count = count
                });
            }
            return costs;
        }
    }
}