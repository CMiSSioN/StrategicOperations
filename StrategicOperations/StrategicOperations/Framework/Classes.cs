﻿using System;
using System.Collections.Generic;
using System.Linq;
using Abilifier.Patches;
using BattleTech;
using BattleTech.Data;
using CustomUnits;
using HBS.Collections;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace StrategicOperations.Framework
{
    public class Classes
    {
        public class StrategicMovementInvocation : AbstractActorMovementInvocation
        {
            public new string ActorGUID = "";
            public new bool AbilityConsumesFiring;
            public new List<WayPoint> Waypoints = new List<WayPoint>();
            public new MoveType MoveType;
            public new Vector3 FinalOrientation;
            public new string MeleeTargetGUID = "";
            public ICombatant MoveTarget;
            public bool IsFriendly;
            public bool IsMountOrSwarm;
            public StrategicMovementInvocation(){}
            public StrategicMovementInvocation(AbstractActor actor, bool abilityConsumesFiring, ICombatant moveTarget, bool isFriendly, bool isMountOrSwarm) : base(actor, abilityConsumesFiring)
            {
                Pathing pathing = actor.Pathing;

                this.MoveTarget = moveTarget;
                pathing.SetSprinting();
                pathing.UpdateFreePath(moveTarget.CurrentPosition, moveTarget.CurrentPosition, false, false);
                pathing.UpdateLockedPath(moveTarget.CurrentPosition, moveTarget.CurrentPosition, false);
                pathing.LockPosition();
                this.ActorGUID = actor.GUID;
                this.AbilityConsumesFiring = abilityConsumesFiring;
                List<WayPoint> collection = ActorMovementSequence.ExtractWaypointsFromPath(actor, pathing.CurrentPath, pathing.ResultDestination, pathing.CurrentMeleeTarget, this.MoveType);
                this.Waypoints = new List<WayPoint>(collection);
                this.MoveType = pathing.MoveType;
                this.FinalOrientation = moveTarget.CurrentRotation.eulerAngles;//pathing.ResultAngleAsVector;
                this.MeleeTargetGUID = "";
                this.IsFriendly = isFriendly;
                this.IsMountOrSwarm = isMountOrSwarm;
            }

            public override bool Invoke(CombatGameState combat)
            {
                InvocationMessage.logger.Log("Invoking a STRATEGIC MOVE!");
                AbstractActor abstractActor = combat.FindActorByGUID(this.ActorGUID);
                if (abstractActor == null)
                {
                    InvocationMessage.logger.LogError(string.Format("MechMovement.Invoke Actor with GUID {0} not found!", this.ActorGUID));
                    return false;
                }
                ICombatant combatant = null;
                if (!string.IsNullOrEmpty(this.MeleeTargetGUID))
                {
                    combatant = combat.FindCombatantByGUID(this.MeleeTargetGUID, false);
                    if (combatant == null)
                    {
                        InvocationMessage.logger.LogError(string.Format("MechMovement.Invoke ICombatant with GUID {0} not found!", this.MeleeTargetGUID));
                        return false;
                    }
                }

                StrategicMovementSequence stackSequence = new StrategicMovementSequence(abstractActor, this.Waypoints, this.FinalOrientation, this.MoveType, combatant, this.AbilityConsumesFiring, this.MoveTarget, this.IsFriendly, this.IsMountOrSwarm);
                base.PublishStackSequence(combat.MessageCenter, stackSequence, this);
                return true;
            }
        }

        public class StrategicMovementSequence : ActorMovementSequence
        {
            public ICombatant Target;
            public bool IsFriendly;
            public bool MountSwarmBA;
            public override bool ConsumesActivation => false;

            //public new virtual bool ForceActivationEnd => false;

            public StrategicMovementSequence(AbstractActor actor, List<WayPoint> waypoints, Vector3 finalOrientation, MoveType moveType, ICombatant meleeTarget, bool consumesFiring, ICombatant target, bool friendly, bool mountORswarm) : base(actor, waypoints, finalOrientation, moveType, meleeTarget, consumesFiring)
            {
                this.Target = target;
                this.IsFriendly = friendly;
                this.MountSwarmBA = mountORswarm;
            }

            public override void CompleteOrders()
            {
                base.owningActor.AutoBrace = false;
                base.CompleteOrders();
                base.owningActor.ResetPathing(false);

                if (MountSwarmBA)
                {
                    if (this.Target is BattleTech.Building building)
                    {
                        ModInit.modLog.LogMessage(
                            $"[StrategicMovementSequence] Called for BA movement to garrison building {building.DisplayName}.");
                        base.owningActor.ProcessGarrisonBuilding(building);
                        return;
                    }
                }

                if (this.Target is AbstractActor targetActor)
                {
                    if (MountSwarmBA)
                    {
                        ModInit.modLog.LogMessage(
                            $"[StrategicMovementSequence] Called for BA movement to mount or swarm.");
                        if (base.owningActor is TrooperSquad squad)
                        {
                            if (this.IsFriendly)
                            {
                                if (!squad.IsMountedUnit())
                                {
                                    squad.ProcessMountFriendly(targetActor);
                                    return;
                                }
                            }

                            if (!squad.IsSwarmingUnit())
                            {
                                squad.ProcessSwarmEnemy(targetActor);
                            }
                        }
                        else
                        {
                            ModInit.modLog.LogMessage(
                                $"[StrategicMovementSequence] ERROR: called sequence for BA, but target actor is not TrooperSquad.");
                            return;
                        }
                    }

                    ModInit.modLog.LogMessage(
                        $"[StrategicMovementSequence] Called for airlift/dropoff for Target {this.Target.DisplayName}.");

                    if (targetActor.IsAirlifted())
                    {
                        targetActor.DetachFromAirliftCarrier(base.OwningActor, IsFriendly);
                        return;
                    }

                    if (!targetActor.IsAirlifted())
                    {
                        targetActor.AttachToAirliftCarrier(base.OwningActor, IsFriendly);
                    }
                }
            }
        }

        public enum BA_TargetEffectType
        {
            MOUNT_INT,
            MOUNT_EXT,
            SWARM,
            GARRISON,
            BOTH
        }
        public class CustomSpawner
        {
            public CombatGameState Combat;
            public AbstractActor Actor;
            public string ChosenUnit;
            public Lance CustomLance;
            public DataManager DM;
            public MechDef NewUnitDef;
            public PilotDef NewPilotDef;
            public Team TeamSelection;
            public Team SourceTeam;
            public Ability SourceAbility;
            public TagSet CustomEncounterTags;
            public Vector3 SpawnLoc = Vector3.zero;
            public Vector3 Loc2 = Vector3.zero;
            public Quaternion SpawnRotation = new Quaternion(0f, 0f, 0f, 0f);
            public HeraldryDef SupportHeraldryDef;
            public PendingStrafeWave StrafeWave;
            public string ParentSequenceIDForStrafe = "";

            public CustomSpawner(CombatGameState combat, AbstractActor actor, string chosen, Lance custLance)
            {
                this.Combat = combat;
                this.Actor = actor;
                this.ChosenUnit = chosen;
                this.CustomLance = custLance;
                this.DM = UnityGameInstance.BattleTechGame.DataManager;
            }

            public CustomSpawner(Team team, Ability ability, CombatGameState combat, string chosen, Lance custLance, Team teamSelection, Vector3 loc, Quaternion rotation, HeraldryDef heraldry, PilotDef supportPilotDef)
            {
                this.SourceTeam = team;
                this.SourceAbility = ability;
                this.Combat = combat;
                this.ChosenUnit = chosen;
                this.CustomLance = custLance;
                this.TeamSelection = teamSelection;
                this.DM = UnityGameInstance.BattleTechGame.DataManager;
                this.SpawnLoc = loc;
                this.SpawnRotation = rotation;
                this.SupportHeraldryDef = heraldry;
                this.NewPilotDef = supportPilotDef;
            }

            public CustomSpawner(string parentSequenceID, PendingStrafeWave wave)
            {
                this.StrafeWave = wave;
                this.Combat = wave.Ability.Combat;
                this.ChosenUnit = wave.ActorResource;
                this.CustomLance = wave.CmdLance;
                this.TeamSelection = wave.NeutralTeam;
                this.DM = UnityGameInstance.BattleTechGame.DataManager;
                this.SpawnLoc = wave.PositionA;
                this.SupportHeraldryDef = wave.SupportHeraldryDef;
                this.NewPilotDef = wave.SupportPilotDef;
                this.ParentSequenceIDForStrafe = parentSequenceID;
            }

            public void OnStratOpsDepsFailed()
            {
                ModInit.modLog.LogTrace($"Failed to load BA Dependencies for {ChosenUnit}. This shouldnt happen!");
            }
            public void OnBADepsLoaded()
            {
                var newBattleArmor = ActorFactory.CreateMech(NewUnitDef, NewPilotDef,
                    CustomEncounterTags, TeamSelection.Combat,
                    TeamSelection.GetNextSupportUnitGuid(), "", Actor.team.HeraldryDef);
                newBattleArmor.Init(Actor.CurrentPosition, Actor.CurrentRotation.eulerAngles.y, false);
                newBattleArmor.InitGameRep(null);
                TeamSelection.AddUnit(newBattleArmor);
                newBattleArmor.AddToTeam(TeamSelection);
                newBattleArmor.AddToLance(CustomLance);
                CustomLance.AddUnitGUID(newBattleArmor.GUID);
                newBattleArmor.BehaviorTree = BehaviorTreeFactory.MakeBehaviorTree(
                    Actor.Combat.BattleTechGame, newBattleArmor, BehaviorTreeIDEnum.CoreAITree);
                newBattleArmor.OnPlayerVisibilityChanged(VisibilityLevel.None);
                newBattleArmor.OnPositionUpdate(Actor.CurrentPosition, Actor.CurrentRotation, -1, true, null, false);
                newBattleArmor.DynamicUnitRole = UnitRole.Brawler;
                UnitSpawnedMessage message = new UnitSpawnedMessage("FROM_ABILITY", newBattleArmor.GUID);
                Actor.Combat.MessageCenter.PublishMessage(message);
                
                newBattleArmor.TeleportActor(Actor.CurrentPosition);
                ModState.PositionLockMount.Add(newBattleArmor.GUID, Actor.GUID);
                if (newBattleArmor is TrooperSquad squad)
                {
                    squad.AttachToCarrier(Actor, true);
                }
                Actor.Combat.ItemRegistry.AddItem(newBattleArmor);
                Actor.Combat.RebuildAllLists();
                ModInit.modLog.LogMessage(
                    $"[SpawnBattleArmorAtActor] Added PositionLockMount with rider  {newBattleArmor.DisplayName} {newBattleArmor.GUID} and carrier {Actor.DisplayName} {Actor.GUID}.");
            }

            public void SpawnBattleArmorAtActor()
            {
                LoadRequest loadRequest = DM.CreateLoadRequest();
                loadRequest.AddBlindLoadRequest(BattleTechResourceType.MechDef, ChosenUnit);
                ModInit.modLog.LogMessage($"Added loadrequest for MechDef: {ChosenUnit}");
                loadRequest.ProcessRequests(1000U);

                var instanceGUID =
                    $"{Actor.Description.Id}_{Actor.team.Name}_{ChosenUnit}";

                if (Actor.Combat.TurnDirector.CurrentRound <= 1)
                {
                    if (ModState.DeferredInvokeBattleArmor.All(x => x.Key != instanceGUID) && !ModState.DeferredBattleArmorSpawnerFromDelegate)
                    {
                        ModInit.modLog.LogTrace(
                            $"Deferred BA Spawner missing, creating delegate and returning. Delegate should spawn {ChosenUnit}");

                        void DeferredInvokeBASpawn() =>
                            SpawnBattleArmorAtActor();

                        var kvp = new KeyValuePair<string, Action>(instanceGUID, DeferredInvokeBASpawn);
                        ModState.DeferredInvokeBattleArmor.Add(kvp);
                        foreach (var value in ModState.DeferredInvokeBattleArmor)
                        {
                            ModInit.modLog.LogTrace(
                                $"there is a delegate {value.Key} here, with value {value.Value}");
                        }
                        return;
                    }
                }

                TeamSelection = Actor.team;
                var alliedActors = Actor.Combat.AllMechs.Where(x => x.team == Actor.team);
                var chosenpilotSourceMech = alliedActors.GetRandomElement();
                var newPilotDefID = chosenpilotSourceMech.pilot.pilotDef.Description.Id;
                DM.PilotDefs.TryGet(newPilotDefID, out this.NewPilotDef);
                ModInit.modLog.LogMessage($"Attempting to spawn {ChosenUnit} with pilot {NewPilotDef.Description.Callsign}.");
                DM.MechDefs.TryGet(ChosenUnit, out NewUnitDef);
                NewUnitDef.Refresh();
                //var injectedDependencyLoadRequest = new DataManager.InjectedDependencyLoadRequest(dm);
                //newBattleArmorDef.GatherDependencies(dm, injectedDependencyLoadRequest, 1000U);
                //newBattleArmorDef.Refresh();
                CustomEncounterTags = new TagSet(TeamSelection.EncounterTags) { "SpawnedFromAbility" };

                if (!NewUnitDef.DependenciesLoaded(1000U) || !NewPilotDef.DependenciesLoaded(1000U))
                {
                    DataManager.InjectedDependencyLoadRequest dependencyLoad = new DataManager.InjectedDependencyLoadRequest(DM);
                    dependencyLoad.RegisterLoadCompleteCallback(new Action(this.OnBADepsLoaded));
                    dependencyLoad.RegisterLoadFailedCallback(new Action(this.OnStratOpsDepsFailed)); // do failure handling here
                    if (!NewUnitDef.DependenciesLoaded(1000U))
                    {
                        NewUnitDef.GatherDependencies(DM, dependencyLoad, 1000U);
                    }
                    if (!NewPilotDef.DependenciesLoaded(1000U))
                    {
                        NewPilotDef.GatherDependencies(DM, dependencyLoad, 1000U);
                    }
                    DM.InjectDependencyLoader(dependencyLoad, 1000U);
                    return;
                }
                this.OnBADepsLoaded();
            }

            public void OnBeaconDepsLoaded()
            {
                var newUnit= ActorFactory.CreateMech(NewUnitDef, NewPilotDef,
                    CustomEncounterTags, TeamSelection.Combat,
                    TeamSelection.GetNextSupportUnitGuid(), "", SupportHeraldryDef);
                newUnit.Init(SpawnLoc, SpawnRotation.eulerAngles.y, false);
                newUnit.InitGameRep(null);
                TeamSelection.AddUnit(newUnit);
                newUnit.AddToTeam(TeamSelection);
                newUnit.AddToLance(CustomLance);
                CustomLance.AddUnitGUID(newUnit.GUID);
                newUnit.BehaviorTree = BehaviorTreeFactory.MakeBehaviorTree(
                    Combat.BattleTechGame, newUnit, BehaviorTreeIDEnum.CoreAITree);
                //newUnit.OnPlayerVisibilityChanged(VisibilityLevel.None);
                newUnit.OnPositionUpdate(SpawnLoc, SpawnRotation, -1, true, null, false);
                newUnit.DynamicUnitRole = UnitRole.Brawler;
                UnitSpawnedMessage message = new UnitSpawnedMessage("FROM_ABILITY", newUnit.GUID);
                Combat.MessageCenter.PublishMessage(message);

                var underMap = newUnit.CurrentPosition;
                underMap.y = -1000f;
                newUnit.TeleportActor(underMap);
                Combat.ItemRegistry.AddItem(newUnit);
                Combat.RebuildAllLists();
                EncounterLayerParent encounterLayerParent = Combat.EncounterLayerData.gameObject.GetComponentInParent<EncounterLayerParent>();
                DropPodUtils.DropPodSpawner dropSpawner = encounterLayerParent.gameObject.GetComponent<DropPodUtils.DropPodSpawner>();
                if (dropSpawner == null) { dropSpawner = encounterLayerParent.gameObject.AddComponent<DropPodUtils.DropPodSpawner>(); }

                dropSpawner.Unit = newUnit;
                dropSpawner.Combat = Combat;
                dropSpawner.Parent = UnityGameInstance.BattleTechGame.Combat.EncounterLayerData
                    .GetComponentInParent<EncounterLayerParent>();
                dropSpawner.DropPodPosition = SpawnLoc;
                dropSpawner.DropPodRotation = SpawnRotation;

                ModInit.modLog.LogTrace($"DropPodAnim location {SpawnLoc} is also {dropSpawner.DropPodPosition}");
                ModInit.modLog.LogTrace($"Is dropAnim null fuckin somehow? {dropSpawner == null}");
                dropSpawner.DropPodVfxPrefab = dropSpawner.Parent.DropPodVfxPrefab;
                dropSpawner.DropPodLandedPrefab = dropSpawner.Parent.dropPodLandedPrefab;
                dropSpawner.LoadDropPodPrefabs(dropSpawner.DropPodVfxPrefab, dropSpawner.DropPodLandedPrefab);
                ModInit.modLog.LogTrace($"loaded prefabs success");
                dropSpawner.StartCoroutine(dropSpawner.StartDropPodAnimation(0f));
                ModInit.modLog.LogTrace($"started drop pod anim");

                if (SourceTeam.IsLocalPlayer && (ModInit.modSettings.commandUseCostsMulti > 0 ||
                                               SourceAbility.Def.getAbilityDefExtension().CBillCost > 0))
                {
                    var unitName = "";
                    var unitCost = 0;
                    var unitID = "";

                    unitName = NewUnitDef.Description.UIName;
                    unitID = NewUnitDef.Description.Id;
                    unitCost = NewUnitDef.Chassis.Description.Cost;

                    if (ModState.CommandUses.All(x => x.UnitID != ChosenUnit))
                    {
                        var commandUse =
                            new CmdUseInfo(unitID, SourceAbility.Def.Description.Name, unitName, unitCost,
                                SourceAbility.Def.getAbilityDefExtension().CBillCost);

                        ModState.CommandUses.Add(commandUse);
                        ModInit.modLog.LogMessage(
                            $"Added usage cost for {commandUse.CommandName} - {commandUse.UnitName}. UnitUseCost (unadjusted): {unitCost}. Ability Use Cost: {SourceAbility.Def.getAbilityDefExtension().CBillCost}");
                    }
                    else
                    {
                        var cmdUse = ModState.CommandUses.FirstOrDefault(x => x.UnitID == ChosenUnit);
                        if (cmdUse == null)
                        {
                            ModInit.modLog.LogMessage($"ERROR: cmdUseInfo was null");
                        }
                        else
                        {
                            cmdUse.UseCount += 1;
                            ModInit.modLog.LogMessage(
                                $"Added usage cost for {cmdUse.CommandName} - {cmdUse.UnitName}. UnitUseCost (unadjusted): {unitCost}. Ability Use Cost: {SourceAbility.Def.getAbilityDefExtension().CBillCost}. Now used {cmdUse.UseCount} times.");
                        }
                    }
                }
            }

            public void SpawnBeaconUnitAtLocation()
            {
                LoadRequest loadRequest = DM.CreateLoadRequest();
                loadRequest.AddBlindLoadRequest(BattleTechResourceType.MechDef, ChosenUnit);
                ModInit.modLog.LogMessage($"Added loadrequest for MechDef: {ChosenUnit}");
                loadRequest.ProcessRequests(1000U);

                if (false) // maybe dont need? initial invoke should already be deferred, and we cant do this round 1 anyway
                {
                    var instanceGUID =
                        $"{Actor.Description.Id}_{Actor.team.Name}_{ChosenUnit}";

                    if (Actor.Combat.TurnDirector.CurrentRound <= 1)
                    {
                        if (ModState.DeferredInvokeBattleArmor.All(x => x.Key != instanceGUID) &&
                            !ModState.DeferredBattleArmorSpawnerFromDelegate)
                        {
                            ModInit.modLog.LogTrace(
                                $"Deferred BA Spawner missing, creating delegate and returning. Delegate should spawn {ChosenUnit}");

                            void DeferredInvokeBASpawn() =>
                                SpawnBattleArmorAtActor();

                            var kvp = new KeyValuePair<string, Action>(instanceGUID, DeferredInvokeBASpawn);
                            ModState.DeferredInvokeBattleArmor.Add(kvp);
                            foreach (var value in ModState.DeferredInvokeBattleArmor)
                            {
                                ModInit.modLog.LogTrace(
                                    $"there is a delegate {value.Key} here, with value {value.Value}");
                            }

                            return;
                        }
                    }
                }

                //var alliedActors = Combat.AllMechs.Where(x => x.team.IsFriendly(TeamSelection));
                //var chosenpilotSourceMech = alliedActors.GetRandomElement();
                //var newPilotDefID = chosenpilotSourceMech.pilot.pilotDef.Description.Id;
                //DM.PilotDefs.TryGet(newPilotDefID, out this.NewPilotDef);
                ModInit.modLog.LogMessage($"Attempting to spawn {ChosenUnit} with pilot {NewPilotDef.Description.Callsign}.");
                DM.MechDefs.TryGet(ChosenUnit, out NewUnitDef);
                NewUnitDef.Refresh();
                //var injectedDependencyLoadRequest = new DataManager.InjectedDependencyLoadRequest(dm);
                //newBattleArmorDef.GatherDependencies(dm, injectedDependencyLoadRequest, 1000U);
                //newBattleArmorDef.Refresh();
                CustomEncounterTags = new TagSet(TeamSelection.EncounterTags) { "SpawnedFromAbility" };

                if (!NewUnitDef.DependenciesLoaded(1000U) || !NewPilotDef.DependenciesLoaded(1000U))
                {
                    DataManager.InjectedDependencyLoadRequest dependencyLoad = new DataManager.InjectedDependencyLoadRequest(DM);
                    dependencyLoad.RegisterLoadCompleteCallback(new Action(this.OnBeaconDepsLoaded));
                    dependencyLoad.RegisterLoadFailedCallback(new Action(this.OnStratOpsDepsFailed)); // do failure handling here
                    if (!NewUnitDef.DependenciesLoaded(1000U))
                    {
                        NewUnitDef.GatherDependencies(DM, dependencyLoad, 1000U);
                    }
                    if (!NewPilotDef.DependenciesLoaded(1000U))
                    {
                        NewPilotDef.GatherDependencies(DM, dependencyLoad, 1000U);
                    }
                    DM.InjectDependencyLoader(dependencyLoad, 1000U);
                    return;
                }
                this.OnBeaconDepsLoaded();
            }

            public void OnStrafeDepsLoaded()
            {
                var newUnit = ActorFactory.CreateMech(NewUnitDef, NewPilotDef,
                    CustomEncounterTags, TeamSelection.Combat,
                    TeamSelection.GetNextSupportUnitGuid(), "", SupportHeraldryDef);
                newUnit.Init(StrafeWave.NeutralTeam.OffScreenPosition, 0f, false);
                newUnit.InitGameRep(null);
                TeamSelection.AddUnit(newUnit);
                newUnit.AddToTeam(TeamSelection);
                newUnit.AddToLance(CustomLance);
                CustomLance.AddUnitGUID(newUnit.GUID);
                newUnit.GameRep.gameObject.SetActive(true);
                newUnit.BehaviorTree = BehaviorTreeFactory.MakeBehaviorTree(
                        this.StrafeWave.Ability.Combat.BattleTechGame, newUnit,
                        BehaviorTreeIDEnum.DoNothingTree);
                var eventID = Guid.NewGuid().ToString();
                ModInit.modLog.LogMessage($"Initializing Strafing Run (wave) with id {eventID}!");
                TB_StrafeSequence eventSequence =
                    new TB_StrafeSequence(this.ParentSequenceIDForStrafe, eventID, newUnit, SpawnLoc,
                        this.StrafeWave.PositionB, this.StrafeWave.Radius, this.StrafeWave.Team, ModState.IsStrafeAOE, this.StrafeWave.Ability.Def.IntParam1);
                TurnEvent tEvent = new TurnEvent(eventID, Combat,
                    this.StrafeWave.Ability.Def.ActivationETA, null, eventSequence, this.StrafeWave.Ability.Def, false);
                Combat.TurnDirector.AddTurnEvent(tEvent);


                if (this.StrafeWave.Team.IsLocalPlayer && (ModInit.modSettings.commandUseCostsMulti > 0 ||
                                                           this.StrafeWave.Ability.Def.getAbilityDefExtension().CBillCost > 0))
                {
                    var unitName = "";
                    var unitCost = 0;
                    var unitID = "";
                    unitName = NewUnitDef.Description.UIName;
                    unitID = NewUnitDef.Description.Id;
                    unitCost = NewUnitDef.Chassis.Description.Cost;
                    ModInit.modLog.LogMessage($"Usage cost will be {unitCost}");

                    if (ModState.CommandUses.All(x => x.UnitID != this.StrafeWave.ActorResource))
                    {

                        var commandUse = new CmdUseInfo(unitID, this.StrafeWave.Ability.Def.Description.Name, unitName,
                            unitCost, this.StrafeWave.Ability.Def.getAbilityDefExtension().CBillCost);

                        ModState.CommandUses.Add(commandUse);
                        ModInit.modLog.LogMessage(
                            $"Added usage cost for {commandUse.CommandName} - {commandUse.UnitName}");
                    }
                    else
                    {
                        var cmdUse = ModState.CommandUses.FirstOrDefault(x => x.UnitID == this.StrafeWave.ActorResource);
                        if (cmdUse == null)
                        {
                            ModInit.modLog.LogMessage($"ERROR: cmdUseInfo was null");
                        }
                        else
                        {
                            cmdUse.UseCount += 1;
                            ModInit.modLog.LogMessage(
                                $"Added usage cost for {cmdUse.CommandName} - {cmdUse.UnitName}, used {cmdUse.UseCount} times");
                        }
                    }
                }
            }

            public void SpawnStrafingUnit()
            {
                LoadRequest loadRequest = DM.CreateLoadRequest();
                loadRequest.AddBlindLoadRequest(BattleTechResourceType.MechDef, this.StrafeWave.ActorResource);
                ModInit.modLog.LogMessage($"Added loadrequest for MechDef: {this.StrafeWave.ActorResource}");
                loadRequest.ProcessRequests(1000U);

                if (false) // maybe dont need? initial invoke should already be deferred, and we cant do this round 1 anyway
                {
                    var instanceGUID =
                        $"{Actor.Description.Id}_{Actor.team.Name}_{ChosenUnit}";

                    if (Actor.Combat.TurnDirector.CurrentRound <= 1)
                    {
                        if (ModState.DeferredInvokeBattleArmor.All(x => x.Key != instanceGUID) &&
                            !ModState.DeferredBattleArmorSpawnerFromDelegate)
                        {
                            ModInit.modLog.LogTrace(
                                $"Deferred BA Spawner missing, creating delegate and returning. Delegate should spawn {ChosenUnit}");

                            void DeferredInvokeBASpawn() =>
                                SpawnBattleArmorAtActor();

                            var kvp = new KeyValuePair<string, Action>(instanceGUID, DeferredInvokeBASpawn);
                            ModState.DeferredInvokeBattleArmor.Add(kvp);
                            foreach (var value in ModState.DeferredInvokeBattleArmor)
                            {
                                ModInit.modLog.LogTrace(
                                    $"there is a delegate {value.Key} here, with value {value.Value}");
                            }

                            return;
                        }
                    }
                }

                //var alliedActors = Combat.AllMechs.Where(x => x.team.IsFriendly(TeamSelection));
                //var chosenpilotSourceMech = alliedActors.GetRandomElement();
                //var newPilotDefID = chosenpilotSourceMech.pilot.pilotDef.Description.Id;
                //DM.PilotDefs.TryGet(newPilotDefID, out this.NewPilotDef);
                ModInit.modLog.LogMessage($"Attempting to spawn {ChosenUnit} with pilot {NewPilotDef.Description.Callsign}.");
                DM.MechDefs.TryGet(ChosenUnit, out NewUnitDef);
                NewUnitDef.Refresh();
                //var injectedDependencyLoadRequest = new DataManager.InjectedDependencyLoadRequest(dm);
                //newBattleArmorDef.GatherDependencies(dm, injectedDependencyLoadRequest, 1000U);
                //newBattleArmorDef.Refresh();
                CustomEncounterTags = new TagSet(TeamSelection.EncounterTags) { "SpawnedFromAbility" };

                if (!NewUnitDef.DependenciesLoaded(1000U) || !NewPilotDef.DependenciesLoaded(1000U))
                {
                    DataManager.InjectedDependencyLoadRequest dependencyLoad = new DataManager.InjectedDependencyLoadRequest(DM);
                    dependencyLoad.RegisterLoadCompleteCallback(new Action(this.OnStrafeDepsLoaded));
                    dependencyLoad.RegisterLoadFailedCallback(new Action(this.OnStratOpsDepsFailed)); // do failure handling here
                    if (!NewUnitDef.DependenciesLoaded(1000U))
                    {
                        NewUnitDef.GatherDependencies(DM, dependencyLoad, 1000U);
                    }
                    if (!NewPilotDef.DependenciesLoaded(1000U))
                    {
                        NewPilotDef.GatherDependencies(DM, dependencyLoad, 1000U);
                    }
                    DM.InjectDependencyLoader(dependencyLoad, 1000U);
                    return;
                }
                this.OnStrafeDepsLoaded();
            }
        }

        public class BA_DeswarmAbilityConfig // key will be AbilityDefID
        {
            //public string AbilityDefID = "";
            public float BaseSuccessChance = 0f;
            public float MaxSuccessChance = 0f;
            public float TotalDamage = 0f;
            public int Clusters = 1;
            public int InitPenalty = 0;

            public BA_DeswarmAbilityConfig(){}
        }

        public class BA_DeswarmMovementConfig
        {
            public float BaseSuccessChance = 0f;
            public float MaxSuccessChance = 0f;
            public float EvasivePipsFactor = 0f;
            public float JumpMovementModifier = 0f;
            public bool UseDFADamage = false;
            public float LocationDamageOverride = 0f;
            public  BA_DeswarmMovementConfig(){}
        }

        public class AI_FactionCommandAbilitySetting
        {
            public string AbilityDefID = "";
            public List<string> FactionIDs = new List<string>();
            public float AddChance = 0f;
            public float DiffMod = 0f;
            public int MaxUsersAddedPerContract = 0;
            public List<AI_BeaconProxyInfo> AvailableBeacons = new List<AI_BeaconProxyInfo>();
        }

        public class AI_BeaconProxyInfo
        {
            public string UnitDefID = "";
            public int Weight = 0;
            public int StrafeWaves = 0;
        }
        public class BA_FactionAssoc
        {
            public List<string> FactionIDs = new List<string>();
            public float SpawnChanceBase = 0f;
            public float SpawnChanceDiffMod = 0f;
            public int MaxSquadsPerContract = 0;
            public Dictionary<string, int> InternalBattleArmorWeight = new Dictionary<string, int>();
            public Dictionary<string, int> MountedBattleArmorWeight = new Dictionary<string, int>();
            public Dictionary<string, int> HandsyBattleArmorWeight = new Dictionary<string, int>();
        }
        public class BA_DamageTracker
        {
            public string TargetGUID = ""; // guid of carrier unit.
            public bool IsSquadInternal;
            public Dictionary<int, int> BA_MountedLocations = new Dictionary<int, int>(); // key is BA unit chassis location (HD, CT, LT, RT, LA, RA), value is BA mounted ARMOR location on carrier.

            public BA_DamageTracker()
            {
                this.TargetGUID = "";
                this.IsSquadInternal = false;
                this.BA_MountedLocations = new Dictionary<int, int>();
            }

            public BA_DamageTracker(string targetGUID, bool internalSquad, Dictionary<int, int> mountedLocations)
            {
                this.TargetGUID = targetGUID;
                this.IsSquadInternal = internalSquad;
                this.BA_MountedLocations = mountedLocations;
            }
        }

        public class AirliftTracker
        {
            public string TargetGUID = ""; // guid of carrier unit.
            public bool IsCarriedInternal;
            public bool IsFriendly; // key is BA unit chassis location (HD, CT, LT, RT, LA, RA), value is BA mounted ARMOR location on carrier.
            public float Offset;

            public AirliftTracker()
            {
                this.TargetGUID = "";
                this.IsCarriedInternal = false;
                this.IsFriendly = false;
                this.Offset = 0f;
            }

            public AirliftTracker(string targetGUID, bool internalCarry, bool isFriendly, float offset)
            {
                this.TargetGUID = targetGUID;
                this.IsCarriedInternal = internalCarry;
                this.IsFriendly = isFriendly;
                this.Offset = offset;
            }
        }


        public class BA_TargetEffect
        {
            public string ID = "";
            public BA_TargetEffectType TargetEffectType = BA_TargetEffectType.BOTH;
            public string Name = "";
            public string Description = "";

            [JsonIgnore]
            public List<EffectData> effects = new List<EffectData>();
            public List<JObject> effectDataJO = new List<JObject>();

        }

        public class AirliftTargetEffect
        {
            public string ID = "";
            public bool FriendlyAirlift;
            public string Name = "";
            public string Description = "";

            [JsonIgnore]
            public List<EffectData> effects = new List<EffectData>();
            public List<JObject> effectDataJO = new List<JObject>();

        }

        public class SpawnCoords
        {
            public string ID;
            public Vector3 Loc;
            public float DistFromTarget;

            public SpawnCoords(string id, Vector3 loc, float distFromTarget)
            {
                this.ID = id;
                this.Loc = loc;
                this.DistFromTarget = distFromTarget;
            }
        }
        public class ColorSetting
        {
            public int r;
            public int g;
            public int b;
            //public int a;

            public float Rf => r / 255f;
            public float Gf => g / 255f;
            public float Bf => b / 255f;
            //public float Af => a / 255f;
        }
        public class CmdUseStat
        {
            public string ID;
            public string stat;
            public string pilotID;
            public bool consumeOnUse;
            public int contractUses;
            public int simStatCount;

            public CmdUseStat(string id, string stat, bool consumeOnUse, int contractUses, int simStatCount, string pilotId = null)
            {
                this.ID = id;
                this.stat = stat;
                this.pilotID = pilotId;
                this.consumeOnUse = consumeOnUse;
                this.contractUses = contractUses;
                this.simStatCount = simStatCount;
            }
        }
        public class CmdUseInfo
        {
            public string UnitID;
            public string CommandName;
            public string UnitName;
            public int UseCost;
            public int AbilityUseCost;
            public int UseCostAdjusted => Mathf.RoundToInt((UseCost * ModInit.modSettings.commandUseCostsMulti) + AbilityUseCost);
            public int UseCount;
            public int TotalCost => UseCount * UseCostAdjusted;

            public CmdUseInfo(string unitId, string commandName, string unitName, int useCost, int abilityUseCost)
            {
                this.UnitID = unitId;
                this.CommandName = commandName;
                this.UnitName = unitName;
                this.UseCost = useCost;
                this.AbilityUseCost = abilityUseCost;
                this.UseCount = 1;
            }
        }

        public class BA_DeswarmMovementInfo
        {
            public AbstractActor Carrier;
            public List<AbstractActor> SwarmingUnits;

            public BA_DeswarmMovementInfo()
            {
                this.Carrier = null;
                this.SwarmingUnits = new List<AbstractActor>();
            }

            public BA_DeswarmMovementInfo(AbstractActor carrier)
            {
                this.Carrier = carrier;
                this.SwarmingUnits = new List<AbstractActor>();
            }
            public BA_DeswarmMovementInfo(AbstractActor carrier, List<AbstractActor> swarmingUnits)
            {
                this.Carrier = carrier;
                this.SwarmingUnits = swarmingUnits;
            }
        }

        public class AI_DealWithBAInvocation
        {
            public Ability ability;
            public AbstractActor targetActor;
            public bool active;

            public AI_DealWithBAInvocation()
            {
                this.ability = default(Ability);
                this.targetActor = default(AbstractActor);
                this.active = false;
            }
            public AI_DealWithBAInvocation(Ability cmdAbility, AbstractActor targetActor, bool active)
            {
                this.ability = cmdAbility;
                this.targetActor = targetActor;
                this.active = active;
            }
        }

        public class BA_MountOrSwarmInvocation
        {
            public Ability ability;
            public AbstractActor targetActor;
            public bool active;

            public BA_MountOrSwarmInvocation()
            {
                this.ability = default(Ability);
                this.targetActor = default(AbstractActor);
                this.active = false;
            }
            public BA_MountOrSwarmInvocation(Ability cmdAbility, AbstractActor targetActor, bool active)
            {
                this.ability = cmdAbility;
                this.targetActor = targetActor;
                this.active = active;
            }
        }

        public class AI_CmdInvocation
        {
            public Ability ability;
            public Vector3 vectorOne;
            public Vector3 vectorTwo;
            public bool active;
            public float dist;

            public AI_CmdInvocation()
            {
                this.ability = default(Ability);
                this.vectorOne = new Vector3();
                this.vectorTwo = new Vector3();
                this.active = false;
                this.dist = Vector3.Distance(vectorOne, vectorTwo);
            }
            public AI_CmdInvocation(Ability cmdAbility, Vector3 firstVector, Vector3 secondVector, bool active)
            {
                this.ability = cmdAbility;
                this.vectorOne = firstVector;
                this.vectorTwo = secondVector;
                this.active = active;
                this.dist = Vector3.Distance(vectorOne, vectorTwo);
            }
        }
        public class AI_SpawnBehavior
        {
            public string Tag;
            public string Behavior;
            public int MinRange;

            public AI_SpawnBehavior()
            {
                Tag = "DEFAULT";
                Behavior = "DEFAULT";
                MinRange = 50;
            }
        }

        public class PendingStrafeWave
        {
            public int RemainingWaves;
            public Ability Ability;
            public Team Team;
            public Vector3 PositionA;
            public Vector3 PositionB;
            public float Radius;
            public string ActorResource;
            public Team NeutralTeam;
            public Lance CmdLance;
            public PilotDef SupportPilotDef;
            public HeraldryDef SupportHeraldryDef;
            public DataManager DM;

            public PendingStrafeWave(int remainingWaves, Ability ability, Team team, Vector3 positionA, Vector3 positionB, float radius, string actorResource, Team neutralTeam, Lance cmdLance, PilotDef supportPilotDef, HeraldryDef supportHeraldryDef, DataManager dm)
            {
                this.RemainingWaves = remainingWaves;
                this.Ability = ability;
                this.Team = team;
                this.PositionA = positionA;
                this.PositionB = positionB;
                this.Radius = radius;
                this.ActorResource = actorResource;
                this.NeutralTeam = neutralTeam;
                this.CmdLance = cmdLance;
                this.SupportPilotDef = supportPilotDef;
                this.SupportHeraldryDef = supportHeraldryDef;
                this.DM = dm;
            }
        }
    }
}
