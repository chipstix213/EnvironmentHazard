using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace EnvironmentHazard
{

	[MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
	public class EnviromentHazardRadiation : MySessionComponentBase
	{

		public string PlanetName;
		public float MinAirDensityForEffect = 0.6f;
		public float DamageDuringEffect = 5;

		public List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
		public List<MyPlanet> Planets = new List<MyPlanet>();
		public List<IMyPlayer> Players = new List<IMyPlayer>();

		public bool SetupComplete = false;

		internal byte _ticks = 0;
		internal List<IHitInfo> _rayHits = new List<IHitInfo>();
		internal bool _debug = false;

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
			foreach (string PlanetName in EnvironmentHazardCore.AtmoList)
			{ 
				
			Debug("Player Count: " + Players.Count);

				for (int i = Players.Count - 1; i >= 0; i--)
				{

					var player = Players[i];

					if (player.IsBot || (player.Character?.IsDead ?? true))
					{

						Debug("Player is Bot or Dead");
						continue;

					}

					ulong PlayID = player.SteamUserId;
					if (MyAPIGateway.Session.IsUserInvulnerable(PlayID))
					{
						//MyVisualScriptLogicProvider.ShowNotification("Player is Invulnerable", 1600, "Red", player.IdentityId);
						Debug("Player is Invulnerable");
						continue;
					}

					if (player.Controller?.ControlledEntity?.Entity as IMyCockpit != null)
					{

						var cockpit = player.Controller.ControlledEntity.Entity as IMyCockpit;

						if (cockpit.OxygenCapacity > 0)
						{

							//Player is in Pressurized Cockpit, so won't get affected by weather
							Debug("Player in Pressurized Cockpit");
							continue;

						}

					}

					var playerPos = player.Character.WorldAABB.Center;

					for (int j = Planets.Count - 1; j >= 0; j--)
					{

						var planet = Planets[j];

						if (planet == null || planet.MarkedForClose)
						{

							Debug("Planet Null or Marked Closed");
							continue;

						}

						if (planet.Generator.Id.SubtypeName != PlanetName)
						{

							Debug("Not Near Eligible Planet");
							continue;

						}

						if (planet.PositionComp.WorldAABB.Contains(playerPos) == VRageMath.ContainmentType.Disjoint)
						{

							Debug("Player Outside Planet Bounding Box");
							continue;

						}

						var airDensity = planet.GetAirDensity(playerPos);

						if (airDensity < MinAirDensityForEffect)
						{

							Debug("Air Density Too Low");
							continue;

						}

						//Maybe do Temperature Here Later?

						if (MyVisualScriptLogicProvider.IsOnDarkSide(planet, playerPos))
						{

							Debug("Dark Side Of Planet, So No Effect");
							continue;

						}

						var sunDir = MyVisualScriptLogicProvider.GetSunDirection();
						_rayHits.Clear();
						//Raycast Checks 800m. Probably should not be higher for performance considerations.
						MyAPIGateway.Physics.CastRay(playerPos, sunDir * 800 + playerPos, _rayHits);

						bool clear = true;

						foreach (var hit in _rayHits)
						{

							if (hit.HitEntity == player.Character || hit.HitEntity == player.Character.EquippedTool)
							{

								continue;

							}

							clear = false;
							break;

						}

						if (!clear)
						{

							Debug("Raycast Detects Object Blocking Sun");
							continue;

						}

						//Player is in the light. BURN THEM!!! BURN THEM TO A CRISPY PILE OF CRUMBS!!!!
						string subtype = planet.Generator.Id.SubtypeName;
						DamageDuringEffect = (float)EnvironmentHazardCore._AtmoDmg[subtype];
						MyVisualScriptLogicProvider.SetPlayersHealth(player.IdentityId, MyVisualScriptLogicProvider.GetPlayersHealth(player.IdentityId) - DamageDuringEffect);
						MyVisualScriptLogicProvider.ShowNotification("Severe Solar Exposure. Seek Shelter Immediately.", 1600, "Red", player.IdentityId);

					}
				}
			}

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
