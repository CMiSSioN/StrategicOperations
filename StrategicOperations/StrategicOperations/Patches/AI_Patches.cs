﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using BattleTech;
using BattleTech.DataObjects;
using BattleTech.UI;
using CustomUnits;
using Harmony;
using StrategicOperations.Framework;
using UnityEngine;
using static StrategicOperations.Framework.Classes;

namespace StrategicOperations.Patches
{
    public class AI_Patches
    {

        [HarmonyPatch(typeof(Team), "AddUnit", new Type[] {typeof(AbstractActor)})]
        public static class Team_AddUnit_Patch
        {
            public static void Postfix(Team __instance, AbstractActor unit)
            {
                if (__instance.IsLocalPlayer)
                {
                    if (unit is Mech && !(unit is TrooperSquad))
                    {
                        if (!string.IsNullOrEmpty(ModInit.modSettings.BattleArmorDeSwarmSwat))
                        {
                            if (unit.GetPilot().Abilities
                                    .All(x => x.Def.Id != ModInit.modSettings.BattleArmorDeSwarmSwat) &&
                                unit.ComponentAbilities.All(y =>
                                    y.Def.Id != ModInit.modSettings.BattleArmorDeSwarmSwat))
                            {
                                unit.Combat.DataManager.AbilityDefs.TryGet(ModInit.modSettings.BattleArmorDeSwarmSwat, out var def);
                                var ability = new Ability(def);
                                ModInit.modLog.LogTrace(
                                    $"Adding {ability.Def?.Description?.Id} to {unit.Description?.Name}.");
                                ability.Init(unit.Combat);
                                unit.GetPilot().Abilities.Add(ability);
                                unit.GetPilot().ActiveAbilities.Add(ability);
                            }
                        }

                        if (!string.IsNullOrEmpty(ModInit.modSettings.BattleArmorDeSwarmRoll))
                        {
                            if (unit.GetPilot().Abilities
                                    .All(x => x.Def.Id != ModInit.modSettings.BattleArmorDeSwarmRoll) &&
                                unit.ComponentAbilities.All(y =>
                                    y.Def.Id != ModInit.modSettings.BattleArmorDeSwarmRoll))
                            {
                                unit.Combat.DataManager.AbilityDefs.TryGet(ModInit.modSettings.BattleArmorDeSwarmRoll, out var def);
                                var ability = new Ability(def);
                                ModInit.modLog.LogTrace(
                                    $"Adding {ability.Def?.Description?.Id} to {unit.Description?.Name}.");
                                ability.Init(unit.Combat);
                                unit.GetPilot().Abilities.Add(ability);
                                unit.GetPilot().ActiveAbilities.Add(ability);
                            }
                        }

                    }
                    return;
                }

                if (unit is Mech mech)
                {
                    if (mech.EncounterTags.Contains("SpawnedFromAbility")) return;
                }
                AI_Utils.GenerateAIStrategicAbilities(unit);
            }
        }

        [HarmonyPatch(typeof(PreferFarthestAwayFromClosestHostilePositionFactor), "EvaluateInfluenceMapFactorAtPosition",
        new Type[] {typeof(AbstractActor), typeof(Vector3), typeof(float), typeof(MoveType), typeof(PathNode) })]
        public static class PreferFarthestAwayFromClosestHostilePositionFactor_EvaluateInfluenceMapFactorAtPosition_BattleArmor
        {
            public static bool Prefix(PreferFarthestAwayFromClosestHostilePositionFactor __instance, AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused, ref float __result)
            {
                if (unit.HasMountedUnits() ||
                    unit.ComponentAbilities.Any(x => x.Def.Id == ModInit.modSettings.BattleArmorMountAndSwarmID))
                {
                    var result = 9001 * (1/unit.DistanceToClosestDetectedEnemy(position));
                    ModInit.modLog.LogDev($"[PreferFarthestAwayFromClosestHostilePositionFactor] Actor {unit.DisplayName} evaluating position {position}, should return {result}");
                    __result = result;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PreferLowerMovementFactor), "EvaluateInfluenceMapFactorAtPosition",
            new Type[] { typeof(AbstractActor), typeof(Vector3), typeof(float), typeof(MoveType), typeof(PathNode) })]
        public static class PreferLowerMovementFactor_EvaluateInfluenceMapFactorAtPosition_BattleArmor
        {
            public static bool Prefix(PreferFarthestAwayFromClosestHostilePositionFactor __instance, AbstractActor unit, Vector3 position, float angle, MoveType moveType_unused, PathNode pathNode_unused, ref float __result)
            {
                if (unit.HasMountedUnits() ||
                    unit.ComponentAbilities.Any(x => x.Def.Id == ModInit.modSettings.BattleArmorMountAndSwarmID))
                {
                    var result = 9001 * (1 / unit.DistanceToClosestDetectedEnemy(position));
                    ModInit.modLog.LogDev($"[PreferLowerMovementFactor] Actor {unit.DisplayName} evaluating position {position}, should return {result}");
                    __result = result;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PreferOptimalDistanceToAllyFactor), "EvaluateInfluenceMapFactorAtPositionWithAlly",
            new Type[] { typeof(AbstractActor), typeof(Vector3), typeof(float), typeof(ICombatant)})]
        public static class PreferOptimalDistanceToAllyFactor_EvaluateInfluenceMapFactorAtPosition_BattleArmor
        {
            public static bool Prefix(PreferFarthestAwayFromClosestHostilePositionFactor __instance, AbstractActor unit, Vector3 position, float angle, ICombatant allyUnit, ref float __result)
            {
                if (unit.HasMountedUnits() ||
                    unit.ComponentAbilities.Any(x => x.Def.Id == ModInit.modSettings.BattleArmorMountAndSwarmID))
                {
                    var result = 9001 * (1 / unit.DistanceToClosestDetectedEnemy(position));
                    ModInit.modLog.LogDev($"[PreferOptimalDistanceToAllyFactor] Actor {unit.DisplayName} evaluating position {position}, should return {result}");
                    __result = result;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PreferHigherExpectedDamageToHostileFactor), "EvaluateInfluenceMapFactorAtPositionWithHostile",
            new Type[] { typeof(AbstractActor), typeof(Vector3), typeof(float), typeof(MoveType), typeof(ICombatant) })]
        public static class PreferHigherExpectedDamageToHostileFactor_EvaluateInfluenceMapFactorAtPosition_BattleArmor
        {
            public static bool Prefix(PreferFarthestAwayFromClosestHostilePositionFactor __instance, AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit, ref float __result)
            {
                if (unit.HasMountedUnits() ||
                    unit.ComponentAbilities.Any(x => x.Def.Id == ModInit.modSettings.BattleArmorMountAndSwarmID))
                {
                    var result = 9001 * (1 / unit.DistanceToClosestDetectedEnemy(position));
                    ModInit.modLog.LogDev($"[PreferFarthestAwayFromClosestHostilePositionFactor] Actor {unit.DisplayName} evaluating position {position}, should return {result}");
                    __result = result;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PreferNoCloserThanMinDistToHostileFactor), "EvaluateInfluenceMapFactorAtPositionWithHostile",
            new Type[] { typeof(AbstractActor), typeof(Vector3), typeof(float), typeof(MoveType), typeof(ICombatant) })]
        public static class PreferNoCloserThanMinDistToHostileFactor_EvaluateInfluenceMapFactorAtPosition_BattleArmor
        {
            public static bool Prefix(PreferNoCloserThanMinDistToHostileFactor __instance, AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit, ref float __result)
            {
                if (unit.HasMountedUnits() ||
                    unit.ComponentAbilities.Any(x => x.Def.Id == ModInit.modSettings.BattleArmorMountAndSwarmID))
                {
                    var result = 9001 * (1 / unit.DistanceToClosestDetectedEnemy(position));
                    ModInit.modLog.LogDev($"[PreferNoCloserThanMinDistToHostileFactor] Actor {unit.DisplayName} evaluating position {position}, should return {result}");
                    __result = result;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PreferOptimalDistanceToHostileFactor), "EvaluateInfluenceMapFactorAtPositionWithHostile",
            new Type[] { typeof(AbstractActor), typeof(Vector3), typeof(float), typeof(MoveType), typeof(ICombatant) })]
        public static class PreferOptimalDistanceToHostileFactor_EvaluateInfluenceMapFactorAtPosition_BattleArmor
        {
            public static bool Prefix(PreferOptimalDistanceToHostileFactor __instance, AbstractActor unit, Vector3 position, float angle, MoveType moveType, ICombatant hostileUnit, ref float __result)
            {
                if (unit.HasMountedUnits() ||
                    unit.ComponentAbilities.Any(x => x.Def.Id == ModInit.modSettings.BattleArmorMountAndSwarmID))
                {
                    var result = 9001 * (1 / unit.DistanceToClosestDetectedEnemy(position));
                    __result = result;
                    ModInit.modLog.LogDev($"[PreferOptimalDistanceToHostileFactor] Actor {unit.DisplayName} evaluating position {position}, should return {result}");
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch]
        public static class CanMoveAndShootWithoutOverheatingNode_Tick_Patch  // may need to be CanMoveAndShootWithoutOverheatingNode. was IsMovementAvailableForUnitNode
        {
            public static MethodBase TargetMethod()
            {
                var type = AccessTools.TypeByName("CanMoveAndShootWithoutOverheatingNode");
                return AccessTools.Method(type, "Tick");
            }
            
            public static bool Prefix(ref BehaviorTreeResults __result, string ___name,
                AbstractActor ___unit)
            {
                if (!___unit.Combat.TurnDirector.IsInterleaved) return true;

                var battleArmorAbility =
                    ___unit.ComponentAbilities.FirstOrDefault(x => x.Def.Id == ModInit.modSettings.BattleArmorMountAndSwarmID);
                if (battleArmorAbility != null)
                {
                    if (battleArmorAbility.IsAvailable)
                    {
                        if (___unit.IsSwarmingUnit())
                        {
                            ModInit.modLog.LogTrace($"[CanMoveAndShootWithoutOverheatingNode] Actor {___unit.DisplayName} is currently swarming. Doing nothing.");
                            //if currently swarming, dont do anything else.
                            __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                            return false;
                        }

                        var closestEnemy = ___unit.GetClosestDetectedEnemy(___unit.CurrentPosition);

                        var distance = Vector3.Distance(___unit.CurrentPosition, closestEnemy.CurrentPosition);
                        var jumpdist = 0f;
                        if (___unit is Mech mech)
                        {
                            jumpdist = mech.JumpDistance;
                        }

                        var maxRange = new List<float>()
                        {
                            ___unit.MaxWalkDistance,
                            ___unit.MaxSprintDistance,
                            jumpdist,
                            battleArmorAbility.Def.IntParam2
                        }.Max();

                        if (___unit.IsMountedUnit())
                        {
                            ModInit.modLog.LogTrace($"[CanMoveAndShootWithoutOverheatingNode] Actor {___unit.DisplayName} is currently mounted. Evaluating range to nearest enemy.");
                            if (distance <= 1.25 * maxRange)
                            {
                                ModInit.modLog.LogTrace($"[CanMoveAndShootWithoutOverheatingNode] Actor {___unit.DisplayName} is {distance} from nearest enemy, maxrange was {maxRange} * 1.25.");
                                var carrier = ___unit.Combat.FindActorByGUID(ModState.PositionLockMount[___unit.GUID]);
                                if (ModState.AiBattleArmorAbilityCmds.ContainsKey(___unit.GUID))
                                {
                                    if (ModState.AiBattleArmorAbilityCmds[___unit.GUID].active)
                                    {
                                        __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                                        return false;
                                    }

                                    var info = new BA_MountOrSwarmInvocation(battleArmorAbility, carrier, true);
                                    ModState.AiBattleArmorAbilityCmds[___unit.GUID] = info;
                                    __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                                    return false; //was true
                                }
                                else
                                {
                                    var info = new BA_MountOrSwarmInvocation(battleArmorAbility, carrier, true);
                                    ModState.AiBattleArmorAbilityCmds.Add(___unit.GUID, info);
                                    __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                                    return false; //was true
                                }
                            }

                            __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                            return false;
                        }

                        //if it isnt mounted, its on the ground and should try to swarm.
                        if (distance <= maxRange && !(closestEnemy is TrooperSquad))
                        {
                            ModInit.modLog.LogTrace($"[CanMoveAndShootWithoutOverheatingNode] Actor {___unit.DisplayName} is on the ground, trying to swarm at {distance} from nearest enemy, maxrange was {maxRange} * 1.25.");
                            if (ModState.AiBattleArmorAbilityCmds.ContainsKey(___unit.GUID))
                            {
                                if (ModState.AiBattleArmorAbilityCmds[___unit.GUID].active)
                                {
                                    __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                                    return false;
                                }

                                var info = new BA_MountOrSwarmInvocation(battleArmorAbility, closestEnemy, true);
                                ModState.AiBattleArmorAbilityCmds[___unit.GUID] = info;
                                __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                                return false; //was true
                            }
                            else
                            {
                                var info = new BA_MountOrSwarmInvocation(battleArmorAbility, closestEnemy, true);
                                ModState.AiBattleArmorAbilityCmds.Add(___unit.GUID, info);
                                __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                                return false; //was true
                            }
                        }
                    }
                }

                if (___unit.HasSwarmingUnits())
                {
                    var deswarm = ___unit.GetDeswarmerAbility();
                    if (deswarm?.Def?.Description?.Id != null)
                    {
                        if (ModState.AiDealWithBattleArmorCmds.ContainsKey(___unit.GUID))
                        {
                            if (ModState.AiDealWithBattleArmorCmds[___unit.GUID].active)
                            {
                                __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                                return false;
                            }

                            var info = new AI_DealWithBAInvocation(deswarm, ___unit, true);
                            ModState.AiDealWithBattleArmorCmds[___unit.GUID] = info;
                            __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                            return false; //was true
                        }
                        else
                        {
                            var info = new AI_DealWithBAInvocation(deswarm, ___unit, true);
                            ModState.AiDealWithBattleArmorCmds.Add(___unit.GUID, info);
                            __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                            return false; //was true
                        }
                    }

                }

                if (ModState.AiCmds.ContainsKey(___unit.GUID))
                {
                    if (ModState.AiCmds[___unit.GUID].active)
                    {
                        __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                        return false;
                    }

                    var spawnVal = AI_Utils.EvaluateSpawn(___unit, out var abilitySpawn, out var vector1Spawn,
                        out var vector2Spawn);
                    var strafeVal = AI_Utils.EvaluateStrafing(___unit, out var abilityStrafe, out var vector1Strafe,
                        out var vector2Strafe);

                    if (spawnVal > ModInit.modSettings.AI_InvokeSpawnThreshold && spawnVal > strafeVal)
                    {
                        var info = new AI_CmdInvocation(abilitySpawn, vector1Spawn, vector2Spawn, true);
                        ModState.AiCmds[___unit.GUID] = info;
                        __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                        return false;
                    }
                    if (strafeVal > ModInit.modSettings.AI_InvokeStrafeThreshold && strafeVal >= spawnVal)
                    {
                        var info = new AI_CmdInvocation(abilityStrafe, vector1Strafe, vector2Strafe, true);
                        ModState.AiCmds[___unit.GUID] = info;
                        __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                        return false;
                    }
                }
                else
                {
                    var spawnVal = AI_Utils.EvaluateSpawn(___unit, out var abilitySpawn, out var vector1Spawn,
                        out var vector2Spawn);
                    var strafeVal = AI_Utils.EvaluateStrafing(___unit, out var abilityStrafe, out var vector1Strafe,
                        out var vector2Strafe);

                    if (strafeVal > spawnVal) goto strafe;
                    if (spawnVal >= ModInit.modSettings.AI_InvokeSpawnThreshold)
                    {
                        var info = new AI_CmdInvocation(abilitySpawn, vector1Spawn, vector2Spawn, true);
                        ModState.AiCmds.Add(___unit.GUID, info);
                        __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                        return false;
                    }
                    strafe:
                    if (strafeVal >= ModInit.modSettings.AI_InvokeStrafeThreshold)
                    {
                        var info = new AI_CmdInvocation(abilityStrafe, vector1Strafe, vector2Strafe, true);
                        ModState.AiCmds.Add(___unit.GUID, info);
                        __result = new BehaviorTreeResults(BehaviorNodeState.Failure);
                        return false;
                    }
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(AITeam), "makeInvocationFromOrders")]
        public static class AITeam_makeInvocationFromOrders_patch
        {
            public static bool Prefix(AITeam __instance, AbstractActor unit, OrderInfo order, ref InvocationMessage __result)
            {
                if (unit.HasSwarmingUnits() && ModState.AiDealWithBattleArmorCmds.ContainsKey(unit.GUID))
                {
                    ModState.AiDealWithBattleArmorCmds[unit.GUID].ability.Activate(unit, unit);
                    //     ModState.AiDealWithBattleArmorCmds[unit.GUID].targetActor);

                    ModInit.modLog.LogMessage(
                        $"activated {ModState.AiDealWithBattleArmorCmds[unit.GUID].ability.Def.Description.Id} on actor {unit.DisplayName} {unit.GUID}");

                   // ModInit.modLog.LogMessage(
                   //     $"activated {ModState.AiDealWithBattleArmorCmds[unit.GUID].ability.Def.Description.Id} on actor {ModState.AiDealWithBattleArmorCmds[unit.GUID].targetActor.DisplayName} {ModState.AiDealWithBattleArmorCmds[unit.GUID].targetActor.GUID}");

                    if (!unit.HasMovedThisRound)
                    {
                        unit.BehaviorTree.IncreaseSprintHysteresisLevel();
                    }

                    __result = new ReserveActorInvocation(unit, ReserveActorAction.DONE,
                        unit.Combat.TurnDirector.CurrentRound);
                    ModState.AiDealWithBattleArmorCmds.Remove(unit.GUID);
                    return false;
                }

                if (unit.IsMountedUnit() && !ModState.AiBattleArmorAbilityCmds.ContainsKey(unit.GUID))
                {
                    __result = new ReserveActorInvocation(unit, ReserveActorAction.DONE,
                        unit.Combat.TurnDirector.CurrentRound);
                    return false;
                }

                if (unit.IsSwarmingUnit())
                {
                    var target = unit.Combat.FindActorByGUID(ModState.PositionLockSwarm[unit.GUID]);
                    ModInit.modLog.LogMessage($"[AITeam.makeInvocationFromOrders] Actor {unit.DisplayName} has active swarm attack on {target.DisplayName}");
                    foreach (var weapon in unit.Weapons)
                    {
                        weapon.EnableWeapon();
                    }
                    var weps = unit.Weapons.Where(x => x.IsEnabled && x.HasAmmo).ToList();

                    var loc = ModState.BADamageTrackers[unit.GUID].BA_MountedLocations.Values.GetRandomElement();
                    var attackStackSequence = new AttackStackSequence(unit, target, unit.CurrentPosition,
                        unit.CurrentRotation, weps, MeleeAttackType.NotSet, loc, -1);
                    unit.Combat.MessageCenter.PublishMessage(new AddSequenceToStackMessage(attackStackSequence));
                    
                    if (!unit.HasMovedThisRound)
                    {
                        unit.BehaviorTree.IncreaseSprintHysteresisLevel();
                    }
                    __result = new ReserveActorInvocation(unit, ReserveActorAction.DONE, unit.Combat.TurnDirector.CurrentRound);
                    return false;
                }
                if (ModState.AiBattleArmorAbilityCmds.ContainsKey(unit.GUID))
                {
                    if (ModState.AiBattleArmorAbilityCmds[unit.GUID].active)
                    {
                        ModInit.modLog.LogTrace(
                            $"BA AI Swarm/Mount Ability DUMP: {ModState.AiBattleArmorAbilityCmds[unit.GUID].active}, {ModState.AiBattleArmorAbilityCmds[unit.GUID].targetActor.DisplayName}.");
                        ModInit.modLog.LogTrace(
                            $"BA AI Swarm/Mount Ability DUMP: {ModState.AiBattleArmorAbilityCmds[unit.GUID].ability} {ModState.AiBattleArmorAbilityCmds[unit.GUID].ability.Def.Id}, Combat is not null? {ModState.AiBattleArmorAbilityCmds[unit.GUID].ability.Combat != null}");

                        ModState.AiBattleArmorAbilityCmds[unit.GUID].ability.Activate(unit,
                            ModState.AiBattleArmorAbilityCmds[unit.GUID].targetActor);
                        ModInit.modLog.LogMessage(
                            $"activated {ModState.AiBattleArmorAbilityCmds[unit.GUID].ability.Def.Description.Id} on actor {ModState.AiBattleArmorAbilityCmds[unit.GUID].targetActor.DisplayName} {ModState.AiBattleArmorAbilityCmds[unit.GUID].targetActor.GUID}");

                        if (!unit.HasMovedThisRound)
                        {
                            unit.BehaviorTree.IncreaseSprintHysteresisLevel();
                        }

                        __result = new ReserveActorInvocation(unit, ReserveActorAction.DONE, unit.Combat.TurnDirector.CurrentRound);
                        ModState.AiBattleArmorAbilityCmds.Remove(unit.GUID);
                        return false;
                    }
                }

                if (!ModState.AiCmds.ContainsKey(unit.GUID)) return true;
                if (!ModState.AiCmds[unit.GUID].active) return true;
                ModState.popupActorResource =
                    AI_Utils.AssignRandomSpawnAsset(ModState.AiCmds[unit.GUID].ability, unit.team.FactionValue.Name, out var waves);
                ModState.strafeWaves = waves;
                //assign waves here if needed

                ModInit.modLog.LogTrace($"AICMD DUMP: {ModState.AiCmds[unit.GUID].active}, {ModState.AiCmds[unit.GUID].vectorOne}, {ModState.AiCmds[unit.GUID].vectorTwo}.");
                ModInit.modLog.LogTrace($"CMD Ability DUMP: {ModState.AiCmds[unit.GUID].ability} { ModState.AiCmds[unit.GUID].ability.Def.Id}, Combat is not null? {ModState.AiCmds[unit.GUID].ability.Combat != null}");

                ModState.AiCmds[unit.GUID].ability.Activate(unit, ModState.AiCmds[unit.GUID].vectorOne,
                    ModState.AiCmds[unit.GUID].vectorTwo);
                ModInit.modLog.LogMessage(
                    $"activated {ModState.AiCmds[unit.GUID].ability.Def.Description.Id} at pos {ModState.AiCmds[unit.GUID].vectorOne.x}, {ModState.AiCmds[unit.GUID].vectorOne.y}, {ModState.AiCmds[unit.GUID].vectorOne.z} and {ModState.AiCmds[unit.GUID].vectorTwo.x}, {ModState.AiCmds[unit.GUID].vectorTwo.y}, {ModState.AiCmds[unit.GUID].vectorTwo.z}, dist = {ModState.AiCmds[unit.GUID].dist}");

                if (!unit.HasMovedThisRound)
                {
                    unit.BehaviorTree.IncreaseSprintHysteresisLevel();
                }
                __result = new ReserveActorInvocation(unit, ReserveActorAction.DONE, unit.Combat.TurnDirector.CurrentRound);
                ModState.AiCmds.Remove(unit.GUID); //somehow spawned BA doesn't always reserve on correct round?
                return false;
                // invoke ability from modstate and then create/use a Brace/Reserve order.

            }
        }
    }
}
