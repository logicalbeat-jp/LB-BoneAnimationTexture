using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using logicalbeat;

namespace logicalbeat
{
	// BAT描画グループ
	public class BATDrawGroup
	{
		protected Matrix4x4		rootMatrix		= Matrix4x4.identity;		// ルートマトリクス
		protected int			instanceMax		= 0;						// インスタンス最大数
		protected Matrix4x4[]	matrices		= null;						// マトリクス群
		protected bool[]		useFlags		= null;						// マトリクス利用フラグ群
		protected bool[]		visibleFlags	= null;						// 表示フラグ群
		protected int			searchIndex		= 0;						// 検索用インデックス
		protected bool			dirtyFlag		= true;						// 更新用フラグ
		protected Matrix4x4[]	drawMatrices	= null;						// 描画用マトリクス群

		// コンストラクタ
		public	BATDrawGroup( int instanceMax )
		{
			// 配列を用意
			this.instanceMax = instanceMax;
			matrices = new Matrix4x4[instanceMax];
			useFlags = new bool[instanceMax];
			visibleFlags = new bool[instanceMax];

			// 更新準備
			dirtyFlag = true;
		}

		// 全消去
		public void	Clear()
		{
			// 利用フラグを全てfalseに
			for (int h = 0;h < instanceMax;++h)
			{
				useFlags[h] = false;
			}
			searchIndex = 0;

			// 更新準備
			dirtyFlag = true;
		}

		// ルートマトリクス設定
		public void	SetRootMatrix( in Matrix4x4 mtx )
		{
			// 値を設定
			rootMatrix = mtx;

			// 更新準備
			dirtyFlag = true;
		}

		// 追加
		public int	Add( in Matrix4x4 mtx, bool visible = true )
		{
			// 空きを探る
			int	index = -1;
			if ( index < 0 ){
				for (int h = searchIndex;h < instanceMax;++h) {
					if ( !useFlags[h] ) { index = h; break; }
				}
			}
			if ( index < 0 ) {
				for (int h = 0;h < searchIndex;++h) {
					if ( !useFlags[h] ) { index = h; break; }
				}
			}
			if ( index < 0 ) return ( -1 );
			searchIndex = (index + 1) % instanceMax;

			// 更新準備
			dirtyFlag = true;

			// 追加してインデックスを返す
			matrices[index]     = mtx;
			useFlags[index]     = true;
			visibleFlags[index] = visible;
			return	( index );
		}

		// マトリクス取得
		public Matrix4x4	GetMatrix( int index )
		{
			// 処理できない時は単位マトリクスを返す
			if ( ( index < 0 ) || ( index >= instanceMax ) ) return ( Matrix4x4.identity );
			if ( !useFlags[index] ) return ( Matrix4x4.identity );

			// 値を返す
			return	( matrices[index] );
		}

		// 更新
		public void	Update( int index, in Matrix4x4 mtx )
		{
			// 処理できない時は戻る
			if ( ( index < 0 ) || ( index >= instanceMax ) ) return;
			if ( !useFlags[index] ) return;

			// 値を上書き
			matrices[index] = mtx;

			// 更新準備
			dirtyFlag = true;
		}

		// 表示制御
		public void	SetVisible( int index, bool visible )
		{
			// 処理できない時は戻る
			if ( ( index < 0 ) || ( index >= instanceMax ) ) return;
			if ( !useFlags[index] ) return;

			// 更新準備
			dirtyFlag |= ( !visibleFlags[index] &&  visible );
			dirtyFlag |= (  visibleFlags[index] && !visible );

			// 値を上書き
			visibleFlags[index] = visible;
		}

		// 削除
		public void	Remove( int index )
		{
			// 処理できない時は戻る
			if ( ( index < 0 ) || ( index >= instanceMax ) ) return;

			// フラグを下げる
			useFlags[index] = false;

			// 更新準備
			dirtyFlag = true;
		}

		// 描画用配列を獲得
		public Matrix4x4[]	GetDrawMatrices()
		{
			// 更新の必要が無い時は作ってあるものをそのまま返す
			if ( !dirtyFlag && ( drawMatrices != null ) ) return ( drawMatrices );

			// 作業用のを確保
			var	mtxTmps = new List<Matrix4x4>();

			// 全て検査
			for (int h = 0;h < searchIndex;++h)
			{
				// 利用していない or 非表示なら次
				if ( !useFlags[h] || !visibleFlags[h] ) continue;

				// マトリクスを作って追加
				var	mtx = rootMatrix * matrices[h];
				mtxTmps.Add( mtx );
			}

			// しばらくは更新しなくて良い
			dirtyFlag = false;

			// 配列化して返す
			drawMatrices = mtxTmps.ToArray();
			return	( drawMatrices );
		}
	}

	// BAT描画処理
	public class BATDrawer : MonoBehaviour
	{
		// 描画モード
		public enum	DrawMode
		{
			Simple,			// シンプル描画
			Instancing,		// インスタンシング描画
		}

		private int					playAnimationIndexPrev	= -1;							// 前回の再生アニメ番号
		private Material[]			materials				= null;							// 描画用マテリアル群
		private Texture2D[]			animationTextures		= null;							// アニメーションテクスチャ群
		private List<BATDrawGroup>	drawGroups				= new List<BATDrawGroup>();		// 描画グループ群

							public  DrawMode			drawMode			= DrawMode.Simple;		// 対象アニメーションデータ群
		[SerializeField]	private BATModelData		modelData			= null;					// 対象モデルデータ
		[SerializeField]	private Material[]			sharedMaterials		= null;					// 対象マテリアル群
		[SerializeField]	private float				frameRate			= -1.0f;				// 再生フレームレート(-1でデフォルト)
		[SerializeField]	private BATAnimationData[]	animationDatas		= null;					// 対象アニメーションデータ群
							public  int					playAnimationIndex	= 0;					// 再生インデックス
							public  bool				castShadows			= true;					// 影を受けるか？

		// 有効な状態か？
		private bool	IsValid()
		{
			// 状態から判断
			return	( true
				&& ( modelData       != null )
				&& ( sharedMaterials != null )
				&& ( animationDatas  != null )
			);
		}

		// アニメーションデータ設定
		private void	SetAnimationData( int index )
		{
			// 既に指定済みの場合は戻る
			if ( playAnimationIndexPrev == playAnimationIndex ) return;
			playAnimationIndexPrev = playAnimationIndex;

			// FPS決定
			float	fps = frameRate;
			if ( fps < 0.0f ) fps = animationDatas[index].GetFrameRate();

			// 全てのマテリアルに設定
			for (int h = 0;h < materials.Length;++h)
			{
				materials[h].SetFloat( "_BAT_FPS", fps );
				materials[h].SetFloat( "_BAT_TimeLength", animationDatas[index].GetTimeLength() );
				materials[h].SetTexture( "_BAT_AnimationTexture", animationTextures[index] );
			}
		}

		// 起動
		private void Awake()
		{
			// 処理できない時は戻る
			if ( !IsValid() ) return;

			// マテリアルを複製
			if ( sharedMaterials != null )
			{
				// 雑用変数の宣言
				var	boneNum = modelData.GetBoneDatas().Length;

				// 全部に対して処理
				materials = new Material[sharedMaterials.Length];
				for (int h = 0;h < sharedMaterials.Length;++h)
				{
					// 生成
					materials[h] = new Material( sharedMaterials[h] );

					// 各種パラメータ設定
					materials[h].SetFloat( "_BAT_BoneNum", (float)modelData.GetBoneIndexNum() );
				}
			}

			// テクスチャ生成
			{
				// テクスチャ配列作成
				animationTextures = new Texture2D[animationDatas.Length];

				// 全部において作成
				for (int h = 0;h < animationTextures.Length;++h)
				{
					animationDatas[h].CreateAnimationTexture( out animationTextures[h], modelData, frameRate );
				}
			}

			// 初期アニメ設定
			SetAnimationData( playAnimationIndex );
		}

		// 更新
	//	private void Update()
		private void LateUpdate()
		{
			// アニメ番号を設定
			SetAnimationData( playAnimationIndex );

			// 描画処理
			if ( drawMode == DrawMode.Simple )
			{
				// シンプル描画
				for (int h = 0;h < sharedMaterials.Length;++h)
				{
					Graphics.DrawMesh(
						modelData.GetMesh(),				//	Mesh mesh,
						transform.localToWorldMatrix,		//	Matrix4x4 matrix,
						materials[h],						//	Material material,
						gameObject.layer,					//	int layer,
						null,								//	Camera camera= null,
						h,									//	int submeshIndex= 0,
						null,								//	MaterialPropertyBlock properties= null,
						castShadows,						//	bool castShadows= true,
						true,								//	bool receiveShadows= true,
						true								//	bool useLightProbes= true
					);
				}
			}
			else
			if ( drawMode == DrawMode.Instancing )
			{
				// インスタンシング描画
				var	shadowCastingMode = ShadowCastingMode.Off;
				if ( castShadows ) shadowCastingMode = ShadowCastingMode.On;

				// 描画グループごとに処理
				for (int h = 0;h < drawGroups.Count;++h)
				{
					// マトリクス配列取得
					var	matrices = drawGroups[h].GetDrawMatrices();
					if ( matrices.Length <= 0 ) continue;

					// 全マテリアルで処理
					for (int i = 0;i < sharedMaterials.Length;++i)
					{
						// マテリアルがインスタンシングに対応しているか？
						if ( !materials[i].enableInstancing )
						{
							UnityEngine.Debug.LogError( "{materials[i].name}がインスタンシングに対応していません！" );
							continue;
						}

						// 描画命令
						Graphics.DrawMeshInstanced(
							modelData.GetMesh(),				//	Mesh mesh,
							i,									//	int submeshIndex,
							materials[i],						//	Material material,
							matrices,							//	Matrix4x4[] matrices,
							matrices.Length,					//	int count = matrices.Length,
							null,								//	MaterialPropertyBlock properties = null,
							shadowCastingMode,					//	Rendering.ShadowCastingMode castShadows = ShadowCastingMode.On,
							true,								//	bool receiveShadows = true,
							gameObject.layer,					//	int layer = 0,
							null,								//	Camera camera = null,
							LightProbeUsage.BlendProbes,		//	Rendering.LightProbeUsage lightProbeUsage = LightProbeUsage.BlendProbes,
							null								//	LightProbeProxyVolume lightProbeProxyVolume = null
						);
					}
				}
			}
		}

		// 破棄
		private void OnDestroy()
		{
			// マテリアルの破棄
			if ( materials != null )
			{
				for (int h = 0;h < materials.Length;++h)
				{
					if ( materials[h] == null ) continue;
					UnityEngine.Object.Destroy( materials[h] );
					materials[h] = null;
				}
				materials = null;
			}
		}

		// 描画グループ追加
		public void	AddDrawGroup( BATDrawGroup group )
		{
			// 追加
			drawGroups.Add( group );
		}

		// 描画グループ削除
		public void	RemoveDrawGroup( BATDrawGroup group )
		{
			// 削除
			drawGroups.Remove( group );
		}
	}
}
