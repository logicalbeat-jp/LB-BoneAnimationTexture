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
		public Vector3[]	localPositions;		// ローカル座標群
		public Quaternion[]	localRotations;		// ローカル回転群
		public Vector3[]	localScales;		// ローカルスケール群

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

		// ローカルVector3取得
		private void	GetLocalVector3( ref Vector3 result, Vector3[] vectors, float time, float timeLength )
		{
			// 取得できない時は戻る
			if ( false
				|| ( vectors == null )
				|| ( vectors.Length <= 0 )
			) return;

			// もし単一の時はそれをそのまま使う
			if ( vectors.Length == 1 )
			{
				result = vectors[0];
				return;
			}

			// インデックスを取得
			int		index;
			float	rate;
			GetTimeIndex( out index, out rate, time, timeLength );

			// 補間処理を行う
			result = Vector3.Lerp( vectors[index+0], vectors[index+1], rate );
		}

		// 時刻からローカル座標を取得
		public void	GetLocalPosition( ref Vector3 position, float time, float timeLength )
		{
			// 別関数で処理
			GetLocalVector3( ref position, localPositions, time, timeLength );
		}

		// 時刻からローカル回転を取得
		public void	GetLocalRotation( ref Quaternion rotation, float time, float timeLength )
		{
			// 取得できない時は戻る
			if ( false
				|| ( localRotations == null )
				|| ( localRotations.Length <= 0 )
			) return;

			// もし単一の時はそれをそのまま使う
			if ( localRotations.Length == 1 )
			{
				rotation = localRotations[0];
				return;
			}

			// インデックスを取得
			int		index;
			float	rate;
			GetTimeIndex( out index, out rate, time, timeLength );

			// 補間処理を行う
			rotation = Quaternion.Slerp( localRotations[index+0], localRotations[index+1], rate );
		}

		// 時刻からローカルスケールを取得
		public void	GetLocalScale( ref Vector3 scale, float time, float timeLength )
		{
			// 別関数で処理
			GetLocalVector3( ref scale, localScales, time, timeLength );
		}
	}

	public class BATAnimationData : ScriptableObject
	{
		[SerializeField]	private float			frameRate;		// フレームレート
		[SerializeField]	private float			timeLength;		// アニメーション時間長
		[SerializeField]	private bool			enabledSSC;		// SSCが有効か？
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
//				tex = new Texture2D( keyNum, modelData.GetBoneIndexNum() * 3, TextureFormat.RGBAFloat, false, true );

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
						curveDatas[curveIndex[h]].GetLocalPosition( ref boneDatas[h].localPosition, time, timeLength );
						curveDatas[curveIndex[h]].GetLocalRotation( ref boneDatas[h].localRotation, time, timeLength );
						curveDatas[curveIndex[h]].GetLocalScale( ref boneDatas[h].localScale, time, timeLength );
					}

					// 骨ごとにカラーを設定
					for (int h = 0;h < boneDatas.Length;++h)
					{
						// 格納しなくていいものなら無視
						if ( boneDatas[h].boneIndex < 0 ) continue;

						// マトリクス取得
						var	wmtx = boneDatas[h].GetWorldMatrix( boneDatas, enabledSSC );
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

				// 座標 or 回転 or スケールじゃない時は戻る
				if ( true
					&& ( binding.propertyName != "m_LocalPosition.x" )
					&& ( binding.propertyName != "m_LocalPosition.y" )
					&& ( binding.propertyName != "m_LocalPosition.z" )
					&& ( binding.propertyName != "m_LocalRotation.x" )
					&& ( binding.propertyName != "m_LocalRotation.y" )
					&& ( binding.propertyName != "m_LocalRotation.z" )
					&& ( binding.propertyName != "m_LocalRotation.w" )
					&& ( binding.propertyName != "m_LocalScale.x" )
					&& ( binding.propertyName != "m_LocalScale.y" )
					&& ( binding.propertyName != "m_LocalScale.z" )
				) continue;

				// 追加
				if ( pathsTmp.IndexOf( binding.path ) < 0 ) pathsTmp.Add( binding.path );

				// カーブ追加
				var	curve = AnimationUtility.GetEditorCurve( clip, binding );
				curves.Add( $"{binding.path}:{binding.propertyName}", curve );
			}

			// カーブデータ構築
			var	curveDatasTmp = new List<BATCurveData>( pathsTmp.Count );
			for (int h = 0;h < pathsTmp.Count;++h)
			{
				// 格納用
				var	curveData = new BATCurveData();

				// パス名設定
				curveData.bindPath = pathsTmp[h];

				// カーブ取得
				AnimationCurve	curvePositionX = null;
				AnimationCurve	curvePositionY = null;
				AnimationCurve	curvePositionZ = null;
				AnimationCurve	curveRotationX = null;
				AnimationCurve	curveRotationY = null;
				AnimationCurve	curveRotationZ = null;
				AnimationCurve	curveRotationW = null;
				AnimationCurve	curveScaleX    = null;
				AnimationCurve	curveScaleY    = null;
				AnimationCurve	curveScaleZ    = null;
				if ( h < pathsTmp.Count )
				{
					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalPosition.x", out curvePositionX );
					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalPosition.y", out curvePositionY );
					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalPosition.z", out curvePositionZ );

					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalRotation.x", out curveRotationX );
					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalRotation.y", out curveRotationY );
					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalRotation.z", out curveRotationZ );
					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalRotation.w", out curveRotationW );

					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalScale.x", out curveScaleX );
					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalScale.y", out curveScaleY );
					curves.TryGetValue( $"{pathsTmp[h]}:m_LocalScale.z", out curveScaleZ );
				}

				// カーブ情報作成
				int	keyNum = (int)((float)frameRate * timeLength) + 1;
				curveData.times = new float[keyNum];
				var	localPositions = new List<Vector3>();
				var	localRotations = new List<Quaternion>();
				var	localScales    = new List<Vector3>();
				for (int x = 0;x < keyNum;++x)
				{
					// 時刻算出
					float	time = Mathf.Min( (float)x / (float)frameRate, timeLength );
					curveData.times[x] = time;

					// 座標対応
					if ( true
						&& ( curvePositionX != null )
						&& ( curvePositionY != null )
						&& ( curvePositionZ != null )
					) {
						// カーブ評価
						float	valueX = curvePositionX.Evaluate( time );
						float	valueY = curvePositionY.Evaluate( time );
						float	valueZ = curvePositionZ.Evaluate( time );

						// 情報追加
						localPositions.Add( new Vector3( valueX, valueY, valueZ ) );
					}

					// 回転対応
					if ( true
						&& ( curveRotationX != null )
						&& ( curveRotationY != null )
						&& ( curveRotationZ != null )
						&& ( curveRotationW != null )
					) {
						// カーブ評価
						float	valueX = valueX = curveRotationX.Evaluate( time );
						float	valueY = valueY = curveRotationY.Evaluate( time );
						float	valueZ = valueZ = curveRotationZ.Evaluate( time );
						float	valueW = valueW = curveRotationW.Evaluate( time );

						// 情報追加
						localRotations.Add( Quaternion.Normalize( new Quaternion( valueX, valueY, valueZ, valueW ) ) );
					}

					// スケール対応
					if ( true
						&& ( curveScaleX != null )
						&& ( curveScaleY != null )
						&& ( curveScaleZ != null )
					) {
						// カーブ評価
						float	valueX = valueX = curveScaleX.Evaluate( time );
						float	valueY = valueY = curveScaleY.Evaluate( time );
						float	valueZ = valueZ = curveScaleZ.Evaluate( time );

						// 情報追加
						localScales.Add( new Vector3( valueX, valueY, valueZ ) );
					}
				}

				// カーブ情報圧縮
				if ( localPositions.Count >= 2 )
				{
					// 全部同一か？
					bool	comp = true;
					for (int i = 1;i < localPositions.Count;++i)
					{
						if ( localPositions[i] != localPositions[0] ) { comp = false; break; }
					}
					if ( comp ) localPositions.RemoveRange( 1, localPositions.Count - 1 );
				}
				if ( localRotations.Count >= 2 )
				{
					// 全部同一か？
					bool	comp = true;
					for (int i = 1;i < localRotations.Count;++i)
					{
						if ( localRotations[i] != localRotations[0] ) { comp = false; break; }
					}
					if ( comp ) localRotations.RemoveRange( 1, localRotations.Count - 1 );
				}
				if ( localScales.Count >= 2 )
				{
					// 全部同一か？
					bool	comp = true;
					for (int i = 1;i < localScales.Count;++i)
					{
						if ( localScales[i] != localScales[0] ) { comp = false; break; }
					}
					if ( comp ) localScales.RemoveRange( 1, localScales.Count - 1 );
				}
				curveData.localPositions = localPositions.ToArray();
				curveData.localRotations = localRotations.ToArray();
				curveData.localScales    = localScales.ToArray();

				// データ追加
				curveDatasTmp.Add( curveData );
			}
			curveDatas = curveDatasTmp.ToArray();
		}
#endif
	}
}
