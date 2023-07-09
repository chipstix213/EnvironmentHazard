
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Voxels;
using VRageMath;


namespace EnvironmentHazard
{

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class EnviromentHazardVoxels : MySessionComponentBase
    {
        public static EnviromentHazardVoxels Instance; // the only way to access session comp from other classes and the only accepted static field.
        internal bool _debug = true;
        internal byte _ticks = 0;
        public bool SetupComplete = false;
        public float DamageDuringEffect = 15f;
        public List<MyPlanet> Planets = new List<MyPlanet>();
        public List<IMyPlayer> Players = new List<IMyPlayer>();
        public List<MyEntity> Entities = new List<MyEntity>();
        private double DamageDistance = 10;

        public override void UpdateBeforeSimulation()
        {
            // executed every tick, 60 times a second, before physics simulation and only if game is not paused.

            if (!SetupComplete)
            {

                if (!MyAPIGateway.Multiplayer.IsServer)
                {

                    MyAPIGateway.Utilities.InvokeOnGameThread(() => { UpdateOrder = MyUpdateOrder.NoUpdate; });

                }

                SetupComplete = true;
                MyAPIGateway.Entities.OnEntityAdd += OnEntityAdded;
                MyVisualScriptLogicProvider.PlayerConnected += PlayerConnected;
                MyVisualScriptLogicProvider.PlayerSpawned += PlayerConnected;
                var tempEnts = new HashSet<IMyEntity>();
                MyAPIGateway.Entities.GetEntities(tempEnts);

                foreach (var ent in tempEnts)
                    OnEntityAdded(ent);

                PlayerConnected(0);

            }
            _ticks++;

            if (_ticks < 99)
                return;

            Update100();
            _ticks = 0;

        }

        public void Update100()
        {

            foreach (string PlanetName in EnvironmentHazardCore.VoxelList)
            {

                foreach (var planet in Planets)
                {
                    if (planet == null || planet.MarkedForClose) continue;
                    if (planet.Generator.Id.SubtypeName == PlanetName)
                    {

                        //List<VRage.Game.Entity.MyEntity> Entities = null;

                        var sphere = new BoundingSphereD(planet.PositionComp.GetPosition(), planet.AverageRadius);

                        Entities.Clear();

                        MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, Entities);
                        foreach (var Entity in Entities)
                        {
                            if (Entity == null) continue;
                            if (Entity?.Physics == null) continue;
                            if (Entity.Closed || Entity.MarkedForClose) continue;
                            if (Entity?.Physics == null) continue;

                            var character = Entity as IMyCharacter;
                            if (character != null)
                            {

                                BurningCharacter(character, planet);
                                continue;
                            }
                            if (Entity is IMyCubeGrid)
                            {
                                var grid = Entity as IMyCubeGrid;
                                if (grid != null)
                                {
                                    GridDamage(grid, planet);
                                    continue;
                                }

                                var floating = Entity as IMyFloatingObject;
                                if (floating != null)
                                {
                                    if (floating?.Physics == null) continue;
                                    var Pos = floating.GetPosition();
                                    var surfacePos = planet.GetClosestSurfacePointGlobal(ref Pos);
                                    var material = GetSurfaceMaterialAt(planet, ref surfacePos);
                                    if (material == null) continue;

                                    string subtype = planet.Generator.Id.SubtypeName;
                                    string name = EnvironmentHazardCore._VoxelSubtype[subtype];
                                    byte LavaIndex;
                                    GetMaterialIndex(name, out LavaIndex);
                                    if (material.Index != LavaIndex) continue;

                                    floating.Close(); // only if on top of lava voxel 
                                    continue;
                                }
                            }
                            // whatever else is passed
                        }
                    }

                }
            }
        }
        private void BurningCharacter(IMyCharacter character, MyPlanet planet)
        {


            var player = (GetPlayer(character));

            if (player == null) return;

            //insert charicter logic
            if (player.IsBot || (player.Character?.IsDead ?? true))
            {

                Debug("Player is Bot or Dead");
                return;

            }
            ulong PlayID = player.SteamUserId;
            if (MyAPIGateway.Session.IsUserInvulnerable(PlayID))
            {
                Debug("Player is Invulnerable");
                return;
            }

            if (player.Controller?.ControlledEntity?.Entity as IMyCockpit != null)
            {

                var cockpit = player.Controller.ControlledEntity.Entity as IMyCockpit;

                if (cockpit.OxygenCapacity > 0)
                {

                    //Player is in Pressurized Cockpit, so won't get damaged
                    Debug("Player in Pressurized Cockpit");
                    return;

                }
            }
            var characterPos = character.GetPosition();
            var surfacePos = planet.GetClosestSurfacePointGlobal(ref characterPos);
            if (Vector3D.DistanceSquared(characterPos, surfacePos) > 6.25) return;

            var material = GetSurfaceMaterialAt(planet, ref surfacePos);


            byte LavaIndex;
            string subtype = planet.Generator.Id.SubtypeName;
            string name = EnvironmentHazardCore._VoxelSubtype[subtype];
            GetMaterialIndex(name, out LavaIndex);//needs value from config


            if (material.Index == LavaIndex)
            {
                
                float DamageDuringEffect = (float)EnvironmentHazardCore._VoxelPlayerDmg[subtype];
                MyVisualScriptLogicProvider.SetPlayersHealth(player.IdentityId, MyVisualScriptLogicProvider.GetPlayersHealth(player.IdentityId) - DamageDuringEffect);
                MyVisualScriptLogicProvider.ShowNotification("Warning Planetary Hazard!", 1600, "Red", player.IdentityId);

            }


        }

        private void GridDamage(IMyCubeGrid grid, MyPlanet planet)
        {
            byte LavaIndex;
            if (grid == null) return;
            if (grid.Physics == null) return; // projector
            if (grid.Closed || grid.MarkedForClose) return;
            if (!IsCloseToPlanetSurface(grid, 1000, planet)) return;


            var _tmpBlocks = new List<IMySlimBlock>();
            _tmpBlocks.Clear();
            grid.GetBlocks(_tmpBlocks);

            foreach (var block in _tmpBlocks)
            {
                if (block == null) continue;

                var blockPos = GetLowestBlockPosition(block, planet);
                var surfacePos = planet.GetClosestSurfacePointGlobal(ref blockPos);
                var material = GetSurfaceMaterialAt(planet, ref surfacePos);
                if (material == null) continue;

                string subtype = planet.Generator.Id.SubtypeName;
                string name = EnvironmentHazardCore._VoxelSubtype[subtype];

                GetMaterialIndex(name, out LavaIndex);

                if (material.Index != LavaIndex) continue;

                var blockSurfaceDistance = Vector3D.Distance(blockPos, surfacePos);
                if (blockSurfaceDistance <= DamageDistance)
                {
                    //dodamage
                    Debug("danger");
                    double damage = EnvironmentHazardCore._VoxelLGDmg[subtype];
                    double dmgamount = (damage * (1.25 / blockSurfaceDistance));
                    block.DoDamage((float)dmgamount, MyDamageType.Environment, true); //  difernt amounts for large vs small and functional vs cube


                    //MyParticleEffect fx;
                    //var ent = block as IMyEntity;
                    //TryCreateFxForEntity(block, out fx);
                    //MyVisualScriptLogicProvider.CreateParticleEffectAtPosition("ExhaustSmokeReactorSmall", blockPos);
  
                }


            }
        }


        void TryCreateFxForEntity(IMySlimBlock block, out MyParticleEffect fxParticle) //broken
        {

            
            var entity = block as IMyEntity;

            Debug(block.ToString());
            fxParticle = null;
            if (entity == null)
            {

                return;
            }
            if (fxParticle == null)
            {
                string SubtypeId = "Meteory_Fire_Atmosphere";
                var matrix = entity.WorldMatrix;
                Vector3D worldPos = entity.GetPosition();
                uint parentId = entity.Render.GetRenderObjectID();
                MyParticlesManager.TryCreateParticleEffect(SubtypeId, ref matrix, ref worldPos, parentId, out fxParticle);

                Debug("particle");

            }
        }

        //Rexxar Cade
        bool IsCloseToPlanetSurface(IMyEntity entity, double distance, MyPlanet planet)
        {
            //Vector3[] corners = null;
            var pos = entity.GetPosition();

            // foreach (var corner in corners)
            //{
            var closestSurfacePoint = planet.GetClosestSurfacePointGlobal(pos);
            var distanceToSurface = Vector3D.Distance(pos, closestSurfacePoint);
            if (distanceToSurface < distance)
            {
                return true;
            }
            //}
            Debug("false");
            return false;
        }
        //Rexxar Cade
        Vector3D GetLowestBlockPosition(IMySlimBlock block, MyPlanet planet)
        {
            //Vector3[] corners = null;
            var fatBlock = block.FatBlock;
            if (fatBlock == null) // armor blocks
            {
                return block.CubeGrid.GridIntegerToWorld(block.Position);
            }

            var lowestBlockPos = default(Vector3D);
            var minDist = double.MaxValue;
            var planetCenterPos = planet.WorldMatrix.Translation;
            //block.FatBlock.LocalAABB.GetCorners(corners);
            var pos = block.FatBlock.GetPosition();
            //foreach (var corner in corners)
            //{
            var dist = Vector3D.DistanceSquared(pos, planetCenterPos);
            if (dist < minDist)
            {
                minDist = dist;
                lowestBlockPos = pos;
            }
            //}

            return lowestBlockPos;
        }


        //Rexxar Cade
        private bool GetMaterialIndex(String name, out byte index)
        {
            foreach (var mat in MyDefinitionManager.Static.GetVoxelMaterialDefinitions())
            {

                if (mat.Id.SubtypeName == name)
                {
                    index = mat.Index;

                    return true;
                }
            }
            index = default(byte);
            Debug("false");
            return false;
        }
        //Rexxar Cade
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


        private IMyPlayer GetPlayer(IMyCharacter character)
        {
            foreach (IMyPlayer player in Players)
            {
                if (player.Character != null && player.Character == character)
                    return player;
            }

            return null;
        }

        private void Debug(string str)
        {

            if (!_debug)
                return;

            MyVisualScriptLogicProvider.ShowNotificationToAll(str, 1000);

        }
        public void PlayerConnected(long id)
        {

            Players.Clear();
            MyAPIGateway.Players.GetPlayers(Players);

        }

        public void OnEntityAdded(IMyEntity entity)
        {

            if (entity as MyPlanet != null)
                Planets.Add(entity as MyPlanet);

        }
        protected override void UnloadData()
        {

            MyVisualScriptLogicProvider.PlayerSpawned -= PlayerConnected;
            MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdded;

        }

    }
}