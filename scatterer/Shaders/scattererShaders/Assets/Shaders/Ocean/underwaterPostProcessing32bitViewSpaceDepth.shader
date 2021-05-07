﻿
Shader "Scatterer/UnderwaterScatter" {
	SubShader {
		Tags {"Queue" = "Transparent-5" "IgnoreProjector" = "True" "RenderType" = "Transparent"}

		Pass {
			//Cull Front
			Cull Off
			ZTest Off
			ZWrite Off

			Blend SrcAlpha OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#include "UnityCG.cginc"
			#include "../CommonAtmosphere.cginc"
			#include "../ClippingUtils.cginc"

			uniform float3 _camPos; // camera position relative to planet's origin
			uniform float3 _camForward; //camera's viewing direction, in world space

			uniform float3 _Underwater_Color;

			uniform sampler2D _customDepthTexture;

			uniform float4x4 _Globals_CameraToWorld;
			uniform float4x4 scattererFrustumCorners;

			uniform float transparencyDepth;
			uniform float darknessDepth;

			struct v2f
			{
				//float4 pos: SV_POSITION;
				float2 uv: TEXCOORD0;
				float3 view_dir:TEXCOORD1;
			};

			v2f vert(appdata_base v, out float4 outpos: SV_POSITION)
			{
				v2f o;
				v.vertex.y = v.vertex.y *_ProjectionParams.x;
				outpos = float4(v.vertex.xy,1.0,1.0);
				o.uv=v.texcoord.xy;
				o.view_dir = scattererFrustumCorners[(int) v.vertex.z]; 	//interpolated from frustum corners world viewdir
				return o;
			}

			float3 oceanColor(float3 viewDir, float3 lightDir, float3 surfaceDir)
			{
				float angleToLightDir = (dot(viewDir, surfaceDir) + 1 )* 0.5;
				float3 waterColor = pow(_Underwater_Color, 4.0 *(-1.0 * angleToLightDir + 1.0));
				return waterColor;
			}

			half4 frag(v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
			{
				float fragDistance = tex2D(_customDepthTexture, i.uv).r * 750000;                

				float3 rayDir=normalize(i.view_dir);

				float waterSurfaceDistance = intersectSphereInside(_camPos,rayDir,float3(0,0,0),Rg); //ocean surface check

				fragDistance = (waterSurfaceDistance != -1) && (waterSurfaceDistance<fragDistance) ? waterSurfaceDistance : fragDistance;

				//using view-space z
				float aa = dot(rayDir, normalize (_camForward)); //here I basically take the angle between the camera direction and the fragment direction
				//and multiply the depth value by it to get the true fragment distance
				//I'm using view-space z value as depth and basically the z depth value is the projection of the fragment on the near plane
				//As far as I can tell view-space z offers better, linear precision so it covers the whole scene and it's easy to work with
				//for other effects like SSAO as well

				float fragDepth=fragDistance*aa;

				bool returnPixel = fragmentInsideOfClippingRange(fragDepth); //if fragment depth outside of current camera clipping range, return empty pixel

				float waterLigthExtinction = length(getSkyExtinction(normalize(_camPos + 10.0) * Rg , SUN_DIR));

				float underwaterDepth = Rg - length(_camPos);

				underwaterDepth = lerp(1.0,0.0,underwaterDepth / darknessDepth);

				float3 waterColor= underwaterDepth * hdrNoExposure( waterLigthExtinction * oceanColor(rayDir,SUN_DIR,normalize(_camPos)));
				float alpha = min(fragDistance/transparencyDepth,1.0);
				return float4(dither(waterColor, screenPos), alpha*returnPixel);
			}

			ENDCG
		}
	}
}