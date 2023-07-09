using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Xml.Serialization;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRage.Voxels;
using VRageMath;

namespace EnvironmentHazard
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Planet), false)]
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class VoxelComponent : MyGameLogicComponent
    {
        public class Config
        {
            [XmlElement]
            public float LargeShipDamagePs1 = 1000f;

            [XmlElement]
            public float SmallShipDamagePs1 = 100f;

            [XmlElement]
            public float PlayerDamagePs1 = 50f;

            [XmlElement]
            public double DamageDistance1 = 3d;

            [XmlElement]
            public float LargeShipDamagePs2 = 1000f;

            [XmlElement]
            public float SmallShipDamagePs2 = 100f;

            [XmlElement]
            public float PlayerDamagePs2 = 50f;

            [XmlElement]
            public double DamageDistance2 = 3d;
        }

        struct DamageState
        {
            public readonly IMyDestroyableObject Entity;
            public readonly float DamagePs;

            public float DamageDealt;
            public MyParticleEffect Fx;

            public DamageState(IMyDestroyableObject entity, float DamagePs)
            {
                Entity = entity;
                this.DamagePs = DamagePs;
                DamageDealt = 0f;
                Fx = null;
            }
        }

        string PLANET_NAME = EnvironmentHazardCore.Planetlist;
        const string LAVA_MATERIAL_NAME_1 = "Rocks_grass";
        const string LAVA_MATERIAL_NAME_2 = "Rocks_grass";
        const string FX_NAME = "FireAndSmoke";

        readonly ConcurrentQueue<DamageState> _damageQueue;
        readonly List<IMySlimBlock> _tmpBlocks;
        readonly Vector3D[] _tmpCorners;
        readonly Queue<DamageState> _tmpDamageQueue;
        readonly List<MyEntity> _tmpEnclosedEntities;

        Config _config;
        string _fxName;
        byte _lavaMaterialIndex1;
        byte _lavaMaterialIndex2;
        bool _initialized;
        bool _processing;
        bool _errorOccured;
        DateTime _lastProcessStartTime;
        DateTime _lastUpdate10Time;

        public VoxelComponent()
        {
            _damageQueue = new ConcurrentQueue<DamageState>();
            _tmpBlocks = new List<IMySlimBlock>();
            _tmpCorners = new Vector3D[8];
            _tmpDamageQueue = new Queue<DamageState>();
            _tmpEnclosedEntities = new List<MyEntity>();
        }

        MyPlanet Planet => Entity as MyPlanet;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            //Log("Hel Script: Init");
            if (!MyAPIGateway.Multiplayer.IsServer)
            {
                Log("Init() skipped; not the server");
                return;
            }

            if (!Planet.Name.StartsWith(PLANET_NAME))
            {
                Log($"Init() skipped; not the target planet: {Planet.Name}");
                return;
            }

            if (!TryGetMaterialIndexByName(LAVA_MATERIAL_NAME_1, out _lavaMaterialIndex1))
            {
                Log($"Init() failed; Lava material 1 not found: {_lavaMaterialIndex1}");
                return;
            }

            if (!TryGetMaterialIndexByName(LAVA_MATERIAL_NAME_2, out _lavaMaterialIndex2))
            {
                Log($"Init() failed; Lava material 2 not found: {_lavaMaterialIndex2}");
                return;
            }

            try
            {
                var fxDefinition = MyDefinitionManager.Static.GetDefinition<MyExhaustEffectDefinition>(FX_NAME);
                _fxName = fxDefinition.ExhaustPipes[0].Effect;
            }
            catch (Exception e)
            {
                Log($"Init() failed; particle effects not found: {FX_NAME}; {e}");
                return;
            }

            try
            {
                _config = new Config();
                ReadOrCreateXmlFile("Config.xml", ref _config);
            }
            catch (Exception e)
            {
                Log($"Init() failed; config failed to load; {e}");
                return;
            }

            Log("Init() started");

            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME |
                           MyEntityUpdateEnum.EACH_10TH_FRAME;

            _initialized = true;
            _lastProcessStartTime = DateTime.UtcNow;
            _lastUpdate10Time = DateTime.UtcNow;
        }

        public override void UpdateBeforeSimulation()
        {
            //Log("UpdateBeforeSimulation");
            if (!_initialized) return;
            if (_errorOccured) return;
            if (_processing) return;

            var nowTime = DateTime.UtcNow;
            if (nowTime - _lastProcessStartTime < TimeSpan.FromSeconds(1)) return;

            _lastProcessStartTime = nowTime;

            MyAPIGateway.Parallel.Start(() => // fire & away
            {
                try
                {
                    _processing = true;
                    EnqueueDamages();
                }
                catch (Exception e)
                {
                    Log($"ERROR: {e}\n{e.StackTrace}");
                    _errorOccured = true;
                }
                finally
                {
                    _processing = false;
                }
            });
        }

        public override void UpdateBeforeSimulation10()
        {
            //Log("UpdateBeforeSimulation10");
            if (!_initialized) return;
            if (_errorOccured) return;

            try
            {
                var nowTime = DateTime.UtcNow;
                var deltaTime = (nowTime - _lastUpdate10Time).TotalSeconds;
                _lastUpdate10Time = nowTime;

                DamageState damageState;
                while (_damageQueue.TryDequeue(out damageState))
                {
                    //Log("UpdateBeforeSimulation10: b4 ProcessDamage");
                    if (!ProcessDamage(ref damageState, deltaTime))
                    {
                        _tmpDamageQueue.Enqueue(damageState);
                    }
                }

                while (_tmpDamageQueue.TryDequeue(out damageState))
                {
                    _damageQueue.Enqueue(damageState);
                }

                _tmpDamageQueue.Clear();
            }
            catch (Exception e)
            {
                Log($"ERROR: {e}\n{e.StackTrace}");
                _errorOccured = true;
            }
        }

        void TryCreateFxForEntity(IMyDestroyableObject obj, out MyParticleEffect fx)
        {
            fx = null;
            var entity = obj as IMyEntity;
            if (entity == null) return;

            var matrix = entity.WorldMatrix;
            var position = entity.WorldMatrix.Translation;
            var parentId = entity.Render.ParentIDs[0];
            MyParticlesManager.TryCreateParticleEffect(_fxName, ref matrix, ref position, parentId, out fx);
        }

        void EnqueueDamages()
        {
            //Log("EDamage: start");
            var sphere = new BoundingSphereD(Planet.PositionComp.GetPosition(), Planet.AverageRadius);
            _tmpEnclosedEntities.Clear();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, _tmpEnclosedEntities);
            foreach (var enclosedEntity in _tmpEnclosedEntities)
            {
                var character = enclosedEntity as IMyCharacter;
                if (character != null)
                {
                    EnqueueCharacterDamage(character);
                    continue;
                }

                var grid = enclosedEntity as IMyCubeGrid;
                if (grid != null)
                {
                    EnqueueGridDamage(grid);
                    continue;
                }

                var floating = enclosedEntity as IMyFloatingObject;
                if (floating != null)
                {
                    EnqueueFloatingObjectDamage(floating);
                }

                // whatever else is passed
            }
        }

        void EnqueueCharacterDamage(IMyCharacter character)
        {
            //Log("ECD: start");
            if (character.Closed || character.MarkedForClose) return;

            var characterPos = character.GetPosition();
            var surfacePos = Planet.GetClosestSurfacePointGlobal(ref characterPos);
            if (Vector3D.DistanceSquared(characterPos, surfacePos) > 6.25) return;

            var material = GetSurfaceMaterialAt(Planet, ref surfacePos);
            if (material.Index == _lavaMaterialIndex1)
            {
                //Log("ECD: mat1: " + _config.PlayerDamagePs1);
                _damageQueue.Enqueue(new DamageState(character, _config.PlayerDamagePs1));
                return;
            }

            if (material.Index == _lavaMaterialIndex2)
            {
                //Log("ECD: mat2: " + _config.PlayerDamagePs2);
                _damageQueue.Enqueue(new DamageState(character, _config.PlayerDamagePs2));
                return;
            }
        }

        void EnqueueFloatingObjectDamage(IMyFloatingObject obj)
        {
            if (obj.Closed || obj.MarkedForClose) return;

            var objPos = obj.GetPosition();
            var surfacePos = Planet.GetClosestSurfacePointGlobal(ref objPos);
            if (Vector3D.DistanceSquared(objPos, surfacePos) > 4) return;

            var material = GetSurfaceMaterialAt(Planet, ref surfacePos);
            if ((material.Index != _lavaMaterialIndex1) && (material.Index != _lavaMaterialIndex2)) return;

            _damageQueue.Enqueue(new DamageState(obj, 999999));
        }

        void EnqueueGridDamage(IMyCubeGrid grid)
        {
            if (grid == null) return;
            if (grid.Physics == null) return; // projector
            if (grid.Closed || grid.MarkedForClose) return;
            if (!IsCloseToPlanetSurface(grid, 1000)) return;

            var damage1 = grid.GridSizeEnum == MyCubeSize.Small
                ? _config.SmallShipDamagePs1
                : _config.LargeShipDamagePs1;

            var damage2 = grid.GridSizeEnum == MyCubeSize.Small
                ? _config.SmallShipDamagePs2
                : _config.LargeShipDamagePs2;

            _tmpBlocks.Clear();
            grid.GetBlocks(_tmpBlocks);
            foreach (var block in _tmpBlocks)
            {
                if (block == null) continue;

                var blockPos = GetLowestBlockPosition(block);
                var surfacePos = Planet.GetClosestSurfacePointGlobal(ref blockPos);
                var material = GetSurfaceMaterialAt(Planet, ref surfacePos);
                if (material == null) continue;
                if ((material.Index != _lavaMaterialIndex1) && (material.Index != _lavaMaterialIndex2)) continue;

                var blockSurfaceDistance = Vector3D.Distance(blockPos, surfacePos);
                if (blockSurfaceDistance <= _config.DamageDistance1)
                {
                    _damageQueue.Enqueue(new DamageState(block, damage1));
                }

                if (blockSurfaceDistance <= _config.DamageDistance2)
                {
                    _damageQueue.Enqueue(new DamageState(block, damage2));
                }
            }
        }

        bool IsCloseToPlanetSurface(IMyEntity entity, double distance)
        {
            entity.WorldAABB.GetCorners(_tmpCorners);
            foreach (var corner in _tmpCorners)
            {
                var closestSurfacePoint = Planet.GetClosestSurfacePointGlobal(corner);
                var distanceToSurface = Vector3D.Distance(corner, closestSurfacePoint);
                if (distanceToSurface < distance)
                {
                    return true;
                }
            }

            return false;
        }

        bool ProcessDamage(ref DamageState damageState, double deltaTime)
        {
            var obj = damageState.Entity;
            if (obj.Integrity <= 0 || ((obj as IMyEntity)?.Closed ?? false))
            {
                damageState.Fx?.Stop(false);
                return true;
            }

            if (damageState.Fx == null)
            {
                TryCreateFxForEntity(obj, out damageState.Fx);
            }

            var damagePs = damageState.DamagePs;
            var deltaDamage = damagePs * (float)deltaTime;
            obj.DoDamage(deltaDamage, MyDamageType.Environment, true);
            if (obj.Integrity <= 0)
            {
                damageState.Fx?.Stop(false);
                return true;
            }

            damageState.DamageDealt += deltaDamage;
            if (damageState.DamageDealt >= damageState.DamagePs)
            {
                damageState.Fx?.Stop(false);
                return true;
            }

            return false;
        }

        Vector3D GetLowestBlockPosition(IMySlimBlock block)
        {
            var fatBlock = block.FatBlock;
            if (fatBlock == null) // armor blocks
            {
                return block.CubeGrid.GridIntegerToWorld(block.Position);
            }

            var lowestBlockPos = default(Vector3D);
            var minDist = double.MaxValue;
            var planetCenterPos = Planet.WorldMatrix.Translation;
            block.FatBlock.WorldAABB.GetCorners(_tmpCorners);
            foreach (var corner in _tmpCorners)
            {
                var dist = Vector3D.DistanceSquared(corner, planetCenterPos);
                if (dist < minDist)
                {
                    minDist = dist;
                    lowestBlockPos = corner;
                }
            }

            return lowestBlockPos;
        }

        static bool TryGetMaterialIndexByName(string name, out byte index)
        {
            foreach (var mat in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
            {
                //Log("Material = " + mat.MaterialTypeName);
                if (mat.MaterialTypeName == name)
                {
                    index = mat.Index;
                    return true;
                }
            }

            index = default(byte);
            return false;
        }

        static MyVoxelMaterialDefinition GetSurfaceMaterialAt(MyVoxelBase voxel, ref Vector3D worldPosition)
        {
            Vector3 localPosition;
            MyVoxelCoordSystems.WorldPositionToLocalPosition(
                worldPosition,
                voxel.PositionComp.WorldMatrixRef,
                voxel.PositionComp.WorldMatrixInvScaled,
                voxel.SizeInMetres / 2f,
                out localPosition);

            var voxelPosition = new Vector3I(localPosition / MyVoxelConstants.VOXEL_SIZE_IN_METRES) + voxel.StorageMin;
            var cache = new MyStorageData();
            cache.Resize(Vector3I.One);
            cache.ClearMaterials(0);
            voxel.Storage.ReadRange(cache, MyStorageDataTypeFlags.Material, 0, voxelPosition, voxelPosition);
            return MyDefinitionManager.Static.GetVoxelMaterialDefinition(cache.Material(0));
        }

        static void ReadOrCreateXmlFile<T>(string fileName, ref T content)
        {
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(fileName, typeof(T)))
            {
                try
                {
                    var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(fileName, typeof(T));
                    var contentText = reader.ReadToEnd();
                    content = MyAPIGateway.Utilities.SerializeFromXML<T>(contentText);
                    Log($"Loaded file: \"{fileName}\": {content}");
                    return;
                }
                catch (Exception exc)
                {
                    Log($"Failed loading file: \"{fileName}\": {exc}");
                    return;
                }
            }

            try
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(fileName, typeof(T)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(content));
                    Log($"Wrote file: \"{fileName}\": {content}");
                }
            }
            catch (Exception exc)
            {
                Log($"Failed writing file: \"{fileName}\": {exc}");
            }
        }

        static void Log(string message)
        {
            MyLog.Default.WriteLine($"{nameof(VoxelComponent)}: {message}");
        }
    }
}
