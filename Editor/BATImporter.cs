using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using logicalbeat;

namespace logicalbeat
{
	public class BATImporter : AssetPostprocessor
	{
		// BATデータであるか？
		private static bool	IsBATAsset( string path )
		{
			// ベース名取得
			string	baseName = Path.GetFileNameWithoutExtension( path );

			// 特定の接尾辞かどうかで判断
			return	( baseName.ToUpper().EndsWith( "__BAT" ) );
		}
		private bool	IsBATAsset()
		{
			// 別関数に引き渡す
			ModelImporter	importer = (ModelImporter)assetImporter;
			return	( IsBATAsset( importer.assetPath ) );
		}

		// 事前処理(アニメーション)
		private void	OnPreprocessAnimation()
		{
			// BATかどうか確認
			if ( !IsBATAsset() ) return;

			// Importer設定
			ModelImporter importer = (ModelImporter)assetImporter;

			// legacyClip変換
			importer.animationType = ModelImporterAnimationType.Legacy;
//			importer.sourceAvatar	= null;
			importer.avatarSetup   = ModelImporterAvatarSetup.NoAvatar;
		}

		// 事前処理(モデル)
		private void	OnPreprocessModel()
		{
			// BATかどうか確認
			if ( !IsBATAsset() ) return;

			// Importer設定
			ModelImporter	importer = (ModelImporter)assetImporter;

			// Legacy対応
			importer.avatarSetup	= ModelImporterAvatarSetup.NoAvatar;
//			importer.sourceAvatar	= null;
			importer.animationType	= ModelImporterAnimationType.Legacy;
		}

		// 事後処理(モデル)
		private void	OnPostprocessModel( GameObject obj )
		{
			// Importer設定
			ModelImporter	importer		= (ModelImporter)assetImporter;
//			string			assetPath		= importer.assetPath.ToLower();
			string			directoryName	= Path.GetDirectoryName( importer.assetPath ).Replace( "\\", "/" );
			string			baseName		= Path.GetFileNameWithoutExtension( importer.assetPath );

			// BATかどうか確認
			if ( !IsBATAsset() ) return;

			// SkinnedMeshRendererを持ってなかったら作らない
			var	smr = obj.GetComponentInChildren<SkinnedMeshRenderer>();
			if ( smr == null ) return;

			// 出力ファイル名を作る
			string	fileName	= $"{baseName}.asset";
			string	fullPath	= Path.Combine( directoryName, fileName );
					baseName	= Path.GetFileNameWithoutExtension( fileName );

			// ScriptableObjectを作る
			var	so = (BATModelData)AssetDatabase.LoadAssetAtPath( fullPath, typeof( BATModelData ) );
			if ( so != null )
			{
				// 値の変更
				so.SetData( obj, smr, directoryName, baseName );
				EditorUtility.SetDirty( so );
				AssetDatabase.SaveAssets();
			}
			else
			{
				// 新規作成
				so = ScriptableObject.CreateInstance<BATModelData>();
				so.SetData( obj, smr, directoryName, baseName );
				AssetDatabase.CreateAsset( so, fullPath );
			}
		}

#if	false
		private Material	OnAssignMaterialModel( Material material, Renderer renderer )
		{
			// ここまで来たらデフォルト
			return	( null );
		}
#endif

		// AnimationClip単体での処理
		private static void	OnPostprocessAnimationClip( string path, AnimationClip clip )
		{
			// 各種パス取得
			string	assetPath = path;
			if ( assetPath == "" ) assetPath = AssetDatabase.GetAssetPath( clip );
			string	directoryName	= Path.GetDirectoryName( assetPath ).Replace( "\\", "/" );
			string	baseName		= Path.GetFileNameWithoutExtension( assetPath );

			// BATかどうか確認
			if ( !IsBATAsset( assetPath ) ) return;

			// 出力ファイル名を作る
#if	false
			string	fileName	= $"{baseName.Substring( 0, baseName.Length - 5 )}_{clip.name}__BAT.asset";
#else
			string	fileName	= $"{baseName}_{clip.name}.asset";
#endif
			if ( path == "" ) fileName = $"{baseName}.asset";
			string	fullPath	= Path.Combine( directoryName, fileName );
					baseName	= Path.GetFileNameWithoutExtension( fileName );

			// ScriptableObjectを作る
			var	so = (BATAnimationData)AssetDatabase.LoadAssetAtPath( fullPath, typeof( BATAnimationData ) );
			if ( so != null )
			{
				// 値の変更
				so.SetData( clip, directoryName, baseName );
				EditorUtility.SetDirty( so );
				AssetDatabase.SaveAssets();
			}
			else
			{
				// 新規作成
				so = ScriptableObject.CreateInstance<BATAnimationData>();
				so.SetData( clip, directoryName, baseName );
				AssetDatabase.CreateAsset( so, fullPath );
			}
		}

		// 事後処理(アニメーション)
		private void	OnPostprocessAnimation( GameObject obj, AnimationClip clip )
		{
#if	true
			// 別関数で処理
			OnPostprocessAnimationClip( ((ModelImporter)assetImporter).assetPath, clip );
#else
			// Importer設定
			ModelImporter	importer		= (ModelImporter)assetImporter;
//			string			assetPath		= importer.assetPath.ToLower();
			string			directoryName	= Path.GetDirectoryName( importer.assetPath ).Replace( "\\", "/" );
			string			baseName		= Path.GetFileNameWithoutExtension( importer.assetPath );

			// BATかどうか確認
			if ( !IsBATAsset() ) return;

			// 出力ファイル名を作る
#if	false
			string	fileName	= $"{baseName.Substring( 0, baseName.Length - 5 )}_{clip.name}__BAT.asset";
#else
			string	fileName	= $"{baseName}_{clip.name}.asset";
#endif
			string	fullPath	= Path.Combine( directoryName, fileName );
				baseName	= Path.GetFileNameWithoutExtension( fileName );

			// ScriptableObjectを作る
			var	so = (BATAnimationData)AssetDatabase.LoadAssetAtPath( fullPath, typeof( BATAnimationData ) );
			if ( so != null )
			{
				// 値の変更
				so.SetData( clip, directoryName, baseName );
				EditorUtility.SetDirty( so );
				AssetDatabase.SaveAssets();
			}
			else
			{
				// 新規作成
				so = ScriptableObject.CreateInstance<BATAnimationData>();
				so.SetData( clip, directoryName, baseName );
				AssetDatabase.CreateAsset( so, fullPath );
			}
#endif
		}

		// 全アセット検査
		private static void	OnPostprocessAllAssets( string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths )
		{
			// アニメーションアセットの時の処理
			foreach ( string path in importedAssets )
			{
				// まず拡張子で判断
				string	extName = Path.GetExtension( path );
				if ( extName.ToLower() != ".anim" ) continue;

				// 情報をロードして処理
				var	clip = AssetDatabase.LoadAssetAtPath<AnimationClip>( path );
				if ( clip == null ) continue;
				OnPostprocessAnimationClip( "", clip );
			}

#if	false
			foreach ( string str in importedAssets )
			{
				Debug.Log("Reimported Asset: " + str);
			}
			foreach ( string str in deletedAssets )
			{
				Debug.Log("Deleted Asset: " + str);
			}

			for (int i = 0; i < movedAssets.Length; i++)
			{
				Debug.Log("Moved Asset: " + movedAssets[i] + " from: " + movedFromAssetPaths[i]);
			}
#endif
		}
	}
}
