
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
using VRage.Utils;
using VRageMath;


namespace EnvironmentHazard
{

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class EnviromentHazardWeather : MySessionComponentBase
    {
        public static EnviromentHazardWeather Instance; // the only way to access session comp from other classes and the only accepted static field.
        internal bool _debug = true;
        internal byte _ticks = 0;
        public bool SetupComplete = false;
        public float DamageDuringEffect = 15f;
        public List<MyPlanet> Planets = new List<MyPlanet>();
        public List<IMyPlayer> Players = new List<IMyPlayer>();
        public List<MyEntity> Entities = new List<MyEntity>();
        public List<IMySlimBlock> blocks = new List<IMySlimBlock>();
        public List<string> badweather = new List<string>();


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

            foreach (string PlanetName in EnvironmentHazardCore.WeatherList)
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
                                //check if player is inside
                                WeatherCharacter(character, planet);
                                continue;
                            }
                            if (Entity is IMyCubeGrid)
                            {
                                var grid = Entity as IMyCubeGrid;
                                if (grid != null)
                                {
                                    WeatherGrid(grid, planet);
                                    continue;
                                }
                            }

                            var floating = Entity as IMyFloatingObject;
                            if (floating != null)
                            {
                                bool checkbool;
                                IsInsideWeather(floating, planet, out checkbool);
                                if (checkbool == false) continue;
                                if (floating?.Physics == null) continue;

                                floating.Close(); // only if in side center of weather
                                continue;
                            }

                            // whatever else is passed
                        }
                    }

                }
            }
        }
        private void WeatherCharacter(IMyCharacter character, MyPlanet planet)
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
            bool weatherbool;
            IsInsideWeather(character, planet, out weatherbool);
            if (weatherbool == true)
            {
                MyVisualScriptLogicProvider.SetPlayersHealth(player.IdentityId, MyVisualScriptLogicProvider.GetPlayersHealth(player.IdentityId) - DamageDuringEffect);
                MyVisualScriptLogicProvider.ShowNotification("Warning Weather Hazard!", 2, "Red", player.IdentityId);
            }
        }

        private void WeatherGrid(IMyCubeGrid grid, MyPlanet planet)
        {
            string subtype = planet.Generator.Id.SubtypeName;
            if (grid == null) return;
            if (grid.Physics == null) return; // projector
            if (grid.Closed || grid.MarkedForClose) return;
            bool weatherbool;
            IsInsideWeather(grid, planet, out weatherbool);
            if (weatherbool == true)
            {
                Debug("weather hazzard");
                blocks.Clear();
                grid.GetBlocks(blocks);
                foreach (var block in blocks)
                {
                    if (block == null) continue;

                    //string name = block.ToString();

                    //Debug(name);

                    float damageAmount = (10f);

                    block.DoDamage(damageAmount, MyDamageType.Environment, true);//  difernt amounts for functional vs cube

                    Debug("Warning weather Hazard!");
                }
            }

        }



        private void IsInsideWeather(IMyEntity entity, MyPlanet planet, out bool weatherbool)
        {
            weatherbool = false;

            //foreach (WeatherDamageType weather in weathersubtypes) //generate list.
            //{
            string subtype = planet.Generator.Id.SubtypeName;
            string weathertype = EnvironmentHazardCore._WeatherSubtype[subtype];



            if (MyAPIGateway.Session.WeatherEffects.GetWeather(entity.WorldVolume.Center) == weathertype)
            {
                Debug("true!");
                weatherbool = true;
                return;
            }

            //}

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