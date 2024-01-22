// BAT関連
// Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
#pragma exclude_renderers gles

#ifndef	BAT_FUNCTIONS_INCLUDED
#define	BAT_FUNCTIONS_INCLUDED

#include "UnityCG.cginc"

float		_BAT_BoneNum;
float		_BAT_FPS;
float		_BAT_TimeLength;
sampler2D	_BAT_AnimationTexture;
float4		_BAT_AnimationTexture_TexelSize;

// インデックスごとのマトリクス取得
inline float3x4	BAT_getBoneMatrix( float u, float boneIndex )
{
	// Vを算出
	float	v0 = (boneIndex * 3.0f + 0.5f) / (_BAT_BoneNum * 3.0f);
	float	v1 = (boneIndex * 3.0f + 1.5f) / (_BAT_BoneNum * 3.0f);
	float	v2 = (boneIndex * 3.0f + 2.5f) / (_BAT_BoneNum * 3.0f);

	// テクスチャからマトリクスを得る
	float4x4	result;
	result[0] = tex2Dlod( _BAT_AnimationTexture, float4( u, v0, 0, 0 ) );
	result[1] = tex2Dlod( _BAT_AnimationTexture, float4( u, v1, 0, 0 ) );
	result[2] = tex2Dlod( _BAT_AnimationTexture, float4( u, v2, 0, 0 ) );
//	result[3] = float4( 0, 0, 0, 1 );

	return	( result );
}

// ローカルトランスフォーム計算
inline void	BAT_calculateLocalTransform( out float4 newPos, out float3 newNorm, out float4 newTang, float4 srcPos, float3 srcNorm, float4 srcTang, float time, float4 boneIndex, float4 boneWeight )
{
	// UVのUを計算
	float	u = frac( ( _BAT_FPS * fmod( time, _BAT_TimeLength ) ) / _BAT_AnimationTexture_TexelSize.z );

	// それぞれのマトリクスを取得し合成
	float3x4	mtx0 = BAT_getBoneMatrix( u, boneIndex.x );
	float3x4	mtx1 = BAT_getBoneMatrix( u, boneIndex.y );
	float3x4	mtx2 = BAT_getBoneMatrix( u, boneIndex.z );
	float3x4	mtx3 = BAT_getBoneMatrix( u, boneIndex.w );
	float3x4	mtx  = mtx0 * boneWeight.x + mtx1 * boneWeight.y + mtx2 * boneWeight.z + mtx3 * boneWeight.w;

	// 座標等を算出
	newPos  = float4( mul( mtx,         srcPos           ), 1 );
	newNorm =         mul( mtx, float4( srcNorm,     0 ) );
	newTang = float4( mul( mtx, float4( srcTang.xyz, 0 ) ), srcTang.w );
}

#endif // MODEL_COMMON_INCLUDED
