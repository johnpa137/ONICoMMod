using Harmony;
using UnityEngine;
using STRINGS;

/**
 * Change List:
 * 
 1. algae terrarium now produces oxygen and new algae from water and co2 in the environment
 2. oxygen diffuser, renamed to algae pyrolyser, outputs coal and steam by breaking down algae
 3. updated descriptions for two buildings and algae to reflect changes
 * 
 */

namespace ConservationOfMassMod
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

        static void Postfix(MineralDeoxidizerConfig __instance, ref GameObject go, ref Tag prefab_tag)
        {
            const float carbon_molar_mass = 0.012011f;
            const float hydrogen_molar_mass = 0.001008f;
            const float oxygen_molar_mass = 0.015999f;

            const float glucose_molar_mass = 6.0f * carbon_molar_mass + 12.0f * hydrogen_molar_mass + 6.0f * oxygen_molar_mass;
            const float glucose_moles_consumed = 0.55f / glucose_molar_mass;
            const float water_molar_mass = hydrogen_molar_mass * 2.0f + oxygen_molar_mass;

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

        static void Prefix()
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

        static void Prefix()
        {
            Strings.Get(ELEMENTS.ALGAE.DESC.key).String = DESC.text;
        }
    }

    // changes the algae terrarium so that it doesn't consume water and offer nothing in return
    // 6CO2 + 6H2O + energy => C6H12O6 + 6O2
    [HarmonyPatch(typeof(AlgaeHabitatConfig), "ConfigureBuildingTemplate")]
    public static class CoMAlgaeTerrariumPatch1
    {
        static void Postfix(AlgaeHabitatConfig __instance, ref GameObject go, ref Tag prefab_tag)
        {
            const float duplicant_co2_rate = 0.002f;

            const float carbon_molar_mass = 0.012011f;
            const float hydrogen_molar_mass = 0.001008f;
            const float oxygen_molar_mass = 0.015999f;

            const float glucose_molar_mass = 6.0f * carbon_molar_mass + 12.0f * hydrogen_molar_mass + 6.0f * oxygen_molar_mass;
            const float co2_molar_mass = oxygen_molar_mass * 2.0f + carbon_molar_mass;
            const float water_molar_mass = hydrogen_molar_mass * 2.0f + oxygen_molar_mass;

            const float duplicant_co2_moles_consumed = duplicant_co2_rate / co2_molar_mass;

            const float arbitrary_algae_mass = 0.03f; // needs to be in consumed elements to make game state machine go from the noAlgae state to the noWater state

            foreach (ElementConverter ec in go.GetComponents<ElementConverter>())
            {
                bool polluted_water_storage = false;

                foreach (ElementConverter.OutputElement outputElement in ec.outputElements)
                {
                    if (outputElement.elementHash == SimHashes.DirtyWater)
                    {
                        polluted_water_storage = true;
                        break;
                    }
                }

                // think of the dirty water as dead algae, just a small balancing act so the algae terrarium doesn't overfill
                if (polluted_water_storage)
                {
                    const float output_mass = duplicant_co2_moles_consumed * ((glucose_molar_mass / 6.0f) + water_molar_mass) * 0.25f;

                    ec.consumedElements = new ElementConverter.ConsumedElement[]
                    {
                        new ElementConverter.ConsumedElement(SimHashes.Algae.CreateTag(), duplicant_co2_moles_consumed*(glucose_molar_mass/6.0f)*0.25f),
                        new ElementConverter.ConsumedElement(SimHashes.Water.CreateTag(), duplicant_co2_moles_consumed*water_molar_mass*0.25f)
                    };

                    ec.outputElements = new ElementConverter.OutputElement[]
                    {
                        new ElementConverter.OutputElement(output_mass, SimHashes.DirtyWater, 303.15f, false, true, 0f, 1f, 1f, byte.MaxValue, 0)
                    };
                }
                else
                {
                    ec.consumedElements = new ElementConverter.ConsumedElement[]
                    {
                        new ElementConverter.ConsumedElement(SimHashes.Algae.CreateTag(), arbitrary_algae_mass),
                        new ElementConverter.ConsumedElement(SimHashes.Water.CreateTag(), duplicant_co2_moles_consumed*water_molar_mass),
                        new ElementConverter.ConsumedElement(SimHashes.CarbonDioxide.CreateTag(), duplicant_co2_rate)
                    };

                    ec.outputElements = new ElementConverter.OutputElement[]
                    {
                        new ElementConverter.OutputElement(duplicant_co2_moles_consumed*oxygen_molar_mass*2.0f, SimHashes.Oxygen, 303.15f, false, false, 0f, 1f, 1f, byte.MaxValue, 0),
                        new ElementConverter.OutputElement((duplicant_co2_moles_consumed/6)*(glucose_molar_mass) + arbitrary_algae_mass, SimHashes.Algae, 303.15f, false, true, 0f, 1f, 1f, byte.MaxValue, 0)
                    };
                }
            }

            ElementConsumer elementConsumer = go.AddOrGet<ElementConsumer>();
            elementConsumer.elementToConsume = SimHashes.CarbonDioxide;
            elementConsumer.consumptionRate = duplicant_co2_rate; // same as duplicant production rate
            elementConsumer.consumptionRadius = 3;
            elementConsumer.showInStatusPanel = true;
            elementConsumer.storeOnConsume = true;
            elementConsumer.capacityKG = 360.0f;
            elementConsumer.sampleCellOffset = new Vector3(0f, 1f, 0f);
            elementConsumer.isRequired = false;
        }
    }

    [HarmonyPatch(typeof(AlgaeHabitat.States), "InitializeStates")]
    public static class CoMAlgaeTerrariumPatch2
    {
        const float ceiling_light_per_lux_count = 1.0f / 1800.0f;

        // requires algae to be in light to perform photosynthesis
        static void Postfix(AlgaeHabitat.States __instance)
        {
            __instance.generatingOxygen.Update("GeneratingOxygen", delegate (AlgaeHabitat.SMInstance smi, float dt)
            {
                int num = Grid.PosToCell(smi.master.transform.GetPosition());
                smi.master.lightBonusMultiplier = 1.0f;
                smi.converter.SetWorkSpeedMultiplier(Grid.LightIntensity[num] * 1.0f * smi.master.lightBonusMultiplier * ceiling_light_per_lux_count + 0.5f);
            }, UpdateRate.SIM_200ms, false).QueueAnim("working_loop", true, null).EventTransition(GameHashes.OnStorageChange, __instance.stoppedGeneratingOxygen, (AlgaeHabitat.SMInstance smi) => !smi.HasEnoughMass(GameTags.Water) || !smi.HasEnoughMass(GameTags.Algae) || smi.NeedsEmptying());
        }
    }

    [HarmonyPatch(typeof(LocString), "CreateLocStringKeys")]
    public static class CoMAlgaeTerrariumPatch3
    {
        public static LocString EFFECT = string.Concat(new string[]
        {
                "Does photosynthesis and consumes ",
                UI.FormatAsLink("Water", "WATER"),
                " and ",
                UI.FormatAsLink("Carbon Dioxide", "CARBONDIOXIDE"),
                " to produce ",
                UI.FormatAsLink("Oxygen", "OXYGEN"),
                " and more ",
                UI.FormatAsLink("Algae", "ALGAE"),
                " works faster in ",
                UI.FormatAsLink("Light", "LIGHT"),
                "."
        });

        static void Prefix()
        {
            Strings.Get(BUILDINGS.PREFABS.ALGAEHABITAT.EFFECT.key).String = EFFECT.text;
        }
    }

    [HarmonyPatch(typeof(OuthouseConfig), "ConfigureBuildingTemplate")]
    public static class CoMOuthousePatch1
    {
        const float cycle_seconds = 600.0f;
        const float duplicant_o2_rate = 0.1f;
        const float duplicant_co2_rate = 0.02f;

        static void Postfix(OuthouseConfig __instance, ref GameObject go, ref Tag prefab_tag)
        {
            Toilet toilet = go.AddOrGet<Toilet>();
            toilet.maxFlushes = 15;
            toilet.solidWastePerUse = new Toilet.SpawnInfo(SimHashes.ToxicSand, 6.7f, 0f);
            toilet.solidWasteTemperature = 310.15f;
            toilet.gasWasteWhenFull = new Toilet.SpawnInfo(SimHashes.ContaminatedOxygen, 0.1f, 15f);
        }
    }
}
