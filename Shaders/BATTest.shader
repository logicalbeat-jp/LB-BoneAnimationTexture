Shader "logicalbeat/BATTest"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_BaseColor ("Base Color", Color) = (1, 1, 1, 1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			// make fog work
			#pragma multi_compile_fog
			#pragma multi_compile_instancing

			#include "UnityCG.cginc"
			#include "BAT_Functions.cginc"

			#define	ENABLE_INSTANCING_RANDOM_TEST		(0)		// インスタンシングごとにランダム処理するテスト

			struct appdata
			{
				float4	vertex     : POSITION;
				float3	normal     : NORMAL;
				float4	tangent    : TANGENT;
				float2	uv         : TEXCOORD0;
				float4	boneIndex  : TEXCOORD2;
				float4	boneWeight : TEXCOORD3;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float4	vertex  : SV_POSITION;
				float3	normal  : NORMAL;
				float4	tangent : TANGENT;
				float2	uv      : TEXCOORD0;
				UNITY_FOG_COORDS(1)
			};

			sampler2D	_MainTex;
			float4		_MainTex_ST;
			half4		_BaseColor;

			v2f vert (appdata v)
			{
				// インスタンシング対応
				UNITY_SETUP_INSTANCE_ID(v);

				v2f o;

				// ローカル座標の変形
#if	ENABLE_INSTANCING_RANDOM_TEST
				float	randomTime  = UNITY_MATRIX_M._m30;
				float	randomSpeed = 1.0f + UNITY_MATRIX_M._m31;
#else
				float	randomTime  = 0.0f;
				float	randomSpeed = 1.0f;
#endif
				float	time    = (_Time.y + randomTime) * randomSpeed;
				float4	vertex  = 0;
				float3	normal  = 0;
				float4	tangent = 0;
				BAT_calculateLocalTransform( vertex, normal, tangent, v.vertex, v.normal, v.tangent, time, v.boneIndex, v.boneWeight );

				// 通常の変換
#if	ENABLE_INSTANCING_RANDOM_TEST
				float4	wpos  = float4( mul( UNITY_MATRIX_M, vertex ).xyz, 1 );
				o.vertex      = mul( UNITY_MATRIX_VP, wpos );
#else
				o.vertex      = UnityObjectToClipPos( vertex );
#endif
				o.normal      = UnityObjectToWorldNormal( normal.xyz );
				o.tangent.xyz = UnityObjectToWorldDir( tangent );
				o.tangent.w   = tangent.w;
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				UNITY_TRANSFER_FOG(o,o.vertex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				// sample the texture
				fixed4 col = tex2D(_MainTex, i.uv);
				col *= _BaseColor;
				// apply fog
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
}
