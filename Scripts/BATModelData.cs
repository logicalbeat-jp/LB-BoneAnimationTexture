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
	public struct BATBoneData
	{
		public string		name;
		public string		fullPath;
		public int			parentIndex;
		public int			boneIndex;
		public Vector3		localPosition;
		public Quaternion	localRotation;
		public Vector3		localScale;
		public Matrix4x4	bindPose;

		// ワールドマトリクス取得
		public Matrix4x4	GetWorldMatrix( in BATBoneData[] datas, bool bSSC )
		{
			// 親を遡ってマトリクス計算
			var	data = this;
			var	mtx  = Matrix4x4.identity;
			while ( true )
			{
				// ローカルマトリクスを作成
				Matrix4x4	localMtx;
				if ( bSSC && ( data.parentIndex >= 0 ) )
				{
					// SSC考慮
					localMtx  = Matrix4x4.Translate( data.localPosition );
					localMtx *= Matrix4x4.Scale( new Vector3( 1.0f / datas[data.parentIndex].localScale.x, 1.0f / datas[data.parentIndex].localScale.y, 1.0f / datas[data.parentIndex].localScale.z ) );
					localMtx *= Matrix4x4.Rotate( data.localRotation );
					localMtx *= Matrix4x4.Scale( data.localScale );
				}
				else
				{
					// SSC無し
					localMtx = Matrix4x4.TRS( data.localPosition, data.localRotation, data.localScale );
				}

				// 親子構成
				mtx = localMtx * mtx;
				if ( data.parentIndex < 0 ) break;
				data = datas[data.parentIndex];
			}

			// 結果を返す
			return	( mtx );
		}

		// クローン生成
		public BATBoneData	Clone()
		{
			var	data = new BATBoneData();
			data.name			= name;
			data.fullPath		= fullPath;
			data.parentIndex	= parentIndex;
			data.boneIndex		= boneIndex;
			data.localPosition	= localPosition;
			data.localRotation	= localRotation;
			data.localScale		= localScale;
			data.bindPose		= bindPose;

			return	( data );
		}
	}

	public class BATModelData : ScriptableObject
	{
		[SerializeField]	private Mesh			sharedMesh;			// 対象メッシュ
		[SerializeField]	private int				boneIndexNum;		// 利用されている骨数
		[SerializeField]	private BATBoneData[]	boneDatas;			// 骨マトリクス群

		// メッシュ獲得
		public Mesh	GetMesh()
		{
			// 値を返す
			return	( sharedMesh );
		}

		// 利用されている骨数獲得
		public int	GetBoneIndexNum()
		{
			// 値を返す
			return	( boneIndexNum );
		}

		// 骨データ獲得
		public BATBoneData[]	GetBoneDatas()
		{
			// 値を返す
			return	( boneDatas );
		}

#if	UNITY_EDITOR
		// 骨情報の取得
		public void	SetDataSub( List<BATBoneData> datas, Transform boneTrans, Transform rootTrans, int parentIndex, Transform[] boneList )
		{
			// Rendererは無視
			if ( boneTrans.GetComponent<Renderer>() != null ) return;

			// データを作る
			var	data = new BATBoneData();
			data.name	= boneTrans.name;
			{
				var	boneTmp = boneTrans;
				string	boneName = "";
				while ( rootTrans != boneTmp )
				{
					if ( boneName != "" ) {
						boneName = $"{boneTmp.name}/{boneName}";
					} else {
						boneName = boneTmp.name;
					}
					boneTmp = boneTmp.parent;
				}
				data.fullPath = boneName;
			}
			data.parentIndex	= parentIndex;
			data.boneIndex		= System.Array.IndexOf( boneList, boneTrans );
			data.localPosition	= boneTrans.localPosition;
			data.localRotation	= boneTrans.localRotation;
			data.localScale		= boneTrans.localScale;
			data.bindPose		= boneTrans.worldToLocalMatrix * rootTrans.localToWorldMatrix;
			if ( boneTrans != rootTrans ) datas.Add( data );

			// 子供を全部探る
			int	currentIndex = datas.Count - 1;
			for (int h = 0;h < boneTrans.childCount;++h)
			{
				var	child = boneTrans.GetChild( h );
				SetDataSub( datas, child, rootTrans, currentIndex, boneList );
			}
		}

		// データの設定
		public void	SetData( GameObject root, SkinnedMeshRenderer smr, string directoryName, string baseName )
		{
			// メッシュを保存
			{
#if	false
				// 焼き付けてRigidなメッシュにする
				var	sharedMeshTmp = new Mesh();
				smr.BakeMesh( sharedMeshTmp );
#else
				// 複製をしてボーン関連情報を消す
				var	sharedMeshTmp = Instantiate( smr.sharedMesh );
				sharedMeshTmp.boneWeights = null;
				sharedMeshTmp.bindposes   = null;
#endif

				// UV2,3にスキニング用の情報を埋める
				var	uv2 = new List<Vector4>();
				var	uv3 = new List<Vector4>();
				for (int h = 0;h < smr.sharedMesh.boneWeights.Length;++h)
				{
					var	boneWeights = smr.sharedMesh.boneWeights[h];
					uv2.Add(
						new Vector4(
							boneWeights.boneIndex0,
							boneWeights.boneIndex1,
							boneWeights.boneIndex2,
							boneWeights.boneIndex3
						)
					);
					uv3.Add(
						new Vector4(
							boneWeights.weight0,
							boneWeights.weight1,
							boneWeights.weight2,
							boneWeights.weight3
						)
					);
				}
				sharedMeshTmp.SetUVs( 2, uv2 );
				sharedMeshTmp.SetUVs( 3, uv3 );

				// .assetとしてメッシュを保存
				var	fullPath = Path.Combine( directoryName, baseName + "_mesh.asset" ).Replace( "\\", "/" );
				var	asset = (Mesh)AssetDatabase.LoadAssetAtPath( fullPath, typeof( Mesh ) );
				if ( asset != null )
				{
					asset = Instantiate( sharedMeshTmp );
					EditorUtility.SetDirty( asset );
					AssetDatabase.SaveAssets();
				}
				else
				{
					AssetDatabase.CreateAsset( sharedMeshTmp, fullPath );
				}
				sharedMesh = AssetDatabase.LoadAssetAtPath( fullPath, typeof( Mesh ) ) as Mesh;
			}

			// 骨情報を取得
			var	boneDatasTmp = new List<BATBoneData>();
			SetDataSub( boneDatasTmp, root.transform, root.transform, -1, smr.bones );
			boneDatas = boneDatasTmp.ToArray();
			boneIndexNum = smr.bones.Length;
		}
#endif
	}
}
