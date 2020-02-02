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
	public class ShaderReplacer : MonoBehaviour
	{
		private static ShaderReplacer instance;
		public Dictionary<string, Shader> LoadedShaders = new Dictionary<string, Shader>();
		string path;

		Dictionary<string, Shader> EVEshaderDictionary;

		public Dictionary<string, string> gameShaders = new Dictionary<string, string>();
		
		private ShaderReplacer()
		{
			Init ();
		}
		
		public static ShaderReplacer Instance
		{
			get 
			{
				if (ReferenceEquals(instance,null))
				{
					instance = new ShaderReplacer();
					Utils.Log("ShaderReplacer instance created");
				}
				return instance;
			}
		}
		

		private void Init()
		{
			string codeBase = Assembly.GetExecutingAssembly ().CodeBase;
			UriBuilder uri = new UriBuilder (codeBase);
			path = Uri.UnescapeDataString (uri.Path);
			path = Path.GetDirectoryName (path);

			LoadAssetBundle ();
		}

		public void LoadAssetBundle()
		{
			string shaderspath;

			if (Application.platform == RuntimePlatform.WindowsPlayer && SystemInfo.graphicsDeviceVersion.StartsWith ("OpenGL"))
				shaderspath = path+"/shaders/scatterershaders-linux";   //fixes openGL on windows
			else
				if (Application.platform == RuntimePlatform.WindowsPlayer)
				shaderspath = path + "/shaders/scatterershaders-windows";
			else if (Application.platform == RuntimePlatform.LinuxPlayer)
				shaderspath = path+"/shaders/scatterershaders-linux";
			else
				shaderspath = path+"/shaders/scatterershaders-macosx";

			LoadedShaders.Clear ();

			using (WWW www = new WWW("file://"+shaderspath))
			{
				AssetBundle bundle = www.assetBundle;
				Shader[] shaders = bundle.LoadAllAssets<Shader>();
				
				foreach (Shader shader in shaders)
				{
					//Utils.Log (""+shader.name+" loaded. Supported?"+shader.isSupported.ToString());
					LoadedShaders.Add(shader.name, shader);
				}
				
				bundle.Unload(false); // unload the raw asset bundle
				www.Dispose();
			}
		}

		public void replaceEVEshaders()
		{
			//reflection get EVE shader dictionary
			Utils.Log ("Replacing EVE shaders");
			
			//find EVE shaderloader
			Type EVEshaderLoaderType = getType ("ShaderLoader.ShaderLoaderClass");

			if (EVEshaderLoaderType == null)
			{
				Utils.Log("Eve shaderloader type not found");
			}
			else
			{
				Utils.Log("Eve shaderloader type found");

				Utils.Log("Eve shaderloader version: " + EVEshaderLoaderType.Assembly.GetName().ToString());
				
				const BindingFlags flags =  BindingFlags.FlattenHierarchy |  BindingFlags.NonPublic | BindingFlags.Public | 
					BindingFlags.Instance | BindingFlags.Static;
				
				try
				{
					//				EVEinstance = EVEType.GetField("Instance", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
					EVEshaderDictionary = EVEshaderLoaderType.GetField("shaderDictionary", flags).GetValue(null) as Dictionary<string, Shader> ;
				}
				catch (Exception)
				{
					Utils.Log("No EVE shader dictionary found");
				}

				if (EVEshaderDictionary == null)
				{
					Utils.Log("Failed grabbing EVE shader dictionary");
				}
				else
				{
					Utils.Log("Successfully grabbed EVE shader dictionary");


					if (EVEshaderDictionary.ContainsKey("EVE/Cloud"))
					{
						EVEshaderDictionary["EVE/Cloud"] = LoadedShaders["Scatterer-EVE/Cloud"];
					}
					else
					{
						List<Material> cloudsList = new List<Material>();
						EVEshaderDictionary.Add("EVE/Cloud",LoadedShaders["Scatterer-EVE/Cloud"]);
					}
					
					Utils.Log("Replaced EVE/Cloud in EVE shader dictionary");
					
					if (EVEshaderDictionary.ContainsKey("EVE/CloudVolumeParticle"))
					{
						EVEshaderDictionary["EVE/CloudVolumeParticle"] = LoadedShaders["Scatterer-EVE/CloudVolumeParticle"];
					}
					else
					{
						List<Material> cloudsList = new List<Material>();
						EVEshaderDictionary.Add("EVE/CloudVolumeParticle",LoadedShaders["Scatterer-EVE/CloudVolumeParticle"]);
					}
					
					Utils.Log("replaced EVE/CloudVolumeParticle in EVE shader dictionary");
				}
			}
			

			//replace shaders of already created materials
			Material[] materials = Resources.FindObjectsOfTypeAll<Material>();

			//gameShaders.Clear ();

			foreach (Material mat in materials)
			{
				//ReplaceGameShader(mat);

				if (EVEshaderDictionary!= null)
				{
					ReplaceEVEShader(mat);
				}
			}

//			foreach (string key in gameShaders.Keys)
//			{
//				Utils.Log(gameShaders[key]);
//			}
		}

//		private void ReplaceGameShader(Material mat)
//		{
//			String name = mat.shader.name;
//			Shader replacementShader = null;
//			
//			if (!gameShaders.ContainsKey (name))
//			{
//				gameShaders [name] = "shader " + name + " materials: " + mat.name;
//			}
//			else
//			{
//				gameShaders [name] = gameShaders [name] + " " + mat.name;
//			}
//			
//			switch (name)
//			{
//			case "Terrain/PQS/PQS Main - Optimised":
//				Utils.Log("replacing PQS main optimised");
//				replacementShader = LoadedShaders["Scatterer/PQS/PQS Main - Optimised"];
//				Utils.Log("Shader replaced");
//				break;
//			default:
//				return;
//			}
//			if (replacementShader != null)
//			{
//				mat.shader = replacementShader;
//			}
//		}

		private void ReplaceEVEShader(Material mat)
		{
			String name = mat.shader.name;
			Shader replacementShader = null;

			switch (name)
			{
			case "EVE/Cloud":
				Utils.Log("replacing EVE/Cloud");
				replacementShader = LoadedShaders["Scatterer-EVE/Cloud"];
				Utils.Log("Shader replaced");
				break;
			case "EVE/CloudVolumeParticle":
				Utils.Log("replacing EVE/CloudVolumeParticle");
				replacementShader = LoadedShaders["Scatterer-EVE/CloudVolumeParticle"];
				Utils.Log("Shader replaced");
				break;
//			case "Terrain/PQS/PQS Main - Optimised":
//				Utils.Log("replacing PQS main optimised");
//				replacementShader = LoadedShaders["Scatterer/PQS/PQS Main - Optimised"];
//				Utils.Log("Shader replaced");
//				break;
			default:
				return;
			}
			if (replacementShader != null)
			{
				mat.shader = replacementShader;
			}
		}

		internal static Type getType(string name)
		{
			Type type = null;
			AssemblyLoader.loadedAssemblies.TypeOperation(t =>
			{
				if (t.FullName == name)
					type = t;
			}
			);
			
			if (type != null)
			{
				return type;
			}
			return null;
		}
	}
}