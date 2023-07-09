using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace EnvironmentHazard
{

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class GasGiantslogic : MySessionComponentBase
    {

        public string PlanetName;
        public float DamageDuringEffect = 15;

        public List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
        public List<MyPlanet> Planets = new List<MyPlanet>();
        public List<IMyPlayer> Players = new List<IMyPlayer>();



        private readonly Random _random = new Random();
        private List<LineD> lines = new List<LineD>();

        public bool SetupComplete = false;

        internal byte _ticks = 0;
        internal byte _block = 0;
        internal bool _debug = true;

        // The rate at which the grid takes damage per second when descending into an atmosphere
        private float DESCENT_DAMAGE_RATE = 1.0f;


        // Reference to the grid
        private IMyCubeGrid grid;

        // Lists for storing the large grid and small grid blocks
        private List<IMySlimBlock> largeGridBlocks = new List<IMySlimBlock>();
        private List<IMySlimBlock> smallGridBlocks = new List<IMySlimBlock>();


        public override void UpdateBeforeSimulation()
        {



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

            foreach (string PlanetName in EnvironmentHazardCore.GasList)
            {

                try
                {
                    foreach (var planet in Planets)
                    {
                        if (planet == null || planet.MarkedForClose) continue;

                        if (planet.Generator.Id.SubtypeName == PlanetName)
                        {
                           var Grav = ((MySphericalNaturalGravityComponent)planet.Components.Get<MyGravityProviderComponent>()).GravityLimit; //GravityLimit

                            var sphere = new BoundingSphereD(planet.PositionComp.GetPosition(), planet.AverageRadius + Grav); //planet.AtmosphereAltitude);

                            var Entities = MyAPIGateway.Entities.GetEntitiesInSphere(ref sphere);

                            foreach (var entity in Entities)
                            {
                                if (entity == null) continue;
                                if (entity?.Physics == null) continue;
                                if (entity.Closed || entity.MarkedForClose) continue;

                                if (entity is IMyCharacter)
                                {
                                    if (entity == null) continue;

                                    CrushPlayer(entity, planet);

                                    continue;
                                }
                                if (entity is IMyFloatingObject)
                                {
                                    entity.Close();
                                    continue;
                                }

                                //BlocksList(entity, (MyPlanet)planet);


                                if (entity is IMyCubeGrid)
                                {
                                    // Clear the existing lists
                                    largeGridBlocks.Clear();
                                    smallGridBlocks.Clear();
                                    var grid = entity as IMyCubeGrid;
                                    GetExteriorBlocklist(grid);
                                    // Process the large grid blocks
                                    foreach (var block in largeGridBlocks)
                                    {
                                        string subtype = planet.Generator.Id.SubtypeName;
                                        double damageAmount = 10000 * EnvironmentHazardCore._GasLGDmg[subtype];

                                        block.DoDamage((float)damageAmount, MyDamageType.Environment, true);//  difernt amounts for functional vs cube

                                        Debug("Warning Gravity Hazard!");
                                    }

                                    // Process the small grid blocks
                                    foreach (var block in smallGridBlocks)
                                    {
                                        if (block == null) continue;

                                        string subtype = planet.Generator.Id.SubtypeName;
                                        double damageAmount = EnvironmentHazardCore._GasSGDmg[subtype];

                                        block.DoDamage(((float)damageAmount / 1), MyDamageType.Environment, true);//  difernt amounts for functional vs cube

                                        Debug("Warning Gravity Hazard!");
                                    }

                                }
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    Debug(ex.ToString());
                    MyLog.Default.WriteLineAndConsole(ex.ToString());
                }

            }


        }


        /*
        private void BlocksList(IMyEntity entity, MyPlanet planet)
        {
            if (planet == null) return;
            if (entity == null) return;

            var grid = entity as IMyCubeGrid;
            if (grid == null) return;


            IMySlimBlock block = GetRandomExteriorBlock(grid);
            //Filter off Large Grid blocks
            if (block.CubeGrid.GridSizeEnum == MyCubeSize.Large)
            {
                CrushLargeBlock(block, planet);
            }
            //anything left is Small Grid blocks
            else if (block.CubeGrid.GridSizeEnum == MyCubeSize.Small)
            {
                CrushSmallBlock(block, planet);
            }

        }

        private void CrushSmallBlock(IMySlimBlock block, MyPlanet planet)
        {
            string subtype = planet.Generator.Id.SubtypeName;
            double damageAmount = EnvironmentHazardCore._GasSGDmg[subtype];

            block.DoDamage((float)damageAmount, MyDamageType.Environment, true);//  difernt amounts for functional vs cube

            Debug("Warning Gravity Hazard!");
        }
        private void CrushLargeBlock(IMySlimBlock block, MyPlanet planet)
        {
            string subtype = planet.Generator.Id.SubtypeName;
            double damageAmount = EnvironmentHazardCore._GasLGDmg[subtype];

            block.DoDamage((float)damageAmount, MyDamageType.Environment, true);//  difernt amounts for functional vs cube

            Debug("Warning Gravity Hazard!");
        }
        */

        private void CrushPlayer(IMyEntity entity, MyPlanet planet)
        {

            var character = entity as IMyCharacter;
            var player = (GetPlayer(character));


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
            string subtype = planet.Generator.Id.SubtypeName;
            DamageDuringEffect = (float)EnvironmentHazardCore._GasPlayerDmg[subtype];
            MyVisualScriptLogicProvider.SetPlayersHealth(player.IdentityId, MyVisualScriptLogicProvider.GetPlayersHealth(player.IdentityId) - DamageDuringEffect);
            MyVisualScriptLogicProvider.ShowNotification("Warning Gravity Hazard!", 1600, "Red", player.IdentityId);
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


        //rexxar code...
        private void GetExteriorBlocklist(IMyCubeGrid grid)  //raycast to get a single random exterior block
        {
            //if (grid == null) return;
            var ExteriorBlocks = new List<IMySlimBlock>();
            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);
            foreach (var block in blocks)
            {
                Vector3D posInt = grid.GridIntegerToWorld(block.Position);
                Vector3D posExt = RandomPositionFromPoint(ref posInt, grid.WorldAABB.HalfExtents.Length());
                Vector3I? blockPos = grid.RayCastBlocks(posExt, posInt);
                if (blockPos.HasValue)
                {
                    IMySlimBlock exteriorblock = grid.GetCubeBlock(blockPos.Value);
                    ExteriorBlocks.Add(exteriorblock);
                }
                if (ExteriorBlocks.Count > 80) break;
            }
            foreach (var block in ExteriorBlocks)
                // Filter off Large Grid blocks
                if (block.CubeGrid.GridSizeEnum == MyCubeSize.Large)
                {
                    if (block == null) continue;
                    largeGridBlocks.Add(block);
                }
                // anything left is Small Grid blocks
                else if (block.CubeGrid.GridSizeEnum == MyCubeSize.Small)
                {
                    if (block == null) continue;
                    smallGridBlocks.Add(block);
                }

        }
        private IMySlimBlock GetRandomExteriorBlock(IMyCubeGrid grid)  //raycast to get a single random exterior block
        {
            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);
            Vector3D posInt = grid.GridIntegerToWorld(blocks.GetRandomItemFromList().Position);
            Vector3D posExt = RandomPositionFromPoint(ref posInt, grid.WorldAABB.HalfExtents.Length());
            Vector3I? blockPos = grid.RayCastBlocks(posExt, posInt);
            return blockPos.HasValue ? grid.GetCubeBlock(blockPos.Value) : null;

        }
        public static Vector3D RandomPositionFromPoint(ref Vector3D start, double distance)
        {
            Random rnd = new Random();
            double z = rnd.NextDouble() * 2 - 1;  // between -1 and 1
            double piVal = rnd.NextDouble() * 2 * Math.PI;
            double zSqrt = Math.Sqrt(1 - z * z);  //between 0 and 1 (biased to 0)
            var direction = new Vector3D(zSqrt * Math.Cos(piVal), zSqrt * Math.Sin(piVal), z);

            direction.Normalize();
            start += direction * -2;
            return start + direction * distance;
        }


        private void Debug(string str)
        {

            if (!_debug)
                return;

            MyVisualScriptLogicProvider.ShowNotificationToAll(str, 1000);

        }

        public void OnEntityAdded(IMyEntity entity)
        {

            if (entity as MyPlanet != null)
                Planets.Add(entity as MyPlanet);

        }

        public void PlayerConnected(long id)
        {

            Players.Clear();
            MyAPIGateway.Players.GetPlayers(Players);

        }

        protected override void UnloadData()
        {

            MyVisualScriptLogicProvider.PlayerSpawned -= PlayerConnected;
            MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdded;

        }

    }

}
