﻿/*
 * Copyright (c) <2020> Side Effects Software Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 * 1. Redistributions of source code must retain the above copyright notice,
 *    this list of conditions and the following disclaimer.
 *
 * 2. The name of Side Effects Software may not be used to endorse or
 *    promote products derived from this software without specific prior
 *    written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY SIDE EFFECTS SOFTWARE "AS IS" AND ANY EXPRESS
 * OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.  IN
 * NO EVENT SHALL SIDE EFFECTS SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA,
 * OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
 * LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE,
 * EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

	
// Expose internal classes/functions
#if UNITY_EDITOR
using System ;
using System.Collections.Generic ;
using System.Runtime.CompilerServices;
using UnityEditor ;
using UnityEngine ;
using UnityEngine.Serialization ;
using UnityEngine.Tilemaps ;

[assembly: InternalsVisibleTo("HoudiniEngineUnityEditor")]
[assembly: InternalsVisibleTo("HoudiniEngineUnityEditorTests")]
[assembly: InternalsVisibleTo("HoudiniEngineUnityPlayModeTests")]
#endif


namespace HoudiniEngineUnity
{
	using HAPI_NodeId = Int32 ;


	/// <summary>
	/// Represents a general node for sending data upstream to Houdini.
	/// Currently only supports sending geometry upstream.
	/// Specify input data as file (eg. bgeo), HDA, and Unity gameobjects.
	/// </summary>
	public class HEU_InputNode: ScriptableObject, IHEU_InputNode,
								IHEU_HoudiniAssetSubcomponent,
								IEquivable< HEU_InputNode > {
		
		public enum InternalObjectType { UNKNOWN, HDA, UNITY_MESH, } ;
		public enum InputActions { ACTION, DELETE, INSERT, } ;
		
		// The type of input data set by user
		[Serializable]
		internal enum InputObjectType {
			HDA,
			UNITY_MESH,
			CURVE,
#if UNITY_2022_1_OR_NEWER
			SPLINE,
#endif
			TERRAIN,
			BOUNDING_BOX,
			TILEMAP,
		} ;
		
		
		// The type of input node based on how it was specified in the HDA
		internal enum InputNodeType {
			/// <summary>As an asset connection</summary>
			CONNECTION,

			/// <summary>Pure input asset node</summary>
			NODE,

			/// <summary>As a parameter</summary>
			PARAMETER,
		} ;
		
		
		
		// PUBLIC FIELDS =================================================================

		/// <inheritdoc />
		public HEU_HoudiniAsset ParentAsset => _parentAsset ;

		/// <inheritdoc />
		public bool KeepWorldTransform {
			get => _keepWorldTransform ;
			set => _keepWorldTransform = value ;
		}

		/// <inheritdoc />
		public bool PackGeometryBeforeMerging {
			get => _packGeometryBeforeMerging ;
			set => _packGeometryBeforeMerging = value ;
		}

		/// <inheritdoc />
		public HEU_InputNodeTypeWrapper NodeType => InputNodeType_InternalToWrapper( _inputNodeType ) ;

		/// <inheritdoc />
		public HEU_InputObjectTypeWrapper ObjectType => InputObjectType_InternalToWrapper( _inputObjectType ) ;

		/// <inheritdoc />
		public HEU_InputObjectTypeWrapper PendingObjectType {
			get => InputObjectType_InternalToWrapper( _pendingInputObjectType ) ;
			set => _pendingInputObjectType = InputObjectType_WrapperToInternal( value ) ;
		}

		/// <inheritdoc />
		public HAPI_NodeId InputNodeID => _nodeID ;

		/// <inheritdoc />
		public string InputName => _inputName ;

		/// <inheritdoc />
		public string LabelName => _labelName ;

		/// <inheritdoc />
		public string ParamName => _paramName ;

		/// <inheritdoc />
		public HEU_InputInterfaceMeshSettings MeshSettings => _meshSettings ;

		/// <inheritdoc />
		public HEU_InputInterfaceTilemapSettings TilemapSettings => _tilemapSettings ;

		/// <inheritdoc />
		public HEU_InputInterfaceSplineSettings SplineSettings => _splineSettings ;

		// ========================================================================

		// DATA -------------------------------------------------------------------------------------------------------


		[SerializeField] InputNodeType _inputNodeType ;
		internal InputNodeType InputType => _inputNodeType ;
		
		
		// I don't want to break backwards compatibility, but I want some options to map onto others to avoid duplication of tested code
		// So we will map InputObjectType -> InternalObjectType when uploading input.
		[SerializeField] InputObjectType _inputObjectType = InputObjectType.UNITY_MESH ;
		[SerializeField] InputObjectType _pendingInputObjectType = InputObjectType.UNITY_MESH ;

		// The IDs of the object merge created for the input objects
		[SerializeField] List< HEU_InputObjectInfo > _inputObjects = new( ) ;
		internal List< HEU_InputObjectInfo > InputObjects => _inputObjects ;

		// This holds node IDs of input nodes that are created for uploading mesh data
		[SerializeField] List< HAPI_NodeId > _inputObjectsConnectedAssetIDs = new( ) ;

#pragma warning disable 0414
		// [DEPRECATED: replaced with _inputAssetInfos]
		// Asset input: external reference used for UI
		[SerializeField] GameObject _inputAsset ;

		// [DEPRECATED: replaced with _inputAssetInfos]
		// Asset input: internal reference to the connected asset (valid if connected)
		[SerializeField] GameObject _connectedInputAsset ;
#pragma warning restore 0414

		// List of input HDAs
		[SerializeField] List< HEU_InputHDAInfo > _inputAssetInfos = new( ) ;

		internal List< HEU_InputHDAInfo > InputAssetInfos => _inputAssetInfos ;

		[SerializeField] HAPI_NodeId _nodeID ;
		[SerializeField] int _inputIndex ;
		[SerializeField] bool _requiresCook ;

		internal bool RequiresCook {
			get => _requiresCook ;
			set => _requiresCook = value ;
		}

		[SerializeField] bool _requiresUpload ;

		internal bool RequiresUpload {
			get => _requiresUpload ;
			set => _requiresUpload = value ;
		}

		[SerializeField] string _inputName ;
		[SerializeField] string _labelName ;
		[SerializeField] internal string _paramName ;
		
		[SerializeField] HAPI_NodeId _connectedNodeID = HEU_Defines.HEU_INVALID_NODE_ID ;

		[SerializeField] bool _keepWorldTransform = true ;
		//! Enabling Keep World Transform by default to keep consistent with other plugins
		

		[SerializeField] bool _packGeometryBeforeMerging ;
		[SerializeField] HEU_HoudiniAsset _parentAsset ;


		// Input Specific settings
		[SerializeField] HEU_InputInterfaceMeshSettings _meshSettings = new( ) ;

		// Tilemap specific settings:
		[SerializeField] HEU_InputInterfaceTilemapSettings _tilemapSettings = new( ) ;

		// Spline specific settings:
		[SerializeField] HEU_InputInterfaceSplineSettings _splineSettings = new( ) ;

		// Field used in UI only.
		[SerializeField] internal bool _usingSelectFromHierarchy ;

		// PUBLIC FUNCTIONS =====================================================================================

		/// <inheritdoc />
		public HEU_SessionBase GetSession( ) => _parentAsset
													? _parentAsset.GetAssetSession( true )
													: HEU_SessionManager.GetOrCreateDefaultSession( ) ;

		/// <inheritdoc />
		public void Recook( ) {
			_requiresCook = true ;
			if ( _parentAsset )
				_parentAsset.RequestCook( ) ;
		}

		/// <inheritdoc />
		public bool IsAssetInput( ) => _inputNodeType is InputNodeType.CONNECTION ;

		/// <inheritdoc />
		public int NumInputEntries( ) {
			if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.UNITY_MESH )
				return _inputObjects.Count ;

			return GetInternalObjectType( _inputObjectType ) is InternalObjectType.HDA
					   ? _inputAssetInfos.Count
					   : 0 ;
		}

		/// <inheritdoc />
		public GameObject GetInputEntryGameObject( int index ) {
			if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.UNITY_MESH ) {
				if ( index >= 0 && index < _inputObjects.Count )
					return _inputObjects[ index ]._gameObject ;

				HEU_Logger.LogErrorFormat( "Get index {0} out of range (number of items is {1})", index,
										   _inputObjects.Count ) ;
			}
			else if ( GetInternalObjectType( _inputObjectType ) == InternalObjectType.HDA ) {
				if ( index >= 0 && index < _inputAssetInfos.Count )
					return _inputAssetInfos[ index ]._pendingGO ;

				HEU_Logger.LogErrorFormat( "Get index {0} out of range (number of items is {1})", index,
										   _inputAssetInfos.Count ) ;
			}

			return null ;
		}

		/// <inheritdoc />
		public GameObject[] GetInputEntryGameObjects( ) {
			GameObject[] inputObjects = new GameObject[ _inputObjects.Count ] ;
			if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.UNITY_MESH ) {
				for ( int i = 0; i < _inputObjects.Count; ++i )
					inputObjects[ i ] = _inputObjects[ i ]._gameObject ;

				return inputObjects ;
			}

			if ( GetInternalObjectType( _inputObjectType ) is not InternalObjectType.HDA ) return null ;

			int count = _inputAssetInfos.Count ;
			inputObjects = new GameObject[ _inputAssetInfos.Count ] ;
			for ( int i = 0; i < count; ++i )
				inputObjects[ i ] = _inputAssetInfos[ i ]._pendingGO ;

			return inputObjects ;
		}

		/// <inheritdoc />
		public void SetInputEntry( int index, GameObject newInputGameObject, bool bRecookAsset = false ) {
			bool bSuccess = true ;

			if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.UNITY_MESH ) {
				if ( index >= 0 && index < _inputObjects.Count ) {
					_inputObjects[ index ] = CreateInputObjectInfo( newInputGameObject ) ;
				}
				else {
					HEU_Logger.LogErrorFormat( "Insert index {0} out of range (number of items is {1})", index,
											   _inputObjects.Count ) ;
					bSuccess = false ;
				}
			}
			else if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.HDA ) {
				if ( index >= 0 && index < _inputAssetInfos.Count )
					_inputAssetInfos[ index ] = CreateInputHDAInfo( newInputGameObject ) ;
				else {
					HEU_Logger.LogErrorFormat( "Insert index {0} out of range (number of items is {1})",
											   index, _inputAssetInfos.Count ) ;
					bSuccess = false ;
				}
			}

			if ( bSuccess && bRecookAsset )
				Recook( ) ;
		}

		/// <inheritdoc />
		public void InsertInputEntry( int index, GameObject newInputGameObject, bool bRecookAsset = false ) {
			bool bSuccess = true ;
			if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.UNITY_MESH ) {
				if ( index >= 0 && index < _inputObjects.Count ) {
					_inputObjects.Insert( index, CreateInputObjectInfo( newInputGameObject ) ) ;
				}
				else {
					HEU_Logger.LogErrorFormat( "Insert index {0} out of range (number of items is {1})", index,
											   _inputObjects.Count ) ;
					bSuccess = false ;
				}
			}
			else if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.HDA ) {
				if ( index >= 0 && index < _inputAssetInfos.Count ) {
					_inputAssetInfos.Insert( index, CreateInputHDAInfo( newInputGameObject ) ) ;
				}
				else {
					HEU_Logger.LogErrorFormat( "Insert index {0} out of range (number of items is {1})", index,
											   _inputAssetInfos.Count ) ;
					bSuccess = false ;
				}
			}

			if ( bSuccess && bRecookAsset ) Recook( ) ;
		}

		/// <inheritdoc />
		public void AddInputEntryAtEnd( GameObject newEntryGameObject, bool bRecookAsset = false ) {
			bool bSuccess = true ;

			if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.UNITY_MESH )
				InternalAddInputObjectAtEnd( newEntryGameObject ) ;
			else if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.HDA )
				InternalAddInputHDAAtEnd( newEntryGameObject ) ;
			else {
				HEU_Logger.LogWarning( "Warning: Unsupported input type!" ) ;
				bSuccess = false ;
			}

			if ( bSuccess && bRecookAsset )
				Recook( ) ;
		}

		/// <inheritdoc />
		public void ResetInputNode( bool bRecookAsset = false ) {
			HEU_SessionBase session = GetSession( ) ;
			if ( session is null ) return ;

			ResetInputNode( session ) ;
			if ( bRecookAsset ) Recook( ) ;
		}

		/// <inheritdoc />
		public void ChangeInputType( HEU_InputObjectTypeWrapper newType, bool bRecookAsset = false ) {
			InputObjectType internalType = InputObjectType_WrapperToInternal( newType ) ;
			if ( internalType == _inputObjectType ) return ;
			HEU_SessionBase session = GetSession( ) ;
			if ( session is null ) return ;

			ChangeInputType( session, internalType ) ;
			if ( bRecookAsset ) Recook( ) ;
		}

		/// <inheritdoc />
		public void RemoveInputEntry( int index, bool bRecookAsset = false ) {
			bool bSuccess = true ;
			if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.UNITY_MESH ) {
				if ( index >= 0 && index < _inputObjects.Count )
					_inputObjects.RemoveAt( index ) ;
				else {
					HEU_Logger.LogErrorFormat( "Insert index {0} out of range (number of items is {1})",
											   index, _inputObjects.Count ) ;
					bSuccess = false ;
				}
			}
			else if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.HDA ) {
				if ( index >= 0 && index < _inputAssetInfos.Count )
					_inputAssetInfos.RemoveAt( index ) ;
				else {
					HEU_Logger.LogErrorFormat( "Insert index {0} out of range (number of items is {1})",
											   index, _inputAssetInfos.Count ) ;
					bSuccess = false ;
				}
			}

			if ( bSuccess && bRecookAsset ) Recook( ) ;
		}

		/// <inheritdoc />
		public void RemoveAllInputEntries( bool bRecookAsset = false ) {
			_inputObjects.Clear( ) ;
			_inputAssetInfos.Clear( ) ;
		}

		/// <inheritdoc />
		public void SetInputEntryObjectUseTransformOffset( int index, bool value, bool bRecookAsset = false ) {
			if ( index >= _inputObjects.Count ) {
				HEU_Logger.LogError( "Index is out of range when setting offset transform." ) ;
				return ;
			}

			_inputObjects[ index ]._useTransformOffset = value ;
			if ( bRecookAsset ) Recook( ) ;
		}

		/// <inheritdoc />
		public void SetInputEntryObjectTransformTranslateOffset( int  index, Vector3 translateOffset,
																 bool bRecookAsset = false ) {
			if ( index >= _inputObjects.Count ) {
				HEU_Logger.LogError( "Index is out of range when setting offset transform." ) ;
				return ;
			}

			_inputObjects[ index ]._translateOffset = translateOffset ;
			if ( bRecookAsset ) Recook( ) ;
		}

		/// <inheritdoc />
		public void SetInputEntryObjectTransformRotateOffset( int  index, Vector3 rotateOffset,
															  bool bRecookAsset = false ) {
			if ( index >= _inputObjects.Count ) {
				HEU_Logger.LogError( "Index is out of range when setting offset transform." ) ;
				return ;
			}

			_inputObjects[ index ]._rotateOffset = rotateOffset ;
			if ( bRecookAsset ) Recook( ) ;
		}

		/// <inheritdoc />
		public void SetInputEntryObjectTransformScaleOffset( int  index, Vector3 scaleOffset,
															 bool bRecookAsset = false ) {
			if ( index >= _inputObjects.Count ) {
				HEU_Logger.LogError( "Index is out of range when setting offset transform." ) ;
				return ;
			}

			_inputObjects[ index ]._scaleOffset = scaleOffset ;
			if ( bRecookAsset ) Recook( ) ;
		}

		/// <inheritdoc />
		public bool AreAnyInputHDAsConnected( ) {
			foreach ( HEU_InputHDAInfo asset in _inputAssetInfos )
				if ( asset._connectedGO )
					return true ;
			return false ;
		}

		/// <inheritdoc />
		public int GetConnectedInputCount( ) {
			if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.UNITY_MESH )
				return _inputObjectsConnectedAssetIDs.Count ;
			return GetInternalObjectType( _inputObjectType ) is InternalObjectType.HDA
					   ? _inputAssetInfos.Count
					   : 0 ;
		}

		/// <inheritdoc />
		public HAPI_NodeId GetConnectedNodeID( int index ) {
			if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.UNITY_MESH ) {
				if ( index >= 0 && index < _inputObjectsConnectedAssetIDs.Count )
					return _inputObjectsConnectedAssetIDs[ index ] ;
			}
			else if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.HDA )
				return _inputAssetInfos[ index ]._connectedInputNodeID ;

			return HEU_Defines.HEU_INVALID_NODE_ID ;
		}

		/// <inheritdoc />
		public void LoadPreset( HEU_InputPreset inputPreset ) {
			HEU_SessionBase session = GetSession( ) ;
			if ( session is not null )
				LoadPreset( session, inputPreset ) ;
		}

		/// <inheritdoc />
		public void PopulateInputPreset( HEU_InputPreset inputPreset ) {
			inputPreset._inputObjectType = _inputObjectType ;

			// Deprecated and replaced with _inputAssetPresets. Leaving it in for backwards compatibility.
			//inputPreset._inputAssetName = _inputAsset != null ? _inputAsset.name : "";

			inputPreset._inputIndex                = _inputIndex ;
			inputPreset._inputName                 = _inputName ;
			inputPreset._keepWorldTransform        = _keepWorldTransform ;
			inputPreset._packGeometryBeforeMerging = _packGeometryBeforeMerging ;

			foreach ( HEU_InputObjectInfo inputObject in _inputObjects ) {
				HEU_InputObjectPreset inputObjectPreset = new( ) ;

				if ( !inputObject._gameObject )
					inputObjectPreset._gameObjectName = string.Empty ;
				else {
					inputObjectPreset._gameObjectName = inputObject._gameObject.name ;

					// Tag whether scene or project input object
					inputObjectPreset._isSceneObject =
						!HEU_GeneralUtility.IsGameObjectInProject( inputObject._gameObject ) ;
					if ( !inputObjectPreset._isSceneObject ) {
						// For inputs in project, use the project path as name
						inputObjectPreset._gameObjectName =
							HEU_AssetDatabase.GetAssetOrScenePath( inputObject._gameObject ) ;
					}
				}

				inputObjectPreset._useTransformOffset = inputObject._useTransformOffset ;
				inputObjectPreset._translateOffset    = inputObject._translateOffset ;
				inputObjectPreset._rotateOffset       = inputObject._rotateOffset ;
				inputObjectPreset._scaleOffset        = inputObject._scaleOffset ;

				inputPreset._inputObjectPresets.Add( inputObjectPreset ) ;
			}

			foreach ( HEU_InputHDAInfo hdaInfo in _inputAssetInfos ) {
				if ( !hdaInfo._connectedGO ) continue ;

				HEU_InputAssetPreset inputAssetPreset = new( )
				{
					_gameObjectName = !HEU_GeneralUtility.IsGameObjectInProject( hdaInfo._connectedGO )
										  ? hdaInfo._connectedGO.name
										  : string.Empty,
				} ;

				inputPreset._inputAssetPresets.Add( inputAssetPreset ) ;
			}
		}

		// =====================================================================================================

		// LOGIC ------------------------------------------------------------------------------------------------------

		internal static HEU_InputNode CreateSetupInput( HAPI_NodeId      nodeID,    int inputIndex, string inputName,
														string           labelName, InputNodeType inputNodeType,
														HEU_HoudiniAsset parentAsset ) {
			HEU_InputNode newInput = CreateInstance< HEU_InputNode >( ) ;
			newInput._nodeID        = nodeID ;
			newInput._inputIndex    = inputIndex ;
			newInput._inputName     = inputName ;
			newInput._labelName     = labelName ;
			newInput._inputNodeType = inputNodeType ;
			newInput._parentAsset   = parentAsset ;

			newInput._requiresUpload = false ;
			newInput._requiresCook   = false ;

			return newInput ;
		}

		internal void SetInputNodeID( HAPI_NodeId nodeID ) => _nodeID = nodeID ;

		internal void DestroyAllData( HEU_SessionBase session ) {
			ClearUICache( ) ;
			DisconnectAndDestroyInputs( session ) ;
			RemoveAllInputEntries( ) ;
		}

		void ResetInputObjectTransforms( ) {
			for ( int i = 0; i < _inputObjects.Count; ++i ) {
				_inputObjects[ i ]._syncdTransform = Matrix4x4.identity ;
				_inputObjects[ i ]._syncdChildTransforms.Clear( ) ;
			}
		}

		internal void ResetInputNode( HEU_SessionBase session ) {
			ResetConnectionForForceUpdate( session ) ;
			RemoveAllInputEntries( ) ;
			ClearUICache( ) ;

			ChangeInputType( session, InputObjectType.UNITY_MESH ) ;
		}

		// Add a new entry to the end (for UNITY_MESH)
		internal HEU_InputObjectInfo AddInputEntryAtEndMesh( GameObject newEntryGameObject ) =>
			GetInternalObjectType( _inputObjectType ) is InternalObjectType.UNITY_MESH
				? InternalAddInputObjectAtEnd( newEntryGameObject )
				: null ;

		// Add a new entry to the end (for HDAs)
		internal HEU_InputHDAInfo AddInputEntryAtEndHDA( GameObject newEntryGameObject ) =>
			GetInternalObjectType( _inputObjectType ) is InternalObjectType.HDA
				? InternalAddInputHDAAtEnd( newEntryGameObject )
				: null ;

		// Change the input type
		internal void ChangeInputType( HEU_SessionBase session, InputObjectType newType ) {
			if ( newType == _inputObjectType ) return ;

			DisconnectAndDestroyInputs( session ) ;
			_inputObjectType        = newType ;
			_pendingInputObjectType = _inputObjectType ;
		}

		/// <summary>Reset the connected state so that any previous connection will be remade.</summary>
		internal void ResetConnectionForForceUpdate( HEU_SessionBase session ) {
			if ( GetInternalObjectType( _inputObjectType ) is not InternalObjectType.HDA
				 || !AreAnyInputHDAsConnected( ) ) return ;

			// By disconnecting here, we can then properly reconnect again.
			// This is needed when loading a saved scene and recooking.
			DisconnectConnectedMergeNode( session ) ;

			// Clear out input HDA hooks (upstream callback)
			ClearConnectedInputHDAs( ) ;
		}

		internal void UploadInput( HEU_SessionBase session ) {
			if ( _nodeID is HEU_Defines.HEU_INVALID_NODE_ID ) {
				HEU_Logger.LogErrorFormat( "Input Node ID is invalid. Unable to upload input. Try recooking." ) ;
				return ;
			}

			if ( _pendingInputObjectType != _inputObjectType )
				ChangeInputType( session, _pendingInputObjectType ) ;

			if ( _inputObjectType is InputObjectType.CURVE ) {
				// Curves are the same as HDAs except with type checking

				foreach ( HEU_InputHDAInfo inputHDAInfo in _inputAssetInfos ) {
					if ( inputHDAInfo is null || !inputHDAInfo._pendingGO ) continue ;

					HEU_HoudiniAssetRoot assetRoot = inputHDAInfo._pendingGO.GetComponent< HEU_HoudiniAssetRoot >( ) ;
					if ( assetRoot is not { _houdiniAsset: not null } ) continue ;
					if ( assetRoot._houdiniAsset.Curves.Count is 0 )
						HEU_Logger.LogErrorFormat( "Input asset {0} contains no curves!",
												   assetRoot.gameObject.name ) ;
				}

				UploadHDAInput( session ) ;
			}
			else if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.HDA ) {
				// An HDA input should be able to use any kind of HDA
				UploadHDAInput( session ) ;
			}
			else {
				UploadUnityInput( session ) ;
				//HEU_Logger.LogErrorFormat("Unsupported input type {0}. Unable to upload input.", _inputObjectType);
			}

			RequiresUpload = false ;
			RequiresCook   = true ;
			ClearUICache( ) ;
		}

		// Actually uploads the HDA input to Houdini
		void UploadHDAInput( HEU_SessionBase session ) {
			// Connect HDAs
			// First clear all previous input connections
			DisconnectAndDestroyInputs( session ) ;

			// Create merge object, and connect all input HDAs
			bool bResult = HEU_InputUtility.CreateInputNodeWithMultiAssets( session, _parentAsset, ref _connectedNodeID,
																			ref _inputAssetInfos,
																			_keepWorldTransform ) ;
			if ( !bResult ) {
				DisconnectAndDestroyInputs( session ) ;
				return ;
			}

			// Now connect from this asset to the merge object
			ConnectToMergeObject( session ) ;

			if ( !UploadObjectMergeTransformType( session ) ) {
				HEU_Logger.LogErrorFormat( "Failed to upload object merge transform type!" ) ;
				return ;
			}

			if ( !UploadObjectMergePackGeometry( session ) ) {
				HEU_Logger.LogErrorFormat( "Failed to upload object merge pack geometry value!" ) ;
			}
		}

		// Actually uploads the Unity input to Houdini
		void UploadUnityInput( HEU_SessionBase session ) {
			// Connect regular gameobjects

			if ( _inputObjects is not { Count: > 0 } )
				DisconnectAndDestroyInputs( session ) ;
			else {
				DisconnectAndDestroyInputs( session ) ;
				List< HEU_InputObjectInfo > inputObjectClone = new( _inputObjects ) ;

				// Special input interface preprocessing
				for ( int i = inputObjectClone.Count - 1; i >= 0; --i ) {
					if ( inputObjectClone[ i ] is not { _gameObject: not null } )
						continue ;

					HEU_BoundingVolume boundingVolume =
						inputObjectClone[ i ]._gameObject.GetComponent< HEU_BoundingVolume >( ) ;
					if ( !boundingVolume ) continue ;

					List< GameObject > boundingBoxObjects = boundingVolume.GetAllIntersectingObjects( ) ;
					if ( boundingBoxObjects is null ) continue ;

					foreach ( GameObject obj in boundingBoxObjects ) {
						if ( !obj ) continue ;
						HEU_InputObjectInfo newObjInfo = new( ) ;
						inputObjectClone[ i ].CopyTo( newObjInfo ) ;
						newObjInfo._gameObject = obj ;
						inputObjectClone.Add( newObjInfo ) ;
					}

					// Remove this because it's not a real interface
					inputObjectClone.RemoveAt( i ) ;
				}

				// Create merge object, and input nodes with data, then connect them to the merge object
				bool bResult = HEU_InputUtility.CreateInputNodeWithMultiObjects( session, _nodeID, ref _connectedNodeID,
																					ref inputObjectClone,
																					ref _inputObjectsConnectedAssetIDs,
																					this ) ;
				if ( !bResult ) {
					DisconnectAndDestroyInputs( session ) ;
					return ;
				}

				// Now connect from this asset to the merge object
				ConnectToMergeObject( session ) ;

				if ( !UploadObjectMergeTransformType( session ) ) {
					HEU_Logger.LogErrorFormat( "Failed to upload object merge transform type!" ) ;
					return ;
				}

				if ( !UploadObjectMergePackGeometry( session ) ) {
					HEU_Logger.LogErrorFormat( "Failed to upload object merge pack geometry value!" ) ;
				}
			}
		}

		internal void ReconnectToUpstreamAsset( ) {
			if ( GetInternalObjectType( _inputObjectType ) is not InternalObjectType.HDA ||
				 !AreAnyInputHDAsConnected( ) ) return ;

			foreach ( HEU_InputHDAInfo hdaInfo in _inputAssetInfos ) {
				HEU_HoudiniAssetRoot inputAssetRoot = hdaInfo._connectedGO
														  ? hdaInfo._connectedGO.GetComponent< HEU_HoudiniAssetRoot >( )
														  : null ;

				if ( inputAssetRoot is { _houdiniAsset: not null } )
					_parentAsset.ConnectToUpstream( inputAssetRoot._houdiniAsset ) ;
			}
		}

		HEU_InputObjectInfo CreateInputObjectInfo( GameObject inputGameObject ) {
			HEU_InputObjectInfo newObjectInfo = new( ) { _gameObject = inputGameObject, } ;
			newObjectInfo.SetReferencesFromGameObject( ) ;
			return newObjectInfo ;
		}

		HEU_InputHDAInfo CreateInputHDAInfo( GameObject inputGameObject ) {
			HEU_InputHDAInfo newInputInfo = new( )
			{
				_pendingGO            = inputGameObject,
				_connectedInputNodeID = HEU_Defines.HEU_INVALID_NODE_ID,
			} ;
			return newInputInfo ;
		}

		// Helper for adding a new input object the end
		HEU_InputObjectInfo InternalAddInputObjectAtEnd( GameObject newInputGameObject ) {
			HEU_InputObjectInfo inputObject = CreateInputObjectInfo( newInputGameObject ) ;
			_inputObjects.Add( inputObject ) ;
			return inputObject ;
		}

		// Helper for adding a new input object the end
		HEU_InputHDAInfo InternalAddInputHDAAtEnd( GameObject newInputHDA ) {
			HEU_InputHDAInfo inputInfo = CreateInputHDAInfo( newInputHDA ) ;
			_inputAssetInfos.Add( inputInfo ) ;
			return inputInfo ;
		}

		void DisconnectConnectedMergeNode( HEU_SessionBase session ) {
			if ( session is null || !_parentAsset ) return ;
			//HEU_Logger.LogWarningFormat("Disconnecting Node Input for _nodeID={0} with type={1}", _nodeID, _inputNodeType);

			if ( _inputNodeType is InputNodeType.PARAMETER ) {
				HEU_ParameterData paramData = _parentAsset.Parameters.GetParameter( _paramName ) ;
				if ( paramData is null )
					HEU_Logger.LogErrorFormat( "Unable to find parameter with name {0}!", _paramName ) ;
				else if ( !session.SetParamStringValue( _nodeID, "", paramData.ParmID, 0 ) )
					HEU_Logger.LogErrorFormat( "Unable to clear object path parameter for input node!" ) ;
			}

			else if ( _nodeID is not HEU_Defines.HEU_INVALID_NODE_ID )
				session.DisconnectNodeInput( _nodeID, _inputIndex, false ) ;
		}

		void ClearConnectedInputHDAs( ) {
			int numInputs = _inputAssetInfos.Count ;
			for ( int i = 0; i < numInputs; ++i ) {
				if ( _inputAssetInfos[ i ] is null )
					continue ;

				HEU_HoudiniAssetRoot inputAssetRoot = _inputAssetInfos[ i ]._connectedGO
														  ? _inputAssetInfos[ i ]._connectedGO
															  .GetComponent< HEU_HoudiniAssetRoot >( )
														  : null ;
				if ( inputAssetRoot )
					_parentAsset.DisconnectFromUpstream( inputAssetRoot._houdiniAsset ) ;

				_inputAssetInfos[ i ]._connectedGO          = null ;
				_inputAssetInfos[ i ]._connectedInputNodeID = HEU_Defines.HEU_INVALID_NODE_ID ;
			}
		}

		/// <summary>Connect the input to the merge object node</summary>
		/// <param name="session"></param>
		void ConnectToMergeObject( HEU_SessionBase session ) {
			if ( _inputNodeType is InputNodeType.PARAMETER ) {
				if ( string.IsNullOrEmpty( _paramName ) ) {
					HEU_Logger.LogErrorFormat( "Invalid parameter name for input node of parameter type!" ) ;
					return ;
				}

				if ( session.SetParamNodeValue( _nodeID, _paramName, _connectedNodeID ) ) return ;
				HEU_Logger.LogErrorFormat( "Unable to connect to input node!" ) ;
				//HEU_Logger.LogFormat("Setting input connection for parameter {0} with {1} connecting to {2}", _paramName, _nodeID, _connectedNodeID);
			}

			if ( session.ConnectNodeInput( _nodeID, _inputIndex, _connectedNodeID ) ) return ;
			HEU_Logger.LogErrorFormat( "Unable to connect to input node!" ) ;
		}

		void DisconnectAndDestroyInputs( HEU_SessionBase session ) {
			// First disconnect the merge node from its connections
			DisconnectConnectedMergeNode( session ) ;

			// Clear out input HDA hooks (upstream callback)
			ClearConnectedInputHDAs( ) ;

			if ( session != null ) {
				// Delete the input nodes that were created
				foreach ( HAPI_NodeId nodeID in _inputObjectsConnectedAssetIDs )
					if ( nodeID is not HEU_Defines.HEU_INVALID_NODE_ID )
						session.DeleteNode( nodeID ) ;

				// Delete the SOP/merge we created
				if ( _connectedNodeID is not HEU_Defines.HEU_INVALID_NODE_ID &&
					 HEU_HAPIUtility.IsNodeValidInHoudini( session, _connectedNodeID ) ) {
					// We'll delete the parent Object because we presume to have created the SOP/merge ourselves.
					// If the parent Object doesn't get deleted, it sticks around unused.
					HAPI_NodeInfo parentNodeInfo = new( ) ;
					if ( session.GetNodeInfo( _connectedNodeID, ref parentNodeInfo )
						 && parentNodeInfo.parentId is not HEU_Defines.HEU_INVALID_NODE_ID )
						session.DeleteNode( parentNodeInfo.parentId ) ;
				}
			}

			_inputObjectsConnectedAssetIDs.Clear( ) ;
			_connectedNodeID = HEU_Defines.HEU_INVALID_NODE_ID ;
		}

		internal bool UploadObjectMergeTransformType( HEU_SessionBase session ) {
			if ( _connectedNodeID is HEU_Defines.HEU_INVALID_NODE_ID )
				return false ;

			int transformType = _keepWorldTransform ? 1 : 0 ;

			// Use _connectedNodeID to find its connections, which should be
			// the object merge nodes. We set the pack parameter on those.
			// Presume that the number of connections to  _connectedNodeID is equal to 
			// size of GetConnectedInputCount() (i.e. the number of inputs)
			int numConnected = GetConnectedInputCount( ) ;
			for ( int i = 0; i < numConnected; ++i ) {
				if ( GetConnectedNodeID( i ) is HEU_Defines.HEU_INVALID_NODE_ID ) continue ;

				if ( session.QueryNodeInput( _connectedNodeID, i, out HAPI_NodeId inputNodeID, false ) )
					session.SetParamIntValue( inputNodeID, HEU_Defines.HAPI_OBJMERGE_TRANSFORM_PARAM, 0,
											  transformType ) ;
			}

			return true ;
		}

		bool UploadObjectMergePackGeometry( HEU_SessionBase session ) {
			if ( _connectedNodeID is HEU_HAPIConstants.HAPI_INVALID_PARM_ID )
				return false ;

			int packEnabled = _packGeometryBeforeMerging ? 1 : 0 ;

			// Use _connectedNodeID to find its connections, which should be
			// the object merge nodes. We set the pack parameter on those.
			// Presume that the number of connections to  _connectedNodeID is equal to 
			// size of GetConnectedInputCount() (i.e. the number of inputs)
			int numConnected = GetConnectedInputCount( ) ;
			for ( int i = 0; i < numConnected; ++i ) {
				if ( GetConnectedNodeID( i ) == HEU_Defines.HEU_INVALID_NODE_ID ) continue ;

				if ( session.QueryNodeInput( _connectedNodeID, i, out HAPI_NodeId inputNodeID, false ) )
					session.SetParamIntValue( inputNodeID, HEU_Defines.HAPI_OBJMERGE_PACK_GEOMETRY, 0, packEnabled ) ;
			}

			return true ;
		}

		// Check if the input node has changed.
		internal bool HasInputNodeTransformChanged( ) {
			bool recursive = HEU_PluginSettings.ChildTransformChangeTriggersCooks ;

			// Only need to check Mesh inputs, since HDA inputs don't upload transform
			if ( GetInternalObjectType( _inputObjectType ) is not InternalObjectType.UNITY_MESH ) return false ;

			foreach ( HEU_InputObjectInfo inputObject in _inputObjects ) {
				if ( !inputObject._gameObject ) continue ;
				if ( inputObject._useTransformOffset ) {
					if ( !HEU_HAPIUtility.IsSameTransform( ref inputObject._syncdTransform,
														   ref inputObject._translateOffset,
														   ref inputObject._rotateOffset,
														   ref inputObject._scaleOffset ) ) return true ;
				}
				else if ( inputObject._gameObject.transform.localToWorldMatrix != inputObject._syncdTransform )
					return true ;

				if ( !recursive ) continue ;
				List< Matrix4x4 > curMatrixTransforms = new( ) ;
				HEU_InputUtility.GetChildrenTransforms( inputObject._gameObject.transform,
														ref curMatrixTransforms ) ;

				if ( curMatrixTransforms.Count != inputObject._syncdChildTransforms.Count )
					return true ;

				int length = curMatrixTransforms.Count ;
				for ( int i = 0; i < length; ++i ) {
					if ( curMatrixTransforms[ i ] != inputObject._syncdChildTransforms[ i ] ) return true ;
				}
			}

			return false ;
		}

		// Upload input object transforms
		internal void UploadInputObjectTransforms( HEU_SessionBase session ) {
			// Only need to upload Mesh inputs, since HDA inputs don't upload transform
			if ( _nodeID is HEU_HAPIConstants.HAPI_INVALID_PARM_ID ||
				 GetInternalObjectType( _inputObjectType ) is not InternalObjectType.UNITY_MESH ) {
				return ;
			}

			int numInputs = GetConnectedInputCount( ) ;
			for ( int i = 0; i < numInputs; ++i ) {
				HAPI_NodeId connectedNodeID = GetConnectedNodeID( i ) ;
				if ( connectedNodeID is not HEU_Defines.HEU_INVALID_NODE_ID
					 && _inputObjects[ i ]._gameObject ) {
					HEU_InputUtility.UploadInputObjectTransform( session,
																 _inputObjects[ i ],
																 connectedNodeID,
																 _keepWorldTransform ) ;
				}
			}
		}

		/// <summary>
		/// Update the input connection based on the fact that the owner asset was recreated
		/// in the given session.
		/// All connections will be invalidated without cleaning up because the IDs can't be trusted.
		/// </summary>
		/// <param name="session"></param>
		internal void UpdateOnAssetRecreation( HEU_SessionBase session ) {
			if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.HDA ) {
				// For HDA inputs, need to recreate the merge node, cook the HDAs, and connect the HDAs to the merge nodes
				// For backwards compatiblity, copy the previous single input asset reference into the new input asset list
				if ( _inputAsset && _inputAssetInfos.Count is 0 ) {
					InternalAddInputHDAAtEnd( _inputAsset ) ;

					// Clear out these deprecated references for forever
					_inputAsset          = null ;
					_connectedInputAsset = null ;
				}

				// Don't delete the merge node ID as its most likely not valid
				_connectedNodeID = HEU_Defines.HEU_INVALID_NODE_ID ;

				int numInputs = _inputAssetInfos.Count ;
				for ( int i = 0; i < numInputs; ++i ) {
					_inputAssetInfos[ i ]._connectedGO          = null ;
					_inputAssetInfos[ i ]._connectedInputNodeID = HEU_Defines.HEU_INVALID_NODE_ID ;
				}
			}
			else if ( GetInternalObjectType( _inputObjectType ) is InternalObjectType.UNITY_MESH ) {
				// For mesh input, invalidate _inputObjectsConnectedAssetIDs and _connectedNodeID as their
				// nodes most likely don't exist, and the IDs will not be correct since this asset got recreated
				// Note that _inputObjects don't need to be cleared as they will be used when recreating the connections.
				_inputObjectsConnectedAssetIDs.Clear( ) ;
				_connectedNodeID = HEU_Defines.HEU_INVALID_NODE_ID ;
			}
		}

		// Helper to copy input values
		internal void CopyInputValuesTo( HEU_SessionBase session, HEU_InputNode destInputNode ) {
			destInputNode._pendingInputObjectType = _inputObjectType ;
			if ( GetInternalObjectType( destInputNode._inputObjectType ) is InternalObjectType.HDA )
				destInputNode.ResetConnectionForForceUpdate( session ) ;
			destInputNode.RemoveAllInputEntries( ) ;

			foreach ( HEU_InputObjectInfo srcInputObject in _inputObjects ) {
				HEU_InputObjectInfo newInputObject = new( ) ;
				srcInputObject.CopyTo( newInputObject ) ;
				destInputNode._inputObjects.Add( newInputObject ) ;
			}

			foreach ( HEU_InputHDAInfo srcInputInfo in _inputAssetInfos ) {
				HEU_InputHDAInfo newInputInfo = new( ) ;
				srcInputInfo.CopyTo( newInputInfo ) ;
				destInputNode._inputAssetInfos.Add( newInputInfo ) ;
			}

			destInputNode._keepWorldTransform        = _keepWorldTransform ;
			destInputNode._packGeometryBeforeMerging = _packGeometryBeforeMerging ;
		}

		internal void LoadPreset( HEU_SessionBase session, HEU_InputPreset inputPreset ) {
			ResetInputNode( session ) ;
			ChangeInputType( session, inputPreset._inputObjectType ) ;

			if ( GetInternalObjectType( inputPreset._inputObjectType ) is InternalObjectType.UNITY_MESH ) {
				int numObjects = inputPreset._inputObjectPresets.Count ;
				for ( int i = 0; i < numObjects; ++i ) {
					bool bSet = false ;

					if ( !string.IsNullOrEmpty( inputPreset._inputObjectPresets[ i ]._gameObjectName ) ) {
						GameObject inputGO ;
						if ( inputPreset._inputObjectPresets[ i ]._isSceneObject ) {
							inputGO =
								HEU_GeneralUtility.GetGameObjectByNameInScene( inputPreset._inputObjectPresets[ i ]
																				   ._gameObjectName ) ;
						}
						else {
							// Use the _gameObjectName as path to find in scene
							inputGO =
								HEU_AssetDatabase.LoadAssetAtPath( inputPreset._inputObjectPresets[ i ]._gameObjectName,
																   typeof( GameObject ) ) as GameObject ;
							if ( inputGO == null ) {
								HEU_Logger.LogErrorFormat( "Unable to find input at {0}",
														   inputPreset._inputObjectPresets[ i ]._gameObjectName ) ;
							}
						}

						if ( inputGO ) {
							HEU_InputObjectInfo inputObject = InternalAddInputObjectAtEnd( inputGO ) ;
							bSet = true ;

							inputObject._useTransformOffset = inputPreset._inputObjectPresets[ i ]._useTransformOffset ;
							inputObject._translateOffset    = inputPreset._inputObjectPresets[ i ]._translateOffset ;
							inputObject._rotateOffset       = inputPreset._inputObjectPresets[ i ]._rotateOffset ;
							inputObject._scaleOffset        = inputPreset._inputObjectPresets[ i ]._scaleOffset ;
						}
						else {
							HEU_Logger
								.LogWarningFormat( "GameObject with name {0} not found. Unable to set input object.",
												   inputPreset._inputAssetName ) ;
						}
					}

					if ( !bSet ) {
						// Add dummy spot (user can replace it manually)
						InternalAddInputObjectAtEnd( null ) ;
					}
				}
			}
			else if ( GetInternalObjectType( inputPreset._inputObjectType ) is InternalObjectType.HDA ) {
				int numInptus = inputPreset._inputAssetPresets.Count ;
				for ( int i = 0; i < numInptus; ++i ) {
					bool bSet = false ;
					if ( !string.IsNullOrEmpty( inputPreset._inputAssetPresets[ i ]._gameObjectName ) )
						bSet = FindAddToInputHDA( inputPreset._inputAssetPresets[ i ]._gameObjectName ) ;

					// Couldn't add for some reason, so just add dummy spot (user can replace it manually)
					if ( !bSet ) InternalAddInputHDAAtEnd( null ) ;
				}

				// Old preset. Add it to input
				if ( numInptus is 0 && !string.IsNullOrEmpty( inputPreset._inputAssetName ) )
					FindAddToInputHDA( inputPreset._inputAssetName ) ;
			}

			KeepWorldTransform        = inputPreset._keepWorldTransform ;
			PackGeometryBeforeMerging = inputPreset._packGeometryBeforeMerging ;
			RequiresUpload            = true ;
			ClearUICache( ) ;
		}

		bool FindAddToInputHDA( string gameObjectName ) {
			HEU_HoudiniAssetRoot inputAssetRoot =
				HEU_GeneralUtility.GetHDAByGameObjectNameInScene( gameObjectName ) ;

			if ( inputAssetRoot is not { _houdiniAsset: not null } ) {
				HEU_Logger.LogWarningFormat( "HDA with GameObject name {0} not found. Unable to set input asset.",
											 gameObjectName ) ;
				return false ;
			}

			// Adding to list will take care of reconnecting
			InternalAddInputHDAAtEnd( inputAssetRoot.gameObject ) ;
			return true ;
		}

		internal void NotifyParentRemovedInput( ) {
			if ( _parentAsset ) _parentAsset.RemoveInputNode( this ) ;
		}

		// UI CACHE ---------------------------------------------------------------------------------------------------

		public HEU_InputNodeUICache _uiCache ;

		internal void ClearUICache( ) => _uiCache = null ;

		/// <summary>
		/// Appends given selectedObjects to the input field.
		/// </summary>
		/// <param name="selectedObjects">Array of GameObjects that should be appended into new input entries</param>
		internal void HandleSelectedObjectsForInputObjects( GameObject[] selectedObjects ) {
			if ( selectedObjects is not { Length: > 0 } ) return ;

			GameObject rootGO = ParentAsset.RootGameObject ;
			foreach ( GameObject selected in selectedObjects ) {
				if ( selected == rootGO ) continue ;
				InternalAddInputObjectAtEnd( selected ) ;
			}

			RequiresUpload = true ;
			if ( HEU_PluginSettings.CookingEnabled && ParentAsset.AutoCookOnParameterChange )
				ParentAsset.RequestCook( bCheckParametersChanged: true,
										 bAsync: true,
										 bSkipCookCheck: false,
										 bUploadParameters: true ) ;
		}

		/// <summary>
		///  Appends given selectedObjects to the input field.
		/// </summary>
		/// <param name="selectedObjects">Array of HDAs that should be appended into new input entries</param>
		internal void HandleSelectedObjectsForInputHDAs( GameObject[] selectedObjects ) {
			if ( selectedObjects is not { Length: > 0 } ) return ;
			GameObject rootGO = ParentAsset.RootGameObject ;

			foreach ( GameObject selected in selectedObjects ) {
				if ( selected == rootGO ) continue ;
				InternalAddInputHDAAtEnd( selected ) ;
			}

			RequiresUpload = true ;
			if ( HEU_PluginSettings.CookingEnabled && ParentAsset.AutoCookOnParameterChange )
				ParentAsset.RequestCook( bCheckParametersChanged: true,
										 bAsync: true,
										 bSkipCookCheck: false,
										 bUploadParameters: true ) ;
		}

		public bool IsEquivalentTo( HEU_InputNode other ) {
			bool bResult = true ;

			string header = "HEU_InputNode" ;

			if ( other == null ) {
				HEU_Logger.LogError( header + " Not equivalent" ) ;
				return false ;
			}

			HEU_TestHelpers.AssertTrueLogEquivalent( _inputNodeType, other._inputNodeType, ref bResult, header,
													 "_inputNodeType" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _pendingInputObjectType, other._pendingInputObjectType,
													 ref bResult, header, "_pendingInputObjectType" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _inputObjects.Count, other._inputObjects.Count, ref bResult,
													 header, "_inputObjects.Count" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _inputObjects, other._inputObjects, ref bResult, header,
													 "_inputObjects" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _inputAssetInfos, other._inputAssetInfos, ref bResult, header,
													 "_inputAssetInfos" ) ;
			//HEU_TestHelpers.AssertTrueLogEquivalent(this._inputIndex, other._inputIndex, ref bResult, header, "_inputIndex");
			//HEU_TestHelpers.AssertTrueLogEquivalent(this._requiresCook, other._requiresCook, ref bResult, header, "_requiresCook");
			//HEU_TestHelpers.AssertTrueLogEquivalent(this._requiresUpload, other._requiresUpload, ref bResult, header, "_requiresUpload");
			HEU_TestHelpers.AssertTrueLogEquivalent( _inputName, other._inputName, ref bResult, header,
													 "_inputName" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _labelName, other._labelName, ref bResult, header,
													 "_labelName" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _paramName, other._paramName, ref bResult, header,
													 "_paramName" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _keepWorldTransform, other._keepWorldTransform, ref bResult,
													 header, "_keepWorldTransform" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _packGeometryBeforeMerging, other.PackGeometryBeforeMerging,
													 ref bResult, header, "_packGeometryBeforeMerging" ) ;

			// Skip conneceted node id
			// Skip _inputObjectsConnectedAssetIds
			// Skip inputAsset/connectedINputAsset
			// Skip parent asset

			return bResult ;
		}

		internal static InternalObjectType GetInternalObjectType( InputObjectType type ) {
			switch ( type ) {
				case InputObjectType.HDA:
				case InputObjectType.CURVE:
					return InternalObjectType.HDA ;
				case InputObjectType.UNITY_MESH:
#if UNITY_2022_1_OR_NEWER
				case InputObjectType.SPLINE:
#endif
				case InputObjectType.TERRAIN:
				case InputObjectType.BOUNDING_BOX:
				case InputObjectType.TILEMAP:
					return InternalObjectType.UNITY_MESH ;
				default:
					return InternalObjectType.UNKNOWN ;
			}
		}

		internal static HEU_InputNodeTypeWrapper InputNodeType_InternalToWrapper(
			InputNodeType inputNodeType ) =>
			inputNodeType switch
			{
				InputNodeType.CONNECTION => HEU_InputNodeTypeWrapper.CONNECTION,
				InputNodeType.NODE       => HEU_InputNodeTypeWrapper.NODE,
				InputNodeType.PARAMETER  => HEU_InputNodeTypeWrapper.PARAMETER,
				_                        => HEU_InputNodeTypeWrapper.CONNECTION
			} ;

		internal static InputNodeType InputNodeType_InternalToWrapper( HEU_InputNodeTypeWrapper inputNodeType ) =>
			inputNodeType switch
			{
				HEU_InputNodeTypeWrapper.CONNECTION => InputNodeType.CONNECTION,
				HEU_InputNodeTypeWrapper.NODE       => InputNodeType.NODE,
				HEU_InputNodeTypeWrapper.PARAMETER  => InputNodeType.PARAMETER,
				_                                   => InputNodeType.CONNECTION
			} ;

		internal static HEU_InputObjectTypeWrapper InputObjectType_InternalToWrapper( InputObjectType inputType ) {
			switch ( inputType ) {
				case InputObjectType.HDA:
					return HEU_InputObjectTypeWrapper.HDA ;
				case InputObjectType.UNITY_MESH:
					return HEU_InputObjectTypeWrapper.UNITY_MESH ;
				case InputObjectType.CURVE:
					return HEU_InputObjectTypeWrapper.CURVE ;
#if UNITY_2022_1_OR_NEWER
				case InputObjectType.SPLINE:
					return HEU_InputObjectTypeWrapper.SPLINE ;
#endif
				case InputObjectType.BOUNDING_BOX:
					return HEU_InputObjectTypeWrapper.BOUNDING_BOX ;
				case InputObjectType.TILEMAP:
					return HEU_InputObjectTypeWrapper.TILEMAP ;
				case InputObjectType.TERRAIN:
				default:
					return HEU_InputObjectTypeWrapper.UNITY_MESH ;
			}
		}

		internal static InputObjectType InputObjectType_WrapperToInternal( HEU_InputObjectTypeWrapper inputType ) {
			switch ( inputType ) {
				case HEU_InputObjectTypeWrapper.HDA:
					return InputObjectType.HDA ;
				case HEU_InputObjectTypeWrapper.UNITY_MESH:
					return InputObjectType.UNITY_MESH ;
				case HEU_InputObjectTypeWrapper.CURVE:
					return InputObjectType.CURVE ;
#if UNITY_2022_1_OR_NEWER
				case HEU_InputObjectTypeWrapper.SPLINE:
					return InputObjectType.SPLINE ;
#endif
				case HEU_InputObjectTypeWrapper.BOUNDING_BOX:
					return InputObjectType.BOUNDING_BOX ;
				case HEU_InputObjectTypeWrapper.TILEMAP:
					return InputObjectType.TILEMAP ;
				case HEU_InputObjectTypeWrapper.TERRAIN:
				default:
					return InputObjectType.UNITY_MESH ;
			}
		}

	}

	// Container for each input object in this node
	[Serializable]
	internal class HEU_InputObjectInfo: IEquivable< HEU_InputObjectInfo > {
		// Gameobject containing mesh
		public GameObject _gameObject ;

		// Hidden variables to serialize UI references
		[HideInInspector] public Terrain _terrainReference ;
		[HideInInspector] public HEU_BoundingVolume _boundingVolumeReference ;
		[HideInInspector] public Tilemap _tilemapReference ;

		// The last upload transform, for diff checks
		public Matrix4x4 _syncdTransform = Matrix4x4.identity ;
		public List< Matrix4x4 > _syncdChildTransforms = new( ) ;

		// Whether to use the transform offset
		[FormerlySerializedAs( "_useTransformOverride" )]
		public bool _useTransformOffset ;

		// Transform offset
		public Vector3 _translateOffset = Vector3.zero ;
		public Vector3 _rotateOffset = Vector3.zero ;
		public Vector3 _scaleOffset = Vector3.one ;
		public Type _inputInterfaceType ;

		public void CopyTo( HEU_InputObjectInfo destObject ) {
			destObject._gameObject              = _gameObject ;
			destObject._terrainReference        = _terrainReference ;
			destObject._boundingVolumeReference = _boundingVolumeReference ;
			destObject._tilemapReference        = _tilemapReference ;
			destObject._syncdTransform          = _syncdTransform ;
			destObject._useTransformOffset      = _useTransformOffset ;
			destObject._translateOffset         = _translateOffset ;
			destObject._rotateOffset            = _rotateOffset ;
			destObject._scaleOffset             = _scaleOffset ;
			destObject._inputInterfaceType      = _inputInterfaceType ;
		}

		internal void SetReferencesFromGameObject( ) {
			if ( _gameObject != null ) {
				_terrainReference        = _gameObject.GetComponent< Terrain >( ) ;
				_tilemapReference        = _gameObject.GetComponent< Tilemap >( ) ;
				_boundingVolumeReference = _gameObject.GetComponent< HEU_BoundingVolume >( ) ;
			}
		}

		public bool IsEquivalentTo( HEU_InputObjectInfo other ) {
			bool   bResult = true ;
			string header  = "HEU_InputObjectInfo" ;

			if ( other is null ) {
				HEU_Logger.LogError( header + " Not equivalent" ) ;
				return false ;
			}

			HEU_TestHelpers.AssertTrueLogEquivalent( _syncdTransform, other._syncdTransform, ref bResult, header,
													 "_syncedTransform" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _useTransformOffset, other._useTransformOffset, ref bResult,
													 header, "_useTransformOffset" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _translateOffset, other._translateOffset, ref bResult, header,
													 "_translateOffset" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _rotateOffset, other._rotateOffset, ref bResult, header,
													 "_rotateOffset" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _scaleOffset, other._scaleOffset, ref bResult, header,
													 "_scaleOffset" ) ;
			// HEU_TestHelpers.AssertTrueLogEquivalent(this._inputInterfaceType, other._inputInterfaceType, ref bResult, header, "_inputInterfaceType");

			return bResult ;
		}
	} ;

	[Serializable]
	internal class HEU_InputHDAInfo: IEquivable< HEU_InputHDAInfo > {
		const string header = "HEU_InputHDAInfo" ;

		// The HDA gameobject that needs to be connected
		public GameObject _pendingGO ;

		// The HDA gameobject that has been connected
		public GameObject _connectedGO ;

		// The ID of the connected HDA
		public HAPI_NodeId _connectedInputNodeID = HEU_Defines.HEU_INVALID_NODE_ID ;

		public HAPI_NodeId _connectedMergeNodeID = HEU_Defines.HEU_INVALID_NODE_ID ;

		public void CopyTo( HEU_InputHDAInfo destInfo ) {
			destInfo._pendingGO   = _pendingGO ;
			destInfo._connectedGO = _connectedGO ;

			destInfo._connectedInputNodeID = HEU_Defines.HEU_INVALID_NODE_ID ;
		}

		public bool IsEquivalentTo( HEU_InputHDAInfo other ) {
			if ( other != null ) return true ;
			HEU_Logger.LogError( header + " Not equivalent" ) ;
			return false ;

			// HEU_TestHelpers.AssertTrueLogEquivalent(this._pendingGO, other._pendingGO, ref bResult, header, "_pendingGO");

			// HEU_TestHelpers.AssertTrueLogEquivalent(this._connectedGO, other._connectedGO, ref bResult, header, "_connectedGO");
		}

	} ;

	// UI cache container
	public class HEU_InputNodeUICache {
#if UNITY_EDITOR
		public SerializedObject _inputNodeSerializedObject ;
		public SerializedProperty _inputObjectTypeProperty ;
		public SerializedProperty _keepWorldTransformProperty ;
		public SerializedProperty _packBeforeMergeProperty ;
		public SerializedProperty _inputObjectsProperty ;
		public SerializedProperty _inputAssetsProperty ;
		public SerializedProperty _meshSettingsProperty ;
		public SerializedProperty _tilemapSettingsProperty ;
		public SerializedProperty _splineSettingsProperty ;
#endif

		public class HEU_InputObjectUICache {
#if UNITY_EDITOR
			public SerializedProperty _gameObjectProperty ;
			public SerializedProperty _transformOffsetProperty ;
			public SerializedProperty _translateProperty ;
			public SerializedProperty _rotateProperty ;
			public SerializedProperty _scaleProperty ;
#endif
		}

		public readonly List< HEU_InputObjectUICache > _inputObjectCache = new( ) ;

		public class HEU_InputAssetUICache {
#if UNITY_EDITOR
			public SerializedProperty _gameObjectProperty ;
#endif
		}

		public readonly List< HEU_InputAssetUICache > _inputAssetCache = new( ) ;
	}
} // HoudiniEngineUnity