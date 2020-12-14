//used just to remove the postprocessing and the ocean from texture replacer's reflections because they look messed up and bog down performance
//this script gets added to the camera to disable the effects on

using UnityEngine;
using System.Collections;
using System.IO;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using KSP.IO;

namespace scatterer
{
	public class DisableEffectsForReflectionsCamera : MonoBehaviour
	{
		public ProlandManager manager;

		public DisableEffectsForReflectionsCamera ()
		{
		}

		//also add EVE cloud Projectors, EVE/PlanetLight, underwaterProjector, sunflare, should be all
		//and scatteringProjector not disabling correctly
		public void OnPreCull()
		{
			//the projector conflicts with underwaterProjector I guess, also, we need it for reflections when ascending to orbit
//			if (!ReferenceEquals(null,manager.GetSkyNode ().localScatteringProjector))
//				manager.GetSkyNode ().localScatteringProjector.setActivated(false);
			if (!ReferenceEquals (manager.GetOceanNode (), null))
			{
				manager.GetOceanNode ().setWaterMeshrenderersEnabled (false);
			}
		}

		public void OnPostRender()
		{
//			if (!ReferenceEquals (null, manager.GetSkyNode ().localScatteringProjector))
//				manager.GetSkyNode ().localScatteringProjector.setActivated (true);
			if (!ReferenceEquals(manager.GetOceanNode (),null))
				manager.GetOceanNode ().setWaterMeshrenderersEnabled (true);
		}
	}
}

