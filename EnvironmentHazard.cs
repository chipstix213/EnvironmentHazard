using Draygo.BlockExtensionsAPI;
using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace EnvironmentHazard
{

    [MySessionComponentDescriptor(MyUpdateOrder.Simulation)]
    public class EnvironmentHazardCore : MySessionComponentBase
    {

        //MyDefinitionId enviromentDef;

        public DefinitionExtensionsAPI DefExAPI;

        public static MyStringId EnviroHazardComp = MyStringId.GetOrCompute("EnvironmentHazardComponent");
        public static MyStringId PlanetHazzards = MyStringId.GetOrCompute("PlanetHazzards");

        public static MyStringId GasGiant = MyStringId.GetOrCompute("GasGiant"); 
        public static MyStringId GasLargeGridDamage = MyStringId.GetOrCompute("GasLargeGridDamage");
        public static MyStringId GasSmallGridDamage = MyStringId.GetOrCompute("GasSmallGridDamage");
        public static MyStringId GasPlayerDamage = MyStringId.GetOrCompute("GasPlayerDamage");

        public static MyStringId Voxel = MyStringId.GetOrCompute("Voxel");
        public static MyStringId VoxelType = MyStringId.GetOrCompute("VoxelType");
        public static MyStringId VoxelLargeGridDamage = MyStringId.GetOrCompute("VoxelLargeGridDamage");
        public static MyStringId VoxelSmallGridDamage = MyStringId.GetOrCompute("VoxelSmallGridDamage");
        public static MyStringId VoxelDistance = MyStringId.GetOrCompute("VoxelDistance");
        public static MyStringId VoxelPlayerDamage = MyStringId.GetOrCompute("VoxelPlayerDamage");
        public static MyStringId VoxelPlayerAmount = MyStringId.GetOrCompute("VoxelPlayerAmount");

        public static MyStringId Weather = MyStringId.GetOrCompute("Weather");
        public static MyStringId WeatherType = MyStringId.GetOrCompute("WeatherType");
        public static MyStringId WeatherLargeGridDamage = MyStringId.GetOrCompute("WeatherLargeGridDamage");
        public static MyStringId WeatherSmallGridDamage = MyStringId.GetOrCompute("WeatherSmallGridDamage");
        public static MyStringId WeatherPlayerDamage = MyStringId.GetOrCompute("WeatherPlayerDamage");
        public static MyStringId WeatherPlayerAmount = MyStringId.GetOrCompute("WeatherPlayerAmount");

        public static MyStringId Atmo = MyStringId.GetOrCompute("Atmo");
        public static MyStringId AtmoDamage = MyStringId.GetOrCompute("AtmoDamage");
        public static MyStringId AtmoGridEffects = MyStringId.GetOrCompute("AtmoGridEffects");

        public static List<string> GasList { get; private set; }
        public static Dictionary<string, double> _GasLGDmg = new Dictionary<string, double>();
        public static Dictionary<string, double> _GasSGDmg = new Dictionary<string, double>();
        public static Dictionary<string, double> _GasPlayerDmg = new Dictionary<string, double>();

        public static List<string> VoxelList { get; private set; }
        public static Dictionary<string, string> _VoxelSubtype = new Dictionary<string, string>();
        public static Dictionary<string, double> _VoxelLGDmg = new Dictionary<string, double>();
        public static Dictionary<string, double> _VoxelSGDmg = new Dictionary<string, double>();
        public static Dictionary<string, double> _VoxelDist = new Dictionary<string, double>();
        public static Dictionary<string, bool> _VoxelPlayer = new Dictionary<string, bool>();
        public static Dictionary<string, double> _VoxelPlayerDmg = new Dictionary<string, double>();

        public static List<string> AtmoList { get; private set; }
        public static Dictionary<string, double> _AtmoDmg = new Dictionary<string, double>();
        public static Dictionary<string, bool> _AtmoGrid = new Dictionary<string, bool>();

        public static List<string> WeatherList { get; private set; }
        public static Dictionary<string, string> _WeatherSubtype = new Dictionary<string, string>();
        public static Dictionary<string, double> _WeatherLGDmg = new Dictionary<string, double>();
        public static Dictionary<string, double> _WeatherSGDmg = new Dictionary<string, double>();
        public static Dictionary<string, bool> _WeatherPlayer = new Dictionary<string, bool>();
        public static Dictionary<string, double> _WeatherPlayerDmg = new Dictionary<string, double>();



        public static string Planetlist { get; set; }



        public EnvironmentHazardCore()
        {

            DefExAPI = new DefinitionExtensionsAPI(onApiReady);

            MyLog.Default.WriteLine("~EnvironmentHazardCore - Init");

            VoxelList = new List<string>();
            AtmoList = new List<string>();
            WeatherList = new List<string>();
            GasList = new List<string>();


        }

        private void onApiReady()
        {

            try
            {
                var defs = MyDefinitionManager.Static.GetPlanetsGeneratorsDefinitions();
                foreach (var def in defs)
                {
                    //string PlanetType;
                    if (def.Id.SubtypeName != null)
                    {
                        //PlanetType = def.Id.SubtypeName;

                        //MyLog.Default.WriteLine("PlanetHazzards Planet Subtype " + PlanetType);
                        // }

                        bool DangerGas = false;

                        DefExAPI.TryGetBool(def.Id, PlanetHazzards, GasGiant, out DangerGas);

                        if (DangerGas == true)
                        {
                            GasList.Add(def.Id.SubtypeName);

                            MyLog.Default.WriteLine("PlanetHazzards GasPlanet " + DangerGas);

                            double GasLGDmg;
                            if (DefExAPI.TryGetDouble(def.Id, PlanetHazzards, GasLargeGridDamage, out GasLGDmg))
                            {
                                MyLog.Default.WriteLine("PlanetHazzards Gas Amount " + GasLGDmg);
                            }
                            else
                            {
                                GasLGDmg = 0;
                                MyLog.Default.WriteLine("PlanetHazzards Fail");
                            }
                            double GasSGDmg;
                            if (DefExAPI.TryGetDouble(def.Id, PlanetHazzards, GasSmallGridDamage, out GasSGDmg))
                            {
                                MyLog.Default.WriteLine("PlanetHazzards Gas Amount " + GasSGDmg);
                            }
                            else
                            {
                                GasSGDmg = 0;
                                MyLog.Default.WriteLine("PlanetHazzards Fail");
                            }
                            double GasPlayerDmg;
                            if (DefExAPI.TryGetDouble(def.Id, PlanetHazzards, GasPlayerDamage, out GasPlayerDmg))
                            {
                                 MyLog.Default.WriteLine("PlanetHazzards Gas player Amount " + GasPlayerDmg);
                            }
                            else
                            {
                                GasPlayerDmg = 0;
                                 MyLog.Default.WriteLine("PlanetHazzards Fail");
                            }
                            _GasLGDmg.Add(def.Id.SubtypeName, GasLGDmg);
                            _GasSGDmg.Add(def.Id.SubtypeName, GasSGDmg);
                            _GasPlayerDmg.Add(def.Id.SubtypeName, GasPlayerDmg);
                        }
                        else
                        {
                            MyLog.Default.WriteLine("Gas Gient false " + def.Id.SubtypeName);

                            bool DangerVoxel = false;

                            DefExAPI.TryGetBool(def.Id, PlanetHazzards, Voxel, out DangerVoxel);

                            if (DangerVoxel)
                            {
                                VoxelList.Add(def.Id.SubtypeName);

                                MyLog.Default.WriteLine("PlanetHazzards Voxel Damage " + DangerVoxel);

                                string Voxels;
                                if (DefExAPI.TryGetString(def.Id, PlanetHazzards, VoxelType, out Voxels))
                                {

                                    MyLog.Default.WriteLine("PlanetHazzards Voxel Type " + Voxels);
                                }
                                else
                                {
                                    Voxels = "Stone";
                                    MyLog.Default.WriteLine("PlanetHazzards Voxel Type Fail");
                                }

                                double VoxLGDmg;
                                if (DefExAPI.TryGetDouble(def.Id, PlanetHazzards, VoxelLargeGridDamage, out VoxLGDmg))
                                {
                                    MyLog.Default.WriteLine("PlanetHazzards Dmage Amount " + VoxLGDmg);
                                }
                                else
                                {
                                    VoxLGDmg = 0;
                                    MyLog.Default.WriteLine("PlanetHazzards Dmage Amount Fail");
                                }

                                double VoxSGDmg;
                                if (DefExAPI.TryGetDouble(def.Id, PlanetHazzards, VoxelSmallGridDamage, out VoxSGDmg))
                                {
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage from Voxels " + VoxSGDmg);

                                }
                                else
                                {
                                    VoxSGDmg = 0;
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage Fail");
                                }
                                double VoxDist;
                                if (DefExAPI.TryGetDouble(def.Id, PlanetHazzards, VoxelDistance, out VoxDist))
                                {
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage from Voxels " + VoxDist);

                                }
                                else
                                {
                                    VoxDist = 0;
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage Fail");
                                }
                                bool VoxPlay;
                                if (DefExAPI.TryGetBool(def.Id, PlanetHazzards, VoxelPlayerDamage, out VoxPlay))
                                {
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage from Voxels " + VoxPlay);

                                }
                                else
                                {
                                    VoxPlay = false;
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage Fail");
                                }
                                double VoxPlayerDmg;
                                if (DefExAPI.TryGetDouble(def.Id, PlanetHazzards, VoxelPlayerAmount, out VoxPlayerDmg))
                                {
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage from Voxels " + VoxPlayerDmg);

                                }
                                else
                                {
                                    VoxPlayerDmg = 0;
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage Fail");
                                }

                                _VoxelSubtype.Add(def.Id.SubtypeName, Voxels);
                                _VoxelLGDmg.Add(def.Id.SubtypeName, VoxLGDmg);
                                _VoxelSGDmg.Add(def.Id.SubtypeName, VoxSGDmg);
                                _VoxelDist.Add(def.Id.SubtypeName, VoxDist);
                                _VoxelPlayer.Add(def.Id.SubtypeName, VoxPlay);
                                _VoxelPlayerDmg.Add(def.Id.SubtypeName, VoxPlayerDmg);
                            }

                            bool DangerAtmo = false;

                            DefExAPI.TryGetBool(def.Id, PlanetHazzards, Atmo, out DangerAtmo);

                            if (DangerAtmo)
                            {
                                AtmoList.Add(def.Id.SubtypeName);

                                MyLog.Default.WriteLine("PlanetHazzards Atmo Damage " + DangerAtmo);


                                double AtmoDmg;
                                if (DefExAPI.TryGetDouble(def.Id, PlanetHazzards, AtmoDamage, out AtmoDmg))
                                {
                                    MyLog.Default.WriteLine("PlanetHazzards Atmo Amount " + AtmoDmg);
                                }
                                else
                                {
                                    AtmoDmg = 0;
                                    MyLog.Default.WriteLine("PlanetHazzards Atmo Amount Fail");
                                }
                                bool AtmoGrid;
                                if (DefExAPI.TryGetBool(def.Id, PlanetHazzards, VoxelSmallGridDamage, out AtmoGrid))
                                {
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage from Voxels " + AtmoGrid);

                                }
                                else
                                {
                                    AtmoGrid = false;
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage Fail");
                                }

                                _AtmoDmg.Add(def.Id.SubtypeName, AtmoDmg);
                                _AtmoGrid.Add(def.Id.SubtypeName, AtmoGrid);
                            }

                            bool DangerWeather = false;

                            DefExAPI.TryGetBool(def.Id, PlanetHazzards, Weather, out DangerWeather);

                            if (DangerWeather)
                            {
                                WeatherList.Add(def.Id.SubtypeName);

                                MyLog.Default.WriteLine("PlanetHazzards Weather Damage " + DangerWeather);

                                string WeatherID;
                                if (DefExAPI.TryGetString(def.Id, PlanetHazzards, WeatherType, out WeatherID))
                                {
                                    MyLog.Default.WriteLine("PlanetHazzards Weather Type " + WeatherID);
                                }
                                else
                                {
                                    WeatherID = "Rain";
                                    MyLog.Default.WriteLine("PlanetHazzards Weather Type Fail");
                                }

                                double WeatherLGDmg;
                                if (DefExAPI.TryGetDouble(def.Id, PlanetHazzards, WeatherLargeGridDamage, out WeatherLGDmg))
                                {
                                    MyLog.Default.WriteLine("PlanetHazzards Weather Amount " + WeatherLGDmg);
                                }
                                else
                                {
                                    WeatherLGDmg = 0;
                                    MyLog.Default.WriteLine("PlanetHazzards Weather Amount Fail");
                                }

                                double WeatherSGDmg;
                                if (DefExAPI.TryGetDouble(def.Id, PlanetHazzards, WeatherSmallGridDamage, out WeatherSGDmg))
                                {
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage for Weather " + WeatherSGDmg);

                                }
                                else
                                {
                                    WeatherSGDmg = 0;
                                    MyLog.Default.WriteLine("PlanetHazzards Grid Damage Fail");
                                }
                                bool WeatherPlay;
                                if (DefExAPI.TryGetBool(def.Id, PlanetHazzards, WeatherPlayerDamage, out WeatherPlay))
                                {
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage from Weatherels " + WeatherPlay);

                                }
                                else
                                {
                                    WeatherPlay = false;
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage Fail");
                                }
                                double WeatherPlayerDmg;
                                if (DefExAPI.TryGetDouble(def.Id, PlanetHazzards, WeatherPlayerAmount, out WeatherPlayerDmg))
                                {
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage from Weatherels " + WeatherPlayerDmg);

                                }
                                else
                                {
                                    WeatherPlayerDmg = 0;
                                    MyLog.Default.WriteLine("PlanetHazzards Emable Grid Damage Fail");
                                }

                                _WeatherSubtype.Add(def.Id.SubtypeName, WeatherID);
                                _WeatherLGDmg.Add(def.Id.SubtypeName, WeatherLGDmg);
                                _WeatherSGDmg.Add(def.Id.SubtypeName, WeatherSGDmg);
                                _WeatherPlayer.Add(def.Id.SubtypeName, WeatherPlay);
                                _WeatherPlayerDmg.Add(def.Id.SubtypeName, WeatherPlayerDmg);

                            }
                        }

                    }

                }


                string Planetlist = '\u0022' + string.Join("\", \"", VoxelList) + '\u0022';
                MyLog.Default.WriteLine("PlanetHazzards Voxel Dmage True " + Planetlist);

                foreach (string AtmoPlanet in AtmoList)
                {
                    MyLog.Default.WriteLine("PlanetHazzards Atmo Dmage True " + AtmoPlanet);
                }

                foreach (string WethPlanet in WeatherList)
                {
                    MyLog.Default.WriteLine("PlanetHazzards Weather Dmage True " + WethPlanet);
                }

                foreach (string GasPlanet in GasList)
                {
                    MyLog.Default.WriteLine("PlanetHazzards Gas Giant True " + GasPlanet);
                }
            }

            catch (Exception ex)
            {
                MyLog.Default.WriteLine(ex);
            }

        }

        protected override void UnloadData()
        {
            base.UnloadData();
            DefExAPI?.UnloadData();
            DefExAPI = null;
        }

    }
}


