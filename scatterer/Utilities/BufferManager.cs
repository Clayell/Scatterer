using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace scatterer
{
	
	public class BufferManager : MonoBehaviour
	{
		public RenderTexture depthTexture;			//full-scene depth texture, from merged built-in depth textures of the two local cameras
		public RenderTexture refractionTexture;		//textures for the refractions, created once and accessed from here, written to from oceanNode
		//public RenderTexture occlusionTexture;	//for SSAO and eclipses, for now will just contain a copy of the screenspace shadowmask, probably not necessary

		public bool depthTextureCleared = false; 	//clear depth texture when away from PQS, for the sunflare shader

		public void start()
		{
			CreateTextures();
		}

		public void CreateTextures() //create simpler method createTexture with params, call it multiple times, make it static and move it to utils, reuse in skynode as well
		{
			if (HighLogic.LoadedScene != GameScenes.TRACKSTATION)
			{
				depthTexture = Utils.CreateTexture ("ScattererDepthTexture", Screen.width, Screen.height,0, RenderTextureFormat.RFloat, false, FilterMode.Point, 1);
				
				if (Core.Instance.mainSettings.useOceanShaders && Core.Instance.mainSettings.oceanRefraction)
				{
					refractionTexture = Utils.CreateTexture ("ScattererRefractionTexture", Screen.width, Screen.height,0, RenderTextureFormat.ARGB32,false,FilterMode.Point,1);
				}
			}
		}

		//Before farCamera renders
		void OnPreRender () 
		{
			if (!depthTexture || !depthTexture.IsCreated())
			{
				Utils.LogDebug("BufferRenderingManager: Recreating textures");
				CreateTextures();
				Core.Instance.onRenderTexturesLost();
			}
		}

		public void ClearDepthTexture()
		{
			RenderTexture rt=RenderTexture.active;
			RenderTexture.active= depthTexture;			
			GL.Clear(false,true,Color.white);
			RenderTexture.active=rt;			
			depthTextureCleared = true;
		}

		public void OnDestroy ()
		{
			if (depthTexture)
			{
				depthTexture.Release ();
				UnityEngine.Object.Destroy (depthTexture);
			}
			if (refractionTexture)
			{
				refractionTexture.Release ();
				UnityEngine.Object.Destroy (refractionTexture);
			}
		}

	}
}