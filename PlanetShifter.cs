//////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////
//Alternis Kerbol project by NovaSilisko - Jool & Duna color maps by Winston//
//You may use this code for reference purposes, all I ask is that you       //
//include a small note in your readme if you use anything from here. If you //
//want to make a derivative mod, just PM me to let me know. I can help out  //
//with any problems (to an extent, I barely understand what's going on here)//
//Space_Kraken's very early pre-pre-alpha dev looksies                      //
//////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Collections;
using System;
using System.IO;
using UnityEngine;

namespace AlternisKerbol
{
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class PlanetShifter : MonoBehaviour
	{
		#region variables
		
		static bool alternisDone = false;
		//do NOT attempt to do anything twice, stuff loading at the main menu is kinda weird in that regard, it seems to run Awake and Start twice.
		
		public bool alternisEnabled = true;
		//with this loaded by CFG, it's a lot easier than uninstalling if you want to switch back to another save
		
		public bool allowHyperWarp = false;
		//remove 5x warp, scoot all the values over, and add 1,000,000x warp at the end
		
		public float globalCometBright = 1.0f;
		//Global comet brightness multiplier
		
		public bool enableKerbinMoon = false;
		//Relocate bop and put it as a moon of Kerbin
		
		//private bool mapBuildMode = false; //if TRUE, build maps and then quit the game!
		
		//Warp stuff
		private float[] newWarpLimits = { 0, 0, 0, 0, 0, 0, 0, 200000 };
		private float[] newWarpLimitsHyper = { 0, 0, 0, 0, 0, 0, 200000, 1000000 };
		private float[] newWarps = { 1, 10, 50, 100, 1000, 10000, 100000, 1000000 };
		
		//Planets to move
		public CelestialBody cb_sun = null;
		public CelestialBody cb_moho = null;
		public CelestialBody cb_eve = null;
		public CelestialBody cb_gilly = null;
		public CelestialBody cb_kerbin = null;
		public CelestialBody cb_duna = null;
		public CelestialBody cb_ike = null;
		public CelestialBody cb_mun = null;
		public CelestialBody cb_minmus = null;
		public CelestialBody cb_jool = null;
		public CelestialBody cb_vall = null;
		public CelestialBody cb_tylo = null;
		public CelestialBody cb_bop = null;
		public CelestialBody cb_pol = null;
		public CelestialBody cb_laythe = null;
		public CelestialBody cb_dres = null;
		public CelestialBody cb_eeloo = null;
		
		//Textures
		public Texture2D newJoolTexture;
		public Texture2D rampBlue;
		public Texture2D rampRed;
		public Texture2D newTyloHeight;
		public Texture2D newTyloColor;
		public Texture2D newLaytheHeight;
		public Texture2D newLaytheScaledColor;
		public Texture2D newLaytheScaledBump;
		public Texture2D newTyloScaledColor;
		public Texture2D newTyloScaledBump;
		public Texture2D newDunaHeight;
		public Texture2D newDunaColor;
		public Texture2D newDunaScaledColor;
		public Texture2D newDunaScaledBump;
		
		//Texture paths
		public string path = "AlternisKerbol/Textures/";
		public string path2 = "AlternisKerbol/Models/";
		public string path3 = "GameData/AlternisKerbol/PluginData/Heightmaps/";
		
		//Texture import sizes
		private int tyloHeightTexWidth = 4096;
		private int tyloColorTexWidth = 1024;
		private int dunaHeightTexWidth = 2048;
		private int dunaColorTexWidth = 1024;
		private int laytheHeightTexWidth = 2048;
		#endregion
		
		//Following equations based on what was found in the RealSolarSystem code - a real breakthrough, I couldn't figure out why these things weren't changing before.
		#region equations
		double GetNewPeriod(CelestialBody body)
		{
			return 2 * Math.PI * Math.Sqrt(Math.Pow(body.orbit.semiMajorAxis, 2) / 6.674E-11 * body.orbit.semiMajorAxis / (body.Mass + body.referenceBody.Mass));
		}
		
		double GetNewSOI(CelestialBody body)
		{
			return body.orbit.semiMajorAxis * Math.Pow(body.Mass / body.orbit.referenceBody.Mass, 0.4);
		}
		
		double GetNewHillSphere(CelestialBody body)
		{
			return body.orbit.semiMajorAxis * (1.0 - body.orbit.eccentricity) * Math.Pow(body.Mass / body.orbit.referenceBody.Mass, 1 / 3);
		}
		#endregion
		
		void Start()
		{
			//Load up all the settings crap
			#region settings
			ConfigNode AKsettings = null;
			foreach(ConfigNode node in GameDatabase.Instance.GetConfigNodes("AlternisSettings"))
				AKsettings = node;
			
			bool btmp;
			if(AKsettings.HasValue("Enabled"))
				if(bool.TryParse(AKsettings.GetValue("Enabled"), out btmp))
					alternisEnabled = btmp;
			
			//Don't bother continuing, mod is disabled...
			if(!alternisEnabled)
				return;
			
			float[] wltmp = newWarpLimits;
			if(AKsettings.HasValue("HyperWarp"))
				if(bool.TryParse(AKsettings.GetValue("HyperWarp"), out btmp))
					if(btmp && TimeWarp.fetch != null)
				{
					TimeWarp.fetch.warpRates = newWarps;
					wltmp = newWarpLimitsHyper;
				}
			
			if(AKsettings.HasValue("EnableKerbinMoon"))
				if(bool.TryParse(AKsettings.GetValue("EnableKerbinMoon"), out btmp))
					enableKerbinMoon = btmp;
			
			float ftmp;
			if(AKsettings.HasValue("CometBrightness"))
				if(float.TryParse(AKsettings.GetValue("CometBrightness"), out ftmp))
					globalCometBright = ftmp;
			
			#endregion
			//Solar system is instantiated at the main menu and transferred to other scenes (apparantly), so it doesn't have to be loaded each time.
			//An advantage to this, too, is that Alternis is configured as soon as the game starts, and doesn't need to keep reordering itself.
			if(HighLogic.LoadedScene == GameScenes.MAINMENU && !alternisDone) //make extra sure something hasn't goofed and tried to run this elsewhere
			{
				#region textures
				//Load scaled maps
				newJoolTexture = GameDatabase.Instance.GetTexture(path + "newjool_dif", false);
				if(newJoolTexture != null)
					print("New jool texture loaded");
				
				newLaytheScaledBump = GameDatabase.Instance.GetTexture(path + "newlaythe_nrm", true);
				if(newLaytheScaledBump != null)
					print("New laythe bump loaded");
				
				newLaytheScaledColor = GameDatabase.Instance.GetTexture(path + "newlaythe_dif", false);
				if(newLaytheScaledColor != null)
					print("New laythe texture loaded");
				
				newTyloScaledBump = GameDatabase.Instance.GetTexture(path + "newtylo_nrm", true);
				if(newTyloScaledBump != null)
					print("New tylo bump loaded");
				
				newTyloScaledColor = GameDatabase.Instance.GetTexture(path + "newtylo_dif", false);
				if(newTyloScaledColor != null)
					print("New tylo texture loaded");
				
				newDunaScaledColor = GameDatabase.Instance.GetTexture(path + "newduna_dif", false);
				if(newDunaScaledColor != null)
					print("New duna texture loaded");
				
				newDunaScaledBump = GameDatabase.Instance.GetTexture(path + "newduna_nrm", true);
				if(newDunaScaledBump != null)
					print("New duna bump loaded");
				
				//load height maps, these are done directly since they are ignored by GameDatabase - that way they can easily be dumped from memory
				newTyloHeight = new Texture2D(tyloHeightTexWidth, tyloHeightTexWidth / 2);
				newTyloHeight.LoadImage(System.IO.File.ReadAllBytes(path3 + "tylo_newheight.png"));
				if(newTyloHeight != null)
					print("New tylo height loadeth");
				
				newDunaHeight = new Texture2D(dunaHeightTexWidth, dunaHeightTexWidth / 2);
				newDunaHeight.LoadImage(System.IO.File.ReadAllBytes(path3 + "duna_newheight.png"));
				if(newDunaHeight != null)
					print("New duna height loadeth");
				
				newLaytheHeight = new Texture2D(laytheHeightTexWidth, laytheHeightTexWidth / 2);
				newLaytheHeight.LoadImage(System.IO.File.ReadAllBytes(path3 + "laythe_newheight.png"));
				if(newLaytheHeight != null)
					print("New laythe height loadeth");
				
				
				//load color maps, same method as above
				newTyloColor = new Texture2D(tyloColorTexWidth, tyloColorTexWidth / 2);
				newTyloColor.LoadImage(System.IO.File.ReadAllBytes(path3 + "tylo_newcolor.png"));
				if(newTyloColor != null)
					print("New tylo color loadeth");
				
				newDunaColor = new Texture2D(dunaColorTexWidth, dunaColorTexWidth / 2);
				newDunaColor.LoadImage(System.IO.File.ReadAllBytes(path3 + "duna_newcolor.png"));
				if(newDunaColor != null)
					print("New duna color loadeth");
				
				
				//Load atmo ramps
				rampBlue = GameDatabase.Instance.GetTexture(path + "ramp_blue", false);
				if(rampBlue != null)
				{
					rampBlue.wrapMode = TextureWrapMode.Clamp;
					print("Blue ramp loaded");
				}
				
				rampRed = GameDatabase.Instance.GetTexture(path + "ramp_red", false);
				if(rampRed != null)
				{
					rampRed.wrapMode = TextureWrapMode.Clamp;
					print("Red ramp loaded");
				}
				#endregion
				#region locator
				
				//declare cb_sun through cb_eeloo here
				foreach(CelestialBody cb in FlightGlobals.Bodies)
					switch(cb.gameObject.name)
				{
					case "Sun":
					cb_sun = cb;
					break;
					case "Moho":
					cb_moho = cb;
					break;
					case "Eve":
					cb_eve = cb;
					break;
					case "Gilly":
					cb_gilly = cb;
					break;
					case "Kerbin":
					cb_kerbin = cb;
					break;
					case "Duna":
					cb_duna = cb;
					break;
					case "Ike":
					cb_ike = cb;
					break;
					case "Mun":
					cb_mun = cb;
					break;
					case "Minmus":
					cb_minmus = cb;
					break;
					case "Jool":
					cb_jool = cb;
					break;
					case "Tylo":
					cb_tylo = cb;
					break;
					case "Laythe":
					cb_laythe = cb;
					break;
					case "Vall":
					cb_vall = cb;
					break;
					case "Bop":
					cb_bop = cb;
					break;
					case "Pol":
					cb_pol = cb;
					break;
					case "Dres":
					cb_dres = cb;
					break;
					case "Eeloo":
					cb_eeloo = cb;
					break;
				}
				// now do your stuff to each cb_ var.
				#endregion
				
				
				
				//Do things to planets, but make sure all of them are present and accounted for first...
				//This feels like a wrong way to do things but it's only done once, so...
				
				if(cb_sun != null && cb_moho != null && cb_eve != null && cb_gilly != null &&
				   cb_kerbin != null && cb_duna != null && cb_ike != null && cb_mun != null &&
				   cb_minmus != null && cb_jool != null && cb_tylo != null && cb_vall != null &&
				   cb_bop != null && cb_pol != null && cb_laythe != null && cb_dres != null && cb_eeloo != null)
				{
					//BEGIN THE MOVEMENT!
					#region sun
					
					//Rescale the sun
					print("PlanetShifter: Shifting Sun");
					cb_sun.GeeASL = 21; //Let's give the sun a more realistic surface gravity...
					cb_sun.Radius = 75431100; //... and a smaller radius, so the density remains the same.
					#endregion
					#region moho
					
					//Do things to moho
					print("PlanetShifter: Shifting Moho...");
					cb_moho.orbit.semiMajorAxis = 14124215;
					cb_moho.orbit.inclination = 0.2502;
					cb_moho.orbit.eccentricity = 0.00175;
					//cb_moho.rotationPeriod = 8930;
					
					#endregion
					#region eve
					
					//Do things to eve
					print("PlanetShifter: Shifting Eve...");
					cb_eve.orbit.semiMajorAxis = 7985550300;
					cb_eve.orbit.inclination = 3.5;
					cb_eve.orbit.eccentricity = 0.008;
					cb_eve.atmosphericAmbientColor = new Color(0.07f, 0.055f, 0.085f);
					cb_eve.atmosphereScaleHeight = 8.2;
					cb_eve.atmosphereMultiplier = 3.303f;
					//cb_eve.Radius = 585800;
					//cb_eve.pqsController.radius = 585800;
					//cb_eve.GeeASL = 0.901;
					
					#endregion
					#region mun
					
					//Do things to the mun
					print("PlanetShifter: Shifting Mun");
					cb_mun.orbit.semiMajorAxis = 43152000;
					cb_mun.orbit.eccentricity = 0.002;
					cb_mun.orbit.inclination = 0.109;
					cb_mun.orbit.LAN = 180;
					cb_mun.orbit.argumentOfPeriapsis = 0;
					cb_mun.orbit.meanAnomalyAtEpoch = 1;
					
					cb_mun.orbitDriver.orbitColor = new Color(0.12f, 0.12f, 0.12f);
					
					cb_mun.Radius = 253000;
					cb_mun.pqsController.radius = 253000;
					
					cb_mun.GeeASL = 0.233;
					
					cb_mun.scienceValues.LandedDataValue = 3;
					cb_mun.scienceValues.InSpaceLowDataValue = 1;
					cb_mun.scienceValues.InSpaceHighDataValue = 1.5f;
					cb_mun.scienceValues.RecoveryValue = 2;
					
					PQSMod_VoronoiCraters muncraters = cb_mun.transform.GetComponentInChildren<PQSMod_VoronoiCraters>();
					if(muncraters != null)
					{
						print(muncraters.rFactor);
						print(muncraters.rOffset);
					}
					
					#endregion
					#region kerbin
					
					//Do things to kerbin
					print("PlanetShifter: Shifting Kerbin...");
					cb_kerbin.orbit.semiMajorAxis = 68506000;
					cb_kerbin.orbit.eccentricity = 0.02;
					cb_kerbin.orbit.inclination = 0.4;
					cb_kerbin.orbit.LAN = 0;
					cb_kerbin.orbit.argumentOfPeriapsis = 0;
					cb_kerbin.orbit.meanAnomalyAtEpoch = 4.14;
					
					cb_kerbin.initialRotation = 208;
					cb_kerbin.atmosphericAmbientColor = new Color(0.175f, 0.18f, 0.195f);
					cb_kerbin.orbitDriver.orbitColor = cb_kerbin.pqsController.mapOceanColor;
					cb_kerbin.tidallyLocked = true;
					
					#endregion
					#region duna
					
					//Do things to duna
					print("PlanetShifter: Shifting Duna...");
					cb_duna.orbit.semiMajorAxis = 34598850;
					cb_duna.orbit.eccentricity = 0.05;
					cb_duna.orbit.inclination = 0.7;
					cb_duna.atmosphereScaleHeight = 4.5;
					cb_duna.atmosphereMultiplier = 0.178f;
					cb_duna.atmosphericAmbientColor = new Color(0.136f, 0.135f, 0.13f);
					cb_duna.tidallyLocked = true;
					
					cb_duna.pqsController.surfaceMaterial = cb_tylo.pqsController.surfaceMaterial;
					
					print("PlanetShifter: Shifting Duna Terrain");
					GameObject dunaOldHeight = cb_duna.pqsController.transform.FindChild("_Height").gameObject;
					if(dunaOldHeight != null)
					{
						foreach(PQSMod_VertexHeightNoiseVertHeightCurve2 c in dunaOldHeight.GetComponentsInChildren<PQSMod_VertexHeightNoiseVertHeightCurve2>())
						{
							c.deformity = 700;
						}
						
						PQSMod_VertexHeightMap cb_duna_vhm = dunaOldHeight.GetComponent<PQSMod_VertexHeightMap>();
						
						if(cb_duna_vhm != null && newDunaHeight != null)
						{
							Destroy(cb_duna_vhm.heightMap); //obliterate the old heightmap
							cb_duna_vhm.heightMap = ScriptableObject.CreateInstance<MapSO>(); //make a new one
							cb_duna_vhm.heightMap.CreateMap(MapSO.MapDepth.Greyscale, newDunaHeight); //turn it into something usable
							cb_duna_vhm.heightMapDeformity = 15900; //tweak deformity
							cb_duna_vhm.heightMapOffset = -200;
							Destroy(newDunaHeight); //and then nuke the texture
						}
						
					}
					//i sure am good at keeping my naming conventions
					PQSMod_VertexColorMapBlend oldDunaColor = cb_duna.transform.GetComponentInChildren<PQSMod_VertexColorMapBlend>();
					if(oldDunaColor != null && newDunaColor != null)
					{
						Destroy(oldDunaColor.vertexColorMap);
						oldDunaColor.vertexColorMap = ScriptableObject.CreateInstance<MapSO>();
						oldDunaColor.vertexColorMap.CreateMap(MapSO.MapDepth.RGB, newDunaColor);
						oldDunaColor.blend = 1.0f;
						Destroy(newDunaColor);
					}
					
					
					
					PQSMod_VertexSimplexNoiseColor oldDunaColorNoise = cb_duna.transform.GetComponentInChildren<PQSMod_VertexSimplexNoiseColor>();
					if(oldDunaColorNoise != null)
					{
						oldDunaColorNoise.order = oldDunaColor.order + 1;
						oldDunaColorNoise.blend = 0.1f;
					}
					
					PQSLandControl cb_duna_lc = cb_duna.pqsController.gameObject.GetComponentInChildren<PQSLandControl>();
					if(cb_duna_lc != null)
					{
						cb_duna_lc.modEnabled = false;
					}
					
					#endregion
					#region ike
					
					//Do things to ike
					print("PlanetShifter: Shifting Ike...");
					cb_ike.orbit.semiMajorAxis = 167988084550;
					cb_ike.orbit.eccentricity = 0.9585;
					cb_ike.orbit.inclination = 28.45;
					cb_ike.orbit.meanAnomalyAtEpoch = -1;
					cb_ike.tidallyLocked = false;
					cb_ike.rotationPeriod = 6983;
					cb_ike.Radius = 17500;
					cb_ike.pqsController.radius = 17500;
					cb_ike.GeeASL = 0.007;
					cb_ike.orbitDriver.orbitColor = new Color(0.15f, 0.15f, 0.19f);
					cb_ike.scienceValues.LandedDataValue = 20;
					cb_ike.scienceValues.spaceAltitudeThreshold = 150000;
					cb_ike.scienceValues.InSpaceLowDataValue = 5;
					cb_ike.scienceValues.InSpaceHighDataValue = 3;
					cb_ike.scienceValues.RecoveryValue = 8;
					
					#endregion
					#region gilly
					
					//Do things to gilly
					print("PlanetShifter: Shifting Gilly...");
					cb_gilly.orbit.semiMajorAxis = 58084734575;
					cb_gilly.orbit.eccentricity = 0.990504;
					cb_gilly.orbit.inclination = 225.05;
					cb_gilly.orbit.LAN = 25.3;
					cb_gilly.orbit.argumentOfPeriapsis = 79.08;
					cb_gilly.orbit.meanAnomalyAtEpoch = 4.603;
					
					cb_gilly.tidallyLocked = false;
					cb_gilly.rotationPeriod = 15034;
					cb_gilly.Radius = 8500;
					cb_gilly.pqsController.radius = 8500;
					cb_gilly.GeeASL = 0.003;
					cb_gilly.orbitDriver.orbitColor = new Color(0.15f, 0.15f, 0.19f);
					cb_gilly.scienceValues.LandedDataValue = 30;
					cb_gilly.scienceValues.spaceAltitudeThreshold = 50000;
					cb_gilly.scienceValues.InSpaceLowDataValue = 5;
					cb_gilly.scienceValues.InSpaceHighDataValue = 3;
					cb_gilly.scienceValues.RecoveryValue = 8;
					
					//string s = "Listing all components! \n";
					//foreach (Component c in cb_gilly.gameObject.GetComponentsInChildren(typeof(Component)))
					//{
					//    s += (c + " \n");
					//}
					//print(s);
					
					Transform cb_gilly_height = cb_gilly.pqsController.transform.FindChild("_Height");
					
					if(cb_gilly_height.gameObject != null)
					{
						if(cb_gilly_height.gameObject.GetComponentInChildren<PQSMod_VoronoiCraters>() == null)
						{
							PQSMod_VoronoiCraters cb_gilly_craters = cb_gilly_height.gameObject.AddComponent<PQSMod_VoronoiCraters>();
							if(cb_gilly_craters != null && muncraters != null)
							{
								print("...and giving it craters");
								cb_gilly_craters.voronoiFrequency = 7;
								cb_gilly_craters.deformation = 800;
								cb_gilly_craters.craterCurve = new AnimationCurve(muncraters.craterCurve.keys);
								cb_gilly_craters.jitter = 0.1f;
								cb_gilly_craters.jitterCurve = new AnimationCurve(muncraters.jitterCurve.keys);
								cb_gilly_craters.jitterHeight = 3;
								cb_gilly_craters.rOffset = 1;
								cb_gilly_craters.rFactor = 0.5f;
								cb_gilly_craters.voronoiDisplacement = 0.1f;
								cb_gilly_craters.order = 50;
							}
						}
					}
					
					#endregion
					#region minmus
					
					//Do things to minmus
					print("PlanetShifter: Shifting Minmus");
					cb_minmus.orbit.semiMajorAxis = 14740300;
					cb_minmus.orbit.inclination = 0.03;
					cb_minmus.orbit.meanAnomalyAtEpoch = 0.9f;
					cb_minmus.Radius = 29000;
					cb_minmus.pqsController.radius = 29000;
					cb_minmus.tidallyLocked = false;
					cb_minmus.GeeASL = 0.022;
					
					
					cb_minmus.scienceValues.LandedDataValue = 5;
					cb_minmus.scienceValues.InSpaceLowDataValue = 2;
					cb_minmus.scienceValues.InSpaceHighDataValue = 2;
					cb_minmus.scienceValues.RecoveryValue = 4;
					
				}
			}
			
			#endregion
			#region jool
			
			//Do things to jool
			print("PlanetShifter: Shifting Jool...");
			cb_jool.orbit.semiMajorAxis = 13605008470;
			cb_jool.orbit.eccentricity = 0.017;
			cb_jool.orbit.inclination = 1.957;
			cb_jool.orbit.LAN = 35;
			cb_jool.orbitDriver.orbitColor = new Color(0.45f, 0.55f, 0.7f);
			
			cb_jool.atmosphereScaleHeight = 20.3;
			cb_jool.atmosphereMultiplier = 6.5f;
			cb_jool.atmosphericAmbientColor = new Color(0.075f, 0.08f, 0.095f);
			
			//foreach (Keyframe k in cb_jool.temperatureCurve.keys)
			//{
			//    print(k.time + " " + k.value);
			//}
			
			cb_jool.temperatureCurve = new AnimationCurve();
			cb_jool.temperatureCurve.AddKey(-0.2385731f, 700f); //these values are based off the debug printout from earlier, just with a *slightly* increased lower value
			cb_jool.temperatureCurve.AddKey(99.73615f, -86.13164f);
			
			cb_jool.scienceValues.InSpaceLowDataValue = 1.5f;
			cb_jool.scienceValues.InSpaceHighDataValue = 1;
			cb_jool.scienceValues.FlyingLowDataValue = 4;
			cb_jool.scienceValues.FlyingHighDataValue = 3;
			cb_jool.scienceValues.RecoveryValue = 1.5f;
			
			#endregion
			#region tylo
			
			//Do things to ty-- oh you get the idea
			cb_tylo.orbit.semiMajorAxis = 24708887045;
			cb_tylo.Radius = 1103000;
			cb_tylo.pqsController.radius = 1103000;
			cb_tylo.GeeASL = 4.035;
			cb_tylo.orbit.eccentricity = 0.03;
			cb_tylo.orbit.inclination = 1.22;
			cb_tylo.tidallyLocked = false;
			cb_tylo.rotationPeriod = 122805;
			cb_tylo.atmosphere = true;
			cb_tylo.atmosphereScaleHeight = 6.9;
			cb_tylo.atmosphereMultiplier = 0.554f;
			cb_tylo.staticPressureASL = 1;
			cb_tylo.atmosphericAmbientColor = new Color(0.175f, 0.15f, 0.15f);
			cb_tylo.orbitDriver.orbitColor = new Color(0.4f, 0.25f, 0.15f);
			cb_tylo.maxAtmosphereAltitude = 80000;
			cb_tylo.useLegacyAtmosphere = true;
			
			cb_tylo.scienceValues.FlyingLowDataValue = 9;
			cb_tylo.scienceValues.FlyingHighDataValue = 8;
			cb_tylo.scienceValues.flyingAltitudeThreshold = 25000;
			cb_tylo.scienceValues.InSpaceLowDataValue = 5;
			cb_tylo.scienceValues.InSpaceHighDataValue = 4;
			cb_tylo.scienceValues.spaceAltitudeThreshold = 1000000;
			cb_tylo.scienceValues.LandedDataValue = 18;
			
			//tweak LOD parameters
			cb_tylo.pqsController.maxLevel = cb_kerbin.pqsController.maxLevel + 2;
			cb_tylo.pqsController.minLevel = cb_kerbin.pqsController.minLevel + 2;
			cb_tylo.pqsController.minDetailDistance = cb_kerbin.pqsController.minDetailDistance + 2;
			cb_tylo.pqsController.maxDetailDistance = cb_kerbin.pqsController.maxDetailDistance + 2;
			{
			PQSMod_VertexHeightMap cb_tylo_height = cb_tylo.transform.GetComponentInChildren<PQSMod_VertexHeightMap>();
			if(cb_tylo_height != null)
			{
				print("PlanetShifter: Shifting Tylo Terrain");
					{
				if(newTyloHeight != null)
				{
					Destroy(cb_tylo_height.heightMap); //obliterate the old heightmap
					cb_tylo_height.heightMap = ScriptableObject.CreateInstance<MapSO>(); //make a new one
					cb_tylo_height.heightMap.CreateMap(MapSO.MapDepth.Greyscale, newTyloHeight); //turn it into something usable
					cb_tylo_height.heightMapDeformity = 21500; //tweak deformity
					Destroy(newTyloHeight); //and then nuke the texture
				}
				Transform cb_tylo_height = cb_tylo.pqsController.transform.FindChild("_Height");
				{
				if(cb_tylo_height.gameObject != null)
				{
				PQSMod_VoronoiCraters cb_tylo_craters = cb_tylo_height.gameObject.AddComponent<PQSMod_VoronoiCraters>();
				if(cb_tylo_craters != null && muncraters != null)
				{
				print("...and giving it craters");
				cb_tylo_craters.voronoiFrequency = 24;
				cb_tylo_craters.deformation = 900;
				cb_tylo_craters.craterCurve = new AnimationCurve(muncraters.craterCurve.keys);
				cb_tylo_craters.craterColourRamp = FetchSolidGradient(new Color(0.58f, 0.54f, 0.4f));
				cb_tylo_craters.order = cb_tylo_height.order + 50;
				cb_tylo_craters.jitter = 0.1f;
				cb_tylo_craters.jitterCurve = new AnimationCurve(muncraters.jitterCurve.keys);
				cb_tylo_craters.jitterHeight = 3;
				cb_tylo_craters.rOffset = 1;
				cb_tylo_craters.rFactor = 1;
				cb_tylo_craters.voronoiDisplacement = muncraters.voronoiDisplacement;
				}
			}
			
			PQSMod_VertexColorMap cb_tylo_vcm = cb_tylo.pqsController.transform.GetComponentInChildren<PQSMod_VertexColorMap>();
			if(cb_tylo_vcm != null && newTyloColor)
			{
				print("PlanetShifter: Shifting tylo color");
				Destroy(cb_tylo_vcm.vertexColorMap);
				cb_tylo_vcm.vertexColorMap = ScriptableObject.CreateInstance<MapSO>();
				cb_tylo_vcm.vertexColorMap.CreateMap(MapSO.MapDepth.RGB, newTyloColor);
				Destroy(newTyloColor);
			}
			
			PQSMod_VertexSimplexHeight cb_tylo_vsh = cb_tylo.pqsController.transform.GetComponentInChildren<PQSMod_VertexSimplexHeight>();
			if(cb_tylo_vsh != null)
			{
				cb_tylo_vsh.deformity = 5500;
				cb_tylo_vsh.frequency = 5;
			}
			
			PQSMod_VertexSimplexHeightAbsolute cb_tylo_vsha = cb_tylo.pqsController.transform.GetComponentInChildren<PQSMod_VertexSimplexHeightAbsolute>();
			if(cb_tylo_vsh != null)
			{
				cb_tylo_vsh.deformity = 3100;
				cb_tylo_vsh.frequency = 6;
			}
			
			#endregion
			#region vall
			
			print("Planetshifter: shifting vall");
			
			cb_vall.orbit.semiMajorAxis = 4995040;
			cb_vall.orbit.eccentricity = 0.011;
			cb_vall.orbit.inclination = 2.337;
			
			cb_vall.GeeASL = 0.0772;
			
			cb_vall.Radius = 99800;
			cb_vall.pqsController.radius = 99800;
			
			PQSMod_VertexHeightMap cb_vall_hm = cb_vall.pqsController.gameObject.GetComponentInChildren<PQSMod_VertexHeightMap>();
			if(cb_vall_hm != null)
			{
				cb_vall_hm.heightMapDeformity = 1800;
			}
			
			#endregion
			#region dres
			
			print("Planetshifter: shifting dres");
			
			cb_dres.orbit.semiMajorAxis = 8000025;
			cb_dres.orbit.eccentricity = 0.002;
			cb_dres.orbit.inclination = 0.000;
			
			cb_dres.scienceValues.LandedDataValue = 6;
			cb_dres.scienceValues.InSpaceLowDataValue = 5;
			cb_dres.scienceValues.InSpaceHighDataValue = 4;
			cb_dres.scienceValues.RecoveryValue = 5;
			
			#endregion
			#region laythe
			
			//Laythe is just as important in the grand scheme of things as kerbin!
			//Let's remake it completely, it deserves better.
			cb_laythe.pqsController.surfaceMaterial = new Material(cb_kerbin.pqsController.surfaceMaterial);
			cb_laythe.pqsController.maxLevel = cb_kerbin.pqsController.maxLevel;
			cb_laythe.pqsController.minLevel = cb_kerbin.pqsController.minLevel;
			cb_laythe.pqsController.minDetailDistance = cb_kerbin.pqsController.minDetailDistance;
			cb_laythe.pqsController.maxDetailDistance = cb_kerbin.pqsController.maxDetailDistance;
			cb_laythe.atmosphericAmbientColor = new Color(0.075f, 0.08f, 0.095f);
			cb_laythe.orbitDriver.orbitColor = new Color(0.15f, 0.25f, 0.05f);
			cb_laythe.orbit.LAN = 0;
			cb_laythe.orbit.argumentOfPeriapsis = 0;
			cb_laythe.orbit.meanAnomalyAtEpoch = 1;
			
			cb_laythe.bodyDescription = "Long known as Kerbin's sister moon, Laythe was seeded with life hundreds of millions of years ago when a large asteroid struck Kerbin - catapulting boulders with various life forms into orbit around Jool. Given its proximity to Kerbin, some of this impact debris found its way to Laythe, depositing a precious cargo of microbes and, according to some theories, whole plant seeds. This life quickly spread across the moon, until it became the lush green world we know today.\n\n(There are, however, fringe theorists who believe that life started on Laythe and was brought to Kerbin, but that's just absurd)";
			
			cb_laythe.scienceValues.LandedDataValue = 3;
			cb_laythe.scienceValues.SplashedDataValue = 3;
			cb_laythe.scienceValues.InSpaceLowDataValue = 1.5f;
			cb_laythe.scienceValues.InSpaceHighDataValue = 1;
			cb_laythe.scienceValues.FlyingLowDataValue = 2.5f;
			cb_laythe.scienceValues.FlyingHighDataValue = 2;
			cb_laythe.scienceValues.RecoveryValue = 3;
			
			
			print("PlanetShifter: Shifting Laythe terrain");
			PQSLandControl cb_laythe_lc = cb_laythe.pqsController.gameObject.GetComponentInChildren<PQSLandControl>();
			if(cb_laythe_lc != null)
			{
				print("Found LandControl, spewing land classes...");
				foreach(PQSLandControl.LandClass lc in cb_laythe_lc.landClasses)
				{
					print(lc.landClassName);
					print(lc.color);
					
					if(lc.landClassName == "Mud")
					{
						lc.color = new Color(0.35f, 0.25f, 0.2f);
						lc.noiseColor = new Color(0.26f, 0.27f, 0.15f);
						lc.noiseBlend = 0.8f;
						lc.noiseFrequency = 20;
					}
					
					if(lc.landClassName == "BaseLand")
					{
						lc.color = new Color(0.16f, 0.185f, 0.15f);
						lc.noiseColor = new Color(0.14f, 0.15f, 0.02f);
						lc.noiseBlend = 0.2f;
					}
					
					if(lc.landClassName == "IceCaps")
					{
						lc.latitudeRange.endStart = 0.09;
						lc.latitudeRange.endEnd = 0.1;
						//print("IceCap latitude whatsit: ss-" + lc.latitudeRange.startStart + " se-" + lc.latitudeRange.startEnd + " es-" + lc.latitudeRange.endStart + " ee-" + lc.latitudeRange.endEnd);
					}
				}
			}
			
			PQSMod_VertexHeightMap cb_laythe_hm = cb_laythe.pqsController.gameObject.GetComponentInChildren<PQSMod_VertexHeightMap>();
			if(cb_laythe_hm != null && newLaytheHeight != null)
			{
				Destroy(cb_laythe_hm.heightMap); //obliterate the old heightmap
				cb_laythe_hm.heightMap = ScriptableObject.CreateInstance<MapSO>();
				cb_laythe_hm.heightMap.CreateMap(MapSO.MapDepth.Greyscale, newLaytheHeight); //turn it into something usable
				cb_laythe_hm.heightMapOffset = -400;
				cb_laythe_hm.heightMapDeformity = 7000; //tweak deformity
				Destroy(newLaytheHeight); //and then nuke the texture
			}
			
			PQSMod_VertexHeightNoise cb_laythe_vhn = cb_laythe.pqsController.gameObject.GetComponentInChildren<PQSMod_VertexHeightNoise>();
			if(cb_laythe_vhn != null)
			{
				cb_laythe_vhn.deformity = 250;
				cb_laythe_vhn.frequency = 8;
			}
			
			#endregion
			#region bop
			
			print("pLaNeTsHiFtEr: sHiFtInG bOp");
			
			if(enableKerbinMoon)
			{
				print("Turning bop into a COMPLETELY IMPOSSIBLE moon of a moon you should be ASHAMED");
				cb_bop.orbit.eccentricity = 0.055;
				cb_bop.orbit.inclination = 0.12;
				cb_bop.orbit.semiMajorAxis = 3250900;
				
				cb_bop.Radius = 4900;
				cb_bop.pqsController.radius = 4900;
				cb_bop.GeeASL = 0.0253;
				
				cb_bop.bodyDescription = "A worthless lump of unusually-dense rock in a hopelessly unstable orbit. Despite the apparant instability, its orbit never seems to change. This merits further investigation...";
				
				cb_bop.pqsController.surfaceMaterial = cb_gilly.pqsController.surfaceMaterial;
				
				cb_bop.scienceValues.LandedDataValue = 2;
				cb_bop.scienceValues.InSpaceLowDataValue = 1;
				cb_bop.scienceValues.InSpaceHighDataValue = 1f;
				cb_bop.scienceValues.RecoveryValue = 2;
				
				//This version of bop is a lot smaller, so reduce its LOD values accordingly
				cb_bop.pqsController.maxLevel = cb_gilly.pqsController.maxLevel;
				cb_bop.pqsController.minLevel = cb_gilly.pqsController.minLevel;
				
				PQSMod_VertexSimplexHeightAbsolute cb_bop_vsha = cb_bop.pqsController.transform.GetComponentInChildren<PQSMod_VertexSimplexHeightAbsolute>();
				if(cb_bop_vsha != null)
				{
					cb_bop_vsha.seed = 348534534;
					cb_bop_vsha.deformity = 1700;
					cb_bop_vsha.frequency = 0.5;
				}
				
				PQSMod_VertexHeightNoise cb_bop_vhn = cb_bop.pqsController.transform.GetComponentInChildren<PQSMod_VertexHeightNoise>();
				if(cb_bop_vhn != null)
				{
					cb_bop_vhn.seed = 994574854;
					cb_bop_vhn.deformity = 250;
					cb_bop_vhn.frequency = 3;
				}
				
				PQSCity kraken = cb_bop.pqsController.transform.FindChild("DeadKraken").GetComponent<PQSCity>();
				if(kraken != null)
				{
					kraken.repositionRadiusOffset = 1000; //Temporary til I can find the damn thing again and set a proper altitude
				}
			}
			else
			{
				cb_bop.orbit.eccentricity = 0.0094;
				cb_bop.orbit.inclination = 0;
				cb_bop.orbit.semiMajorAxis = 14000543;
				
				cb_bop.pqsController.surfaceMaterial = cb_gilly.pqsController.surfaceMaterial;
				
				cb_bop.scienceValues.LandedDataValue = 3;
				cb_bop.scienceValues.InSpaceLowDataValue = 2;
				cb_bop.scienceValues.InSpaceHighDataValue = 1.5f;
				cb_bop.scienceValues.RecoveryValue = 3;
			}
			
			#endregion
			#region pol
			
			print("pLaNeTsHiFtEr: sHiFtInG pOl");
			cb_pol.orbit.LAN = 120;
			cb_pol.orbit.eccentricity = 0.083;
			cb_pol.orbit.semiMajorAxis = 145500000;
			
			cb_pol.scienceValues.LandedDataValue = 3;
			cb_pol.scienceValues.InSpaceLowDataValue = 2;
			cb_pol.scienceValues.InSpaceHighDataValue = 1.5f;
			cb_pol.scienceValues.RecoveryValue = 3;
			
			#endregion
			#region eeloo
			
			print("PlanetShifter: Shifting Eeloo");
			cb_eeloo.orbit.inclination = 1.15;
			cb_eeloo.orbit.eccentricity = 0.08;
			cb_eeloo.Radius = 478000;
			cb_eeloo.pqsController.radius = 478000;
			cb_eeloo.GeeASL = 0.687;
			
			#endregion
			
			#region relocator
			cb_kerbin.orbitingBodies.Remove(cb_mun);
			cb_kerbin.orbitingBodies.Remove(cb_minmus);
			cb_jool.orbitingBodies.Remove(cb_tylo);
			cb_jool.orbitingBodies.Remove(cb_vall);
			cb_duna.orbitingBodies.Remove(cb_ike);
			cb_eve.orbitingBodies.Remove(cb_gilly);
			cb_sun.orbitingBodies.Remove(cb_moho);
			//cb_sun.orbitingBodies.Remove(cb_kerbin); <- for some reason this really screws up everything... there don't seem to be any obvious adverse effects to commenting it out so, I guess it'll have to do.
			cb_sun.orbitingBodies.Remove(cb_duna);
			cb_eve.orbitingBodies.Add(cb_moho);
			cb_jool.orbitingBodies.Add(cb_kerbin);
			cb_vall.orbitingBodies.Add(cb_minmus);
			cb_jool.orbitingBodies.Add(cb_mun);
			cb_tylo.orbitingBodies.Add(cb_duna);
			cb_sun.orbitingBodies.Add(cb_vall);
			cb_sun.orbitingBodies.Add(cb_ike);
			cb_sun.orbitingBodies.Add(cb_tylo);
			cb_sun.orbitingBodies.Add(cb_gilly);
			
			if(enableKerbinMoon)
			{
				cb_jool.orbitingBodies.Remove(cb_bop);
				cb_kerbin.orbitingBodies.Add(cb_bop);
			}
			
			cb_mun.orbit.referenceBody = cb_jool;
			cb_kerbin.orbit.referenceBody = cb_jool;
			cb_duna.orbit.referenceBody = cb_tylo;
			cb_ike.orbit.referenceBody = cb_sun;
			cb_dres.orbit.referenceBody = cb_sun;
			cb_vall.orbit.referenceBody = cb_sun;
			cb_minmus.orbit.referenceBody = cb_vall;
			cb_gilly.orbit.referenceBody = cb_sun;
			cb_tylo.orbit.referenceBody = cb_sun;
			cb_moho.orbit.referenceBody = cb_eve;
			
			
			if(enableKerbinMoon)
				cb_bop.orbit.referenceBody = cb_kerbin;
			
			#endregion
			
			//finalize everything
			#region final tweaks
			foreach(CelestialBody cb2 in FlightGlobals.Bodies)
			{
				if(cb2.gameObject.name != "Sun")
				{
					cb2.sphereOfInfluence = GetNewSOI(cb2);
					cb2.hillSphere = GetNewHillSphere(cb2);
					
					//Kerbin, Laythe, and the Mun are in a Laplace resonance. To be absolutely sure that that remains the case, force the periods.
					if(cb2.gameObject.name == "Kerbin")
						cb2.orbit.period = 211926; //2 days 10 hours 52 minutes 6 seconds
					else if(cb2.gameObject.name == "Mun")
						cb2.orbit.period = 105963; //1 day 5 hours 26 minutes 3 seconds
					else if(cb2.gameObject.name == "Laythe")
						cb2.orbit.period = 52982; //14 hours 43 minutes 2 seconds
					else
						cb2.orbit.period = GetNewPeriod(cb2); //Just calculate the rest
					
					cb2.timeWarpAltitudeLimits = newWarpLimits;
					
					//Thank you, eggrobin & NathanKell
					cb2.orbit.meanAnomaly = cb2.orbit.meanAnomalyAtEpoch; // let KSP handle epoch
					cb2.orbit.orbitPercent = cb2.orbit.meanAnomalyAtEpoch / 6.2831853071795862;
					cb2.orbit.ObTAtEpoch = cb2.orbit.orbitPercent * cb2.orbit.period;
					
					cb2.CBUpdate();
				}
			}
			#endregion
			
			//god, I need to rewrite this section sometimes. I mean, it WORKS but it just feels so WRONG
			#region scaled space also known as the galaxy of terror
			GameObject kerbinAtmo = null;
			GameObject tyloAtmo = null;
			Material kerbinMat = null;
			Mesh plainSphere = null;
			Transform sun = null;
			
			foreach(Transform t in ScaledSpace.Instance.scaledSpaceTransforms)
			{
				if(t.gameObject.name == "Kerbin")
				{
					print("PlanetShifter: HACKY HACK GRABBING KERBIN ATMOSPHERE FOR DUPLICATION"); //forgive me father for i have sinned
					kerbinAtmo = t.FindChild("Atmosphere").gameObject;
					kerbinMat = t.gameObject.renderer.material;
					plainSphere = t.gameObject.GetComponent<MeshFilter>().mesh;
				}
				
				if(t.gameObject.name == "Sun")
				{
					sun = t;
				}
			}
			
			if(kerbinAtmo != null)
			{
				AtmosphereFromGround atmk = kerbinAtmo.GetComponent<AtmosphereFromGround>();
				if(atmk != null)
				{
					atmk.waveLength = new Color(0.750f, 0.670f, 0.575f);
				}
				
				foreach(Transform t in ScaledSpace.Instance.scaledSpaceTransforms)
				{
					//first do the stuff that needs new atmospheres
					if(t.gameObject.name == "Tylo" && kerbinMat != null)
					{
						print("PlanetShifter: Shifting Tylo scaled space");
						t.localScale = new Vector3(0.1838f, 0.1838f, 0.1838f);
						
						tyloAtmo = Instantiate(kerbinAtmo, t.position, t.rotation) as GameObject;
						
						tyloAtmo.name = "Atmosphere";
						tyloAtmo.transform.parent = t;
						tyloAtmo.transform.localScale = Vector3.one;
						
						Material m = new Material(kerbinMat);
						t.gameObject.renderer.material = m;
						t.gameObject.renderer.material.SetTexture("_MainTex", newTyloScaledColor);
						t.gameObject.renderer.material.SetTexture("_BumpMap", newTyloScaledBump);
						t.gameObject.renderer.material.SetTexture("_rimColorRamp", rampRed);
						t.gameObject.renderer.material.SetColor("_SpecColor", Color.black);
						
						t.gameObject.GetComponent<MeshFilter>().mesh = plainSphere;
						t.gameObject.renderer.material.SetColor("_Color", Color.grey); //Default is too bright. This matches better!
						
						AtmosphereFromGround atm = tyloAtmo.GetComponent<AtmosphereFromGround>();
						if(atm != null)
						{
							atm.planet = cb_tylo;
							atm.waveLength = new Color(0.8f, 0.93f, 0.95f);
						}
					}
					
					//and now the tweaks
					//if (t.gameObject.name == "Eve")
					//{
					//t.localScale = new Vector3(0.09751f, 0.09751f, 0.09751f);
					//}
					
					if(t.gameObject.name == "Laythe")
					{
						t.gameObject.renderer.material.SetTexture("_rimColorRamp", rampBlue);
						t.gameObject.renderer.material.SetTexture("_MainTex", newLaytheScaledColor);
						t.gameObject.renderer.material.SetTexture("_BumpMap", newLaytheScaledBump);
						t.gameObject.renderer.material.SetColor("_Color", new Color(0.75f, 0.75f, 0.75f));
						
						AtmosphereFromGround atm = t.FindChild("Atmosphere").GetComponent<AtmosphereFromGround>();
						if(atm != null)
						{
							atm.waveLength = new Color(0.730f, 0.70f, 0.640f);
						}
					}
					
					if(t.gameObject.name == "Kerbin")
					{
						t.gameObject.renderer.material.SetTexture("_rimColorRamp", rampBlue);
						
						AtmosphereFromGround atm = t.FindChild("Atmosphere").GetComponent<AtmosphereFromGround>();
						if(atm != null)
						{
							atm.waveLength = new Color(0.725f, 0.670f, 0.620f);
						}
					}
					
					if(t.gameObject.name == "Duna")
					{
						t.gameObject.GetComponent<MeshFilter>().mesh = plainSphere;
						
						t.gameObject.renderer.material.SetTexture("_MainTex", newDunaScaledColor);
						t.gameObject.renderer.material.SetTexture("_BumpMap", newDunaScaledBump);
						
						AtmosphereFromGround atm = t.FindChild("Atmosphere").GetComponent<AtmosphereFromGround>();
						if(atm != null)
						{
							atm.waveLength = new Color(0.525f, 0.570f, 0.620f);
						}
					}
					
					if(t.gameObject.name == "Ike")
					{
						t.localScale = new Vector3(0.002917f, 0.002917f, 0.002917f);
						
						SpawnCometTail(t, sun, "IonTail", false, null, 2.6f, 0.5f);
						SpawnCometTail(t, sun, "DustTail", true, cb_ike.orbit, 2.75f, 0.8f);
					}
					
					if(t.gameObject.name == "Gilly")
					{
						t.localScale = new Vector3(0.01415f, 0.01415f, 0.01415f);
						
						SpawnCometTail(t, sun, "IonTail", false, null, 0.6f, 1.5f);
						SpawnCometTail(t, sun, "DustTail", true, cb_gilly.orbit, 1.0f, 1.0f);
					}
					
					if(t.gameObject.name == "Bop" && enableKerbinMoon)
					{
						t.localScale = new Vector3(0.0090352f, 0.0090352f, 0.0090352f);
					}
					
					if(t.gameObject.name == "Jool")
					{
						AtmosphereFromGround atm = t.FindChild("Atmosphere").GetComponent<AtmosphereFromGround>();
						if(atm != null)
						{
							atm.waveLength = new Color(0.750f, 0.720f, 0.680f);
						}
						
						if(kerbinMat != null)
						{
							Texture oldJoolNorm = t.gameObject.renderer.material.GetTexture("_BumpMap");
							
							Material m = new Material(kerbinMat);
							t.gameObject.renderer.material = m;
							t.gameObject.renderer.material.SetTexture("_MainTex", newJoolTexture);
							t.gameObject.renderer.material.SetTexture("_BumpMap", oldJoolNorm);
							t.gameObject.renderer.material.SetTexture("_rimColorRamp", rampBlue);
							t.gameObject.renderer.material.SetColor("_SpecColor", Color.black);
							t.gameObject.renderer.material.SetTextureScale("_BumpMap", new Vector2(32, 16));
							t.gameObject.renderer.material.SetColor("_Color", new Color(0.75f, 0.75f, 0.75f));
						}
						
					}
					
					if(t.gameObject.name == "Eeloo")
					{
						t.localScale = new Vector3(0.07968f, 0.07968f, 0.07968f);
					}
					
					if(t.gameObject.name == "Vall")
					{
						t.localScale = new Vector3(0.1663f, 0.1663f, 0.1663f);
					}
					
					if(t.gameObject.name == "Mun")
					{
						t.localScale = new Vector3(0.0421751f, 0.0421751f, 0.0421751f);
					}
					
					if(t.gameObject.name == "Minmus")
					{
						t.localScale = new Vector3(0.004834f, 0.004834f, 0.004834f);
						t.gameObject.renderer.material.SetTexture("_MainTex", newMinmusTexture);
						t.gameObject.renderer.material.SetColor("_SpecColor", Color.black);
						t.gameObject.renderer.material.SetColor("_Color", Color.grey);
					}

					if(t.gameObject.name == "Sun")
					{
						t.localScale = new Vector3(11.82235f, 11.82235f, 11.82235f);
					}
				}
								alternisDone = true;
			}
						}
			

		   
						
				
						
					
			

				
				
		#endregion
		#region comet tails
		
		void SpawnCometTail(Transform target, Transform sun, string tailname, bool dustTail, Orbit targetorbit, float scale, float brightness)
		{
			if(target != null && sun != null)
			{
				if(target.FindChild(tailname) == null)
				{
					print("Adding comet tail " + tailname + " to " + target.gameObject.name);
					GameObject tail = new GameObject(tailname, typeof(CometLogic));
					if(tail != null)
					{
						tail.name = tailname;
						tail.SetActive(true);
						tail.transform.parent = target;
						tail.transform.localPosition = Vector3.zero;
						
						GameObject tailVisual = null;
						
						if(dustTail)
						{
							tailVisual = GameDatabase.Instance.GetModel(path2 + "comet_dusttail/comet_dusttail");
							tailVisual.transform.localScale = new Vector3(250 * scale, 250 * scale, 250 * scale);
						}
						else
						{
							tailVisual = GameDatabase.Instance.GetModel(path2 + "comet_iontail/comet_iontail");
							tailVisual.transform.localScale = new Vector3(220 * scale, 220 * scale, 220 * scale);
						}
						
						CometLogic cl = tail.GetComponent<CometLogic>();
						if(cl != null && tailVisual != null)
						{
							tailVisual.transform.parent = tail.transform;
							tailVisual.transform.localPosition = Vector3.zero;
							tailVisual.transform.localEulerAngles = new Vector3(-90, 0, 0);
							tailVisual.SetActive(true);
							
							cl.target = target;
							cl.sun = sun;
							cl.dustTail = dustTail;
							cl.brightness = brightness * globalCometBright;
							
							if(dustTail)
								cl.cometOrbit = targetorbit;
							
							cl.visual = tailVisual.transform.GetChild(0).gameObject;
							
							print("Tail FX creation successful");
						}
						else
						{
							print("ERROR: tail creator code SNAFU - code 2");
						}
					}
					else
					{
						print("ERROR: tail creator code SNAFU - code 1");
					}
				}
				else
				{
					print("Comet of this name already existed");
				}
			}
			
			#endregion
			#region map building
			/*void BuildMaps()
        {
            if (mapBuildMode && cb_laythe.pqsController != null)
            {
                Texture2D[] maps = cb_laythe.pqsController.CreateMaps(2048, 12000, true, 0, cb_laythe.pqsController.mapOceanColor);
                int i = 0;
                foreach (Texture2D t in maps)
                {
                    File.WriteAllBytes(Application.dataPath + "/newlaythe" + i + ".png", t.EncodeToPNG());
                    i++;
                }

                Application.Quit();
            }
        }*/
			#endregion
		}
	}
}
