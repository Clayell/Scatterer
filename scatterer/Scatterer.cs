using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Reflection;
using System.Runtime;
using KSP;
using KSP.IO;
using UnityEngine;
using UnityEngine.Rendering;

[assembly:AssemblyVersion("0.0725")]
namespace scatterer
{
	[KSPAddon(KSPAddon.Startup.EveryScene, false)]
	public class Scatterer: MonoBehaviour
	{	
		private static Scatterer instance;
		public static Scatterer Instance {get {return instance;}}

		public MainSettingsReadWrite mainSettings = new MainSettingsReadWrite();
		public PluginDataReadWrite pluginData     = new PluginDataReadWrite();
		public ConfigReader planetsConfigsReader  = new ConfigReader ();

		public GUIhandler guiHandler = new GUIhandler();
		
		public ScattererCelestialBodiesManager scattererCelestialBodiesManager = new ScattererCelestialBodiesManager ();
		public BufferManager bufferManager;
		public SunflareManager sunflareManager;
		public EVEReflectionHandler eveReflectionHandler;
		public PlanetshineManager planetshineManager;

		DisableAmbientLight ambientLightScript;
		public SunlightModulatorsManager sunlightModulatorsManagerInstance;
		
		public ShadowRemoveFadeCommandBuffer shadowFadeRemover;
		public TweakShadowCascades shadowCascadeTweaker;

		//probably move these to buffer rendering manager
		DepthToDistanceCommandBuffer farDepthCommandbuffer, nearDepthCommandbuffer;
		public DepthPrePassMerger nearDepthPassMerger;
		
		public Light sunLight,scaledSpaceSunLight, mainMenuLight;
		public Light[] lights;
		public Camera farCamera, scaledSpaceCamera, nearCamera;

		//classic SQUAD
		ReflectionProbeChecker reflectionProbeChecker;
		GameObject ReflectionProbeCheckerGO;
		
		bool coreInitiated = false;
		public bool isActive = false;
		public bool unifiedCameraMode = false;
		public string versionNumber = "0.0725 dev";

		public TemporalAntiAliasing temporalAA;

		void Awake ()
		{
			if (instance == null)
			{
				instance = this;
				Utils.LogDebug("Core instance created");
			}
			else
			{
				//destroy any duplicate instances that may be created by a duplicate install
				Utils.LogError("Destroying duplicate instance, check your install for duplicate scatterer folders, or nested GameData folders");
				UnityEngine.Object.Destroy(this);
			}

			Utils.LogInfo ("Version:"+versionNumber);
			Utils.LogInfo ("Running on: " + SystemInfo.graphicsDeviceVersion + " on " +SystemInfo.operatingSystem);
			Utils.LogInfo ("Game resolution: " + Screen.width.ToString() + "x" +Screen.height.ToString());
			Utils.LogInfo ("Compute shader support: " + SystemInfo.supportsComputeShaders.ToString());
			Utils.LogInfo ("Async GPU readback support: " + SystemInfo.supportsAsyncGPUReadback.ToString());
			Utils.LogInfo ("Using depth buffer mode: " + mainSettings.useDepthBufferMode.ToString());

			if (HighLogic.LoadedSceneIsFlight || HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.TRACKSTATION || HighLogic.LoadedScene == GameScenes.MAINMENU)
			{
				isActive = true;

				LoadSettings ();
				scattererCelestialBodiesManager.Init ();

				guiHandler.Init();

				if (HighLogic.LoadedScene == GameScenes.MAINMENU)
				{
					if (mainSettings.useOceanShaders)
					{
						OceanUtils.removeStockOceans();
					}
					
					if (mainSettings.integrateWithEVEClouds)
					{
						ShaderReplacer.Instance.replaceEVEshaders();
					}
				}

				QualitySettings.antiAliasing = mainSettings.useDepthBufferMode ? 0 : GameSettings.ANTI_ALIASING;
			} 

			if (isActive)
			{
				StartCoroutine (DelayedInit ());
			}
		}

		//wait for 5 frames for EVE and the game to finish setting up
		IEnumerator DelayedInit()
		{
			int delayFrames = (HighLogic.LoadedScene == GameScenes.MAINMENU) ? 5 : 1;
			for (int i=0; i<delayFrames; i++)
				yield return new WaitForFixedUpdate ();

			Init();
		}

		void Init()
		{
			SetupMainCameras ();

			FindSunlights ();

			SetShadows();
			
			Utils.FixKopernicusRingsRenderQueue ();			
			Utils.FixSunsCoronaRenderQueue ();

			AddReflectionProbeFixer ();

			if (mainSettings.usePlanetShine)
			{
				planetshineManager = new PlanetshineManager();
				planetshineManager.Init();
			}

			if (HighLogic.LoadedScene != GameScenes.TRACKSTATION)
			{
				// Note: Stock KSP dragCubes make a copy of components and removes them rom far/near cameras when rendering
				// This can cause issues with renderTextures and commandBuffers, to keep in mind for when implementing godrays
				bufferManager = (BufferManager)scaledSpaceCamera.gameObject.AddComponent (typeof(BufferManager));	// This doesn't need to be added to any camera anymore
																													// TODO: move to appropriate gameObject

				//copy stock depth buffers and combine into a single depth buffer
				//TODO: shouldn't this be moved to bufferManager?
				if (!unifiedCameraMode && (mainSettings.useOceanShaders || mainSettings.fullLensFlareReplacement))
				{
					farDepthCommandbuffer = farCamera.gameObject.AddComponent<DepthToDistanceCommandBuffer>();
					nearDepthCommandbuffer = nearCamera.gameObject.AddComponent<DepthToDistanceCommandBuffer>();
				}
			}

			//TODO: change these later to support multiple cameras
			//TODO: also remove the loadedSceneIsFlight thing
			if (mainSettings.useDepthBufferMode && HighLogic.LoadedSceneIsFlight)
			{
				if(mainSettings.useTemporalAntiAliasing)
					temporalAA = nearCamera.gameObject.AddComponent<TemporalAntiAliasing>();
				
				if(mainSettings.mergeDepthPrePass)
				{
					Utils.LogInfo("Adding nearDepthPassMerger");
					nearDepthPassMerger = (DepthPrePassMerger) nearCamera.gameObject.AddComponent<DepthPrePassMerger>();
				}
			}

			if ((mainSettings.fullLensFlareReplacement) && (HighLogic.LoadedScene != GameScenes.MAINMENU))
			{
				sunflareManager = new SunflareManager();
				sunflareManager.Init();
			}

			if (mainSettings.integrateWithEVEClouds)
			{
				eveReflectionHandler = new EVEReflectionHandler();
				eveReflectionHandler.Start();
			}

			if (mainSettings.disableAmbientLight && !ambientLightScript)
			{
				ambientLightScript = (DisableAmbientLight) scaledSpaceCamera.gameObject.AddComponent (typeof(DisableAmbientLight));
			}

			if (!unifiedCameraMode)
				shadowFadeRemover = (ShadowRemoveFadeCommandBuffer)nearCamera.gameObject.AddComponent (typeof(ShadowRemoveFadeCommandBuffer));

			//magically fix stupid issues when reverting to space center from map view
			if (HighLogic.LoadedScene == GameScenes.SPACECENTER)
			{
				MapView.MapIsEnabled = false;
			}

			if (mainSettings.sunlightExtinction || (mainSettings.underwaterLightDimming && mainSettings.useOceanShaders))
			{
				sunlightModulatorsManagerInstance = new SunlightModulatorsManager();
			}

			if (mainSettings.useOceanShaders && mainSettings.oceanCraftWaveInteractions && SystemInfo.supportsComputeShaders && SystemInfo.supportsAsyncGPUReadback)
			{
				if (mainSettings.oceanCraftWaveInteractionsOverrideWaterCrashTolerance)
				{
					PhysicsGlobals.BuoyancyCrashToleranceMult = mainSettings.buoyancyCrashToleranceMultOverride;
				}

				if (mainSettings.oceanCraftWaveInteractionsOverrideDrag)
				{
					PhysicsGlobals.BuoyancyWaterDragTimer = -10.0; //disabled because ti's bs doesn't really disable it?
					PhysicsGlobals.BuoyancyWaterDragScalar = mainSettings.buoyancyWaterDragScalarOverride;
					PhysicsGlobals.BuoyancyWaterAngularDragScalar = mainSettings.buoyancyWaterAngularDragScalarOverride;
				}
			}

			coreInitiated = true;

			Utils.LogDebug("Core setup done");
		}

		void Update ()
		{
			guiHandler.UpdateGUIvisible ();

			//TODO: get rid of this check, maybe move to coroutine? what happens when coroutine exits?
			if (coreInitiated)
			{
				scattererCelestialBodiesManager.Update ();

				//move this out of this update, let it be a one time thing
				//TODO: check what this means
				if (bufferManager)
				{
					if (!bufferManager.depthTextureCleared && (MapView.MapIsEnabled || !scattererCelestialBodiesManager.isPQSEnabledOnScattererPlanet) )
						bufferManager.ClearDepthTexture();
				}

				if (!ReferenceEquals(sunflareManager,null))
				{
					sunflareManager.UpdateFlares();
				}

				if(!ReferenceEquals(planetshineManager,null))
				{
					planetshineManager.UpdatePlanetshine();
				}
			}
		} 

		void OnDestroy ()
		{
			if (isActive)
			{
				if (!ReferenceEquals(sunlightModulatorsManagerInstance,null))
				{
					sunlightModulatorsManagerInstance.Cleanup();
				}

				if(!ReferenceEquals(planetshineManager,null))
				{
					planetshineManager.CleanUp();
					Component.Destroy(planetshineManager);
				}

				if (!ReferenceEquals(scattererCelestialBodiesManager,null))
				{
					scattererCelestialBodiesManager.Cleanup();
				}

				if (ambientLightScript)
				{
					ambientLightScript.restoreLight();
					Component.Destroy(ambientLightScript);
				}				

				if (nearCamera)
				{
					if (nearCamera.gameObject.GetComponent (typeof(Wireframe)))
						Component.Destroy (nearCamera.gameObject.GetComponent (typeof(Wireframe)));
					
					
					if (farCamera && farCamera.gameObject.GetComponent (typeof(Wireframe)))
						Component.Destroy (farCamera.gameObject.GetComponent (typeof(Wireframe)));
					
					
					if (scaledSpaceCamera.gameObject.GetComponent (typeof(Wireframe)))
						Component.Destroy (scaledSpaceCamera.gameObject.GetComponent (typeof(Wireframe)));
				}

				if (!ReferenceEquals(sunflareManager,null))
				{
					sunflareManager.Cleanup();
					UnityEngine.Component.Destroy(sunflareManager);
				}

				if (shadowFadeRemover)
				{
					shadowFadeRemover.OnDestroy();
					Component.Destroy(shadowFadeRemover);
				}

				if (shadowCascadeTweaker)
				{
					Component.Destroy(shadowCascadeTweaker);
				}

				if (farDepthCommandbuffer)
					Component.Destroy (farDepthCommandbuffer);
				
				if (nearDepthCommandbuffer)
					Component.Destroy (nearDepthCommandbuffer);

				if (nearDepthPassMerger)
					Component.Destroy (nearDepthPassMerger);

				if (bufferManager)
				{
					bufferManager.OnDestroy();
					Component.Destroy (bufferManager);
				}

				if (temporalAA)
				{
					temporalAA.Cleanup();
					Component.Destroy(temporalAA);
				}

				if (reflectionProbeChecker)
				{
					reflectionProbeChecker.OnDestroy ();
					Component.Destroy (reflectionProbeChecker);
				}

				if (ReflectionProbeCheckerGO)
				{
					UnityEngine.GameObject.Destroy (ReflectionProbeCheckerGO);
				}

				pluginData.inGameWindowLocation=new Vector2(guiHandler.windowRect.x,guiHandler.windowRect.y);
				SaveSettings();
			}

			UnityEngine.Object.Destroy (guiHandler);
			
		}

		void OnGUI ()
		{
			guiHandler.DrawGui ();
		}
		
		public void LoadSettings ()
		{
			mainSettings.loadMainSettings ();
			pluginData.loadPluginData ();
			planetsConfigsReader.loadConfigs ();

			// HACK: for mainMenu everything is jumbled, so just attempt to load every planet always
			if (HighLogic.LoadedScene == GameScenes.MAINMENU)
			{
				foreach (ScattererCelestialBody _SCB in planetsConfigsReader.scattererCelestialBodies)
				{
					_SCB.loadDistance = Mathf.Infinity;
					_SCB.unloadDistance = Mathf.Infinity;
				}
			}
		}
		
		public void SaveSettings ()
		{
			pluginData.savePluginData ();
			mainSettings.saveMainSettingsIfChanged ();
		}

		void SetupMainCameras()
		{
			scaledSpaceCamera = Camera.allCameras.FirstOrDefault (_cam => _cam.name == "Camera ScaledSpace");
			farCamera = Camera.allCameras.FirstOrDefault (_cam => _cam.name == "Camera 01");
			nearCamera = Camera.allCameras.FirstOrDefault (_cam => _cam.name == "Camera 00");

			if (nearCamera && !farCamera) 
			{
				Utils.LogInfo("Running in unified camera mode");
				unifiedCameraMode = true;
			}

			if (scaledSpaceCamera && nearCamera)
			{
				//move these to be used only with long-distance shadows?
				if (!unifiedCameraMode && (mainSettings.dualCamShadowCascadeSplitsOverride != Vector3.zero))
				{
					shadowCascadeTweaker = (TweakShadowCascades) Utils.getEarliestLocalCamera().gameObject.AddComponent(typeof(TweakShadowCascades));
					shadowCascadeTweaker.Init(mainSettings.dualCamShadowCascadeSplitsOverride);
				}
				else if (unifiedCameraMode && (mainSettings.unifiedCamShadowCascadeSplitsOverride != Vector3.zero))
				{
					shadowCascadeTweaker = (TweakShadowCascades) Utils.getEarliestLocalCamera().gameObject.AddComponent(typeof(TweakShadowCascades));
					shadowCascadeTweaker.Init(mainSettings.unifiedCamShadowCascadeSplitsOverride);
				}
				
				if (mainSettings.overrideNearClipPlane)
				{
					Utils.LogDebug("Override near clip plane from:"+nearCamera.nearClipPlane.ToString()+" to:"+mainSettings.nearClipPlane.ToString());
					nearCamera.nearClipPlane = mainSettings.nearClipPlane;
				}
			}
			else if (HighLogic.LoadedScene == GameScenes.MAINMENU)
			{
				// If are in main menu, where there is only 1 camera, affect all cameras to Landscape camera
				scaledSpaceCamera = Camera.allCameras.Single(_cam  => _cam.name == "Landscape Camera");
				farCamera = scaledSpaceCamera;
				nearCamera = scaledSpaceCamera;
			}
			else if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
			{
				// If in trackstation, just to get rid of some nullrefs
				farCamera = scaledSpaceCamera;
				nearCamera = scaledSpaceCamera;
			}
		}

		void SetShadows()
		{
			if (HighLogic.LoadedScene != GameScenes.MAINMENU)
			{
				if (unifiedCameraMode && (mainSettings.d3d11ShadowFix || mainSettings.terrainShadows))
				{
					QualitySettings.shadowProjection = ShadowProjection.StableFit; //way more resistant to jittering
					GraphicsSettings.SetShaderMode (BuiltinShaderType.ScreenSpaceShadows, BuiltinShaderMode.UseCustom);
					if ((mainSettings.terrainShadows) && (mainSettings.unifiedCamShadowsDistance > 8000f))
					{
						GraphicsSettings.SetCustomShader (BuiltinShaderType.ScreenSpaceShadows, ShaderReplacer.Instance.LoadedShaders [("Scatterer/longDistanceScreenSpaceShadows")]);
					}
					else
					{
						GraphicsSettings.SetCustomShader (BuiltinShaderType.ScreenSpaceShadows, ShaderReplacer.Instance.LoadedShaders [("Scatterer/fixedScreenSpaceShadows")]);
					}
				}

				if (mainSettings.shadowsOnOcean)
				{
					if (unifiedCameraMode || SystemInfo.graphicsDeviceVersion.Contains("Direct3D 11.0"))
					{
						QualitySettings.shadowProjection = ShadowProjection.StableFit;	//StableFit + splitSpheres is the only thing that works Correctly for unified camera (dx11) ocean shadows
																					  	//Otherwise we get artifacts near shadow cascade edges
					}
					else
					{
						QualitySettings.shadowProjection = ShadowProjection.CloseFit;	//CloseFit without SplitSpheres seems to be the only setting that works for OpenGL for ocean shadows
																						//Seems like I lack the correct variables to determine which shadow path to take
																						//also try without the transparent tag
					}
				}

				if (mainSettings.terrainShadows)
				{
					QualitySettings.shadowDistance = unifiedCameraMode ? mainSettings.unifiedCamShadowsDistance : mainSettings.dualCamShadowsDistance;
					Utils.LogDebug ("Set shadow distance: " + QualitySettings.shadowDistance.ToString ());
					Utils.LogDebug ("Number of shadow cascades detected " + QualitySettings.shadowCascades.ToString ());

					SetShadowsForLight (sunLight);

					// And finally force shadow Casting and receiving on celestial bodies if not already set
					foreach (CelestialBody _sc in scattererCelestialBodiesManager.CelestialBodies)
					{
						if (_sc.pqsController)
						{
							_sc.pqsController.meshCastShadows = true;
							_sc.pqsController.meshRecieveShadows = true;
						}
					}
				}
			}
		}

		public void SetShadowsForLight (Light light)
		{
			if (light && mainSettings.terrainShadows && (HighLogic.LoadedScene != GameScenes.MAINMENU))
			{
				//fixes checkerboard artifacts aka shadow acne
				float bias = unifiedCameraMode ? mainSettings.unifiedCamShadowNormalBiasOverride : mainSettings.dualCamShadowNormalBiasOverride;
				float normalBias = unifiedCameraMode ? mainSettings.unifiedCamShadowBiasOverride : mainSettings.dualCamShadowBiasOverride;
				if (bias != 0f)
					light.shadowBias = bias;
				if (normalBias != 0f)
					light.shadowNormalBias = normalBias;
				int customRes = unifiedCameraMode ? mainSettings.unifiedCamShadowResolutionOverride : mainSettings.dualCamShadowResolutionOverride;
				if (customRes != 0)
				{
					if (Utils.IsPowerOfTwo (customRes))
					{
						Utils.LogDebug ("Setting shadowmap resolution to: " + customRes.ToString () + " on " + light.name);
						light.shadowCustomResolution = customRes;
					}
					else
					{
						Utils.LogError ("Selected shadowmap resolution not a power of 2: " + customRes.ToString ());
					}
				}
			}
		}

		void FindSunlights ()
		{
			lights = (Light[])Light.FindObjectsOfType (typeof(Light));
			foreach (Light _light in lights)
			{
				if (_light.gameObject.name == "SunLight")
				{
					sunLight = _light;
				}
				if (_light.gameObject.name == "Scaledspace SunLight")
				{
					scaledSpaceSunLight = _light;
				}
				if (_light.gameObject.name.Contains ("PlanetLight") || _light.gameObject.name.Contains ("Directional light"))
				{
					mainMenuLight = _light;
				}
			}
		}

		public void OnRenderTexturesLost()
		{
			foreach (ScattererCelestialBody _cur in planetsConfigsReader.scattererCelestialBodies)
			{
				if (_cur.active)
				{
					_cur.m_manager.m_skyNode.ReInitMaterialUniformsOnRenderTexturesLoss ();
					if (_cur.m_manager.hasOcean && mainSettings.useOceanShaders && !_cur.m_manager.m_skyNode.inScaledSpace)
					{
						_cur.m_manager.reBuildOcean ();
					}
				}
			}
		}

		// Just a dummy gameObject so the reflectionProbeChecker can capture the reflection Camera
		public void AddReflectionProbeFixer()
		{
			ReflectionProbeCheckerGO = new GameObject ("Scatterer ReflectionProbeCheckerGO");
			//ReflectionProbeCheckerGO.transform.parent = nearCamera.transform; //VesselViewer doesn't like this for some reason
			ReflectionProbeCheckerGO.layer = 15;

			reflectionProbeChecker = ReflectionProbeCheckerGO.AddComponent<ReflectionProbeChecker> ();

			MeshFilter _mf = ReflectionProbeCheckerGO.AddComponent<MeshFilter> ();
			_mf.mesh.Clear ();
			_mf.mesh = MeshFactory.MakePlane (2, 2, MeshFactory.PLANE.XY, false, false);
			_mf.mesh.bounds = new Bounds (Vector4.zero, new Vector3 (Mathf.Infinity, Mathf.Infinity, Mathf.Infinity));

			MeshRenderer _mr = ReflectionProbeCheckerGO.AddComponent<MeshRenderer> ();
			_mr.sharedMaterial = new Material (ShaderReplacer.Instance.LoadedShaders[("Scatterer/invisible")]);
			_mr.material = new Material (ShaderReplacer.Instance.LoadedShaders[("Scatterer/invisible")]);
			_mr.receiveShadows = false;
			_mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			_mr.enabled = true;
		}

	}
}
