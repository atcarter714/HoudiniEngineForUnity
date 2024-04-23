/*
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

// Uncomment to profile
//#define HEU_PROFILER_ON

// Expose internal classes/functions
#if UNITY_EDITOR
using System ;
using System.Collections.Generic ;
using System.Runtime.CompilerServices;
using System.Text ;
using System.Text.RegularExpressions ;
using UnityEngine ;
using Object = UnityEngine.Object ;
using Random = UnityEngine.Random ;

[assembly: InternalsVisibleTo("HoudiniEngineUnityEditor")]
[assembly: InternalsVisibleTo("HoudiniEngineUnityEditorTests")]
[assembly: InternalsVisibleTo("HoudiniEngineUnityPlayModeTests")]
#endif

namespace HoudiniEngineUnity
{
	/////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// Typedefs (copy these from HEU_Common.cs)
	using HAPI_NodeId = Int32 ;
	using HAPI_PartId = Int32 ;


	/// <summary>
	/// Represents a Part object containing mesh / geometry/ attribute data.
	/// </summary>
	public class HEU_PartData: ScriptableObject,
							   IHEU_PartData,
							   IHEU_HoudiniAssetSubcomponent,
							   IEquivable< HEU_PartData > {
		
		// NESTED TYPES ===============================================================================
		/// <summary>Inidicates the type of output for this part.</summary>
		public enum PartOutputType {
			/// <summary>No output type</summary>
			NONE,
			/// <summary>Mesh output</summary>
			MESH,
			/// <summary>Volume output</summary>
			VOLUME,
			/// <summary>Curve output</summary>
			CURVE,
			/// <summary>Instancer output</summary>
			INSTANCER,
		}
		
		
		
		// PUBLIC FIELDS ==============================================================================
		
		/// <inheritdoc />
		public HEU_HoudiniAsset ParentAsset =>
			_geoNode ? _geoNode.ParentAsset : null ;

		/// <inheritdoc />
		public HAPI_PartId PartID => _partID ;

		/// <inheritdoc />
		public string? PartName => _partName ;

		/// <inheritdoc />
		public HAPI_NodeId GeoID => _geoID ;

		/// <inheritdoc />
		public HAPI_PartType PartType => _partType ;

		/// <inheritdoc />
		public HEU_GeoNode ParentGeoNode => _geoNode ;

		/// <inheritdoc />
		public List< HEU_ObjectInstanceInfo > ObjectInstanceInfos => _objectInstanceInfos ;
		
		/// <inheritdoc />
		public HEU_Curve Curve => _curve ;

		/// <inheritdoc />
		public int MeshVertexCount => _meshVertexCount ;

		/// <inheritdoc />
		public HEU_GeneratedOutput GeneratedOutput => _generatedOutput ;

		/// <inheritdoc />
		public GameObject OutputGameObject => _generatedOutput._outputData._gameObject ;

		// ============================================================================================


		//	DATA ------------------------------------------------------------------------------------------------------

		[SerializeField]          HAPI_PartId                    _partID = HEU_Defines.HEU_INVALID_NODE_ID ;
		[SerializeField]          string?                        _partName ;
		[SerializeField]          HAPI_NodeId                    _objectNodeID = HEU_Defines.HEU_INVALID_NODE_ID ;
		[SerializeField]          HAPI_NodeId                    _geoID        = HEU_Defines.HEU_INVALID_NODE_ID ;
		[SerializeField]          HAPI_PartType                  _partType ;
		[SerializeField]          HEU_GeoNode                    _geoNode ;
		[SerializeField]          bool                           _isAttribInstancer ;
		[SerializeField]          bool                           _isPartInstanced ;
		[SerializeField]          int                            _partPointCount ;
		[SerializeField]          bool                           _isObjectInstancer ;
		[SerializeField] internal bool                           _objectInstancesGenerated ;
		[SerializeField]          List< HEU_ObjectInstanceInfo > _objectInstanceInfos ;

		// Store volume position to use when applying transform
		[SerializeField] Vector3 _terrainOffsetPosition ;

#pragma warning disable 0414
		[SerializeField] Object _assetDBTerrainData ;
#pragma warning restore 0414

		[SerializeField] bool                _isPartEditable ;
		[SerializeField] PartOutputType      _partOutputType = PartOutputType.NONE ;
		[SerializeField] HEU_Curve           _curve ;
		[SerializeField] HEU_AttributesStore _attributesStore ;
		[SerializeField] bool                _haveInstancesBeenGenerated ;
		[SerializeField] int                 _meshVertexCount ;
		[SerializeField] HEU_GeneratedOutput _generatedOutput = new( ) ;
		[SerializeField] string?             _volumeLayerName ;

		
		// PUBLIC FUNCTIONS ==========================================================================
		
		/// <inheritdoc />
		public HEU_SessionBase GetSession( ) {
			if ( ParentAsset != null ) {
				return ParentAsset.GetAssetSession( true ) ;
			}

			return HEU_SessionManager.GetOrCreateDefaultSession( ) ;
		}

		/// <inheritdoc />
		public void Recook( ) {
			if ( ParentAsset != null ) ParentAsset.RequestCook( ) ;
		}


		/// <inheritdoc />
		public bool IsPartInstancer( ) => _partType is HAPI_PartType.HAPI_PARTTYPE_INSTANCER ;

		/// <inheritdoc />
		public bool IsAttribInstancer( ) => _isAttribInstancer ;

		/// <inheritdoc />
		public bool IsInstancerAnyType( ) => IsPartInstancer( ) || IsObjectInstancer( ) || IsAttribInstancer( ) ;

		/// <inheritdoc />
		public bool IsPartInstanced( ) => _isPartInstanced ;

		/// <inheritdoc />
		public int GetPartPointCount( ) => _partPointCount ;

		/// <inheritdoc />
		public bool IsObjectInstancer( ) => _isObjectInstancer ;

		/// <inheritdoc />
		public bool IsPartVolume( ) => _partOutputType is PartOutputType.VOLUME ;

		/// <inheritdoc />
		public bool IsPartCurve( ) => _partOutputType is PartOutputType.CURVE ;

		/// <inheritdoc />
		public bool IsPartMesh( ) => _partOutputType is PartOutputType.MESH ;

		/// <inheritdoc />
		public bool IsPartEditable( ) => _isPartEditable ;

		/// <inheritdoc />
		public bool HaveInstancesBeenGenerated( ) => _haveInstancesBeenGenerated ;

		/// <inheritdoc />
		public void SetGameObjectName( string? partName ) {
			if ( !_generatedOutput._outputData._gameObject || !ParentAsset )
				return ;

			string currentName = _generatedOutput._outputData._gameObject.name ;
			if ( !currentName.Equals( partName ) &&
				 ( !currentName.EndsWith( ")" ) || !currentName.StartsWith( partName ) ) ) {
				// Only updating name if not already the same. Otherwise GetUniqueNameForSibling will append an unique identifier which is annoying.
				// Also not updating if current name is not a unique version of partName (ie. with (*) appended). This keeps the previous partName as is.
				partName = HEU_EditorUtility.GetUniqueNameForSibling( ParentAsset.RootGameObject.transform, partName ) ;
				HEU_GeneralUtility.RenameGameObject( _generatedOutput._outputData._gameObject, partName ) ;
			}
		}

		/// <inheritdoc />
		public void SetGameObject( GameObject gameObject ) => _generatedOutput._outputData._gameObject = gameObject ;

		/// <inheritdoc />
		public void SetVolumeLayerName( string? volName ) => _volumeLayerName = volName ;

		/// <inheritdoc />
		public string? GetVolumeLayerName( ) => _volumeLayerName ;

		/// <inheritdoc />
		public void DestroyAllData( bool bIsRebuild = false ) {
			ClearObjectInstanceInfos( ) ;

			if ( _curve ) {
				if ( ParentAsset ) 
					ParentAsset.RemoveCurve( _curve ) ;
				_curve.DestroyAllData( bIsRebuild ) ;
				HEU_GeneralUtility.DestroyImmediate( _curve ) ;
				_curve = null ;
			}

			if ( _attributesStore ) 
				DestroyAttributesStore( ) ;

			if ( _generatedOutput is not null ) 
				HEU_GeneratedOutput.DestroyGeneratedOutput( _generatedOutput ) ;
		}

		/// <inheritdoc />
		public bool IsUsingMaterial( HEU_MaterialData materialData ) => 
			HEU_GeneratedOutput.IsOutputUsingMaterial( materialData._material, _generatedOutput ) ;


		/// <inheritdoc />
		public void GetOutputGameObjects( List< GameObject > outputObjects ) {
			// TODO: check if geotype not HAPI_GeoType.HAPI_GEOTYPE_INTERMEDIATE

			if ( !IsPartInstanced( ) && OutputGameObject != null ) {
				outputObjects.Add( OutputGameObject ) ;
			}
		}

		/// <inheritdoc />
		public void GetOutput( List< HEU_GeneratedOutput > outputs ) {
			if ( _generatedOutput is not null && !IsPartInstanced( ) ) 
				outputs.Add( _generatedOutput ) ;
		}

		/// <inheritdoc />
		public HEU_PartData GetHDAPartWithGameObject( GameObject inGameObject ) =>
													( inGameObject == OutputGameObject ) ? this : null ;


		public void CalculateVisibility( bool bParentVisibility, bool bParentDisplayGeo ) {
			// Editable part is hidden unless parent is a display geo
			bool bIsVisible = !IsPartInstanced( ) && bParentVisibility 
												  && ( !_isPartEditable || bParentDisplayGeo ) ;
			SetVisibility( bIsVisible ) ;
		}

		/// <inheritdoc />
		public void ClearInstances( ) {
			GameObject outputGO = OutputGameObject ;
			if ( !outputGO ) return ;

			List< GameObject > instances = HEU_GeneralUtility.GetInstanceChildObjects( outputGO ) ;
			for ( int i = 0; i < instances.Count; ++i ) {
				HEU_GeneralUtility.DestroyGeneratedComponents( instances[ i ] ) ;
				HEU_GeneralUtility.DestroyImmediate( instances[ i ] ) ;
			}

			_haveInstancesBeenGenerated = false ;
		}

		/// <inheritdoc />
		public HEU_Curve GetCurve( bool bEditableOnly ) {
			if ( _curve != null && ( !bEditableOnly || _curve.IsEditable( ) ) ) {
				return _curve ;
			}

			return null ;
		}

		/// <inheritdoc />
		public void SetVisibility( bool bVisibility ) {
			if ( _curve != null ) {
				bVisibility &= HEU_PluginSettings.Curves_ShowInSceneView ;
			}

			if ( HEU_GeneratedOutput.HasLODGroup( _generatedOutput ) ) {
				foreach ( HEU_GeneratedOutputData childOutput in _generatedOutput._childOutputs ) {
					HEU_GeneralUtility.SetGameObjectRenderVisiblity( childOutput._gameObject, bVisibility ) ;
				}
			}
			else {
				HEU_GeneralUtility.SetGameObjectRenderVisiblity( OutputGameObject, bVisibility ) ;
			}
		}

		/// <inheritdoc />
		public void SetColliderState( bool bEnabled ) {
			HEU_GeneralUtility.SetGameObjectColliderState( OutputGameObject, bEnabled ) ;
		}

		/// <inheritdoc />
		public HEU_ObjectInstanceInfo GetObjectInstanceInfoWithObjectPath( string? path ) {
			int numSourceInfos = _objectInstanceInfos.Count ;
			for ( int i = 0; i < numSourceInfos; ++i ) {
				if ( _objectInstanceInfos[ i ]._instancedObjectPath.Equals( path ) ) {
					return _objectInstanceInfos[ i ] ;
				}
			}

			return null ;
		}

		/// <inheritdoc />
		public HEU_ObjectInstanceInfo GetObjectInstanceInfoWithObjectID( HAPI_NodeId objNodeID ) {
			int numSourceInfos = _objectInstanceInfos.Count ;
			for ( int i = 0; i < numSourceInfos; ++i ) {
				if ( _objectInstanceInfos[ i ]._instancedObjectNodeID == objNodeID ) {
					return _objectInstanceInfos[ i ] ;
				}
			}

			return null ;
		}

		/// <inheritdoc />
		public void SetTerrainOffsetPosition( Vector3 offsetPosition ) => _terrainOffsetPosition = offsetPosition ;

		/// <inheritdoc />
		public void SetTerrainData( TerrainData terrainData, string? exportPathRelative, string? exportPathUser ) {
			if ( ParentAsset is null ) return ;
			
			// Remove the old asset from the AssetDB if its different
			if ( _assetDBTerrainData is not null 
					 && terrainData != _assetDBTerrainData 
						&& HEU_AssetDatabase.ContainsAsset(_assetDBTerrainData) ) {
				HEU_AssetDatabase.DeleteAsset( _assetDBTerrainData ) ;
				_assetDBTerrainData = null ;
			}

			// Add new asset if it doesn't exist in AssetDB
			if ( !HEU_AssetDatabase.ContainsAsset(terrainData) ) {
				if ( string.IsNullOrEmpty(exportPathUser) ) {
					// Save to Working folder
					string? assetPathName = "TerrainData" + HEU_Defines.HEU_EXT_ASSET ;
					ParentAsset.AddToAssetDBCache( assetPathName,
												   terrainData,
												   exportPathRelative,
												   ref _assetDBTerrainData ) ;
				}
				else {
					// Save to user specified path
					string? folderPath = HEU_Platform.GetFolderPath( exportPathUser, true ) ;
					HEU_AssetDatabase.CreatePathWithFolders( folderPath ) ;
					HEU_AssetDatabase.CreateAsset( terrainData, exportPathUser ) ;
				}
			}
			else { _assetDBTerrainData = terrainData ; }
		}

		// ===========================================================================================

		//  LOGIC -----------------------------------------------------------------------------------------------------

		internal void Initialize( HEU_SessionBase session, HAPI_PartId partID, HAPI_NodeId geoID,
								  HAPI_NodeId objectNodeID, HEU_GeoNode geoNode,
								  ref HAPI_PartInfo partInfo, PartOutputType partOutputType,
								  bool isEditable, bool isObjectInstancer, bool isAttribInstancer ) {
			_partID       = partID ;
			_geoID        = geoID ;
			_objectNodeID = objectNodeID ;
			_geoNode      = geoNode ;
			_partOutputType = partOutputType ;
			_partType       = partInfo.type ;


			string? realName = HEU_SessionManager.GetString( partInfo.nameSH, session ) ;
			
			if ( !HEU_PluginSettings.ShortenFolderPaths || realName.Length < 3 )
				_partName  = realName ;
			else _partName = realName[ ..3 ] + GetHashCode( ) ;

			_isPartInstanced   = partInfo.isInstanced ;
			_partPointCount    = partInfo.pointCount ;
			_isPartEditable    = isEditable ;
			_meshVertexCount   = partInfo.vertexCount ;
			_isAttribInstancer = isAttribInstancer ;

			_isObjectInstancer        = isObjectInstancer ;
			_objectInstancesGenerated = false ;
			_objectInstanceInfos      = new( ) ;
			_volumeLayerName = null ;

			_generatedOutput.IsInstancer = IsInstancerAnyType( ) ;

			//HEU_Logger.LogFormat("PartData initialized with ID: {0} and name: {1}", partID, _partName);
		}


		/// <summary>Apply given HAPI transform to this part's gameobject</summary>
		/// <param name="hapiTransform">The HAPI transform to apply</param>
		internal void ApplyHAPITransform( ref HAPI_Transform hapiTransform ) {
			GameObject outputGO = OutputGameObject ;
			if ( !outputGO ) return ;
			
			if ( !IsPartVolume() )
				HEU_HAPIUtility.ApplyLocalTransfromFromHoudiniToUnity( ref hapiTransform, outputGO.transform ) ;
			else {
				HAPI_Transform hapiTransformVolume = new( ) ;
				HEU_GeneralUtility.CopyHAPITransform( ref hapiTransform, ref hapiTransformVolume ) ;

				hapiTransformVolume.position[ 0 ] += _terrainOffsetPosition[ 0 ] ;
				hapiTransformVolume.position[ 1 ] += _terrainOffsetPosition[ 1 ] ;
				hapiTransformVolume.position[ 2 ] += _terrainOffsetPosition[ 2 ] ;

				HEU_HAPIUtility.ApplyLocalTransfromFromHoudiniToUnity( ref hapiTransformVolume, outputGO.transform ) ;
			}
		}
		
		/// <summary>Get debug info for this part</summary>
		internal void GetDebugInfo( StringBuilder sb ) {
			sb.AppendFormat( "PartID: {0}, PartName: {1}, ObjectID: {2}, GeoID: {3}, PartType: {4}, GameObject: {5}\n",
							 PartID, PartName, _objectNodeID, _geoID, _partType, OutputGameObject ) ;
		}
		
		/// <summary>Adds gameobjects that should be cloned when cloning the whole asset.</summary>
		/// <param name="clonableObjects">List of game objects to add to</param>
		internal void GetClonableObjects( List< GameObject > clonableObjects ) {
			// TODO: check if geotype not HAPI_GeoType.HAPI_GEOTYPE_INTERMEDIATE
			if ( OutputGameObject && !IsPartInstanced() ) 
				clonableObjects.Add( OutputGameObject ) ;
		}

		internal void GetClonableParts( List< HEU_PartData > clonableParts ) {
			if ( OutputGameObject && !IsPartInstanced() ) 
				clonableParts.Add( this ) ;
		}


		void SetObjectInstancer( bool bObjectInstancer ) {
			_isObjectInstancer = bObjectInstancer ;
		}


				Debug.Assert( partData.OutputGameObject != null,
							  "Instancer's reference (part) is missing gameobject!" ) ;

		/// <summary>Clear out object instance infos for this part.</summary>
		void ClearObjectInstanceInfos( ) {
			if ( _objectInstanceInfos is not null ) {
				int numObjInstances = _objectInstanceInfos.Count ;
				for ( int i = 0; i < numObjInstances; ++i )
					HEU_GeneralUtility.DestroyImmediate( _objectInstanceInfos[i] ) ;
				
				_objectInstanceInfos.Clear( ) ;
				_objectInstancesGenerated = false ;
			}
		}

		/// <summary>
		/// Clean up and remove any HEU_ObjectInstanceInfos that don't have 
		/// valid parts. This can happen if the object node being instanced
		/// has changed (no parts). The instancer should then clear out 
		/// any created HEU_ObjectInstanceInfos for that object node as otherwise
		/// it leaves a dangling instance input for the user.
		/// </summary>
		internal void ClearInvalidObjectInstanceInfos( ) {
			if ( !ParentAsset || _objectInstanceInfos is null ) return ;
			
			int numObjInstances = _objectInstanceInfos.Count ;
			for ( int i = 0; i < numObjInstances; ++i ) {
				// Presume that if invalid ID then this is using Unity object instead of Houdini generated object
				if ( _objectInstanceInfos[ i ]._instancedObjectNodeID 
									is HEU_Defines.HEU_INVALID_NODE_ID ) continue ;

				bool bDestroyIt = true ;
				if ( ParentAsset.GetObjectWithID(_objectInstanceInfos[ i ]._instancedObjectNodeID) 
													is { } instancedObjNode ) {
					List< HEU_PartData > cloneParts = new( ) ;
					instancedObjNode.GetClonableParts( cloneParts ) ;
					bDestroyIt = cloneParts.Count is 0 ;
				}
				if ( !bDestroyIt ) continue ; //! Not destroying, continue to next ...

				numObjInstances = _destroyObjData( ) ;
				continue ;
				
				//! Local function to destroy object data:
				[MethodImpl( 0x100 | 0x200 )] int _destroyObjData( ) {
					HEU_ObjectInstanceInfo objInstanceInfo = _objectInstanceInfos[ i ] ;
					HEU_GeneralUtility.DestroyImmediate( objInstanceInfo ) ;
					_objectInstanceInfos.RemoveAt( i-- ) ; //! Decrements after evaluation
					numObjInstances = _objectInstanceInfos.Count ;
					return numObjInstances ;
				}
			}
		}

		/// <summary>
		/// Clear generated data for this part.
		/// </summary>
		internal void ClearGeneratedData( ) {
			ClearInstances( ) ;

			// Commented out because we need to keep components around until we parse the cooked data
			// and compare user overrides HEU_GeneralUtility.DestroyGeneratedComponents(_gameObject);

			_objectInstancesGenerated = false ;
		}

		/// <summary>
		/// Clears the generated mesh output for this part.
		/// </summary>
		internal void ClearGeneratedMeshOutput( ) {
			if ( _generatedOutput != null ) {
				HEU_GeneratedOutput.DestroyAllGeneratedColliders( _generatedOutput._outputData ) ;
				HEU_GeneralUtility.DestroyGeneratedMeshMaterialsLODGroups( _generatedOutput._outputData._gameObject,
																		   true ) ;
				HEU_GeneratedOutput.DestroyGeneratedOutputChildren( _generatedOutput ) ;
				HEU_GeneratedOutput.ClearGeneratedMaterialReferences( _generatedOutput._outputData ) ;
				HEU_GeneralUtility.DestroyGeneratedMeshComponents( _generatedOutput._outputData._gameObject ) ;
			}
		}

		internal void ClearGeneratedVolumeOutput( ) {
			if ( _generatedOutput != null ) {
				HEU_GeneralUtility.DestroyTerrainComponents( _generatedOutput._outputData._gameObject ) ;
				_assetDBTerrainData = null ;
			}
		}

		/// <summary>
		/// Generate part instances (packed primvites).
		/// </summary>
		internal bool GeneratePartInstances( HEU_SessionBase session ) {
			if ( ParentAsset == null ) {
				return false ;
			}

			if ( HaveInstancesBeenGenerated( ) ) {
				HEU_Logger.LogWarningFormat( "Part {0} has already had its instances generated!", name ) ;
				return true ;
			}

			HAPI_PartInfo partInfo = new( ) ;
			if ( !session.GetPartInfo( _geoID, _partID, ref partInfo ) ) {
				return false ;
			}

			//HEU_Logger.LogFormat("Instancer: name={0}, instanced={1}, instance count={2}, instance part count={3}",
			//	HEU_SessionManager.GetString(partInfo.nameSH, session), partInfo.isInstanced, partInfo.instanceCount, partInfo.instancedPartCount);

			if ( !IsPartInstancer( ) ) {
				HEU_Logger.LogErrorFormat( "Generate Part Instances called on a non-instancer part {0} for asset {1}!",
										   PartName, ParentAsset.AssetName ) ;
				return false ;
			}

			if ( partInfo.instancedPartCount <= 0 ) {
				HEU_Logger.LogErrorFormat( "Invalid instanced part count: {0} for part {1} of asset {2}",
										   partInfo.instancedPartCount, PartName, ParentAsset.AssetName ) ;
				return false ;
			}

			// Get the instance node IDs to get the geometry to be instanced.
			// Get the instanced count to all the instances. These will end up being mesh references to the mesh from instance node IDs.

			Transform partTransform = OutputGameObject.transform ;

			// Get each instance's transform
			HAPI_Transform[] instanceTransforms = new HAPI_Transform[ partInfo.instanceCount ] ;
			if ( !HEU_GeneralUtility.GetArray3Arg( _geoID, PartID, HAPI_RSTOrder.HAPI_SRT,
												   session.GetInstancerPartTransforms, instanceTransforms, 0,
												   partInfo.instanceCount ) ) {
				return false ;
			}

			// Get part IDs for the parts being instanced
			HAPI_NodeId[] instanceNodeIDs = new HAPI_NodeId[ partInfo.instancedPartCount ] ;
			if ( !HEU_GeneralUtility.GetArray2Arg( _geoID, PartID, session.GetInstancedPartIds, instanceNodeIDs, 0,
												   partInfo.instancedPartCount ) ) {
				return false ;
			}

			// Get instance names if set
			string?[]           instancePrefixes       = null ;
			HAPI_AttributeInfo instancePrefixAttrInfo = new( ) ;
			HEU_GeneralUtility.GetAttributeInfo( session, _geoID, PartID, HEU_Defines.DEFAULT_INSTANCE_PREFIX_ATTR,
												 ref instancePrefixAttrInfo ) ;
			if ( instancePrefixAttrInfo.exists ) {
				instancePrefixes = HEU_GeneralUtility.GetAttributeStringData( session, _geoID, PartID,
																			  HEU_Defines.DEFAULT_INSTANCE_PREFIX_ATTR,
																			  ref instancePrefixAttrInfo ) ;
			}

			int numInstances = instanceNodeIDs.Length ;
			for ( int i = 0; i < numInstances; ++i ) {
				HEU_PartData partData = _geoNode.GetPartFromPartID( instanceNodeIDs[ i ] ) ;
				if ( partData == null ) {
					if ( !_geoNode.ObjectNode._recentlyDestroyedParts.Contains( instanceNodeIDs[ i ] ) ) {
						HEU_Logger.LogWarningFormat( "Part with id {0} is missing. Unable to generate instance!",
													 instanceNodeIDs[ i ] ) ;
					}

					return false ;
				}

				// If the part we're instancing is itself an instancer, make sure it has generated its instances
				if ( partData.IsPartInstancer( ) && !partData.HaveInstancesBeenGenerated( ) ) {
					bool result = partData.GeneratePartInstances( session ) ;
					if ( !result ) {
						return false ;
					}
				}

				Debug.Assert( partData.OutputGameObject != null,
							  "Instancer's reference (part) is missing gameobject!" ) ;

				HAPI_PartInfo instancePartInfo = new( ) ;
				session.GetPartInfo( _geoID, instanceNodeIDs[ i ], ref instancePartInfo ) ;

				int numTransforms = instanceTransforms.Length ;
				for ( int j = 0; j < numTransforms; ++j ) {
					GameObject newInstanceGO =
						HEU_EditorUtility.InstantiateGameObject( partData.OutputGameObject, partTransform, false,
																 false ) ;

					HEU_GeneralUtility.RenameGameObject( newInstanceGO,
														 HEU_GeometryUtility.GetInstanceOutputName( PartName,
															 instancePrefixes, ( j + 1 ) ) ) ;

					HEU_GeneralUtility.CopyFlags( OutputGameObject, newInstanceGO, true ) ;

					HEU_HAPIUtility.ApplyLocalTransfromFromHoudiniToUnityForInstance( ref instanceTransforms[ j ],
						newInstanceGO.transform ) ;

					// When cloning, the instanced part might have been made invisible, so re-enable renderer to have the cloned instance display it.
					HEU_GeneralUtility.SetGameObjectRenderVisiblity( newInstanceGO, true ) ;
					HEU_GeneralUtility.SetGameObjectChildrenRenderVisibility( newInstanceGO, true ) ;
					HEU_GeneralUtility.SetGameObjectColliderState( newInstanceGO, true ) ;
					HEU_GeneralUtility.SetGameObjectChildrenColliderState( newInstanceGO, true ) ;
				}
			}

			_haveInstancesBeenGenerated = true ;

			return true ;
		}

		/// <summary>
		/// Generate instances from given Houdini Engine object node ID
		/// </summary>
		/// <param name="session">Active session to use</param>
		/// <param name="objectNodeID">The source object node ID to create instances from</param>
		/// <param name="instancePrefixes">Array of instance names to use</param>
		/// <param name="instanceMaterialPaths"></param>
		internal void GenerateInstancesFromObjectID( HEU_SessionBase session, HAPI_NodeId objectNodeID,
													 string?[] instancePrefixes, string?[] instanceMaterialPaths ) {
			int numInstances = GetPartPointCount( ) ;
			if ( numInstances < 1 ) return ;
			
			Transform partTransform = OutputGameObject.transform ;
			Transform[ ] instanceToChildTransform = null ;
			
			bool bUseSplitAttr =
				ComposeUnityInstanceSplitHierarchy( session, _geoID, _partID,
													partTransform, numInstances,
													ref instanceToChildTransform ) ;

			HEU_ObjectInstanceInfo instanceInfo = GetObjectInstanceInfoWithObjectID( objectNodeID ) ;
			if ( instanceInfo && instanceInfo._instancedInputs.Count > 0 ) {
				List< HEU_InstancedInput > validInstancedGameObjects = instanceInfo._instancedInputs ;
				int instancedObjCount = validInstancedGameObjects.Count ;
				
				SetObjectInstancer( true ) ;
				_objectInstancesGenerated = true ;

				HAPI_Transform[ ] instanceTransforms = new HAPI_Transform[ numInstances ] ;
				if ( HEU_GeneralUtility.GetArray3Arg( _geoID, _partID,
													  HAPI_RSTOrder.HAPI_SRT,
													  session.GetInstanceTransformsOnPart,
													  instanceTransforms, 0, numInstances ) ) {
					int numTransforms = instanceTransforms.Length ;
					for ( int j = 0; j < numTransforms; ++j ) {
						int randomIndex = Random.Range( 0, instancedObjCount ) ;
						Transform instanceParentTransform = partTransform ;
						
						if ( bUseSplitAttr 
								 && instanceToChildTransform is not null 
									&& j < instanceToChildTransform.Length )
							instanceParentTransform = instanceToChildTransform[ j ] ;
						
						CreateNewInstanceFromObject( validInstancedGameObjects[ randomIndex ]._instancedGameObject, j,
													 instanceParentTransform,
													 ref instanceTransforms[ j ], objectNodeID, null,
													 validInstancedGameObjects[ randomIndex ]._rotationOffset,
													 validInstancedGameObjects[ randomIndex ]._scaleOffset,
													 instancePrefixes, instanceMaterialPaths, null ) ;
					}
				}
			}
			else {
				HEU_ObjectNode instancedObjNode = ParentAsset.GetObjectWithID( objectNodeID ) ;
				
				if ( instancedObjNode )
					GenerateInstancesFromObject( session, instancedObjNode,
												 instancePrefixes, instanceMaterialPaths ) ;
				else {
					HEU_Logger
						.LogWarningFormat( "Instanced object with ID {0} not found. Unable to generate instances!",
										   objectNodeID ) ;
				}
			}
		}

		/// <summary>Generate instances from another object node (sourceObject).</summary>
		/// <param name="session"></param>
		/// <param name="sourceObject">The object node to create instances from.</param>
		/// <param name="instancePrefixes"></param>
		/// <param name="instanceMaterialPaths"></param>
		internal void GenerateInstancesFromObject( HEU_SessionBase session,          HEU_ObjectNode sourceObject,
												   string?[]       instancePrefixes, string?[] instanceMaterialPaths ) {
			// Create instance of this object for all points
			List< HEU_PartData > clonableParts = new( ) ;
			sourceObject.GetClonableParts( clonableParts ) ;

			int numInstances = GetPartPointCount( ) ;
			if ( numInstances <= 0 ) return ;

			SetObjectInstancer( true ) ;
			_objectInstancesGenerated = true ;
			Transform partTransform = OutputGameObject.transform ;

			Transform[ ] instanceToChildTransform = null ;
			bool bUseSplitAttr =
				ComposeUnityInstanceSplitHierarchy( session, _geoID, _partID, 
													partTransform, numInstances,
													ref instanceToChildTransform ) ;

			HAPI_Transform[ ] instanceTransforms = new HAPI_Transform[ numInstances ] ;
			
			if ( !HEU_GeneralUtility.GetArray3Arg( _geoID, _partID, 
												   HAPI_RSTOrder.HAPI_SRT,
												   session.GetInstanceTransformsOnPart, 
												   instanceTransforms, 0, numInstances ) ) return ;
			
			int numInstancesCreated = 0 ;
			int numTransforms = instanceTransforms.Length ;
			for ( int j = 0; j < numTransforms; ++j ) {
				int numClones = clonableParts.Count ;
				for ( int c = 0; c < numClones; ++c ) {

					Transform instanceParentTransform = partTransform ;
					if ( bUseSplitAttr && instanceToChildTransform != null &&
						 numInstancesCreated < instanceToChildTransform.Length ) {
						instanceParentTransform = instanceToChildTransform[ numInstancesCreated ] ;
					}

					CreateNewInstanceFromObject( clonableParts[ c ].OutputGameObject, numInstancesCreated,
												 instanceParentTransform, ref instanceTransforms[ j ],
												 sourceObject.ObjectID, null, Vector3.zero, Vector3.one,
												 instancePrefixes, instanceMaterialPaths, null ) ;
					numInstancesCreated++ ;
				}
			}
		}

		/// <summary>Generate instances from object IDs found in the asset.</summary>
		/// <param name="session"></param>
		/// <param name="instancePrefixes"></param>
		/// <param name="instanceMaterialPaths"></param>
		internal void GenerateInstancesFromObjectIds( HEU_SessionBase session, 
													  string?[]       instancePrefixes,
													  string?[]       instanceMaterialPaths ) {
			if ( ParentAsset == null ) {
				return ;
			}

			int numInstances = GetPartPointCount( ) ;
			if ( numInstances <= 0 ) {
				return ;
			}

			HAPI_NodeId[] instancedNodeIds = new HAPI_NodeId[ numInstances ] ;
			if ( !HEU_GeneralUtility.GetArray1Arg( _geoID, session.GetInstancedObjectIds, instancedNodeIds, 0,
												   numInstances ) ) {
				return ;
			}

			HAPI_Transform[] instanceTransforms = new HAPI_Transform[ numInstances ] ;
			if ( !HEU_GeneralUtility.GetArray3Arg( _geoID, _partID, HAPI_RSTOrder.HAPI_SRT,
												   session.GetInstanceTransformsOnPart, instanceTransforms, 0,
												   numInstances ) ) {
				return ;
			}

			SetObjectInstancer( true ) ;
			_objectInstancesGenerated = true ;

			Transform partTransform = OutputGameObject.transform ;

			Transform[] instanceToChildTransform = null ;
			bool bUseSplitAttr =
				ComposeUnityInstanceSplitHierarchy( session, _geoID, _partID, partTransform, numInstances,
													ref instanceToChildTransform ) ;

			for ( int i = 0; i < numInstances; ++i ) {
				if ( instancedNodeIds[ i ] == HEU_Defines.HEU_INVALID_NODE_ID ) {
					// Skipping points without valid instanced IDs
					continue ;
				}

				Transform instanceParentTransform = partTransform ;

				if ( bUseSplitAttr && instanceToChildTransform != null && i < instanceToChildTransform.Length ) {
					instanceParentTransform = instanceToChildTransform[ i ] ;
				}

				HEU_ObjectInstanceInfo instanceInfo = GetObjectInstanceInfoWithObjectID( instancedNodeIds[ i ] ) ;
				if ( instanceInfo != null && ( instanceInfo._instancedInputs.Count > 0 ) ) {
					List< HEU_InstancedInput > validInstancedGameObjects = instanceInfo._instancedInputs ;
					int randomIndex = Random.Range( 0, validInstancedGameObjects.Count ) ;

					CreateNewInstanceFromObject( validInstancedGameObjects[ randomIndex ]._instancedGameObject, i,
												 instanceParentTransform, ref instanceTransforms[ i ],
												 instanceInfo._instancedObjectNodeID, null,
												 validInstancedGameObjects[ randomIndex ]._rotationOffset,
												 validInstancedGameObjects[ randomIndex ]._scaleOffset,
												 instancePrefixes, instanceMaterialPaths, null ) ;
				}
				else {
					HEU_ObjectNode instancedObjNode = ParentAsset.GetObjectWithID( instancedNodeIds[ i ] ) ;
					if ( instancedObjNode == null ) {
						HEU_Logger.LogErrorFormat( "Object with ID {0} not found for instancing!",
												   instancedNodeIds[ i ] ) ;
						continue ;
					}

					List< HEU_PartData > cloneParts = new( ) ;
					instancedObjNode.GetClonableParts( cloneParts ) ;

					int numClones = cloneParts.Count ;
					for ( int c = 0; c < numClones; ++c ) {
						CreateNewInstanceFromObject( cloneParts[ c ].OutputGameObject, i, instanceParentTransform,
													 ref instanceTransforms[ i ],
													 instancedObjNode.ObjectID, null, Vector3.zero, Vector3.one,
													 instancePrefixes, instanceMaterialPaths, null ) ;
					}
				}
			}
		}

		/// <summary>Generate instances from Unity objects specified via attributes.</summary>
		/// <param name="session"></param>
		/// <param name="unityInstanceAttr">Name of the attribute to get the Unity path from.</param>
		internal void GenerateInstancesFromUnityAssetPathAttribute( HEU_SessionBase session, string? unityInstanceAttr ) {
			if ( ParentAsset == null ) {
				return ;
			}

			if ( !IsAttribInstancer( ) ) {
				return ;
			}

			int numInstances = GetPartPointCount( ) ;
			if ( numInstances <= 0 ) {
				return ;
			}

			// Get the part-specific instance transforms
			HAPI_Transform[] instanceTransforms = new HAPI_Transform[ numInstances ] ;
			if ( !HEU_GeneralUtility.GetArray3Arg( _geoID, _partID, HAPI_RSTOrder.HAPI_SRT,
												   session.GetInstanceTransformsOnPart, instanceTransforms, 0,
												   numInstances ) ) {
				return ;
			}

			HAPI_AttributeInfo instanceAttrInfo = new( ) ;
			int[]              instanceAttrID   = new int[ 0 ] ;
			HEU_GeneralUtility.GetAttribute( session, _geoID, _partID, unityInstanceAttr, ref instanceAttrInfo,
											 ref instanceAttrID, session.GetAttributeStringData ) ;

			string?[] instancePathAttrValues = HEU_SessionManager.GetStringValuesFromStringIndices( instanceAttrID ) ;

			if ( instanceAttrInfo.owner == HAPI_AttributeOwner.HAPI_ATTROWNER_DETAIL &&
				 instancePathAttrValues.Length > 0 ) {
				string? path = instancePathAttrValues[ 0 ] ;
				instancePathAttrValues = new string[ numInstances ] ;
				for ( int i = 0; i < numInstances; i++ ) {
					instancePathAttrValues[ i ] = path ;
				}
			}

			Debug.AssertFormat( instanceAttrInfo.owner == HAPI_AttributeOwner.HAPI_ATTROWNER_POINT || instanceAttrInfo.owner == HAPI_AttributeOwner.HAPI_ATTROWNER_DETAIL,
								"Expected to parse {0} owner attribute but got {1} instead!",
								HAPI_AttributeOwner.HAPI_ATTROWNER_POINT, instanceAttrInfo.owner ) ;
			Debug.AssertFormat( instancePathAttrValues.Length == numInstances,
								"Number of instances {0} does not match point attribute count {1} for part {2} of asset {3}",
								numInstances, instancePathAttrValues.Length, PartName, ParentAsset.AssetName ) ;

			string?[]           instancePrefixes       = null ;
			HAPI_AttributeInfo instancePrefixAttrInfo = new( ) ;
			HEU_GeneralUtility.GetAttributeInfo( session, _geoID, _partID, HEU_Defines.DEFAULT_INSTANCE_PREFIX_ATTR,
												 ref instancePrefixAttrInfo ) ;
			if ( instancePrefixAttrInfo.exists ) {
				instancePrefixes = HEU_GeneralUtility.GetAttributeStringData( session, _geoID, _partID,
																			  HEU_Defines.DEFAULT_INSTANCE_PREFIX_ATTR,
																			  ref instancePrefixAttrInfo ) ;
			}

			string?[]           collisionAssetPaths  = null ;
			HAPI_AttributeInfo collisionGeoAttrInfo = new( ) ;
			HEU_GeneralUtility.GetAttributeInfo( session, _geoID, _partID, HEU_PluginSettings.CollisionGroupName,
												 ref collisionGeoAttrInfo ) ;
			if ( collisionGeoAttrInfo.owner == HAPI_AttributeOwner.HAPI_ATTROWNER_POINT
				 || collisionGeoAttrInfo.owner == HAPI_AttributeOwner.HAPI_ATTROWNER_DETAIL ) {
				collisionAssetPaths =
					HEU_GeneralUtility.GetAttributeStringData( session, _geoID, _partID,
															   HEU_PluginSettings.CollisionGroupName,
															   ref collisionGeoAttrInfo ) ;
			}

			GameObject singleCollisionGO = null ;
			if ( collisionAssetPaths is { Length: 1 } &&
				 !string.IsNullOrEmpty( collisionAssetPaths[ 0 ] ) ) {
				// Single collision override
				HEU_AssetDatabase.ImportAsset( collisionAssetPaths[ 0 ],
											   HEU_AssetDatabase.HEU_ImportAssetOptions.Default ) ;
				singleCollisionGO =
					HEU_AssetDatabase.LoadAssetAtPath( collisionAssetPaths[ 0 ], typeof( GameObject ) ) as GameObject ;

				if ( singleCollisionGO == null ) {
					// Continue on but log error
					HEU_Logger.LogErrorFormat( "Collision asset at path {0} not found for instance.",
											   collisionAssetPaths[ 0 ] ) ;
				}
			}

			HAPI_AttributeInfo useUnityInstanceFlagsInfo = new( ) ;
			int[]              useUnityInstanceFlags     = new int[ 0 ] ;
			bool               copyParentFlags           = true ;
			HEU_GeneralUtility.GetAttribute( session, _geoID, _partID, HEU_Defines.UNITY_USE_INSTANCE_FLAGS_ATTR,
											 ref useUnityInstanceFlagsInfo, ref useUnityInstanceFlags,
											 session.GetAttributeIntData ) ;
			if ( useUnityInstanceFlagsInfo.exists && useUnityInstanceFlags.Length > 0 &&
				 useUnityInstanceFlags[ 0 ] == 1 ) {
				copyParentFlags = false ;
			}

			string?[]           instanceMaterialPaths = null ;
			HAPI_AttributeInfo materialAttrInfo      = new( ) ;
			HEU_GeneralUtility.GetAttributeInfo( session, _geoID, _partID, HEU_PluginSettings.UnityMaterialAttribName,
												 ref materialAttrInfo ) ;
			if ( materialAttrInfo.exists ) {
				instanceMaterialPaths =
					HEU_GeneralUtility.GetAttributeStringData( session, _geoID, _partID,
															   HEU_PluginSettings.UnityMaterialAttribName,
															   ref materialAttrInfo ) ;
			}

			SetObjectInstancer( true ) ;
			_objectInstancesGenerated = true ;

			Transform partTransform = OutputGameObject.transform ;

			Transform[] instanceToChildTransform = null ;
			bool bUseSplitAttr =
				ComposeUnityInstanceSplitHierarchy( session, _geoID, _partID, partTransform, numInstances,
													ref instanceToChildTransform ) ;

			// Keep track of loaded objects so we only need to load once for each object
			Dictionary< string?, GameObject > loadedUnityObjectMap = new( ) ;

			// Keep track of loaded collision assets
			Dictionary< string?, GameObject > loadedCollisionObjectMap = new( ) ;

			// Temporary empty gameobject in case where specified Unity object is not found
			GameObject tempGO = null ;

			for ( int i = 0; i < numInstances; ++i ) {
				GameObject unitySrcGO = null ;

				Vector3 rotationOffset = Vector3.zero ;
				Vector3 scaleOffset    = Vector3.one ;

				HEU_ObjectInstanceInfo instanceInfo =
					GetObjectInstanceInfoWithObjectPath( instancePathAttrValues[ i ] ) ;
				if ( instanceInfo != null && ( instanceInfo._instancedInputs.Count > 0 ) ) {
					List< HEU_InstancedInput > validInstancedGameObjects = instanceInfo._instancedInputs ;
					int randomIndex = Random.Range( 0, validInstancedGameObjects.Count ) ;

					unitySrcGO     = validInstancedGameObjects[ randomIndex ]._instancedGameObject ;
					rotationOffset = validInstancedGameObjects[ randomIndex ]._rotationOffset ;
					scaleOffset    = validInstancedGameObjects[ randomIndex ]._scaleOffset ;
				}

				if ( unitySrcGO == null ) {
					if ( string.IsNullOrEmpty( instancePathAttrValues[ i ] ) ) {
						continue ;
					}

					if ( !loadedUnityObjectMap.TryGetValue( instancePathAttrValues[ i ], out unitySrcGO ) ) {
						unitySrcGO = HEU_GeneralUtility.GetPrefabFromPath( instancePathAttrValues[ i ] ) ;

						if ( unitySrcGO == null ) {
							HEU_Logger.LogErrorFormat( "Unable to load asset at {0} for instancing!",
													   instancePathAttrValues[ i ] ) ;

							// Even though the source Unity object is not found, we should create an object instance info so
							// that it will be exposed in UI and user can override
							if ( tempGO == null ) {
								tempGO = HEU_GeneralUtility.CreateNewGameObject( ) ;
							}

							unitySrcGO = tempGO ;
						}

						// Adding to map even if not found so we don't flood the log with the same error message
						loadedUnityObjectMap.Add( instancePathAttrValues[ i ], unitySrcGO ) ;
					}
				}

				GameObject collisionSrcGO = null ;
				if ( singleCollisionGO != null ) {
					// Single collision geo
					collisionSrcGO = singleCollisionGO ;
				}
				else if ( collisionAssetPaths != null
						  && ( i < collisionAssetPaths.Length )
						  && !string.IsNullOrEmpty( collisionAssetPaths[ i ] ) ) {
					// Mutliple collision geo (one per instance).
					if ( !loadedCollisionObjectMap.TryGetValue( collisionAssetPaths[ i ], out collisionSrcGO ) ) {
						collisionSrcGO =
							HEU_AssetDatabase.LoadAssetAtPath( collisionAssetPaths[ i ], typeof( GameObject ) ) as
								GameObject ;
						if ( collisionSrcGO == null ) {
							HEU_Logger.LogErrorFormat( "Unable to load collision asset at {0} for instancing!",
													   collisionAssetPaths[ i ] ) ;
						}
						else {
							loadedCollisionObjectMap.Add( collisionAssetPaths[ i ], collisionSrcGO ) ;
						}
					}
				}

				Transform instanceParentTransform = partTransform ;

				if ( bUseSplitAttr && instanceToChildTransform != null && i < instanceToChildTransform.Length ) {
					instanceParentTransform = instanceToChildTransform[ i ] ;
				}

				CreateNewInstanceFromObject( unitySrcGO, i, instanceParentTransform, ref instanceTransforms[ i ],
											 HEU_Defines.HEU_INVALID_NODE_ID, instancePathAttrValues[ i ],
											 rotationOffset, scaleOffset, instancePrefixes, instanceMaterialPaths,
											 collisionSrcGO, copyParentFlags: copyParentFlags ) ;
			}

			if ( tempGO != null ) {
				HEU_GeneralUtility.DestroyImmediate( tempGO, bRegisterUndo: false ) ;
			}
		}

		/// <summary>Create a new instance of the sourceObject.</summary>
		/// <param name="sourceObject">GameObject to instance.</param>
		/// <param name="instanceIndex">Index of the instance within the part.</param>
		/// <param name="parentTransform">Parent of the new instance.</param>
		/// <param name="hapiTransform">HAPI transform to apply to the new instance.</param>
		/// <param name="instancedObjectPath"></param>
		/// <param name="rotationOffset"></param>
		/// <param name="scaleOffset"></param>
		/// <param name="instancePrefixes"></param>
		/// <param name="instanceMaterialPaths"></param>
		/// <param name="collisionSrcGO"></param>
		/// <param name="instancedObjectNodeID"></param>
		/// <param name="copyParentFlags"></param>
		void CreateNewInstanceFromObject( GameObject sourceObject, int instanceIndex, Transform parentTransform,
										  ref HAPI_Transform hapiTransform, HAPI_NodeId instancedObjectNodeID, string? instancedObjectPath,
										  Vector3 rotationOffset, Vector3  scaleOffset,
										  string?[] instancePrefixes, string?[] instanceMaterialPaths,
										  GameObject collisionSrcGO, bool copyParentFlags = true ) {
			GameObject newInstanceGO ;
			if ( HEU_EditorUtility.IsPrefabAsset(sourceObject) ) {
				newInstanceGO = HEU_EditorUtility.InstantiatePrefab( sourceObject ) as GameObject ;
				if( newInstanceGO )
					newInstanceGO.transform.parent = parentTransform ;
			}
			else {
				newInstanceGO = HEU_EditorUtility.InstantiateGameObject( sourceObject, parentTransform, false, false ) ;
			}

			if ( collisionSrcGO ) 
				HEU_GeneralUtility.ReplaceColliderMeshFromMeshFilter( newInstanceGO, collisionSrcGO ) ;

			// To get the instance output name, we pass in the instance index. The actual name will be +1 from this.
			HEU_GeneralUtility.RenameGameObject( newInstanceGO,
												 HEU_GeometryUtility.GetInstanceOutputName( PartName, instancePrefixes,
													 instanceIndex + 1 ) ) ;

			if ( copyParentFlags )
				HEU_GeneralUtility.CopyFlags( OutputGameObject, newInstanceGO, true ) ;

			if ( newInstanceGO ) {
				Transform instanceTransform = newInstanceGO.transform ;
				HEU_HAPIUtility.ApplyLocalTransfromFromHoudiniToUnityForInstance( ref hapiTransform, instanceTransform ) ;

				// Apply offsets
				instanceTransform.localRotation = Quaternion.Euler( rotationOffset ) * instanceTransform.localRotation ;
				instanceTransform.localScale    = Vector3.Scale( instanceTransform.localScale, scaleOffset ) ;
			}

			// When cloning, the instanced part might have been made invisible, so re-enable renderer to have the cloned instance display it.
			HEU_GeneralUtility.SetGameObjectRenderVisiblity( newInstanceGO, true ) ;
			HEU_GeneralUtility.SetGameObjectChildrenRenderVisibility( newInstanceGO, true ) ;
			HEU_GeneralUtility.SetGameObjectColliderState( newInstanceGO, true ) ;
			HEU_GeneralUtility.SetGameObjectChildrenColliderState( newInstanceGO, true ) ;

			// Add to object instance info map. Find existing object instance info, or create it.
			HEU_ObjectInstanceInfo instanceInfo = null ;
			if ( instancedObjectNodeID is not HEU_Defines.HEU_INVALID_NODE_ID )
				instanceInfo = GetObjectInstanceInfoWithObjectID( instancedObjectNodeID ) ;
			
			else if ( !string.IsNullOrEmpty(instancedObjectPath) ) 
				instanceInfo = GetObjectInstanceInfoWithObjectPath( instancedObjectPath ) ;

			if ( !instanceInfo ) 
				instanceInfo = CreateObjectInstanceInfo( sourceObject, instancedObjectNodeID, instancedObjectPath ) ;

			if ( instanceInfo && instanceMaterialPaths is not null
							  && instanceIndex < instanceMaterialPaths.Length ) {
				string?   materialPath     = instanceMaterialPaths[ instanceIndex ] ;
				Material instanceMaterial = HEU_MaterialFactory.LoadUnityMaterial( materialPath ) ;
				if ( instanceMaterial ) {
					// TODO: Support material overrides
					if ( newInstanceGO ) {
						MeshRenderer meshRenderer = newInstanceGO.GetComponent< MeshRenderer >( ) ;
						
						// We only support materials on instances if the prefab is a MeshRenderer and it only has one material slot
						if ( meshRenderer && meshRenderer.sharedMaterials.Length <= 1 ) 
							meshRenderer.sharedMaterial = instanceMaterial ;
					}
				}
			}

			instanceInfo._instances.Add( newInstanceGO ) ;
		}
		
		internal void GenerateAttributesStore( HEU_SessionBase session ) {
			if ( !OutputGameObject ) return ;

			HEU_GeneralUtility.UpdateGeneratedAttributeStore( session,
															  _geoID, PartID,
															  OutputGameObject
															) ;
		}

		internal void CalculateColliderState( ) {
			// Using visiblity to figure out collider state, for now
			bool bEnabled = true ;

			if ( HEU_GeneratedOutput.HasLODGroup( _generatedOutput ) ) {
				foreach ( HEU_GeneratedOutputData childOutput in _generatedOutput._childOutputs ) {
					MeshRenderer partMeshRenderer = childOutput._gameObject.GetComponent< MeshRenderer >( ) ;
					bEnabled = partMeshRenderer && partMeshRenderer.enabled ;
					HEU_GeneralUtility.SetGameObjectColliderState( childOutput._gameObject, bEnabled ) ;
				}
			}
			else {
				if ( !OutputGameObject ) return ;
				
				MeshRenderer partMeshRenderer = 
					OutputGameObject.GetComponent< MeshRenderer >( ) ;
				
				if ( partMeshRenderer )
					bEnabled = partMeshRenderer.enabled ;
				
				HEU_GeneralUtility.SetGameObjectColliderState( OutputGameObject, bEnabled ) ;
			}
		}


		/// <summary>Copy relevant components from sourceGO to targetGO.</summary>
		/// <param name="partData">Part data that we're looking at.</param>
		/// <param name="sourceGO">Source gameobject to copy from.</param>
		/// <param name="targetGO">Target gameobject to copy to.</param>
		/// <param name="assetName">Name of the asset.</param>
		/// <param name="sourceToTargetMeshMap">Map of existing meshes to newly created meshes. This helps keep track of shared meshes that should be copied but still shared in new asset.</param>
		/// <param name="sourceToCopiedMaterials">Map of existing materials with their new copied counterparts. Keeps track of which materials have been newly copied in order to reuse.</param>
		/// <param name="bWriteMeshesToAssetDatabase">Whether to store meshes to database. Required for prefabs.</param>
		/// <param name="bakedAssetPath">Path to asset's database cache. Could be null in which case it will be filled.</param>
		/// <param name="assetDBObject">The asset database object to write out the persistent mesh data to. Could be null, in which case it might be created.</param>
		/// <param name="assetObjectFileName">File name of the asset database object. This will be used to create new assetDBObject.</param>
		/// <param name="bDeleteExistingComponents"></param>
		/// <param name="bDontDeletePersistantResources"></param>
		/// <param name="lodTransformValues"> Data to sets the local transform of LOD after copy. Set to null if default </param>
		static void CopyGameObjectComponents( HEU_PartData partData, GameObject sourceGO, GameObject targetGO,
											  string? assetName, Dictionary< Mesh, Mesh > sourceToTargetMeshMap,
											  Dictionary< Material, Material > sourceToCopiedMaterials,
											  bool bWriteMeshesToAssetDatabase,
											  ref string? bakedAssetPath, ref Object assetDBObject,
											  string? assetObjectFileName, bool bDeleteExistingComponents,
											  bool bDontDeletePersistantResources,
											  List< TransformData > lodTransformValues ) {
			// Copy mesh, collider, material, and textures into its own directory in the Assets folder

			// Handle LOD group. This should have child gameobjects whose components need to be parsed properly to make sure
			// the mesh and materials are properly copied.
			LODGroup sourceLODGroup = sourceGO.GetComponent< LODGroup >( ) ;
			if ( sourceLODGroup ) {
				LODGroup targetLODGroup = targetGO.GetComponent< LODGroup >( ) ;
				
				if ( !targetLODGroup ) 
					targetLODGroup = targetGO.AddComponent< LODGroup >( ) ;

				CopyChildGameObjects( partData, sourceGO, targetGO, assetName, sourceToTargetMeshMap,
									  sourceToCopiedMaterials, bWriteMeshesToAssetDatabase, ref bakedAssetPath,
									  ref assetDBObject, assetObjectFileName, bDeleteExistingComponents,
									  bDontDeletePersistantResources, lodTransformValues != null ) ;

				LOD[ ] sourceLODs = sourceLODGroup.GetLODs( ) ;
				if ( sourceLODs is not null ) {
					List< GameObject > targetChilden = HEU_GeneralUtility.GetChildGameObjects( targetGO ) ;

					LOD[ ] targetLODs = new LOD[ sourceLODs.Length ] ;
					for ( int i = 0; i < sourceLODs.Length; ++i ) {
						if ( sourceLODs[ i ].renderers != null && sourceLODs[ i ].renderers.Length > 0 ) {
							GameObject childGO = sourceLODs[ i ].renderers[ 0 ].gameObject ;
							if ( !childGO ) continue ;
							
							GameObject targetChildGO =
								HEU_GeneralUtility.GetGameObjectByName( targetChilden, childGO.name ) ;
							
							if ( !targetChildGO ) continue ;
							targetLODs[ i ] = new( sourceLODs[ i ].screenRelativeTransitionHeight,
												   targetChildGO.GetComponents< Renderer >( ) ) ;
						}
					}

					// Sort by screen transition as it might not be properly ordered. Unity complains if not in decreasing order.
					Array.Sort( targetLODs,
									   ( a, b ) =>
										   ( b.screenRelativeTransitionHeight > a.screenRelativeTransitionHeight )
											   ? 1
											   : -1 ) ;

					targetLODGroup.SetLODs( targetLODs ) ;
				}
			}

			if ( lodTransformValues is not null )
				HEU_GeneralUtility.SetLODTransformValues( targetGO, lodTransformValues ) ;

			// Mesh for render
			MeshFilter targetMeshFilter = targetGO.GetComponent< MeshFilter >( ) ;
			MeshFilter sourceMeshFilter = sourceGO.GetComponent< MeshFilter >( ) ;
			if ( sourceMeshFilter ) {
				if ( !targetMeshFilter )
					targetMeshFilter = HEU_EditorUtility.AddComponent< MeshFilter >( targetGO, true ) as MeshFilter ;
				
				Mesh originalMesh = sourceMeshFilter.sharedMesh ;
				if ( originalMesh ) {
					if ( !sourceToTargetMeshMap.TryGetValue( originalMesh, out Mesh targetMesh ) ) {
						// Create this mesh
						targetMesh                            = Instantiate( originalMesh ) ;
						sourceToTargetMeshMap[ originalMesh ] = targetMesh ;
						if ( bWriteMeshesToAssetDatabase ) {
							HEU_AssetDatabase.CreateAddObjectInAssetCacheFolder( assetName, assetObjectFileName,
																					targetMesh, "", 
																					ref bakedAssetPath, ref assetDBObject ) ;
						}
					}
					if ( targetMeshFilter ) 
						targetMeshFilter.sharedMesh = targetMesh ;
				}
			}
			else if ( targetMeshFilter )
				HEU_GeneralUtility.DestroyImmediate( targetMeshFilter ) ;

			// Mesh for collider
			MeshCollider targetMeshCollider = targetGO.GetComponent< MeshCollider >( ) ;
			MeshCollider sourceMeshCollider = sourceGO.GetComponent< MeshCollider >( ) ;
			if ( sourceMeshCollider ) {
				if ( !targetMeshCollider )
					targetMeshCollider =
						HEU_EditorUtility.AddComponent< MeshCollider >( targetGO, true ) as MeshCollider ;

				Mesh originalColliderMesh = sourceMeshCollider.sharedMesh ;
				if ( originalColliderMesh ) {
					if ( !sourceToTargetMeshMap.TryGetValue( originalColliderMesh, 
															 out Mesh targetColliderMesh ) ) {
						// Create this mesh
						targetColliderMesh = Instantiate( originalColliderMesh ) ;
						sourceToTargetMeshMap[ originalColliderMesh ] = targetColliderMesh ;

						if ( bWriteMeshesToAssetDatabase ) {
							HEU_AssetDatabase.CreateAddObjectInAssetCacheFolder( assetName, assetObjectFileName,
																					targetColliderMesh, "",
																					ref bakedAssetPath,
																					ref assetDBObject ) ;
						}
					}

					targetMeshCollider.sharedMesh = targetColliderMesh ;
				}
			}
			else if ( targetMeshCollider )
				HEU_GeneralUtility.DestroyImmediate( targetMeshFilter ) ;

			// Materials and textures
			MeshRenderer targetMeshRenderer = targetGO.GetComponent< MeshRenderer >( ) ;
			MeshRenderer sourceMeshRenderer = sourceGO.GetComponent< MeshRenderer >( ) ;
			if ( sourceMeshRenderer ) {
				if ( !targetMeshRenderer )
					targetMeshRenderer =
						HEU_EditorUtility.AddComponent< MeshRenderer >( targetGO, true ) as MeshRenderer ;

				Material[ ] generatedMaterials = null ;
				if ( partData )
					generatedMaterials =
						HEU_GeneratedOutput.GetGeneratedMaterialsForGameObject( partData._generatedOutput, sourceGO ) ;

				Material[ ] materials = sourceMeshRenderer.sharedMaterials ;
				if ( materials is { Length: > 0 } ) {
					if ( string.IsNullOrEmpty(bakedAssetPath) ) {
						// Need to create the baked folder in order to store materials and textures
						bakedAssetPath = HEU_AssetDatabase.CreateUniqueBakePath( assetName ) ;
					}

					int numMaterials = materials.Length ;
					for ( int m = 0; m < numMaterials; ++m ) {
						Material srcMaterial = materials[ m ] ;
						if ( !srcMaterial ) continue ;

						if ( sourceToCopiedMaterials.TryGetValue( srcMaterial, out Material newMaterial ) ) {
							materials[ m ] = newMaterial ;
							continue ;
						}

						// If srcMaterial is a Unity material (not Houdini generated), then skip copying
						if ( partData ) {
							HEU_MaterialData materialData = partData.ParentAsset.GetMaterialData( srcMaterial ) ;
							if ( materialData && materialData.IsExistingMaterial() )
								continue ;
						}

						// Check override material
						if ( generatedMaterials is not null
								 && ( m < generatedMaterials.Length )
									&& ( srcMaterial != generatedMaterials[m] ) ) {
							// This materials has been overriden. No need to copy it, just use as is.
							continue ;
						}

						string? materialPath = HEU_AssetDatabase.GetAssetPath( srcMaterial ) ;
						if ( !string.IsNullOrEmpty( materialPath ) &&
							 HEU_AssetDatabase.IsPathInAssetCache( materialPath ) ) {
							newMaterial =
								HEU_AssetDatabase.CopyAndLoadAssetWithRelativePath( srcMaterial, bakedAssetPath, "",
									typeof( Material ), false ) as Material ;
							if ( !newMaterial ) {
								throw new
									HEU_HoudiniEngineError( "Unable to copy material. Stopping bake!" ) ;
							}
						}
						else if ( HEU_AssetDatabase.ContainsAsset( srcMaterial ) ) {
							// Material is stored in Asset Database, but outside the cache. This is most likely an existing material specified by user, so use as is.
							continue ;
						}
						else {
							// Material is not in Asset Database (probably default material). So create a copy of it in Asset Database.
							newMaterial = HEU_MaterialFactory.CopyMaterial( srcMaterial ) ;
							HEU_MaterialFactory.WriteMaterialToAssetCache( newMaterial, bakedAssetPath,
																		   newMaterial.name,
																		   bDeleteExistingComponents ) ;
						}

						if ( !newMaterial ) continue ;
						sourceToCopiedMaterials.Add( srcMaterial, newMaterial ) ;
						
						// Diffuse texture
						if ( newMaterial.HasProperty( "_MainTex" ) ) {
							Texture srcDiffuseTexture = newMaterial.mainTexture ;
							if ( srcDiffuseTexture ) {
								Texture newDiffuseTexture =
									HEU_AssetDatabase.CopyAndLoadAssetWithRelativePath( srcDiffuseTexture,
											 bakedAssetPath, "", typeof( Texture ), false ) as Texture ;
									
								if ( !newDiffuseTexture )
									throw new HEU_HoudiniEngineError( "Unable to copy texture. Stopping bake!" ) ;

								newMaterial.mainTexture = newDiffuseTexture ;
							}
						}

						// Normal map
						if ( materials[ m ].HasProperty( HEU_Defines.UNITY_SHADER_BUMP_MAP ) ) {
							Texture srcNormalMap = materials[ m ].GetTexture( HEU_Defines.UNITY_SHADER_BUMP_MAP ) ;
							if ( srcNormalMap ) {
								Texture newNormalMap =
									HEU_AssetDatabase.CopyAndLoadAssetWithRelativePath( srcNormalMap,
											 bakedAssetPath, "", typeof( Texture ), false ) as Texture ;
									
								if ( !newNormalMap )
									throw new HEU_HoudiniEngineError( "Unable to copy texture. Stopping bake!" ) ;

								newMaterial.SetTexture( HEU_Defines.UNITY_SHADER_BUMP_MAP, newNormalMap ) ;
							}
						}

						materials[ m ] = newMaterial ;
					}

					targetMeshRenderer.sharedMaterials = materials ;
				}
			}
			else if ( targetMeshRenderer ) 
				HEU_GeneralUtility.DestroyImmediate( targetMeshRenderer ) ;

			// Terrain component
			Terrain targetTerrain = targetGO.GetComponent< Terrain >( ) ;
			Terrain sourceTerrain = sourceGO.GetComponent< Terrain >( ) ;
			TerrainData targetTerrainData = null ;
			if ( sourceTerrain ) {
				if ( !targetTerrain ) 
					targetTerrain = HEU_EditorUtility.AddComponent< Terrain >( targetGO, true ) as Terrain ;

				TerrainData sourceTerrainData = sourceTerrain.terrainData ;
				if ( sourceTerrainData ) {
					targetTerrainData = targetTerrain.terrainData ;
					if ( targetTerrainData && targetTerrainData != sourceTerrainData &&
						 HEU_AssetDatabase.ContainsAsset( targetTerrainData ) ) {
						// Get path to existing terrain data asset location
						bakedAssetPath = HEU_AssetDatabase.GetAssetRootPath( targetTerrainData ) ;
					}

					if ( string.IsNullOrEmpty( bakedAssetPath ) ) {
						bakedAssetPath = HEU_AssetDatabase.CreateUniqueBakePath( assetName ) ;
					}

					// Copy over the TerrainData, and TerrainLayers. But both of these are stored as files on disk. 
					// We will need to copy them if the HDA generated them.
					// Note: ignoring bWriteMeshesToAssetDatabase and always writing to asset db because terrain 
					// files need to stored in asset db

					string? sourceAssetPath = HEU_AssetDatabase.GetAssetPath( sourceTerrainData ) ;

					// Form the baked terrain path with sub folders, by acquiring the geo name, and terrain tile index:
					//	 sourceAssetPath	= "Assets/HoudiniEngineAssetCache/Working/{asset name}/{geo name}/Terrain/Tile0/TerrainData.asset"
					//	 bakedAssetPath		= "Assets/HoudiniEngineAssetCache/Baked/{asset name}"
					// =>bakedTerrainPath	= "Assets/HoudiniEngineAssetCache/Baked/{asset name}/{geo name}/Terrain/Tile0"
					string? bakedTerrainPath = bakedAssetPath ;

					// Find the geo name and terrain tile index
					//	@"/(Working)/(\w+)/(\w+)/(Terrain/Tile[0-9]+)/TerrainData.asset$"
					string pattern = string.Format( @"{0}(Working){0}(\w+.*){0}(\w+){0}({1}{0}{2}[0-9]+){0}\w.*{3}",
													HEU_Platform.DirectorySeparatorStr,
													HEU_Defines.HEU_FOLDER_TERRAIN,
													HEU_Defines.HEU_FOLDER_TILE,
													HEU_Defines.HEU_EXT_ASSET ) ;
					Regex reg   = new( pattern ) ;
					Match match = reg.Match( sourceAssetPath ) ;

					/* Leaving it in for debugging
					HEU_Logger.Log("Match: " + match.Success);
					if (match.Success)
					{
						int numGroups = match.Groups.Count;
						for(int g = 0; g < numGroups; ++g)
						{
							HEU_Logger.LogFormat("Group: {0} - {1}", g, match.Groups[g].Value);
						}
					}
					*/

					// We should get 5 groups matched: {full match}, Working, {asset name}, {geo name}, Terrain/Tile{index}
					// e.g.: "Assets/HoudiniEngineAssetCache/Working/simple_heightfield/heightfield_noise1/Terrain/Tile0/TerrainData.asset"
					if ( match.Success && match.Groups.Count == 5 ) {
						bakedTerrainPath = HEU_Platform.BuildPath( bakedTerrainPath, match.Groups[ 2 ].Value,
																   match.Groups[ 3 ].Value, match.Groups[ 4 ].Value ) ;
					}
					else {
						// Sometimes path can have 3 layers (one is the node)
						string pattern_extrapart =
							string.Format( @"{0}(Working){0}(\w+.*){0}(\w+.*){0}(\w+.*){0}({1}{0}{2}[0-9]+){0}\w.*{3}",
										   HEU_Platform.DirectorySeparatorStr,
										   HEU_Defines.HEU_FOLDER_TERRAIN,
										   HEU_Defines.HEU_FOLDER_TILE,
										   HEU_Defines.HEU_EXT_ASSET ) ;
						reg   = new( pattern_extrapart ) ;
						match = reg.Match( sourceAssetPath ) ;

						if ( match.Success && match.Groups.Count == 6 ) {
							bakedTerrainPath = HEU_Platform.BuildPath( bakedTerrainPath, match.Groups[ 2 ].Value,
																	   match.Groups[ 3 ].Value, match.Groups[ 4 ].Value,
																	   match.Groups[ 5 ].Value ) ;
						}
						else {
							// pdg has a slightly different folder path:
							// e.g. "Assets/HoudiniEngineAssetCache/Working/simple_PDG/PDGCache/Terrain/Terrain/Tile0/Terrain/TerrainData.asset"
							string pattern_pdg =
								string.Format( @"{0}(Working){0}(\w+.*){0}(\w+){0}(\w+){0}({1}{0}{2}[0-9]+){0}(\w+){0}\w.*{3}",
											   HEU_Platform.DirectorySeparatorStr,
											   HEU_Defines.HEU_FOLDER_TERRAIN,
											   HEU_Defines.HEU_FOLDER_TILE,
											   HEU_Defines.HEU_EXT_ASSET ) ;
							Regex reg_pdg   = new( pattern_pdg ) ;
							Match match_pdg = reg_pdg.Match( sourceAssetPath ) ;
							if ( match_pdg.Success ) {
								bakedTerrainPath =
									HEU_Platform.BuildPath( bakedTerrainPath, match.Groups[ 2 ].Value,
															match_pdg.Groups[ 3 ].Value, match_pdg.Groups[ 5 ].Value ) ;
							}
							else {
								string? supposedTerrainPath =
									HEU_Platform.BuildPath( bakedTerrainPath, match.Groups[ 3 ].Value,
															match.Groups[ 4 ].Value ) ;
								HEU_Logger
									.LogErrorFormat( "Invalid build path format\nSource: {0}\nPattern1: {1}\nPattern2: {2}",
													 sourceAssetPath, pattern, pattern_pdg ) ;
							}
						}


					}

					// We're going to copy the source terrain data asset file, then load the copy and assign to the target
					targetTerrainData =
						HEU_AssetDatabase.CopyAndLoadAssetFromAssetCachePath( sourceTerrainData, bakedTerrainPath,
																			  typeof( TerrainData ),
																			  true ) as TerrainData ;

#if UNITY_2018_3_OR_NEWER
					// Copy over the TerrainLayers
					TerrainLayer[ ] sourceTerrainLayers = sourceTerrainData.terrainLayers ;
					if ( sourceTerrainLayers is not null ) {
						var tergetTerrainLayers = new TerrainLayer[ sourceTerrainLayers.Length ] ;
						for ( int m = 0; m < sourceTerrainLayers.Length; ++m ) {
							TerrainLayer copylayer =
								HEU_AssetDatabase.CopyAndLoadAssetFromAssetCachePath( 
								 sourceTerrainLayers[ m ],
								 bakedTerrainPath,
								 typeof( TerrainLayer ),
								 true ) as TerrainLayer ;
							
							tergetTerrainLayers[ m ] = copylayer
														   ? copylayer 
																: sourceTerrainLayers[ m ] ;
						}

						targetTerrainData.terrainLayers = tergetTerrainLayers ;
					}

					Material srcMat = sourceTerrain.materialTemplate ;
					if ( srcMat ) {
						Material dstMat = HEU_MaterialFactory.CopyMaterial( srcMat ) ;
#if UNITY_2019_2_OR_NEWER
						targetTerrain.materialTemplate = dstMat ;
						HEU_MaterialFactory.WriteMaterialToAssetCache( dstMat, bakedTerrainPath, dstMat.name,
																	   bDeleteExistingComponents ) ;
#else
			    targetTerrain.materialType = sourceTerrain.materialType;
	        	    targetTerrain.materialTemplate = dstMat;

			    if (targetTerrain.materialType == Terrain.MaterialType.Custom)
			    {
				HEU_MaterialFactory.WriteMaterialToAssetCache(dstMat, bakedTerrainPath, dstMat.name, bDeleteExistingComponents);
			    }
#endif
					}

#endif

					targetTerrain.terrainData = targetTerrainData ;
					targetTerrain.Flush( ) ;
				}
			}
			else if ( targetTerrain ) {
				targetTerrainData = targetTerrain.terrainData ;
				if ( HEU_AssetDatabase.ContainsAsset( targetTerrainData ) ) {
					targetTerrain.terrainData = null ;
					HEU_AssetDatabase.DeleteAsset( targetTerrainData ) ;
					targetTerrainData = null ;
				}

				HEU_GeneralUtility.DestroyImmediate( targetTerrain ) ;
			}

#if !HEU_TERRAIN_COLLIDER_DISABLED
			// Terrain collider
			TerrainCollider targetTerrainCollider = targetGO.GetComponent< TerrainCollider >( ) ;
			TerrainCollider sourceTerrainCollider = sourceGO.GetComponent< TerrainCollider >( ) ;
			if ( sourceTerrainCollider ) {
				if ( !targetTerrainCollider )
					targetTerrainCollider =
						HEU_EditorUtility.AddComponent< TerrainCollider >( targetGO, true ) as TerrainCollider ;

				targetTerrainCollider.terrainData = targetTerrainData ;
			}
			else if ( targetTerrainCollider )
				HEU_GeneralUtility.DestroyImmediate( targetTerrainCollider ) ;
#endif
		}

		/// <summary>
		/// Copy the child GameObjects of the given sourceGO to targetGO, along with making sure all components have been properly copied.
		/// targetGO might already have existing children.
		/// </summary>
		/// <param name="partData"></param>
		/// <param name="sourceGO">Source gameobject to copy from.</param>
		/// <param name="targetGO">Target gameobject to copy to.</param>
		/// <param name="assetName">Name of the asset.</param>
		/// <param name="sourceToTargetMeshMap">Map of existing meshes to newly created meshes. This helps keep track of shared meshes that should be copied but still shared in new asset.</param>
		/// <param name="sourceToCopiedMaterials">Map of existing materials with their new copied counterparts. Keeps track of which materials have been newly copied in order to reuse.</param>
		/// <param name="bWriteMeshesToAssetDatabase">Whether to store meshes to database. Required for prefabs.</param>
		/// <param name="bakedAssetPath">Path to asset's database cache. Could be null in which case it will be filled.</param>
		/// <param name="assetDBObject">The asset database object to write out the persistent mesh data to. Could be null, in which case it might be created.</param>
		/// <param name="assetObjectFileName">File name of the asset database object. This will be used to create new assetDBObject.</param>
		/// <param name="bDeleteExistingComponents">True if should delete existing components to then re-add.</param>
		/// <param name="bDontDeletePersistantResources">True if not to delete persisten file resources in the project.</param>
		/// <param name="bKeepPreviousTransformValues"></param>
		static void CopyChildGameObjects( HEU_PartData partData, GameObject sourceGO, GameObject targetGO,
										  string? assetName, Dictionary< Mesh, Mesh > sourceToTargetMeshMap,
										  Dictionary< Material, Material > sourceToCopiedMaterials,
										  bool bWriteMeshesToAssetDatabase, ref string? bakedAssetPath,
										  ref Object assetDBObject, string? assetObjectFileName,
										  bool bDeleteExistingComponents, bool bDontDeletePersistantResources,
										  bool bKeepPreviousTransformValues ) {
			Transform          targetTransform           = targetGO.transform ;
			List< GameObject > unprocessedTargetChildren = HEU_GeneralUtility.GetChildGameObjects( targetGO ) ;

			List< GameObject > srcChildGameObjects = HEU_GeneralUtility.GetChildGameObjects( sourceGO ) ;
			int                numChildren         = srcChildGameObjects.Count ;
			for ( int i = 0; i < numChildren; ++i ) {
				GameObject srcChildGO = srcChildGameObjects[ i ] ;

				GameObject targetChildGO =
					HEU_GeneralUtility.GetGameObjectByName( unprocessedTargetChildren, srcChildGO.name ) ;
				List< TransformData > previousTransformValues = null ;
				if ( targetChildGO == null ) {
					targetChildGO                  = HEU_GeneralUtility.CreateNewGameObject( srcChildGO.name ) ;
					targetChildGO.transform.parent = targetTransform ;
				}
				else {

					if ( bKeepPreviousTransformValues ) {
						previousTransformValues = new( ) ;
						List< Transform > previousTransforms = HEU_GeneralUtility.GetLODTransforms( targetGO ) ;
						previousTransforms.ForEach( trans =>
													{
														previousTransformValues.Add( new( trans ) ) ;
													} ) ;
					}

					if ( bDeleteExistingComponents ) {
						HEU_GeneralUtility.DestroyGeneratedMeshMaterialsLODGroups( targetChildGO,
							bDontDeletePersistantResources ) ;
					}

					unprocessedTargetChildren.Remove( targetChildGO ) ;

					// Update transform of each existing instance
					HEU_GeneralUtility.CopyLocalTransformValues( srcChildGO.transform, targetChildGO.transform ) ;
				}

				// Copy component data
				CopyGameObjectComponents( partData, srcChildGO, targetChildGO, assetName, sourceToTargetMeshMap,
										  sourceToCopiedMaterials, bWriteMeshesToAssetDatabase, ref bakedAssetPath,
										  ref assetDBObject, assetObjectFileName, bDeleteExistingComponents,
										  bDontDeletePersistantResources, previousTransformValues ) ;
			}

			if ( unprocessedTargetChildren.Count > 0 ) {
				// Clean up any children that we haven't updated as they don't exist in the source
				HEU_GeneralUtility.DestroyBakedGameObjects( unprocessedTargetChildren ) ;
			}
		}

		/// <summary>
		/// Bake this part out to a new gameobject, and returns it. 
		/// Copies all relevant components.
		/// Supports baking of part and object instances.
		/// </summary>
		/// <param name="parentTransform">The parent for the new object. Can be null.</param>
		/// <param name="bWriteMeshesToAssetDatabase">Whether to store meshes to database. Required for prefabs.</param>
		/// <param name="bakedAssetPath">Path to asset's database cache. Could be null in which case it will be filled.</param>
		/// <param name="sourceToTargetMeshMap">Map of existing meshes to newly created meshes. This helps keep track of shared meshes that should be copied but still shared in new asset.</param>
		/// <param name="sourceToCopiedMaterials">Map of existing materials with their new copied counterparts. Keeps track of which materials have been newly copied in order to reuse.</param>
		/// <param name="assetDBObject">The asset database object to write out the persistent mesh data to. Could be null, in which case it might be created.</param>
		/// <param name="assetObjectFileName">File name of the asset database object. This will be used to create new assetDBObject.</param>
		/// <param name="bReconnectPrefabInstances">Reconnect prefab instances to its prefab parent.</param>
		/// <returns>The newly created gameobject.</returns>
		internal GameObject BakePartToNewGameObject( Transform parentTransform, bool bWriteMeshesToAssetDatabase,
													 ref string? bakedAssetPath,
													 Dictionary< Mesh, Mesh > sourceToTargetMeshMap,
													 Dictionary< Material, Material > sourceToCopiedMaterials,
													 ref Object assetDBObject, string? assetObjectFileName,
													 bool bReconnectPrefabInstances ) {
			GameObject outputGameObject = OutputGameObject ;
			if ( !outputGameObject ) return null ;

			// This creates a copy of the part's gameobject, along with instances if it has them.
			// If the instances are prefab instances, then this disconnects the connection. We re-connect them back in the call below.
			GameObject targetGO =
				HEU_EditorUtility.InstantiateGameObject( outputGameObject, parentTransform, 
														 true, true ) ;
			
			HEU_GeneralUtility.RenameGameObject( targetGO, AppendBakedCloneName( outputGameObject.name ) ) ;
			
			BakePartToGameObject( targetGO, false, false, 
								  bWriteMeshesToAssetDatabase, ref bakedAssetPath,
								  sourceToTargetMeshMap, sourceToCopiedMaterials, ref assetDBObject,
								  assetObjectFileName, bReconnectPrefabInstances, false ) ;

			return targetGO ;
		}

		/// <summary>
		/// Bake this part out to the given targetGO. Existing components might be destroyed.
		/// Supports baking of part and object instances.
		/// </summary>
		/// <param name="srcGO"></param>
		/// <param name="targetGO">Target gameobject to bake out to.</param>
		/// <param name="assetName"></param>
		/// <param name="bIsInstancer"></param>
		/// <param name="bDeleteExistingComponents">Whether to destroy existing components on the targetGO.</param>
		/// <param name="bDontDeletePersistantResources">Whether to delete persistant resources stored in the project.</param>
		/// <param name="bWriteMeshesToAssetDatabase">Whether to store meshes to database. Required for prefabs.</param>
		/// <param name="bakedAssetPath">Path to asset's database cache. Could be null in which case it will be filled.</param>
		/// <param name="sourceToTargetMeshMap">Map of existing meshes to newly created meshes. This helps keep track of shared meshes that should be copied but still shared in new asset.</param>
		/// <param name="sourceToCopiedMaterials">Map of existing materials with their new copied counterparts. Keeps track of which materials have been newly copied in order to reuse.</param>
		/// <param name="assetDBObject">The asset database object to write out the persistent mesh data to. Could be null, in which case it might be created.</param>
		/// <param name="assetObjectFileName">File name of the asset database object. This will be used to create new assetDBObject.</param>
		/// <param name="bReconnectPrefabInstances">Reconnect prefab instances to its prefab parent.</param>
		/// <param name="bKeepPreviousTransformValues">Keeps transform values of previous groups.</param>
		/// <param name="partData"></param>
		internal static void BakePartToGameObject( HEU_PartData partData, GameObject srcGO, GameObject targetGO,
												   string? assetName, bool bIsInstancer, bool bDeleteExistingComponents,
												   bool bDontDeletePersistantResources,
												   bool bWriteMeshesToAssetDatabase, ref string? bakedAssetPath,
												   Dictionary< Mesh, Mesh > sourceToTargetMeshMap,
												   Dictionary< Material, Material > sourceToCopiedMaterials,
												   ref Object assetDBObject, string? assetObjectFileName,
												   bool bReconnectPrefabInstances, bool bKeepPreviousTransformValues ) {
			if ( srcGO == null ) {
				return ;
			}

			if ( srcGO == targetGO ) {
				HEU_Logger.LogError( "Copy and target objects cannot be the same!" ) ;
				return ;
			}

			Transform targetTransform = targetGO.transform ;

			if ( bIsInstancer ) {
				// Instancer

				// Instancer has a gameobject with children. The parent is an empty transform, while the
				// the children have all the data. The children could have an assortment of meshes.

				// Keeps track of unprocessed children. Any leftover will be destroyed.
				List< GameObject > unprocessedTargetChildren = HEU_GeneralUtility.GetChildGameObjects( targetGO ) ;

				List< GameObject > srcChildGameObjects = HEU_GeneralUtility.GetChildGameObjects( srcGO ) ;
				int                numChildren         = srcChildGameObjects.Count ;
				for ( int i = 0; i < numChildren; ++i ) {
					GameObject srcChildGO = srcChildGameObjects[ i ] ;

					bool bSrcPrefabInstance = HEU_EditorUtility.IsPrefabInstance( srcChildGO ) ;

					GameObject targetChildGO =
						HEU_GeneralUtility.GetGameObjectByName( unprocessedTargetChildren, srcChildGO.name ) ;

					List< TransformData > previousTransformValues = null ;

					if ( bSrcPrefabInstance && targetChildGO != null &&
						 !HEU_EditorUtility.IsPrefabInstance( targetChildGO ) ) {
						// A not-so-ideal workaround to the fact that when calling GameObject.Instantiate, copies of child prefab instances
						// are not created as prefab instances (they are created as regular gameobjects).
						// And with Unity 2018.3, it is no longer possible to reconnect regular gameobjects to prefab assets (via ConnectGameObjectToPrefab).
						// So by clearing the targetChildGO reference here, the code below will create a proper prefab instance.
						targetChildGO = null ;
					}

					if ( targetChildGO == null ) {
						if ( bSrcPrefabInstance ) {
							GameObject prefabAsset = HEU_EditorUtility.GetPrefabAsset( srcChildGO ) as GameObject ;
							if ( prefabAsset ) {
								targetChildGO = HEU_EditorUtility.InstantiatePrefab( prefabAsset ) as GameObject ;
								HEU_GeneralUtility.RenameGameObject( targetChildGO, srcChildGO.name ) ;
							}
						}
						else {
							targetChildGO = HEU_GeneralUtility.CreateNewGameObject( srcChildGO.name ) ;
						}

						if ( targetChildGO == null ) {
							HEU_Logger.LogErrorFormat( "Unable to create instance for: {0}", srcChildGO.name ) ;
							continue ;
						}

						targetChildGO.transform.parent = targetTransform ;
					}
					else {

						if ( bKeepPreviousTransformValues ) {
							previousTransformValues = new( ) ;
							List< Transform > previousTransforms = HEU_GeneralUtility.GetLODTransforms( targetGO ) ;
							previousTransforms.ForEach( trans =>
														{
															previousTransformValues.Add( new( trans ) ) ;
														} ) ;
						}

						if ( bDeleteExistingComponents ) {
							HEU_GeneralUtility.DestroyGeneratedMeshMaterialsLODGroups( targetChildGO,
								bDontDeletePersistantResources ) ;
						}

						unprocessedTargetChildren.Remove( targetChildGO ) ;
					}

					// Update transform of each existing instance
					HEU_GeneralUtility.CopyLocalTransformValues( srcChildGO.transform, targetChildGO.transform ) ;

					if ( !bSrcPrefabInstance ) {
						// Copy component data only if not a prefab instance. 
						// Otherwise, copying prefab instances breaks the prefab connection and creates duplicates (e.g. instancing existing prefabs).
						CopyGameObjectComponents( partData, srcChildGO, targetChildGO, assetName, sourceToTargetMeshMap,
												  sourceToCopiedMaterials, bWriteMeshesToAssetDatabase,
												  ref bakedAssetPath,
												  ref assetDBObject, assetObjectFileName, bDeleteExistingComponents,
												  bDontDeletePersistantResources, previousTransformValues ) ;
					}
					else {
						// Special case that copies overridden MeshCollider (via collision_geo attribute on instance)
						MeshCollider sourceMeshCollider = srcChildGO.GetComponent< MeshCollider >( ) ;
						if ( sourceMeshCollider != null &&
							 HEU_EditorUtility.PrefabIsAddedComponentOverride( sourceMeshCollider ) ) {
							HEU_GeneralUtility.ReplaceColliderMeshFromMeshCollider( targetChildGO, srcChildGO ) ;
						}
					}
				}

				if ( unprocessedTargetChildren.Count > 0 ) {
					// Clean up any children that we haven't updated as they don't exist in the source
					HEU_GeneralUtility.DestroyBakedGameObjects( unprocessedTargetChildren ) ;
				}
			}
			else {
				// Not an instancer, regular object (could also be instanced)
				// TODO: For instanced object, should we not instantiate if it is not visible?

				List< TransformData > previousTransformValues = null ;

				if ( bKeepPreviousTransformValues ) {
					previousTransformValues = new( ) ;
					List< Transform > previousTransforms = HEU_GeneralUtility.GetLODTransforms( targetGO ) ;
					previousTransforms.ForEach( trans =>
												{
													previousTransformValues.Add( new( trans ) ) ;
												} ) ;
				}

				if ( bDeleteExistingComponents ) {
					HEU_GeneralUtility.DestroyGeneratedMeshMaterialsLODGroups( targetGO,
																			   bDontDeletePersistantResources ) ;
				}

				// Copy component data
				CopyGameObjectComponents( partData, srcGO, targetGO, assetName, sourceToTargetMeshMap,
										  sourceToCopiedMaterials, bWriteMeshesToAssetDatabase, ref bakedAssetPath,
										  ref assetDBObject, assetObjectFileName, bDeleteExistingComponents,
										  bDontDeletePersistantResources, previousTransformValues ) ;

			}
		}

		/// <summary>
		/// Bake this part out to the given targetGO. Existing components might be destroyed.
		/// Supports baking of part and object instances.
		/// </summary>
		/// <param name="targetGO">Target gameobject to bake out to.</param>
		/// <param name="bDeleteExistingComponents">Whether to destroy existing components on the targetGO.</param>
		/// <param name="bDontDeletePersistantResources">Whether to delete persistant resources stored in the project.</param>
		/// <param name="bWriteMeshesToAssetDatabase">Whether to store meshes to database. Required for prefabs.</param>
		/// <param name="bakedAssetPath">Path to asset's database cache. Could be null in which case it will be filled.</param>
		/// <param name="sourceToTargetMeshMap">Map of existing meshes to newly created meshes. This helps keep track of shared meshes that should be copied but still shared in new asset.</param>
		/// <param name="sourceToCopiedMaterials">Map of existing materials with their new copied counterparts. Keeps track of which materials have been newly copied in order to reuse.</param>
		/// <param name="assetDBObject">The asset database object to write out the persistent mesh data to. Could be null, in which case it might be created.</param>
		/// <param name="assetObjectFileName">File name of the asset database object. This will be used to create new assetDBObject.</param>
		/// <param name="bReconnectPrefabInstances">Reconnect prefab instances to its prefab parent.</param>
		/// <param name="bKeepPreviousTransformValues">Keeps transform values of previous groups.</param>
		internal void BakePartToGameObject( GameObject targetGO, bool bDeleteExistingComponents,
											bool bDontDeletePersistantResources, bool bWriteMeshesToAssetDatabase,
											ref string? bakedAssetPath, Dictionary< Mesh, Mesh > sourceToTargetMeshMap,
											Dictionary< Material, Material > sourceToCopiedMaterials,
											ref Object assetDBObject, string? assetObjectFileName,
											bool bReconnectPrefabInstances, bool bKeepPreviousTransformValues ) {
			if ( ParentAsset == null ) {
				return ;
			}

			bool isInstancer = IsInstancerAnyType( ) ;
			BakePartToGameObject( this, OutputGameObject, targetGO, ParentAsset.AssetName, isInstancer,
								  bDeleteExistingComponents, bDontDeletePersistantResources,
								  bWriteMeshesToAssetDatabase, ref bakedAssetPath, sourceToTargetMeshMap,
								  sourceToCopiedMaterials, ref assetDBObject, assetObjectFileName,
								  bReconnectPrefabInstances, bKeepPreviousTransformValues ) ;
		}

		/// <summary>Processs and build the mesh for this part.</summary>
		/// <param name="session">Active session to use.</param>
		/// <param name="bGenerateUVs">Whether to generate UVs manually.</param>
		/// <param name="bGenerateTangents">Whether to generate tangents manually.</param>
		/// <param name="bGenerateNormals">Whether to generate normals manually.</param>
		/// <param name="bUseLODGroups"></param>
		/// <returns>True if successfully built the mesh.</returns>
		internal bool GenerateMesh( HEU_SessionBase session, bool bGenerateUVs, 
									bool bGenerateTangents, bool bGenerateNormals, bool bUseLODGroups ) {
			if ( !OutputGameObject || !ParentAsset ) return false ;

			if ( IsPartCurve( ) ) {
				_curve.GenerateMesh( OutputGameObject, session ) ;

				// When a Curve asset is used as input node, it creates this editable and useless curve part type.
				// For now deleting it as it causes issues on recook (from scene load), as well as unnecessary curve editor UI.
				// Should revisit sometime in the future to review this.
				return ( _curve && _curve.ShouldKeepNode(session) ) ;
			}

			bool bResult ;
			if ( MeshVertexCount > 0 ) {
				// Get the geometry and material information from Houdini

				HEU_HoudiniAsset asset = ParentAsset ;
				if ( !asset ) {
					HEU_Logger.LogErrorFormat( "Asset not found. Unable to generate mesh for part {0}!", _partName ) ;
					return false ;
				}

				List< HEU_MaterialData > materialCache = asset.MaterialCache ;

				HEU_GenerateGeoCache geoCache =
					HEU_GenerateGeoCache.GetPopulatedGeoCache( session, ParentAsset.AssetID, _geoID, _partID,
															   bUseLODGroups,
															   materialCache,
															   asset.GetValidAssetCacheFolderPath( ) ) ;
				if ( geoCache is null ) {
					// Failed to get necessary info for generating geometry.
					return false ;
				}

				int defaultMaterialKey ;
				List< HEU_GeoGroup > LODGroupMeshes ;
				
				// Build the GeoGroup using points or vertices
				if ( asset.GenerateMeshUsingPoints ) {
					bResult = HEU_GenerateGeoCache.GenerateGeoGroupUsingGeoCachePoints( session, geoCache,
							 bGenerateUVs, bGenerateTangents, bGenerateNormals, bUseLODGroups, IsPartInstanced( ),
							 out LODGroupMeshes, out defaultMaterialKey ) ;
				}
				else {
					bResult = HEU_GenerateGeoCache.GenerateGeoGroupUsingGeoCacheVertices( session, geoCache,
							 bGenerateUVs, bGenerateTangents, bGenerateNormals, bUseLODGroups, IsPartInstanced( ),
							 out LODGroupMeshes, out defaultMaterialKey ) ;
				}
				
				if ( !bResult ) return false ;
				
				// Now generate and attach meshes and materials
				int numLODs = LODGroupMeshes?.Count ?? 0 ;
				switch ( numLODs ) {
					case > 1:
						bResult = HEU_GenerateGeoCache.GenerateLODMeshesFromGeoGroups( session, LODGroupMeshes,
								 geoCache, _generatedOutput, defaultMaterialKey, bGenerateUVs, bGenerateTangents,
								 bGenerateNormals, IsPartInstanced( ) ) ;
						break ;
						
					case 1:
						bResult = HEU_GenerateGeoCache.GenerateMeshFromSingleGroup( session, LODGroupMeshes?[ 0 ],
								 geoCache, _generatedOutput, defaultMaterialKey, bGenerateUVs, bGenerateTangents,
								 bGenerateNormals, IsPartInstanced( ) ) ;
						break ;
						
					default:
						// Set return state to false if no mesh and no colliders (i.e. nothing is generated)
						HEU_GeneralUtility.DestroyGeneratedComponents( _generatedOutput._outputData._gameObject ) ;
						bResult = ( geoCache._colliderInfos.Count > 0 ) ;
						break ;
				}

				HEU_GenerateGeoCache.UpdateColliders( geoCache, _generatedOutput._outputData ) ;
			}
			else if ( IsPartInstancer() 
					  || IsObjectInstancer() 
					  || IsAttribInstancer() ) {
				// Always returning true for meshes without geometry that are instancers. These
				// are handled after this.
				bResult = true ;
			}
			else {
				// No geometry -> default case is to return false to clean up
				bResult = false ;
			}

			return bResult ;
		}

		internal void ProcessCurvePart( HEU_SessionBase session, HAPI_PartId partId ) {
			HEU_HoudiniAsset parentAsset = ParentAsset ;
			if ( !parentAsset ) return ;

			bool bNewCurve = !_curve ;
			if ( bNewCurve ) {
				_curve = HEU_Curve.CreateSetupCurve( session, parentAsset, _geoNode.Editable, 
													 _partName, _geoID, partId, false ) ;
			}
			else _curve.UploadParameterPreset( session, _geoID, parentAsset ) ;

			_curve.SyncFromParameters( session, parentAsset, bNewCurve ) ;
			_curve.UpdateCurve( session, _partID ) ;

			if ( bNewCurve )
				_curve.DownloadAsDefaultPresetData( session ) ;
		}

		internal void SyncAttributesStore( HEU_SessionBase session, HAPI_NodeId geoID, ref HAPI_PartInfo partInfo ) {
			if ( !_attributesStore )
				_attributesStore = CreateInstance< HEU_AttributesStore >( ) ;
			if ( !ParentAsset ) return ;
			
			_attributesStore.SyncAllAttributesFrom( session, ParentAsset, geoID,
													ref partInfo, OutputGameObject ) ;
			
			ParentAsset.AddAttributeStore( _attributesStore ) ;
		}

		internal void SetupAttributeGeometry( HEU_SessionBase session ) {
			if ( !_attributesStore || !ParentAsset ) return ;
			_attributesStore.SetupMeshAndMaterials( ParentAsset,
													_partType,
													OutputGameObject ) ;
		}

		internal void DestroyAttributesStore( ) {
			if ( _attributesStore != null ) {
				HEU_HoudiniAsset parentAsset = ParentAsset ;
				if ( parentAsset != null ) {
					parentAsset.RemoveAttributeStore( _attributesStore ) ;

					_attributesStore.DestroyAllData( parentAsset ) ;
				}

				HEU_GeneralUtility.DestroyImmediate( _attributesStore ) ;
				_attributesStore = null ;
			}
		}

		/// <summary>
		/// Fill in the objInstanceInfos list with the HEU_ObjectInstanceInfos used by this part.
		/// </summary>
		/// <param name="objInstanceInfos">List to fill in</param>
		internal void PopulateObjectInstanceInfos( List< HEU_ObjectInstanceInfo > objInstanceInfos ) => 
															objInstanceInfos.AddRange( _objectInstanceInfos ) ;

		/// <summary>Set object instance infos from the given part into this.</summary>
		/// <param name="sourceObjectInstanceInfos"></param>
		internal void SetObjectInstanceInfos( List< HEU_ObjectInstanceInfo > sourceObjectInstanceInfos ) {
			int numSourceInfos = sourceObjectInstanceInfos.Count ;
			for ( int i = 0; i < numSourceInfos; ++i ) {
				sourceObjectInstanceInfos[ i ]._instances.Clear( ) ;
				sourceObjectInstanceInfos[ i ]._partTarget = this ;

				_objectInstanceInfos.Add( sourceObjectInstanceInfos[ i ] ) ;
			}
		}

		/// <summary>
		/// Return list of HEU_ObjectInstanceInfo used by this part.
		/// </summary>
		/// <returns></returns>
		internal List< HEU_ObjectInstanceInfo > GetObjectInstanceInfos( ) {
			return _objectInstanceInfos ;
		}

		/// <summary>
		/// Helper to create a HEU_ObjectInstanceInfo, representing an instanced object
		/// containing list of instances.
		/// Adds this new object to _objectInstanceInfos.
		/// </summary>
		/// <param name="instancedObject">The source instanced object</param>
		/// <param name="instancedObjectNodeID">If instancedObject is a Houdini Engine object node, then this would be its node ID</param>
		/// <param name="instancedObjectPath">Path in Unity to the instanced object (could be empty or null if not a Unity instanced object)</param>
		/// <returns>The created object</returns>
		HEU_ObjectInstanceInfo CreateObjectInstanceInfo( GameObject  instancedObject,
														 HAPI_NodeId instancedObjectNodeID,
														 string?     instancedObjectPath ) {
			HEU_ObjectInstanceInfo newInfo = CreateInstance< HEU_ObjectInstanceInfo >( ) ;
			
			newInfo._partTarget            = this ;
			newInfo._instancedObjectNodeID = instancedObjectNodeID ;
			newInfo._instancedObjectPath   = instancedObjectPath ;

			HEU_InstancedInput input = new( ) {
				_instancedGameObject = instancedObject,
			} ;
			
			newInfo._instancedInputs.Add( input ) ;
			_objectInstanceInfos.Add( newInfo ) ;
			return newInfo ;
		}


		internal static string? AppendBakedCloneName( string name ) {
			return name + HEU_Defines.HEU_BAKED_CLONE ;
		}

		public override string ToString( ) {
			return ( !string.IsNullOrEmpty( _partName ) ? ( "Part: " + _partName ) : base.ToString( ) ) ;
		}

		/// <summary>
		/// Destroy list of parts and their data.
		/// </summary>
		internal static void DestroyParts( List< HEU_PartData > parts, bool bIsRebuild = false ) {
			int numParts = parts.Count ;
			for ( int i = 0; i < numParts; ++i ) {
				DestroyPart( parts[ i ], bIsRebuild ) ;
			}

			parts.Clear( ) ;
		}

		/// <summary>Destroy the given part and its data.</summary>
		/// <param name="part"></param>
		/// <param name="bIsRebuild"></param>
		internal static void DestroyPart( HEU_PartData part, bool bIsRebuild = false ) {
			part.DestroyAllData( bIsRebuild ) ;
			HEU_GeneralUtility.DestroyImmediate( part ) ;
		}

		// Return is whether or not split attribute exists
		internal static bool ComposeUnityInstanceSplitHierarchy( HEU_SessionBase session, HAPI_NodeId geoID,
																 HAPI_PartId partID, Transform parentTransform,
																 int numInstances,
																 ref Transform[ ] instanceToChildTransform ) {
			string? instanceSplitAttrName = string.Empty ;
			HAPI_AttributeInfo splitAttrInfo = new( ) ;
			
			if ( !HEU_GeneralUtility.GetAttributeInfo( session, 
													   geoID, 
													   partID,
													   HEU_Defines.HEU_INSTANCE_SPLIT_ATTR,
													   ref splitAttrInfo ) ) { return false ; }

			if ( !splitAttrInfo.exists ) return false ;

			string?[] instanceSplitAttr =
				HEU_GeneralUtility.GetAttributeStringData( session, geoID, partID, 
														   HEU_Defines.HEU_INSTANCE_SPLIT_ATTR, 
																ref splitAttrInfo ) ;

			if ( instanceSplitAttr is { Length: > 0 } ) 
				instanceSplitAttrName = instanceSplitAttr[ 0 ] ;

			if ( string.IsNullOrEmpty(instanceSplitAttrName) ) return false ;

			HAPI_AttributeInfo splitAttrValuesInfo = new( ) ;
			string[ ] splitAttrValues =
				HEU_GeneralUtility.GetAttributeDataAsString( session, geoID, partID, 
															 instanceSplitAttrName,
															 ref splitAttrValuesInfo ) ;
			
			if ( !splitAttrValuesInfo.exists || splitAttrValues?.Length != numInstances )
				return false ;
			
			// Instance split: Attribute that splits instances based on an attribute.
			int instanceSplitIndex = 0 ;
			Dictionary< string, int > instanceSplitToChildIndex = new( ) ;
			
			for ( int i = 0; i < splitAttrValues.Length; ++i ) {
				if ( instanceSplitToChildIndex.ContainsKey(splitAttrValues[i]) ) continue ;
				instanceSplitToChildIndex.Add( splitAttrValues[i], instanceSplitIndex++ ) ;
			}
			
			int numRequiredChildren = instanceSplitIndex ;
			List< GameObject > childGameObjects = new( ) ;
			
			HEU_GeneralUtility.ComposeNChildren( parentTransform.gameObject, 
												 numRequiredChildren, 
												 ref childGameObjects,
												 true ) ;
			
			instanceToChildTransform = new Transform[ numInstances ] ;

			// Finally, construct the mapping between instance index and transform
			for ( int i = 0; i < numInstances; ++i ) {
				if ( i >= splitAttrValues.Length ) return false ;
				
				string splitAttrValue = splitAttrValues[ i ] ;
				if ( !instanceSplitToChildIndex.ContainsKey(splitAttrValue) )
					return false ;
				
				int childIndex = instanceSplitToChildIndex[ splitAttrValue ] ;
				if ( parentTransform.childCount < childIndex ) return false ;

				instanceToChildTransform[ i ] = parentTransform.GetChild( childIndex ) ;
			}

			return true ;
		}

		public bool IsEquivalentTo( HEU_PartData other ) {

			bool bResult = true ;

			string header = "HEU_PartData" ;

			if ( other == null ) {
				HEU_Logger.LogError( header + " Not equivalent" ) ;
				return false ;
			}

			// Skip _partId, _objectNodeID, _geoId

			//HEU_TestHelpers.AssertTrueLogEquivalent(this._partName, other._partName, ref bResult, header, "_partName");
			HEU_TestHelpers.AssertTrueLogEquivalent( _partType, other._partType, ref bResult, header,
													 "_partType" ) ;

			// Skip HEU_GeoNode
			HEU_TestHelpers.AssertTrueLogEquivalent( _isAttribInstancer, other._isAttribInstancer, ref bResult,
													 header, "_isAttribInstancer" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _isPartInstanced, other._isPartInstanced, ref bResult, header,
													 "_isPartInstanced" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _partPointCount, other._partPointCount, ref bResult, header,
													 "_partPointCount" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _isObjectInstancer, other._isObjectInstancer, ref bResult,
													 header, "_isObjectInstancer" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _objectInstancesGenerated, other._objectInstancesGenerated,
													 ref bResult, header, "_objectInstanceGenerated" ) ;

			HEU_TestHelpers.AssertTrueLogEquivalent( _objectInstanceInfos, other._objectInstanceInfos, ref bResult,
													 header, "_objectInstanceInfo" ) ;

			HEU_TestHelpers.AssertTrueLogEquivalent( _terrainOffsetPosition, other._terrainOffsetPosition,
													 ref bResult, header, "_terrainOffsetPosition" ) ;

			HEU_TestHelpers.AssertTrueLogEquivalent( _isPartEditable, other._isPartEditable, ref bResult, header,
													 "_isPartEditable" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _partOutputType, other._partOutputType, ref bResult, header,
													 "_partOutputType" ) ;

			HEU_TestHelpers.AssertTrueLogEquivalent( _curve, other._curve, ref bResult, header, "_curve" ) ;


			HEU_TestHelpers.AssertTrueLogEquivalent( _attributesStore, other._attributesStore, ref bResult, header,
													 "_attributesStore" ) ;

			HEU_TestHelpers.AssertTrueLogEquivalent( _haveInstancesBeenGenerated,
													 other._haveInstancesBeenGenerated, ref bResult, header,
													 "_haveInstancesBeenGenerated" ) ;
			HEU_TestHelpers.AssertTrueLogEquivalent( _meshVertexCount, other._meshVertexCount, ref bResult, header,
													 "_meshVertexCount" ) ;

			HEU_TestHelpers.AssertTrueLogEquivalent( _generatedOutput, other._generatedOutput, ref bResult, header,
													 "_generatedOutput" ) ;

			return bResult ;
		}
	}

} // HoudiniEngineUnity
