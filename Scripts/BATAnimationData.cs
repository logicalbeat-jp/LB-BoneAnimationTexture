using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if	UNITY_EDITOR
using UnityEditor;
#endif
using logicalbeat;

namespace logicalbeat
{
	[System.Serializable]
	public struct BATCurveData
	{
		public string		bindPath;			// バインドパス名群
		public float[]		times;				// キーの時刻
		public Quaternion[]	localRotations;		// ローカル回転群

		// 時刻インデックスを取得
		private void	GetTimeIndex( out int index, out float rate, float time, float timeLength )
		{
			// 時間を補正
			while ( time < 0.0f ) time += timeLength;
			time = time % timeLength;

			// インデックスを検知
			for (int h = 0;h < times.Length - 1;++h)
			{
				if ( ( time < times[h] ) || ( time > times[h+1] ) ) continue;
				index = h;
				rate  = ( time - times[h] ) / ( times[h+1] - times[h] );
				return;
			}

			// ここまで来たら見つかってない(エラー回避のため適当な数値を)
			index = 0;
			rate  = 0.0f;
		}

		// 時刻からローカル回転を取得
		public Quaternion	GetLocalRotation( float time, float timeLength )
		{
			// インデックスを取得
			int		index;
			float	rate;
			GetTimeIndex( out index, out rate, time, timeLength );

			// 補間処理を行う
			var	q = Quaternion.Slerp( localRotations[index+0], localRotations[index+1], rate );
			return	( q );
		}
	}

	public class BATAnimationData : ScriptableObject
	{
		[SerializeField]	private float			frameRate;		// フレームレート
		[SerializeField]	private float			timeLength;		// アニメーション時間長
		[SerializeField]	private BATCurveData[]	curveDatas;		// カーブデータ

		// フレームレート獲得
		public float	GetFrameRate()
		{
			// 値を返す
			return	( frameRate );
		}

		// アニメーション時間長獲得
		public float	GetTimeLength()
		{
			// 値を返す
			return	( timeLength );
		}

		// アニメーションテクスチャ作成
		public void	CreateAnimationTexture( out Texture2D tex, BATModelData modelData, float fps = -1.0f )
		{
			// 雑用変数の宣言
			var	boneDatasTmp = modelData.GetBoneDatas();
			var	boneDatas = new BATBoneData[boneDatasTmp.Length];
			for (int h = 0;h < boneDatas.Length;++h) boneDatas[h] = boneDatasTmp[h].Clone();

			// 骨における対象カーブを決定
			var	curveIndex = new int[boneDatas.Length];
			for (int h = 0;h < curveIndex.Length;++h)
			{
				curveIndex[h] = -1;
				for (int i = 0;i < curveDatas.Length;++i)
				{
					// 格納しなくていいものなら無視
					if ( boneDatas[h].boneIndex < 0 ) continue;

					// パス調査
					if ( boneDatas[h].fullPath.EndsWith( curveDatas[i].bindPath ) )
					{
						curveIndex[h] = i;
						break;
					}
				}
			}

			// テクスチャ作成
			{
				// FPSからキー数を取得
				if ( fps < 0.0f ) fps = frameRate;
				int	keyNum = (int)((float)fps * timeLength) + 1;

				// テクスチャのガワを作成
				tex = new Texture2D( keyNum, modelData.GetBoneIndexNum() * 3, TextureFormat.RGBAHalf, false, true );
//			tex = new Texture2D( keyNum, modelData.GetBoneIndexNum() * 3, TextureFormat.RGBAFloat, false, true );

				// ピクセル確保
				var	pixels = new Color[tex.width*tex.height];
				for (int y = 0;y < tex.height;y+=3)
				{
					for (int x = 0;x < tex.width;++x)
					{
						pixels[tex.width*(y+0)+x] = new Color( 1, 0, 0, 0 );
						pixels[tex.width*(y+1)+x] = new Color( 0, 1, 0, 0 );
						pixels[tex.width*(y+2)+x] = new Color( 0, 0, 1, 0 );
					}
				}

				// 情報格納
				for (int x = 0;x < keyNum;++x)
				{
					// 時刻決定
					float	time = (float)x / fps;

					// 骨データを更新
					for (int h = 0;h < curveIndex.Length;++h)
					{
						if ( curveIndex[h] < 0 ) continue;
						boneDatas[h].localRotation = curveDatas[curveIndex[h]].GetLocalRotation( time, timeLength );
					}

					// 骨ごとにカラーを設定
					for (int h = 0;h < boneDatas.Length;++h)
					{
						// 格納しなくていいものなら無視
						if ( boneDatas[h].boneIndex < 0 ) continue;

						// マトリクス取得
						var	wmtx = boneDatas[h].GetWorldMatrix( boneDatas );
						var	mtx  = wmtx * boneDatas[h].bindPose;

						// ピクセルを作る
						var	pixel0 = new Color( mtx.GetRow( 0 ).x, mtx.GetRow( 0 ).y, mtx.GetRow( 0 ).z, mtx.GetRow( 0 ).w );
						var	pixel1 = new Color( mtx.GetRow( 1 ).x, mtx.GetRow( 1 ).y, mtx.GetRow( 1 ).z, mtx.GetRow( 1 ).w );
						var	pixel2 = new Color( mtx.GetRow( 2 ).x, mtx.GetRow( 2 ).y, mtx.GetRow( 2 ).z, mtx.GetRow( 2 ).w );

						// ピクセルを対象位置へ格納
						int	y = boneDatas[h].boneIndex;
						pixels[keyNum*(y*3+0)+x] = pixel0;
						pixels[keyNum*(y*3+1)+x] = pixel1;
						pixels[keyNum*(y*3+2)+x] = pixel2;
					}
				}
				tex.SetPixels( pixels );
//				tex.filterMode = FilterMode.Point;
				tex.filterMode = FilterMode.Bilinear;
//				tex.wrapMode   = TextureWrapMode.Clamp;
				tex.wrapMode   = TextureWrapMode.Repeat;
				tex.Apply();
			}
		}

#if	UNITY_EDITOR
		// データの設定
		public void	SetData( AnimationClip clip, string directoryName, string baseName )
		{
//			// クリップを記憶
//			this.clip = clip;

			// フレームレート設定
			frameRate = clip.frameRate;

			// 関連情報を取得
			var	bindings = AnimationUtility.GetCurveBindings( clip );

			// アニメーション時間を測る
			float	animationLength = 0.0f;
			foreach ( var binding in bindings )
			{
				var	curve = AnimationUtility.GetEditorCurve( clip, binding );
				foreach ( var key in curve.keys )
				{
					animationLength = Mathf.Max( key.time );
				}
			}
			timeLength = animationLength;

			// バインドパス取得
			var	pathsTmp = new List<string>();
			var	curves   = new Dictionary<string, AnimationCurve>();
			for (int h = 0;h < bindings.Length;++h)
			{
				// バインド取得
				var	binding = bindings[h];

				// 回転じゃない時は戻る
				if ( true
					&& ( binding.propertyName != "m_LocalRotation.x" )
					&& ( binding.propertyName != "m_LocalRotation.y" )
					&& ( binding.propertyName != "m_LocalRotation.z" )
					&& ( binding.propertyName != "m_LocalRotation.w" )
				) continue;

				// 追加
				if ( pathsTmp.IndexOf( binding.path ) < 0 ) pathsTmp.Add( binding.path );

				// カーブ追加
				var	curve = AnimationUtility.GetEditorCurve( clip, binding );
				curves.Add( $"{binding.path}:{binding.propertyName}", curve );
			}
			curveDatas = new BATCurveData[pathsTmp.Count];

			// カーブデータ構築
			for (int h = 0;h < curveDatas.Length;++h)
			{
				// パス名設定
				curveDatas[h].bindPath = pathsTmp[h];

				// カーブ取得
				AnimationCurve	curveX = null;
				AnimationCurve	curveY = null;
				AnimationCurve	curveZ = null;
				AnimationCurve	curveW = null;
				if ( h < pathsTmp.Count )
				{
					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalRotation.x", out curveX );
					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalRotation.y", out curveY );
					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalRotation.z", out curveZ );
					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalRotation.w", out curveW );
				}

				// カーブ情報作成
				int	keyNum = (int)((float)frameRate * timeLength) + 1;
				curveDatas[h].times = new float[keyNum];
				curveDatas[h].localRotations = new Quaternion[keyNum];
				for (int x = 0;x < keyNum;++x)
				{
					// 時刻算出
					float	time = Mathf.Min( (float)x / (float)frameRate, timeLength );
					curveDatas[h].times[x] = time;

					// カーブ評価
					float	valueX = 1.0f;
					float	valueY = 1.0f;
					float	valueZ = 1.0f;
					float	valueW = 1.0f;
					if ( curveX != null ) valueX = curveX.Evaluate( time );
					if ( curveY != null ) valueY = curveY.Evaluate( time );
					if ( curveZ != null ) valueZ = curveZ.Evaluate( time );
					if ( curveW != null ) valueW = curveW.Evaluate( time );

					// 回転追加
					curveDatas[h].localRotations[x] = Quaternion.Normalize( new Quaternion( valueX, valueY, valueZ, valueW ) );
				}
			}
		}
#endif
	}
}
