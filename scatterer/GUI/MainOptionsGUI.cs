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

namespace scatterer
{
	public class MainOptionsGUI
	{
		enum MainMenuTabs
		{
			QualityPresets,
			IndividualSettings
		}
		
		enum IndividualSettingsTabs
		{
			Scattering,
			Ocean,
			Sunflare,
			Lighting,
			Shadows,
			EVEintegration,
			Misc
		}
		
		MainMenuTabs selectedMainMenuTab = MainMenuTabs.QualityPresets;
		IndividualSettingsTabs selectedIndividualSettingsTab = IndividualSettingsTabs.Scattering;

		String[] qualityPresetsStrings;
		string currentPreset;
		int selQualityPresetInt = 0;
		
		public MainOptionsGUI ()
		{
		}

		public void DrawOptionsMenu ()
		{
			GUILayout.BeginHorizontal ();
			{
				GUILayout.Label ("Menu scroll section height");
				Scatterer.Instance.pluginData.scrollSectionHeight = (Int32)(Convert.ToInt32 (GUILayout.TextField (Scatterer.Instance.pluginData.scrollSectionHeight.ToString ())));
			}
			GUILayout.EndHorizontal ();
			
			GUILayout.BeginHorizontal ();
			{
				GUILayout.Label (".cfg file used (display only):");
				GUILayout.TextField (Scatterer.Instance.planetsConfigsReader.baseConfigs [0].parent.url);
			}
			GUILayout.EndHorizontal ();
			
			GUILayout.BeginHorizontal ();
			{
				if (GUILayout.Button ("Quality Presets"))
				{
					selectedMainMenuTab = MainMenuTabs.QualityPresets;
				}
				if (GUILayout.Button ("Customize Settings"))
				{
					selectedMainMenuTab = MainMenuTabs.IndividualSettings;
				}
			}
			GUILayout.EndHorizontal ();
			
			if (selectedMainMenuTab == MainMenuTabs.QualityPresets)
			{
				DrawQualityPresets();
			}
			else
			{
				DrawIndividualSettings ();
			}
		}

		void DrawQualityPresets ()
		{
			if (ReferenceEquals (qualityPresetsStrings, null))
			{
				qualityPresetsStrings = QualityPresetsLoader.GetPresetsList ();
				currentPreset = QualityPresetsLoader.FindPresetOfCurrentSettings(Scatterer.Instance.mainSettings);
				
				int index = qualityPresetsStrings.IndexOf(currentPreset);
				
				if (index != -1)
				{
					selQualityPresetInt = index;
				}
			}
			else
			{
				GUILayout.BeginVertical ();
				GUILayout.BeginHorizontal ();
				{
					GUILayout.Label("Current preset:");
					GUILayout.TextField(currentPreset);
				}
				GUILayout.EndHorizontal ();
				selQualityPresetInt = GUILayout.SelectionGrid (selQualityPresetInt, qualityPresetsStrings, 1);
				GUILayout.Label("");
				if (GUILayout.Button ("Apply preset"))
				{
					if (qualityPresetsStrings.Count() > 0)
					{
						Utils.LogInfo("Applying quality preset "+qualityPresetsStrings[selQualityPresetInt]);
						QualityPresetsLoader.LoadPresetIntoMainSettings(Scatterer.Instance.mainSettings, qualityPresetsStrings[selQualityPresetInt]);
						currentPreset = qualityPresetsStrings[selQualityPresetInt];
					}
				}
				GUILayout.EndVertical ();
			}
		}
		
		void DrawIndividualSettings ()
		{
			GUILayout.BeginHorizontal ();
			{
				if (GUILayout.Button ("Scattering")) {
					selectedIndividualSettingsTab = IndividualSettingsTabs.Scattering;
				}
				if (GUILayout.Button ("Ocean")) {
					selectedIndividualSettingsTab = IndividualSettingsTabs.Ocean;
				}
				if (GUILayout.Button ("Sunflare")) {
					selectedIndividualSettingsTab = IndividualSettingsTabs.Sunflare;
				}
				if (GUILayout.Button ("Lighting")) {
					selectedIndividualSettingsTab = IndividualSettingsTabs.Lighting;
				}
				if (GUILayout.Button ("Shadows")) {
					selectedIndividualSettingsTab = IndividualSettingsTabs.Shadows;
				}
				if (GUILayout.Button ("EVE Clouds")) {
					selectedIndividualSettingsTab = IndividualSettingsTabs.EVEintegration;
				}
				if (GUILayout.Button ("Misc.")) {
					selectedIndividualSettingsTab = IndividualSettingsTabs.Misc;
				}
			}
			GUILayout.EndHorizontal ();
			if (selectedIndividualSettingsTab == IndividualSettingsTabs.Scattering) {
				Scatterer.Instance.mainSettings.useGodrays = GUILayout.Toggle (Scatterer.Instance.mainSettings.useGodrays, "Godrays (Requires unified camera, long-distance shadows and shadowMapResolution override, Directx11 only)");
				if (Scatterer.Instance.mainSettings.useGodrays)
				{
					//Godrays tesselation placeholder
				}
				Scatterer.Instance.mainSettings.useDepthBufferMode = !GUILayout.Toggle (!Scatterer.Instance.mainSettings.useDepthBufferMode, "Use projector mode (Slower, less compatible but supports MSAA)");
				Scatterer.Instance.mainSettings.useDepthBufferMode = GUILayout.Toggle (Scatterer.Instance.mainSettings.useDepthBufferMode, "Use depth buffer mode (Recommended: Faster, better compatible with Parallax and trees/scatters, disables MSAA in flight/KSC)");
				if (Scatterer.Instance.mainSettings.useDepthBufferMode)
				{
					GUILayout.BeginHorizontal ();
					{
						GUILayout.Label ("\t");
						GUILayout.BeginVertical ();
						{
							Scatterer.Instance.mainSettings.quarterResScattering = GUILayout.Toggle (Scatterer.Instance.mainSettings.quarterResScattering, "Render scattering in 1/4 resolution (speedup, incompatible and disabled with godrays)");
							Scatterer.Instance.mainSettings.mergeDepthPrePass = GUILayout.Toggle (Scatterer.Instance.mainSettings.mergeDepthPrePass, "Merge depth pre-pass into main depth for culling (experimental, may give small speedup but may cause z-fighting");

							Scatterer.Instance.mainSettings.useTemporalAntiAliasing = GUILayout.Toggle (Scatterer.Instance.mainSettings.useTemporalAntiAliasing, "Temporal Antialiasing (Recommended)") && !Scatterer.Instance.mainSettings.useSubpixelMorphologicalAntialiasing;
							Scatterer.Instance.mainSettings.useSubpixelMorphologicalAntialiasing = GUILayout.Toggle (Scatterer.Instance.mainSettings.useSubpixelMorphologicalAntialiasing, "Subpixel Morphological Antialiasing (Faster but worse than TAA)")  && !Scatterer.Instance.mainSettings.useTemporalAntiAliasing;
							if (Scatterer.Instance.mainSettings.useSubpixelMorphologicalAntialiasing)
							{
								GUILayout.BeginHorizontal ();
								{
									GUILayout.Label ("SMAA quality (1:normal,2:high)");
									Scatterer.Instance.mainSettings.smaaQuality = (Int32) Mathf.Clamp( (float)(Convert.ToInt32 (GUILayout.TextField (Scatterer.Instance.mainSettings.smaaQuality.ToString ()))),1f,2f);
								}
								GUILayout.EndHorizontal ();
							}
						}
						GUILayout.EndVertical ();
					}
					GUILayout.EndHorizontal ();
				}
			}
			else if (selectedIndividualSettingsTab == IndividualSettingsTabs.Ocean)
			{
				Scatterer.Instance.mainSettings.useOceanShaders = GUILayout.Toggle (Scatterer.Instance.mainSettings.useOceanShaders, "Ocean shaders");
				if (Scatterer.Instance.mainSettings.useOceanShaders)
				{
					GUILayout.BeginHorizontal ();
					{
						GUILayout.Label ("\t");
						GUILayout.BeginVertical ();
						{
							GUILayout.BeginHorizontal ();
							GUILayout.Label ("Fourier grid size (64:fast,128:normal,256:HQ)");
							Scatterer.Instance.mainSettings.m_fourierGridSize = (Int32)(Convert.ToInt32 (GUILayout.TextField (Scatterer.Instance.mainSettings.m_fourierGridSize.ToString ())));
							GUILayout.EndHorizontal ();
							GUILayout.BeginHorizontal ();
							GUILayout.Label ("Mesh resolution (pixels covered by a mesh quad, lower is better but slower)");
							Scatterer.Instance.mainSettings.oceanMeshResolution = (Int32)(Convert.ToInt32 (GUILayout.TextField (Scatterer.Instance.mainSettings.oceanMeshResolution.ToString ())));
							GUILayout.EndHorizontal ();
							Scatterer.Instance.mainSettings.oceanTransparencyAndRefractions = GUILayout.Toggle (Scatterer.Instance.mainSettings.oceanTransparencyAndRefractions, "Transparency and refractions");
							Scatterer.Instance.mainSettings.oceanFoam = GUILayout.Toggle (Scatterer.Instance.mainSettings.oceanFoam, "Foam");
							Scatterer.Instance.mainSettings.oceanSkyReflections = GUILayout.Toggle (Scatterer.Instance.mainSettings.oceanSkyReflections, "Sky reflections");
							Scatterer.Instance.mainSettings.shadowsOnOcean = GUILayout.Toggle (Scatterer.Instance.mainSettings.shadowsOnOcean, "Surface receives shadows");
							Scatterer.Instance.mainSettings.oceanPixelLights = GUILayout.Toggle (Scatterer.Instance.mainSettings.oceanPixelLights, "Secondary lights compatibility (huge performance hit when lights on)");
							Scatterer.Instance.mainSettings.oceanCaustics = GUILayout.Toggle (Scatterer.Instance.mainSettings.oceanCaustics, "Underwater caustics");
							Scatterer.Instance.mainSettings.oceanLightRays = GUILayout.Toggle (Scatterer.Instance.mainSettings.oceanLightRays, "Underwater light rays (requires ocean surface shadows)");
							GUI.contentColor = SystemInfo.supportsAsyncGPUReadback && SystemInfo.supportsComputeShaders ? Color.white : Color.gray;
							Scatterer.Instance.mainSettings.oceanCraftWaveInteractions = GUILayout.Toggle (Scatterer.Instance.mainSettings.oceanCraftWaveInteractions, "Waves interact with ships (Requires asyncGPU readback, Directx11 only)");
							if (Scatterer.Instance.mainSettings.oceanCraftWaveInteractions) {
								GUILayout.BeginHorizontal ();
								{
									GUILayout.Label ("\t");
									GUILayout.BeginVertical ();
									{
										Scatterer.Instance.mainSettings.oceanCraftWaveInteractionsOverrideWaterCrashTolerance = GUILayout.Toggle (Scatterer.Instance.mainSettings.oceanCraftWaveInteractionsOverrideWaterCrashTolerance, "Override water crash tolerance");
										if (Scatterer.Instance.mainSettings.oceanCraftWaveInteractionsOverrideWaterCrashTolerance) {
											GUILayout.BeginHorizontal ();
											GUILayout.Label ("Crash tolerance (default is 1.2)");
											Scatterer.Instance.mainSettings.buoyancyCrashToleranceMultOverride = float.Parse (GUILayout.TextField (Scatterer.Instance.mainSettings.buoyancyCrashToleranceMultOverride.ToString ("00.00")));
											GUILayout.EndHorizontal ();
										}
										Scatterer.Instance.mainSettings.oceanCraftWaveInteractionsOverrideDrag = GUILayout.Toggle (Scatterer.Instance.mainSettings.oceanCraftWaveInteractionsOverrideDrag, "Override water drag");
										if (Scatterer.Instance.mainSettings.oceanCraftWaveInteractionsOverrideDrag) {
											GUILayout.BeginHorizontal ();
											GUILayout.Label ("Drag scalar (default is 4.5)");
											Scatterer.Instance.mainSettings.buoyancyWaterDragScalarOverride = float.Parse (GUILayout.TextField (Scatterer.Instance.mainSettings.buoyancyWaterDragScalarOverride.ToString ("00.00")));
											GUILayout.EndHorizontal ();
											GUILayout.BeginHorizontal ();
											GUILayout.Label ("Angular drag scalar (default is 0.001");
											Scatterer.Instance.mainSettings.buoyancyWaterAngularDragScalarOverride = float.Parse (GUILayout.TextField (Scatterer.Instance.mainSettings.buoyancyWaterAngularDragScalarOverride.ToString ("0.0000000")));
											GUILayout.EndHorizontal ();
										}
										Scatterer.Instance.mainSettings.oceanCraftWaveInteractionsOverrideRecoveryVelocity = GUILayout.Toggle (Scatterer.Instance.mainSettings.oceanCraftWaveInteractionsOverrideRecoveryVelocity, "Override max water recovery velocity");
										if (Scatterer.Instance.mainSettings.oceanCraftWaveInteractionsOverrideRecoveryVelocity) {
											GUILayout.BeginHorizontal ();
											GUILayout.Label ("Maximum recovery velocity (default is 0.3)");
											Scatterer.Instance.mainSettings.waterMaxRecoveryVelocity = float.Parse (GUILayout.TextField (Scatterer.Instance.mainSettings.waterMaxRecoveryVelocity.ToString ("00.00")));
											GUILayout.EndHorizontal ();
										}
									}
									GUILayout.EndVertical ();
								}
								GUILayout.EndHorizontal ();
							}
						}
						GUILayout.EndVertical ();
					}
					GUILayout.EndHorizontal ();
				}
			}
			else if (selectedIndividualSettingsTab == IndividualSettingsTabs.Lighting)
			{
				Scatterer.Instance.mainSettings.useEclipses = GUILayout.Toggle (Scatterer.Instance.mainSettings.useEclipses, "Eclipses (WIP, sky/orbit only for now)");
				Scatterer.Instance.mainSettings.useRingShadows = GUILayout.Toggle (Scatterer.Instance.mainSettings.useRingShadows, "Kopernicus ring shadows (linear only, tiled rings not supported)");
				Scatterer.Instance.mainSettings.disableAmbientLight = GUILayout.Toggle (Scatterer.Instance.mainSettings.disableAmbientLight, "Disable scaled space ambient light");
				Scatterer.Instance.mainSettings.sunlightExtinction = GUILayout.Toggle (Scatterer.Instance.mainSettings.sunlightExtinction, "Sunlight extinction (direct sun light changes color with sunset/dusk)");
				Scatterer.Instance.mainSettings.underwaterLightDimming = GUILayout.Toggle (Scatterer.Instance.mainSettings.underwaterLightDimming, "Dim light underwater");
			}
			else if (selectedIndividualSettingsTab == IndividualSettingsTabs.Shadows)
			{
				Scatterer.Instance.mainSettings.d3d11ShadowFix = GUILayout.Toggle (Scatterer.Instance.mainSettings.d3d11ShadowFix, "1.9+ Directx11 flickering shadows fix (recommended for 1.9, 1.10)");
				Scatterer.Instance.mainSettings.terrainShadows = GUILayout.Toggle (Scatterer.Instance.mainSettings.terrainShadows, "Long-Distance Terrain shadows");
				if (Scatterer.Instance.mainSettings.terrainShadows)
				{
					GUILayout.BeginHorizontal ();
					{
						GUILayout.Label ("  ");
						GUILayout.BeginVertical ();
						{
							GUI.contentColor = Scatterer.Instance.unifiedCameraMode ? Color.white : Color.gray;
							GUILayout.Label ((Scatterer.Instance.unifiedCameraMode ? "[Active] " : "[Inactive] ") + "Unified camera mode (1.9+ Directx11):");
							GUILayout.BeginHorizontal ();
							{
								GUILayout.Label ("\t");
								GUILayout.BeginVertical ();
								{
									GUILayout.BeginHorizontal ();
									{
										GUILayout.Label ("Shadows Distance (in meters):");
										Scatterer.Instance.mainSettings.unifiedCamShadowsDistance = float.Parse (GUILayout.TextField (Scatterer.Instance.mainSettings.unifiedCamShadowsDistance.ToString ("0")));
									}
									GUILayout.EndHorizontal ();
									GUILayout.BeginHorizontal ();
									{
										GUILayout.Label ("Shadowmap resolution (power of 2, zero for no override):");
										Scatterer.Instance.mainSettings.unifiedCamShadowResolutionOverride = (Int32)(Convert.ToInt32 (GUILayout.TextField (Scatterer.Instance.mainSettings.unifiedCamShadowResolutionOverride.ToString ())));
									}
									GUILayout.EndHorizontal ();
									GUIvector3NoButton ("Shadow cascade splits (zeroes for no override):", ref Scatterer.Instance.mainSettings.unifiedCamShadowCascadeSplitsOverride);
									GUILayout.BeginHorizontal ();
									{
										GUILayout.Label ("Shadow bias (0 for no override)");
										Scatterer.Instance.mainSettings.unifiedCamShadowBiasOverride = float.Parse (GUILayout.TextField (Scatterer.Instance.mainSettings.unifiedCamShadowBiasOverride.ToString ("0.000")));
										GUILayout.Label ("Normal bias (0 for no override)");
										Scatterer.Instance.mainSettings.unifiedCamShadowNormalBiasOverride = float.Parse (GUILayout.TextField (Scatterer.Instance.mainSettings.unifiedCamShadowNormalBiasOverride.ToString ("0.000")));
									}
									GUILayout.EndHorizontal ();
								}
								GUILayout.EndVertical ();
							}
							GUILayout.EndHorizontal ();
							GUI.contentColor = !Scatterer.Instance.unifiedCameraMode ? Color.white : Color.gray;
							GUILayout.Label ((!Scatterer.Instance.unifiedCameraMode ? "[Active] " : "[Inactive] ") + "Dual camera mode (1.8, 1.9 and 1.10 Opengl):");
							GUILayout.BeginHorizontal ();
							{
								GUILayout.Label ("\t");
								GUILayout.BeginVertical ();
								{
									GUILayout.BeginHorizontal ();
									{
										GUILayout.Label ("Shadows Distance (in meters):");
										Scatterer.Instance.mainSettings.dualCamShadowsDistance = float.Parse (GUILayout.TextField (Scatterer.Instance.mainSettings.dualCamShadowsDistance.ToString ("0")));
									}
									GUILayout.EndHorizontal ();
									GUILayout.BeginHorizontal ();
									{
										GUILayout.Label ("Shadowmap resolution (power of 2, zero for no override):");
										Scatterer.Instance.mainSettings.dualCamShadowResolutionOverride = (Int32)(Convert.ToInt32 (GUILayout.TextField (Scatterer.Instance.mainSettings.dualCamShadowResolutionOverride.ToString ())));
									}
									GUILayout.EndHorizontal ();
									GUIvector3NoButton ("Shadow cascade splits (zeroes for no override):", ref Scatterer.Instance.mainSettings.dualCamShadowCascadeSplitsOverride);
									GUILayout.BeginHorizontal ();
									{
										GUILayout.Label ("Shadow bias (0 for no override)");
										Scatterer.Instance.mainSettings.dualCamShadowBiasOverride = float.Parse (GUILayout.TextField (Scatterer.Instance.mainSettings.dualCamShadowBiasOverride.ToString ("0.000")));
										GUILayout.Label ("Normal bias (0 for no override)");
										Scatterer.Instance.mainSettings.dualCamShadowNormalBiasOverride = float.Parse (GUILayout.TextField (Scatterer.Instance.mainSettings.dualCamShadowNormalBiasOverride.ToString ("0.000")));
									}
									GUILayout.EndHorizontal ();
								}
								GUILayout.EndVertical ();
							}
							GUILayout.EndHorizontal ();
							GUILayout.EndVertical ();
						}
						GUILayout.EndHorizontal ();
					}
				}
			}
			else if (selectedIndividualSettingsTab == IndividualSettingsTabs.Sunflare)
			{
				Scatterer.Instance.mainSettings.fullLensFlareReplacement = GUILayout.Toggle (Scatterer.Instance.mainSettings.fullLensFlareReplacement, "Lens flare shader");
			}
			else if (selectedIndividualSettingsTab == IndividualSettingsTabs.EVEintegration)
			{
				Scatterer.Instance.mainSettings.integrateWithEVEClouds = GUILayout.Toggle (Scatterer.Instance.mainSettings.integrateWithEVEClouds, "Integrate effects with EVE clouds (may require restart)");
				//				if (Scatterer.Instance.mainSettings.integrateWithEVEClouds)
				//				{
				//					Scatterer.Instance.mainSettings.integrateEVECloudsGodrays = GUILayout.Toggle (Scatterer.Instance.mainSettings.integrateEVECloudsGodrays, "EVE clouds cast godrays (require godrays)");
				//				}
			}
			else if (selectedIndividualSettingsTab == IndividualSettingsTabs.Misc)
			{
				GUILayout.BeginHorizontal ();
				{
					Scatterer.Instance.mainSettings.overrideNearClipPlane = GUILayout.Toggle (Scatterer.Instance.mainSettings.overrideNearClipPlane, "Override Near ClipPlane (not recommended - restart on disable)");
					Scatterer.Instance.mainSettings.nearClipPlane = float.Parse (GUILayout.TextField (Scatterer.Instance.mainSettings.nearClipPlane.ToString ("0.000")));
				}
				GUILayout.EndHorizontal ();
				GUILayout.BeginHorizontal ();
				{
					Scatterer.Instance.mainSettings.useDithering = GUILayout.Toggle (Scatterer.Instance.mainSettings.useDithering, "Use dithering (Reduces color banding in sky and scattering, disable if you notice dithering patterns");
				}
				GUILayout.EndHorizontal ();
			}
		}

		public void GUIvector3NoButton (string label, ref Vector3 target)
		{
			GUILayout.BeginHorizontal ();
			GUILayout.Label (label);
			
			target.x = float.Parse (GUILayout.TextField (target.x.ToString ("0.0000")));
			target.y = float.Parse (GUILayout.TextField (target.y.ToString ("0.0000")));
			target.z = float.Parse (GUILayout.TextField (target.z.ToString ("0.0000")));
			
			GUILayout.EndHorizontal ();
		}

	}
}

