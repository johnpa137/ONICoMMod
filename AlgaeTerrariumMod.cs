using Harmony;
using UnityEngine;
using STRINGS;

namespace ConservationOfMass
{
    // changes the algae terrarium so that it doesn't consume water and offer nothing in return
    // 6CO2 + 6H2O + energy => C6H12O6 + 6O2

    // algae terrarium, may need work depending on how I want to handle polluted water and polluted dirt and polluted oxygen
    // like what I want their contents to actually be element-wise and mass-wise
    [HarmonyPatch(typeof(AlgaeHabitatConfig), "ConfigureBuildingTemplate")]
    public static class CoMAlgaeTerrariumPatch1
    {
        public static void Postfix(AlgaeHabitatConfig __instance, ref GameObject go, ref Tag prefab_tag)
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
        public static void Postfix(AlgaeHabitat.States __instance)
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

        public static void Prefix()
        {
            Strings.Get(BUILDINGS.PREFABS.ALGAEHABITAT.EFFECT.key).String = EFFECT.text;
        }
    }
}
