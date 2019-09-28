using Harmony;
using UnityEngine;
using STRINGS;

// Dev notes
/**
 * Pretty happy with the oxygendiffuser/algaepyrolyser
 * algae terrarium, may need work depending on how I want to handle polluted water and polluted dirt
 */

/**
 * Change List:
 * 
 1. algae terrarium now produces oxygen and new algae from water and co2 in the environment
 2. oxygen diffuser, renamed to algae pyrolyser, outputs coal and steam by breaking down algae
 3. updated descriptions for two buildings and algae to reflect changes
 * 
 */

namespace ConservationOfMass
{
    // changes the outputs of the oxygen diffuser to carbon/char/coal and steam
    // C6H12O2
    [HarmonyPatch(typeof(MineralDeoxidizerConfig), "ConfigureBuildingTemplate")]
    public static class CoMOxygenDiffuserPatch1
    {
        const float carbon_molar_mass = 0.012011f;
        const float hydrogen_molar_mass = 0.001008f;
        const float oxygen_molar_mass = 0.015999f;

        const float glucose_molar_mass = 6.0f * carbon_molar_mass + 12.0f * hydrogen_molar_mass + 6.0f * oxygen_molar_mass;
        const float glucose_moles_consumed = 0.55f / glucose_molar_mass;
        const float water_molar_mass = hydrogen_molar_mass * 2.0f + oxygen_molar_mass;

        public static void Postfix(MineralDeoxidizerConfig __instance, ref GameObject go, ref Tag prefab_tag)
        {
            ElementConverter elementConverter = go.AddOrGet<ElementConverter>();
            elementConverter.outputElements = new ElementConverter.OutputElement[]
            {
                new ElementConverter.OutputElement(6.0f*carbon_molar_mass*glucose_moles_consumed, SimHashes.Carbon, 303.15f, false, true, 0f, 1f, 1f, byte.MaxValue, 0),
                new ElementConverter.OutputElement(6.0f*water_molar_mass*glucose_moles_consumed, SimHashes.Steam, 373.15f, false, false, 0f, 1f, 1f, byte.MaxValue, 0)
            };
        }
    }

    // changes the name of the oxygen diffuser into the algae pyrolyser and the description and effect text to reflect the change
    [HarmonyPatch(typeof(LocString), "CreateLocStringKeys")]
    public static class CoMOxygenDiffuserPatch2
    {
        public static LocString NAME = UI.FormatAsLink("Algae Pyrolyser", "MINERALDEOXIDIZER");
        public static LocString DESC = "Algae Pyrolysers break down the cellulose and sugars in algae into its constituents of carbon and Water.";
        public static LocString EFFECT = string.Concat(new string[]
        {
                "Converts large amounts of ",
                UI.FormatAsLink("Algae", "ALGAE"),
                " into ",
                UI.FormatAsLink("Charcoal", "CARBON"),
                " and ",
                UI.FormatAsLink("Steam", "STEAM"),
                ".\n\nBecomes idle when the area reaches maximum pressure capacity."
        });

        public static void Prefix()
        {
            Strings.Get(BUILDINGS.PREFABS.MINERALDEOXIDIZER.NAME.key).String = NAME.text;
            Strings.Get(BUILDINGS.PREFABS.MINERALDEOXIDIZER.DESC.key).String = DESC.text;
            Strings.Get(BUILDINGS.PREFABS.MINERALDEOXIDIZER.EFFECT.key).String = EFFECT.text;
        }
    }

    [HarmonyPatch(typeof(LocString), "CreateLocStringKeys")]
    public static class CoMOxygenDiffuserPatch3
    {
        public static LocString DESC = "Algae is a cluster of non-motile, single-celled lifeforms.\n\nIt can broken down into " + UI.FormatAsLink("Water", "WATER") + " and " + UI.FormatAsLink("Carbon", "CARBON") + " when used in a " + UI.FormatAsLink("Algae Pyrolyser", "MINERALDEOXIDIZER");
        public static void Prefix()
        {
            Strings.Get(ELEMENTS.ALGAE.DESC.key).String = DESC.text;
        }
    }
}
