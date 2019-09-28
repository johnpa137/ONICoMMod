using Harmony;
using UnityEngine;
using STRINGS;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace ConservationOfMass
{
    // in the base game:
    // A bathroom produces 11.7kg of polluted water per use and only consumes 5 kg
    // 5 kg H2O -> 11.7 kg (polluted water?)
    // polluted water is 5/11.7 H2O and 6.7/11.7 something else
    // An outhouse consumes 6.7kg of dirt and produces 6.7 kg of polluted dirt
    // 6.7kg of "dirt"? -> 6.7kg of "polluted dirt"
    // What's in polluted water and polluted dirt?
    // So we can use the bathroom as a base
    // A duplicant will consume a certain amount of mass as food and that mass needs to be conserved
    // so a duplicant's output in the bathroom should be equivalent to their food mass intake and liquid intake (i.e. none)
    // we also have to make sure food is converted properly
    // there's also a duplicant's co2 rate to take into consideration and their o2 intake rate
    // both polluted dirt and water sublimate into polluted oxygen
    // what's in polluted oxygen

    // so a duplicant consumed 100g/s of oxygen and a cycle is 600 seconds
    // so a duplicant consumes 60000g or 60kg of O2 per cycle
    // a duplicant produces 2 g/s of co2 and 1.2kg of co2 per cycle

    // so per cycle a duplicant gains 60kg of O2 - (1.2kg co2) * (o2/co2)
    // and every cycle a duplicant needs to use the bathroom, twice for duplicant's with small bladders
    // every bathroom use should empty the duplicant of their accumulated "O2" and "food mass"

    // so a duplicant needs to keep track of the amount of oxygen they consume as well as the amount of food mass they consume
    // for oxygen we can add to the minionconfig createprefab function a component for tracking oxygen
    // and we need a way to access that from the "Flush" method in a Toilet, which now works, since base of the Worker object is the just the gameobject



    //[HarmonyPatch(typeof(OuthouseConfig), "ConfigureBuildingTemplate")]
    //public static class CoMOuthousePatch1
    //{
    //    const float carbon_molar_mass = 0.012011f;
    //    const float hydrogen_molar_mass = 0.001008f;
    //    const float oxygen_molar_mass = 0.015999f;

    //    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    //    {
    //        List<CodeInstruction> code = instructions.ToList();
    //        foreach (CodeInstruction codeInstruction in code)
    //        {
    //            /* Example:
    //            if (codeInstruction.opcode == OpCodes.Ldc_R4)
    //            {
    //                codeInstruction.operand = 323.15f;
    //            }
    //            */
    //            yield return codeInstruction;
    //        }
    //    }
    //}

    // add the MassConsumptionTracker component
    [HarmonyPatch(typeof(MinionConfig), "CreatePrefab")]
    public static class CoMMinionPatch1
    {
        public static void Postfix(ref GameObject __result)
        {
            __result.AddComponent<Storage>();
        }
    }

    // chaning the Toilet Flush method to use the MassConsumptionTracker added to the MinionConfig CreatePrefab gameobject
    [HarmonyPatch(typeof(Toilet), "Flush")]
    public static class CoMToiletPatch1
    {
        // this will essentially make sure the function doesn't do anything, so are postfix will do the actual method
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();

            foreach (CodeInstruction codeInstruction in code)
            {

                if (codeInstruction.opcode != OpCodes.Ret)
                {
                    codeInstruction.opcode = OpCodes.Nop;
                }

                yield return codeInstruction;
            }
        }

        public static void Postfix(Toilet __instance, ref Worker worker)
        {
            Element element = ElementLoader.FindElementByHash(__instance.solidWastePerUse.elementID);

            byte index = Db.Get().Diseases.GetIndex(__instance.diseaseId);

            Storage massConsumptionTracker = worker.GetComponents<Storage>()[1];

            GameObject go = element.substance.SpawnResource(__instance.transform.GetPosition(), massConsumptionTracker.MassStored(), __instance.solidWasteTemperature, index, __instance.diseasePerFlush, true, false, false);

            Traverse.Create(__instance).Field("storage").Method("Store", go, false, false, true, false).GetValue<GameObject>();
            PrimaryElement component = worker.GetComponent<PrimaryElement>();
            component.AddDisease(index, __instance.diseaseOnDupePerFlush, "Toilet.Flush");
            PopFXManager.Instance.SpawnFX(PopFXManager.Instance.sprite_Resource, string.Format(DUPLICANTS.DISEASES.ADDED_POPFX, Db.Get().Diseases[(int)index].Name, __instance.diseasePerFlush + __instance.diseaseOnDupePerFlush), __instance.transform, Vector3.up, 1.5f, false, false);
            __instance.FlushesUsed++;
            Traverse.Create(__instance).Field("meter").Method("SetPositionPercent", (float)__instance.FlushesUsed/ (float)__instance.maxFlushes).GetValue();
            Tutorial.Instance.TutorialMessage(Tutorial.TutorialMessages.TM_LotsOfGerms, true);
        }
    }

    // changing the Sim200ms method to accumulate oxygen using the mass_consumption_tracker object added to the minion_config prefab
    [HarmonyPatch(typeof(OxygenBreather), "Sim200ms")]
    public static class CoMOxygenBreatherPatch1
    {
        const float carbon_molar_mass = 0.012011f;
        const float hydrogen_molar_mass = 0.001008f;
        const float oxygen_molar_mass = 0.015999f;

        const float o2_fraction_of_co2 = (oxygen_molar_mass * 2.0f) / (carbon_molar_mass + oxygen_molar_mass * 2.0f);

        // this will essentially make sure the function doesn't do anything, so are postfix will do the actual method
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();

            foreach (CodeInstruction codeInstruction in code)
            {

                if (codeInstruction.opcode != OpCodes.Ret)
                {
                    codeInstruction.opcode = OpCodes.Nop;
                }

                yield return codeInstruction;
            }
        }

        // changing oxygen breather to accumulate oxygen
        public static void Postfix(OxygenBreather __instance, ref float dt)
        {
            if (!__instance.gameObject.HasTag(GameTags.Dead))
            {
                float num = Traverse.Create(__instance).Field("airConsumptionRate").Method("GetTotalValue").GetValue<float>() * dt;
                
                bool flag = Traverse.Create(__instance).Field("gasProvider").Method("ConsumeGas", __instance, num).GetValue<bool>();
                if (flag)
                {
                    // add oxygen to storage
                    __instance.GetComponents<Storage>()[1].AddGasChunk(SimHashes.Oxygen, num, Traverse.Create(__instance).Field("temperature").Field("value").GetValue<float>(), byte.MaxValue, 0, false, true);

                    if (Traverse.Create(__instance).Field("gasProvider").Method("ShouldEmitCO2").GetValue<bool>())
                    {
                        float num2 = num * __instance.O2toCO2conversion;

                        Game.Instance.accumulators.Accumulate(Traverse.Create(__instance).Field("co2Accumulator").GetValue<HandleVector<int>.Handle>(), num2);
                        __instance.accumulatedCO2 += num2;
                        if (__instance.accumulatedCO2 >= __instance.minCO2ToEmit)
                        {
                            GameObject o2Chunk = __instance.GetComponents<Storage>()[1].FindFirst(SimHashes.Oxygen.CreateTag());
                            PrimaryElement primaryElementO2 = o2Chunk.GetComponent<PrimaryElement>();
                            primaryElementO2.Mass -= o2_fraction_of_co2 * __instance.minCO2ToEmit;

                            __instance.accumulatedCO2 -= __instance.minCO2ToEmit;
                            Vector3 position = __instance.transform.GetPosition();
                            position.x += ((!Traverse.Create(__instance).Field("facing").GetValue<Facing>().GetFacing()) ? __instance.mouthOffset.x : (-__instance.mouthOffset.x));
                            position.y += __instance.mouthOffset.y;
                            position.z -= 0.5f;
                            CO2Manager.instance.SpawnBreath(position, __instance.minCO2ToEmit, Traverse.Create(__instance).Field("temperature").Field("value").GetValue<float>());
                        }
                    }
                    else if (Traverse.Create(__instance).Field("gasProvider").Method("ShouldStoreCO2").GetValue<bool>())
                    {
                        Equippable equippable = __instance.GetComponent<SuitEquipper>().IsWearingAirtightSuit();
                        if (equippable != null)
                        {
                            float num3 = num * __instance.O2toCO2conversion;
                            Game.Instance.accumulators.Accumulate(Traverse.Create(__instance).Field("co2Accumulator").GetValue<HandleVector<int>.Handle>(), num3);
                            __instance.accumulatedCO2 += num3;
                            if (__instance.accumulatedCO2 >= __instance.minCO2ToEmit)
                            {
                                GameObject o2Chunk = __instance.GetComponents<Storage>()[1].FindFirst(SimHashes.Oxygen.CreateTag());
                                PrimaryElement primaryElementO2 = o2Chunk.GetComponent<PrimaryElement>();
                                primaryElementO2.KeepZeroMassObject = true; // makes sure the mass doesn't go negative
                                primaryElementO2.Mass -= o2_fraction_of_co2 * __instance.minCO2ToEmit; // subtract o2 in co2 from oxygen storage

                                __instance.accumulatedCO2 -= __instance.minCO2ToEmit;
                                equippable.GetComponent<Storage>().AddGasChunk(SimHashes.CarbonDioxide, __instance.minCO2ToEmit, Traverse.Create(__instance).Field("temperature").Field("value").GetValue<float>(), byte.MaxValue, 0, false, true);
                            }
                        }
                    }
                }
                if (flag != Traverse.Create(__instance).Field("hasAir").GetValue<bool>())
                {
                    Traverse.Create(__instance).Field("hasAirTimer").Method("Start").GetValue();
                    if (Traverse.Create(__instance).Field("hasAirTimer").Method("TryStop", 2f).GetValue<bool>())
                    {
                        Traverse.Create(__instance).Field("hasAir").SetValue(flag);
                    }
                }
                else
                {
                    Traverse.Create(__instance).Field("hasAirTimer").Method("Stop").GetValue();
                }
            }
        }
    }
}
