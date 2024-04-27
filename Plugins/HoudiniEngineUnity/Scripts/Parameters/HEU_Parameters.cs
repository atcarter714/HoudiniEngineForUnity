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

using System ;
using System.Collections.Generic ;
using UnityEngine ;

#region Type Aliases
// Typedefs (copy these from HEU_Common.cs)
using HAPI_NodeId = System.Int32 ;
using HAPI_StringHandle = System.Int32 ;
using HAPI_ParmId = System.Int32 ;
#endregion


namespace HoudiniEngineUnity {
	

	/// <summary>
	/// Holds all parameter data for an asset.
	/// </summary>
	public class HEU_Parameters: ScriptableObject,
								 IHEU_Parameters,
								 IHEU_HoudiniAssetSubcomponent,
								 IEquivable< HEU_Parameters > {
		// PUBLIC FIELDS ================================================================

		/// <inheritdoc />
		public HEU_HoudiniAsset? ParentAsset => _parentAsset ;

		/// <inheritdoc />
		public bool ShowParameters {
			get => _showParameters ;
			set => _showParameters = value ;
		}

		/// <inheritdoc />
		public HAPI_NodeId NodeID {
			get => _nodeID ;
			internal set => _nodeID = value ;
		}

		/// <inheritdoc />
		public List< int > RootParameters => _rootParameters ;

		/// <inheritdoc />
		public List< HEU_ParameterModifier > ParameterModifiers => _parameterModifiers ;

		// =======================================================================

		
		//	DATA ------------------------------------------------------------------------------------------------------
		[SerializeField] HAPI_NodeId _nodeID ;

		[SerializeField] internal string _uiLabel = "ASSET PARAMETERS" ;

		[SerializeField] int[]? _paramInts ;
		[SerializeField] float[]? _paramFloats ;
		[SerializeField] string[]? _paramStrings ;
		[SerializeField] HAPI_ParmChoiceInfo[]? _paramChoices ;

		// Hierarychy list (for UI)		
		[SerializeField] List< int > _rootParameters = new( ) ;
		[SerializeField] List< HEU_ParameterData > _parameterList = new( ) ;
		[SerializeField] List< HEU_ParameterModifier > _parameterModifiers = new( ) ;

		// If true, need to recreate the parameters by querying HAPI.
		// Should be called after inserting or removing an multiparm instance.
		[SerializeField] bool _regenerateParameters ;

		internal bool RequiresRegeneration {
			get => _regenerateParameters ;
			private set => _regenerateParameters = value ;
		}

		// Cache the parameter preset. This is reloaded back into Houdini after scene deserialization.
		[SerializeField] byte[]? _presetData ;

		internal byte[]? GetPresetData( ) => _presetData ;
		internal void SetPresetData( byte[] data ) => _presetData = data ;

		// Cache the defaul parameter preset when HDA is initially loaded. Used when resetting parameters.
		[SerializeField] byte[]? _defaultPresetData ;
		internal byte[]? GetDefaultPresetData( ) => _defaultPresetData ;

		// Specifies whether the parameters are in a valid state to interact with Houdini
		[SerializeField] bool _validParameters ;


		// Disable the warning for unused variable. We're accessing this as a SerializedProperty.
#pragma warning disable 0414

		[SerializeField] [HideInInspector] bool _showParameters = true ;

#pragma warning restore 0414

		//[SerializeField]
		// Flag that the UI needs to be recached. Should be done whenever any of the parameters change.
		internal bool RecacheUI { get ; set ; } = true ;

		[SerializeField] HEU_HoudiniAsset? _parentAsset ;


		// PUBLIC FUNCTIONS ===============================================================================================

		/// <inheritdoc />
		public bool AreParametersValid( ) {
			return _validParameters ;
		}

		/// <inheritdoc />
		public HEU_SessionBase? GetSession( ) => _parentAsset != null
													 ? _parentAsset.GetAssetSession( true )
													 : HEU_SessionManager.GetOrCreateDefaultSession( ) ;

		/// <inheritdoc />
		public void Recook( ) {
			//RequiresRegeneration = true;

			if ( _parentAsset ) _parentAsset!.RequestCook( ) ;
		}

		/// <inheritdoc />
		public List< HEU_ParameterData > GetParameters( ) {
			return _parameterList ;
		}

		/// <inheritdoc />
		public HEU_ParameterData? GetParameter( int listIndex ) {
			if ( listIndex >= 0 && listIndex < _parameterList.Count ) {
				return _parameterList[ listIndex ] ;
			}

			return null ;
		}

		/// <inheritdoc />
		public HEU_ParameterData? GetParameter( string? parmName ) {
			foreach ( HEU_ParameterData parameterData in _parameterList )
				if ( parameterData._name.Equals( parmName ) )
					return parameterData ;
			return null ;
		}

		public HEU_ParameterData? GetParameterWithParmID( HAPI_ParmId parmID ) {
			foreach ( HEU_ParameterData parameterData in _parameterList ) {
				if ( parameterData.ParmID == parmID ) {
					return parameterData ;
				}
			}

			return null ;
		}

		/// <inheritdoc />
		public void RemoveParameter( int listIndex ) {
			if ( listIndex > -1 && listIndex < _parameterList.Count )
				_parameterList.RemoveAt( listIndex ) ;
		}

		/// <inheritdoc />
		public bool HaveParametersChanged( ) {
			if ( !AreParametersValid( ) )
				return false ;

			// For the auto-cook on mouse release option, ignore changed parameters until it has been released
			if ( HEU_PluginSettings.CookOnMouseUp
				 && ParentAsset && ParentAsset!.PendingAutoCookOnMouseRelease )
				return false ;

			foreach ( HEU_ParameterData parameterData in _parameterList ) {
				// Compare parameter data value against the value from arrays

				switch ( parameterData._parmInfo.type ) {
					case HAPI_ParmType.HAPI_PARMTYPE_INT:
					case HAPI_ParmType.HAPI_PARMTYPE_BUTTON:
					{
						if ( !HEU_GeneralUtility.DoArrayElementsMatch( _paramInts,
																	   parameterData._parmInfo.intValuesIndex,
																	   parameterData._intValues, 0,
																	   parameterData.ParmSize ) ) {
							return true ;
						}

						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_FLOAT:
					{
						if ( !HEU_GeneralUtility.DoArrayElementsMatch( _paramFloats,
																	   parameterData._parmInfo.floatValuesIndex,
																	   parameterData._floatValues, 0,
																	   parameterData.ParmSize ) ) {
							return true ;
						}

						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_STRING:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_DIR:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_GEO:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_IMAGE:
					{
						if ( !HEU_GeneralUtility.DoArrayElementsMatch( _paramStrings,
																	   parameterData._parmInfo.stringValuesIndex,
																	   parameterData._stringValues, 0,
																	   parameterData.ParmSize ) ) {
							return true ;
						}

						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_TOGGLE: {
						if ( _paramInts?[ parameterData._parmInfo.intValuesIndex ] !=
							 Convert.ToInt32( parameterData._toggle ) ) return true ;
						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_COLOR: {

						float r1 = _paramFloats?[ parameterData._parmInfo.floatValuesIndex ] ?? 0f,
							  g1 = _paramFloats?[ parameterData._parmInfo.floatValuesIndex + 1 ] ?? 0f,
							  b1 = _paramFloats?[ parameterData._parmInfo.floatValuesIndex + 2 ] ?? 0f,
							  a1 = parameterData.ParmSize is 4
								   ? _paramFloats?[ parameterData._parmInfo.floatValuesIndex + 3 ] ?? 1.0f
										: 1.0f ;
						if ( !Mathf.Approximately( r1, parameterData._color[ 0 ] ) ||
							 !Mathf.Approximately( g1, parameterData._color[ 1 ] ) ||
							 !Mathf.Approximately( b1, parameterData._color[ 2 ] ) ||
							 !Mathf.Approximately( a1, parameterData._color[ 3 ] ) ) {
							return true ;
						}
						
						/* These kinds of direct floating-point comparisons are not reliable/safe and should be avoided ...
						 if ( _paramFloats[ parameterData._parmInfo.floatValuesIndex ] != parameterData._color[ 0 ]
							 || _paramFloats[ parameterData._parmInfo.floatValuesIndex + 1 ] != parameterData._color[ 1 ]
							 || _paramFloats[ parameterData._parmInfo.floatValuesIndex + 2 ] != parameterData._color[ 2 ]
							 || ( parameterData.ParmSize is 4
								  && _paramFloats[ parameterData._parmInfo.floatValuesIndex + 3 ] != parameterData._color[ 3 ] ) ) {
							return true ;
						}
						*/

						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_NODE:
					{
						if ( parameterData._paramInputNode != null &&
							 ( parameterData._paramInputNode.RequiresUpload ||
							   parameterData._paramInputNode.HasInputNodeTransformChanged( ) ) ) {
							return true ;
						}

						break ;
					}
					// TODO: add support for rest of types
				}
			}
			
			return false ;
		}
		
		/// <inheritdoc />
		public bool ResetAllToDefault( bool bRecookAsset = false ) {
			HEU_SessionBase? session = GetSession( ) ;
			if ( session is null ) return false ;

			ResetAllToDefault( session ) ;
			if ( bRecookAsset ) Recook( ) ;
			return true ;
		}

		/// <inheritdoc />
		public bool SetFloatParameterValue( string? parameterName, float value,
											int     atIndex = 0,   bool  bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsFloat( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( paramData._floatValues is null || atIndex >= paramData._floatValues.Length ) {
				HEU_Logger.LogWarningFormat( "Parameter tuple index {0} is out of range (tuple size == {0}).",
											 atIndex, paramData?._floatValues?.Length ?? 0 ) ;
				return false ;
			}

			paramData._floatValues[ atIndex ] = value ;
			if ( bRecookAsset ) Recook( ) ;
			return true ;
		}

		/// <inheritdoc />
		public bool GetFloatParameterValue( string? parameterName, out float value, int atIndex = 0 ) {
			value = 0 ;
			
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsFloat( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( paramData._floatValues is null || atIndex >= paramData._floatValues.Length ) {
				HEU_Logger.LogWarningFormat( "Parameter tuple index {0} is out of range (tuple size == {0}).",
											 atIndex, paramData?._floatValues?.Length ?? 0 ) ;
				return false ;
			}

			value = paramData._floatValues[ atIndex ] ;
			return true ;
		}

		/// <inheritdoc />
		public bool SetFloatParameterValues( string? parameterName, float[]? values, bool bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsFloat( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( values is null || values.Length != (paramData?._floatValues?.Length ?? 0) ) {
				HEU_Logger.LogWarningFormat( "Incorrect number of values for {0}: Expected: {1}, Actual: {2}",
											 parameterName,
											 paramData?._floatValues?.Length ?? 0, 
											 values?.Length ?? 0 ) ;
				return false ;
			}

			// Copy by value
			for ( int i = 0; i < paramData?._floatValues?.Length; ++i )
				paramData._floatValues[ i ] = values[ i ] ;
			
			if ( bRecookAsset ) Recook( ) ;
			return true ;
		}

		/// <inheritdoc />
		public bool GetFloatParameterValues( string? parameterName, out float[]? values ) {
			values = null ;

			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsFloat( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}
			
			values = new float[ paramData?._floatValues?.Length ?? 0 ] ;

			// Copy by value
			for ( int i = 0; i < paramData?._floatValues?.Length; ++i )
				values[ i ] = paramData._floatValues[ i ] ;

			return true ;
		}

		/// <inheritdoc />
		public bool SetColorParameterValue( string? parameterName, Color value, bool bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsColor( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			paramData._color = value ;

			if ( bRecookAsset ) Recook( ) ;

			return true ;
		}

		/// <inheritdoc />
		public bool GetColorParameterValue( string? parameterName, out Color value ) {
			value = Color.white ;
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;

			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsColor( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			value = paramData._color ;
			return true ;
		}

		/// <inheritdoc />
		public bool SetIntParameterValue( string? parameterName,
										  int     value, int atIndex = 0,
										  bool    bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsInt( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( paramData._intValues is null
				 || atIndex >= paramData._intValues.Length ) {
				HEU_Logger.LogWarningFormat( "Parameter tuple index {0} is out of range (tuple size == {0}).",
											 atIndex, paramData?._intValues?.Length ?? 0 ) ;
				return false ;
			}

			paramData._intValues[ atIndex ] = value ;
			if ( bRecookAsset ) Recook( ) ;
			return true ;
		}

		/// <inheritdoc />
		public bool GetIntParameterValue( string? parameterName, out int value, int atIndex = 0 ) {
			value = 0 ;
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsInt( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( paramData._intValues is null || atIndex >= paramData._intValues.Length ) {
				HEU_Logger.LogWarningFormat( "Parameter tuple index {0} is out of range (tuple size == {0}).", atIndex,
											 paramData?._intValues?.Length ?? 0 ) ;
				return false ;
			}

			value = paramData._intValues[ atIndex ] ;
			return true ;
		}

		/// <inheritdoc />
		public bool SetIntParameterValues( string? parameterName, int[]? values, bool bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsInt( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( values is null || values.Length != (paramData?._intValues?.Length ?? 0) ) {
				HEU_Logger.LogWarningFormat( "Incorrect number of values for {0}: Expected: {1}, Actual: {2}", parameterName,
											 paramData?._intValues?.Length ?? 0, values?.Length ?? 0 ) ;
				return false ;
			}

			// Copy by value
			for ( int i = 0; i < paramData?._intValues?.Length; ++i ) {
				paramData._intValues[ i ] = values[ i ] ;
			}

			if ( bRecookAsset ) Recook( ) ;

			return true ;

		}

		/// <inheritdoc />
		public bool GetIntParameterValues( string? parameterName, out int[] values ) {
			values = null ;

			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsFloat( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			values = new int[ paramData._intValues.Length ] ;

			// Copy by value
			for ( int i = 0; i < paramData._floatValues.Length; ++i ) {
				values[ i ] = paramData._intValues[ i ] ;
			}

			return true ;
		}

		/// <inheritdoc />
		public bool SetChoiceParameterValue( string? parameterName, int value, bool bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsInt( ) ||
				 paramData._parmInfo.scriptType == HAPI_PrmScriptType.HAPI_PRM_SCRIPT_TYPE_BUTTONSTRIP ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( value <= 0 || value >= paramData._choiceIntValues.Length ) {
				HEU_Logger.LogWarningFormat( "{0}: choice is not the correct index!.", parameterName ) ;
				return false ;
			}

			paramData._intValues[ 0 ] = paramData._choiceIntValues[ value ] ;

			if ( bRecookAsset ) Recook( ) ;

			return true ;
		}

		/// <inheritdoc />
		public bool GetChoiceParameterValue( string? parameterName, out int value ) {
			value = 0 ;
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsInt( ) ||
				 paramData._parmInfo.scriptType == HAPI_PrmScriptType.HAPI_PRM_SCRIPT_TYPE_BUTTONSTRIP ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( value <= 0 || value >= paramData._choiceIntValues.Length ) {
				HEU_Logger.LogWarningFormat( "{0}: choice is not the correct index!.", parameterName ) ;
				return false ;
			}

			value = paramData._intValues[ 0 ] ;

			return true ;
		}

		/// <inheritdoc />
		public bool SetBoolParameterValue( string? parameterName, bool value, int atIndex = 0,
										   bool    bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsToggle( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			paramData._toggle = value ;

			if ( bRecookAsset ) Recook( ) ;

			return true ;
		}

		/// <inheritdoc />
		public bool GetBoolParameterValue( string? parameterName, out bool value, int atIndex = 0 ) {
			value = false ;

			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsToggle( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			value = paramData._toggle ;
			return true ;
		}

		/// <inheritdoc />
		public bool SetStringParameterValue( string? parameterName, string value,
											 int     atIndex = 0,   bool   bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsString() ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( paramData._stringValues is null
				 || atIndex >= (paramData?._stringValues?.Length ?? 0) ) {
				HEU_Logger.LogWarningFormat( "Parameter tuple index {0} is out of range (tuple size == {0}).", 
											 atIndex, paramData?._stringValues?.Length ?? 0 ) ;
				return false ;
			}

			if ( paramData is { _stringValues: { Length: > 0, }, } ) 
				paramData._stringValues[ atIndex ] = value ;
			if ( bRecookAsset ) Recook( ) ;
			return true ;
		}
		
		/// <inheritdoc />
		public bool GetStringParameterValue( string? parameterName, out string value, int atIndex = 0 ) {
			value = string.Empty ;
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsString( ) && !paramData.IsPathFile( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( paramData?._stringValues is null 
				 || atIndex >= (paramData?._stringValues?.Length ?? 0) ) {
				HEU_Logger.LogWarningFormat( "Parameter tuple index {0} is out of range (tuple size == {0}).",
											 atIndex, paramData?._stringValues?.Length ?? 0 ) ;
				return false ;
			}
			
			value = paramData!._stringValues![ atIndex ] ;
			return true ;
		}

		/// <inheritdoc />
		public bool SetStringParameterValues( string? parameterName, string[]? values, bool bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsString( ) && !paramData.IsPathFile( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( values is null || values.Length != (paramData?._stringValues?.Length ?? 0) ) {
				HEU_Logger.LogWarningFormat( "Incorrect number of values for {0}: Expected: {1}, Actual: {2}",
											 parameterName, paramData?._stringValues?.Length ?? 0, values?.Length ?? 0 ) ;
				return false ;
			}

			// Copy by value
			if ( paramData?._stringValues is not { Length: > 0, } ) {
				HEU_Logger.LogWarningFormat( "Parameter {0} has no string values.", parameterName ) ;
				return false ;
			}
			
			for ( int i = 0; i < paramData?._intValues?.Length; ++i )
				paramData._stringValues[ i ] = values[ i ] ;

			if ( bRecookAsset ) Recook( ) ;
			return true ;
		}

		/// <inheritdoc />
		public bool GetStringParameterValues( string? parameterName, out string[]? values ) {
			values = null ;

			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsString( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			values = new string[ paramData?._stringValues?.Length ?? 0 ] ;

			// Copy by value
			for ( int i = 0; i < paramData?._stringValues?.Length; ++i )
				values[ i ] = paramData._stringValues[ i ] ;

			return true ;
		}

		/// <inheritdoc />
		public bool SetAssetRefParameterValue( string? parameterName, GameObject value, int atIndex = 0,
											   bool    bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( paramData._paramInputNode == null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( atIndex < paramData._paramInputNode.NumInputEntries( ) ) {
				paramData._paramInputNode.SetInputEntry( atIndex, value ) ;
			}
			else {
				paramData._paramInputNode.AddInputEntryAtEnd( value ) ;
			}

			paramData._paramInputNode.RequiresUpload = true ;
			if ( bRecookAsset ) Recook( ) ;

			return true ;
		}

		/// <inheritdoc />
		public bool SetAssetRefParameterValues( string? parameterName, GameObject[] values, bool bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( paramData._paramInputNode == null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			for ( int i = 0; i < values.Length; ++i ) {
				if ( i < paramData._paramInputNode.NumInputEntries( ) ) {
					paramData._paramInputNode.SetInputEntry( i, values[ i ] ) ;
				}
				else {
					paramData._paramInputNode.AddInputEntryAtEnd( values[ i ] ) ;
				}
			}

			if ( bRecookAsset ) Recook( ) ;

			return true ;

		}

		/// <inheritdoc />
		public bool GetAssetRefParameterValue( string? parameterName, out GameObject? value, int atIndex = 0 ) {
			value = null ;

			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData._paramInputNode ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			value = paramData!._paramInputNode!.GetInputEntryGameObject( atIndex ) ;
			return true ;
		}

		/// <inheritdoc />
		public bool GetAssetRefParameterValues( string? parameterName, out GameObject[]? values ) {
			values = null ;

			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData._paramInputNode ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			int numEntries = paramData!._paramInputNode!.NumInputEntries( ) ;
			values = new GameObject[ numEntries ] ;
			
			for ( int i = 0; i < numEntries; ++i )
				values[ i ] = paramData._paramInputNode.GetInputEntryGameObject( i ) ;

			return true ;
		}

		/// <inheritdoc />
		public bool SetRampParameterNumPoints( string? parameterName, int numPoints, bool bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsRamp( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}
			
			int oldCount = paramData._parmInfo.instanceCount ;
			if ( oldCount == numPoints ) return true ;

			int numParamsPerPoint = paramData._parmInfo.instanceLength ;
			int pointID = ( oldCount - 1 ) * numParamsPerPoint ;

			if ( pointID >= paramData?._childParameterIDs?.Count ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}

			int parmIndex = paramData!._childParameterIDs![ pointID ] ;

			if ( numPoints > oldCount ) {
				int numAdd = numPoints - oldCount ;
				HEU_ParameterData childParameter = _parameterList[ parmIndex ] ;
				int instanceIndex  = childParameter._parmInfo.instanceNum ;
				InsertInstanceToMultiParm( paramData._unityIndex, instanceIndex, numAdd ) ;
			}
			else {
				int numRemove = oldCount - numPoints ;
				HEU_ParameterData childParameter = _parameterList[ parmIndex ] ;
				int instanceIndex = childParameter._parmInfo.instanceNum ;
				RemoveInstancesFromMultiParm( paramData._unityIndex, instanceIndex, numRemove ) ;
			}

			if ( bRecookAsset ) Recook( ) ;
			return true ;
		}

		/// <inheritdoc />
		public bool GetRampParameterNumPoints( string? parameterName, out int numPoints ) {
			numPoints = 0 ;

			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsRamp( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			numPoints = paramData._parmInfo.instanceCount ;
			return true ;
		}

		/// <inheritdoc />
		public bool SetFloatRampParameterPointValue(
			string? parameterName,
			int pointIndex,
			float pointPosition,
			float pointValue,
			HEU_HoudiniRampInterpolationTypeWrapper interpolationType
				= HEU_HoudiniRampInterpolationTypeWrapper.LINEAR,
			bool bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsFloatRamp( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( paramData._childParameterIDs == null || paramData._childParameterIDs.Count < 3 ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}

			// Return true if we can set at least one of them
			bool bResult = true ;

			// For ramps, the number of instances is the number of points in the ramp
			// Each point can then have a number of parameters.
			int numPoints = paramData._parmInfo.instanceCount ;
			int numParamsPerPoint = paramData._parmInfo.instanceLength ;
			int pointID = pointIndex * numParamsPerPoint ;

			if ( pointID >= (paramData?._childParameterIDs?.Count ?? 0) ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}

			int parmIndex = paramData._childParameterIDs[ pointID ] ;
			if ( parmIndex + 2 >= _parameterList.Count ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}

			HEU_ParameterData paramDataPosition = _parameterList[ parmIndex ] ;
			if ( paramDataPosition is not null 
				 && paramDataPosition.IsFloat( )
				 && paramDataPosition?._floatValues?.Length > 0 ) {
				paramDataPosition._floatValues[ 0 ] = pointPosition ;
			}
			else bResult = false ;

			HEU_ParameterData paramDataValue = _parameterList[ parmIndex + 1 ] ;
			if ( paramDataValue != null && paramDataValue.IsFloat( )
										&& paramDataValue?._floatValues?.Length > 0 )
				paramDataValue._floatValues[ 0 ] = pointValue ;
			
			else bResult = false ;

			HEU_ParameterData paramDataInterpolationType = _parameterList[ parmIndex + 2 ] ;
			if ( paramDataInterpolationType is { _intValues: { Length: > 0, }, } )
				paramDataInterpolationType._intValues[ 0 ] = (int)interpolationType ;
			else bResult = false ;


			if ( !bResult ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}
			if ( bRecookAsset ) Recook( ) ;
			return true ;
		}

		/// <inheritdoc />
		public bool GetFloatRampParameterPointValue( string? parameterName, int pointIndex,
													 out float pointPosition, out float pointValue,
													 out HEU_HoudiniRampInterpolationTypeWrapper interpolationType ) {
			pointPosition = 0 ;
			pointValue = 0 ;
			interpolationType = HEU_HoudiniRampInterpolationTypeWrapper.CONSTANT ;
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}
			if ( !paramData.IsFloatRamp( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}
			if ( paramData is { _childParameterIDs: { Count: < 3, }, } ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}

			// Return true if we can set all of them
			bool bResult = true ;

			// For ramps, the number of instances is the number of points in the ramp
			// Each point can then have a number of parameters.
			int numPoints = paramData._parmInfo.instanceCount ;
			int numParamsPerPoint = paramData._parmInfo.instanceLength ;
			int pointID = pointIndex * numParamsPerPoint ;

			if ( pointID >= paramData?._childParameterIDs?.Count ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}

			int parmIndex = paramData?._childParameterIDs?[ pointID ] ?? -1 ;
			if ( parmIndex + 2 >= _parameterList.Count ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}

			HEU_ParameterData paramDataPosition = _parameterList[ parmIndex ] ;
			if ( paramDataPosition != null && paramDataPosition.IsFloat( )
										   && paramDataPosition?._floatValues?.Length > 0 )
				pointPosition = paramDataPosition._floatValues[ 0 ] ;
			else bResult = false ;
			
			HEU_ParameterData paramDataValue = _parameterList[ parmIndex + 1 ] ;
			if ( paramDataValue != null && paramDataValue.IsFloat( )
										&& paramDataValue?._floatValues?.Length > 0 )
				pointValue = paramDataValue._floatValues[ 0 ] ;
			else bResult = false ;

			HEU_ParameterData paramDataInterpolationType = _parameterList[ parmIndex + 2 ] ;
			if ( paramDataInterpolationType is { _intValues: { Length: > 0, }, } )
				interpolationType =
					(HEU_HoudiniRampInterpolationTypeWrapper)paramDataInterpolationType._intValues[ 0 ] ;
			else bResult = false ;
			
			if ( bResult ) return true ;
			HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
			return false ;
		}

		/// <inheritdoc />
		public bool SetFloatRampParameterPoints( string?                      parameterName,
												 HEU_FloatRampPointWrapper[ ] rampPoints,
												 bool                         bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}
			if ( !paramData.IsFloatRamp( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			for ( int i = 0; i < rampPoints.Length; ++i )
				SetFloatRampParameterPointValue( parameterName, i, rampPoints[ i ].Position,
												 rampPoints[ i ].Value, rampPoints[ i ].Interpolation ) ;

			if ( bRecookAsset ) Recook( ) ;
			return true ;
		}

		/// <inheritdoc />
		public bool GetFloatRampParameterPoints( string?                           parameterName,
												 out HEU_FloatRampPointWrapper[ ]? rampPoints ) {
			rampPoints = null ;
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsFloatRamp( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			int numPoints = paramData._parmInfo.instanceCount ;
			rampPoints = new HEU_FloatRampPointWrapper[ numPoints ] ;

			for ( int i = 0; i < numPoints; ++i ) {
				GetFloatRampParameterPointValue( parameterName, i, out float pointPosition, out float pointValue,
												 out HEU_HoudiniRampInterpolationTypeWrapper interpolationType ) ;

				rampPoints[ i ] = new( pointPosition, pointValue, interpolationType ) ;
			}

			return true ;
		}

		/// <inheritdoc />
		public bool SetColorRampParameterPointValue( string? parameterName,
													 int     pointIndex,
													 float   pointPosition,
													 Color   pointValue,
													 HEU_HoudiniRampInterpolationTypeWrapper interpolationType =
														 HEU_HoudiniRampInterpolationTypeWrapper.LINEAR,
													 bool bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsColorRamp( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( paramData._childParameterIDs == null || paramData._childParameterIDs.Count < 3 ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}

			// Return true if we can set all of them
			bool bResult = true ;

			// For ramps, the number of instances is the number of points in the ramp
			// Each point can then have a number of parameters.
			int numPoints         = paramData._parmInfo.instanceCount ;
			int numParamsPerPoint = paramData._parmInfo.instanceLength ;
			int pointID           = pointIndex * numParamsPerPoint ;

			if ( pointID >= (paramData?._childParameterIDs?.Count ?? 0) ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}

			int parmIndex = paramData?._childParameterIDs?[ pointID ] ?? -1 ;
			if ( parmIndex + 2 >= _parameterList.Count ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}

			HEU_ParameterData paramDataPosition = _parameterList[ parmIndex ] ;
			if ( paramDataPosition != null && paramDataPosition.IsFloat( )
										   && paramDataPosition?._floatValues?.Length > 0 )
				paramDataPosition._floatValues[ 0 ] = pointPosition ;
			else bResult = false ;

			HEU_ParameterData paramDataValue = _parameterList[ parmIndex + 1 ] ;
			if ( paramDataValue != null && paramDataValue.IsColor( ) )
				paramDataValue._color = pointValue ;
			else bResult = false ;

			HEU_ParameterData paramDataInterpolationType = _parameterList[ parmIndex + 2 ] ;
			if ( paramDataInterpolationType is { _intValues: { Length: > 0, }, } )
				paramDataInterpolationType._intValues[ 0 ] = (int)interpolationType ;
			else bResult = false ;
			
			if ( !bResult ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}
			if ( bRecookAsset ) Recook( ) ;
			return true ;
		}

		/// <inheritdoc />
		public bool GetColorRampParameterPointValue( string? parameterName, int pointIndex,
													 out float pointPosition, out Color pointValue,
													 out HEU_HoudiniRampInterpolationTypeWrapper interpolationType ) {
			pointPosition = 0 ;
			pointValue = Color.black ;
			interpolationType = HEU_HoudiniRampInterpolationTypeWrapper.CONSTANT ;
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}
			if ( !paramData.IsColorRamp( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}
			if ( paramData is { _childParameterIDs: { Count: < 3, }, } ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}
			
			// Return true if we can set all of them
			bool bResult = true ;

			// For ramps, the number of instances is the number of points in the ramp
			// Each point can then have a number of parameters.
			int numPoints         = paramData._parmInfo.instanceCount ;
			int numParamsPerPoint = paramData._parmInfo.instanceLength ;
			int pointID           = pointIndex * numParamsPerPoint ;

			if ( pointID >= (paramData?._childParameterIDs?.Count ?? 0) ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}

			int parmIndex = paramData?._childParameterIDs?[ pointID ] ?? -1 ;
			if ( parmIndex + 2 >= _parameterList.Count ) {
				HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
				return false ;
			}

			HEU_ParameterData paramDataPosition = _parameterList[ parmIndex ] ;
			if ( paramDataPosition != null && paramDataPosition.IsFloat( ) &&
				 paramDataPosition?._floatValues?.Length > 0 )
				pointPosition = paramDataPosition._floatValues[ 0 ] ;
			else bResult = false ;

			HEU_ParameterData paramDataValue = _parameterList[ parmIndex + 1 ] ;
			if ( paramDataValue != null && paramDataValue.IsColor( ) )
				pointValue = paramDataValue._color ;
			else bResult   = false ;

			HEU_ParameterData paramDataInterpolationType = _parameterList[ parmIndex + 2 ] ;
			if ( paramDataInterpolationType is not null
				 && paramDataInterpolationType?._intValues?.Length > 0 ) {
				interpolationType =
					(HEU_HoudiniRampInterpolationTypeWrapper)paramDataInterpolationType._intValues[ 0 ] ;
			}
			else bResult = false ;

			if ( bResult ) return true ;
			HEU_Logger.LogWarningFormat( "{0}: sub-parameters are not found.", parameterName ) ;
			return false ;
		}

		/// <inheritdoc />
		public bool SetColorRampParameterPoints( string?                      parameterName,
												 HEU_ColorRampPointWrapper[ ] rampPoints,
												 bool                         bRecookAsset = false ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}
			if ( !paramData.IsColorRamp( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			for ( int i = 0; i < rampPoints.Length; ++i ) {
				SetColorRampParameterPointValue( parameterName, i, rampPoints[ i ].Position,
												 rampPoints[ i ].Value, rampPoints[ i ].Interpolation ) ;
			}

			if ( bRecookAsset ) Recook( ) ;
			return true ;
		}

		/// <inheritdoc />
		public bool GetColorRampParameterPoints( string?                           parameterName,
												 out HEU_ColorRampPointWrapper[ ]? rampPoints ) {
			rampPoints = null ;
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}
			if ( !paramData.IsColorRamp( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			int numPoints = paramData._parmInfo.instanceCount ;
			rampPoints = new HEU_ColorRampPointWrapper[ numPoints ] ;

			for ( int i = 0; i < numPoints; ++i ) {
				GetColorRampParameterPointValue( parameterName, i, out float pointPosition, out Color pointValue,
												 out HEU_HoudiniRampInterpolationTypeWrapper interpolationType ) ;

				rampPoints[ i ] = new( pointPosition, pointValue, interpolationType ) ;
			}

			return true ;
		}

		/// <inheritdoc />
		public bool TriggerButtonParameter( string? parameterName ) {
			HEU_ParameterData? paramData = GetParameter( parameterName ) ;
			if ( paramData is null ) {
				HEU_Logger.LogWarningFormat( "{0}: is not found.", parameterName ) ;
				return false ;
			}

			if ( !paramData.IsButton( ) ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			if ( paramData._intValues is null || paramData._intValues.Length == 0 ) {
				HEU_Logger.LogWarningFormat( "{0}: is not the correct type!.", parameterName ) ;
				return false ;
			}

			paramData._intValues[ 0 ] = paramData._intValues[ 0 ] == 0 ? 1 : 0 ;
			Recook( ) ;

			return true ;
		}

		/// <inheritdoc />
		public bool SetParameterTuples( Dictionary< string, HEU_ParameterTupleWrapper > parameterTuples, 
											bool bRecook = true ) {
			foreach ( (string? nameStr, HEU_ParameterTupleWrapper? parameterTuple) in parameterTuples ) {
				HEU_ParameterData? parameterData = GetParameter( nameStr ) ;
				if ( parameterTuple is null || parameterData is null ) {
					HEU_Logger.LogWarningFormat( "{0}: is not found.", nameStr ) ;
					return false ;
				}
				
				bool bResult = true ;
				if ( parameterTuple._boolValues is { Length: > 0, } ) {
					int tupleSize = parameterTuple._boolValues.Length ;
					for ( int i = 0; i < tupleSize; ++i )
						bResult &= SetBoolParameterValue( nameStr, parameterTuple._boolValues[ i ], i ) ;
				}
				if ( parameterTuple._intValues is { Length: > 0, } ) {
					int tupleSize = parameterTuple._intValues.Length ;
					for ( int i = 0; i < tupleSize; ++i )
						bResult &= SetIntParameterValue( nameStr, parameterTuple._intValues[ i ], i ) ;
				}
				if ( parameterTuple._floatValues is { Length: > 0, } ) {
					int tupleSize = parameterTuple._floatValues.Length ;
					for ( int i = 0; i < tupleSize; ++i )
						bResult &= SetFloatParameterValue( nameStr, parameterTuple._floatValues[ i ], i ) ;
				}
				if ( parameterTuple._stringValues is { Length: > 0, } ) {
					int tupleSize = parameterTuple._stringValues.Length ;
					for ( int i = 0; i < tupleSize; ++i )
						bResult &= SetStringParameterValue( nameStr, parameterTuple._stringValues[ i ], i ) ;
				}
				
				if ( bResult ) continue ;
				HEU_Logger.LogWarningFormat( "{0}: failed to set parameter!", nameStr ) ;
				return false ;
			}

			return true ;
		}

		/// <inheritdoc />
		public bool GetParameterTuples( out Dictionary< string, HEU_ParameterTupleWrapper > parameterTuples ) {
			parameterTuples = new( ) ;
			List< HEU_ParameterData > parameters = GetParameters( ) ;
			int numParameters = parameters.Count ;

			for ( int i = 0; i < numParameters; ++i ) {
				HEU_ParameterData parameter = parameters[ i ] ;
				if ( parameter is null ) {
					HEU_Logger.LogWarning( $"{nameof(HEU_Parameters)} :: " +
												$"Parameter is null at index: {i}!" ) ;
					continue ;
				}
				
				int tupleSize = parameter._parmInfo.size ;
				string? nameStr = parameter._name ;
				if ( string.IsNullOrEmpty( nameStr ) ) {
					HEU_Logger.LogWarning( $"{nameof(HEU_Parameters)} :: " +
												$"Parameter name is null or empty!" ) ;
					continue ;
				}
				
				bool bSkipped = false ;
				HEU_ParameterTupleWrapper parameterTuple = new( ) ;

				switch ( parameter._parmInfo.type ) {
					case HAPI_ParmType.HAPI_PARMTYPE_INT:
					case HAPI_ParmType.HAPI_PARMTYPE_MULTIPARMLIST: {
						// A float/color ramp is a multiparm list.
						if ( parameter.IsColorRamp( ) )
							GetColorRampParameterPoints( nameStr!, out parameterTuple._colorRampValues ) ;
						else if ( parameter.IsFloatRamp( ) )
							GetFloatRampParameterPoints( nameStr!, out parameterTuple._floatRampValues ) ;
						else {
							// Regular int
							parameterTuple._intValues = new int[ tupleSize ] ;
							for ( int j = 0; j < tupleSize; ++j )
								GetIntParameterValue( nameStr!, out parameterTuple._intValues[ j ], j ) ;
						}
						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_TOGGLE: {
						parameterTuple._boolValues = new bool[ tupleSize ] ;
						for ( int j = 0; j < tupleSize; ++j )
							GetBoolParameterValue( nameStr!, out parameterTuple._boolValues[ j ] ) ;
						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_FLOAT:
					case HAPI_ParmType.HAPI_PARMTYPE_COLOR: {
						parameterTuple._floatValues = new float[ tupleSize ] ;
						for ( int j = 0; j < tupleSize; ++j )
							GetFloatParameterValue( nameStr!, out parameterTuple._floatValues[ j ], j ) ;
						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_STRING:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_GEO:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_IMAGE:
					case HAPI_ParmType.HAPI_PARMTYPE_NODE:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_DIR: {
						parameterTuple.StringValues = new string[ tupleSize ] ;
						for ( int j = 0; j < tupleSize; ++j )
							GetStringParameterValue( nameStr!, out parameterTuple.StringValues[ j ], j ) ;
						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_BUTTON:
					case HAPI_ParmType.HAPI_PARMTYPE_FOLDER:
					case HAPI_ParmType.HAPI_PARMTYPE_LABEL:
					case HAPI_ParmType.HAPI_PARMTYPE_SEPARATOR:
					default:
						bSkipped = true ;
						break ;
				}

				if ( !bSkipped ) parameterTuples.Add( nameStr!, parameterTuple ) ;
			}

			return true ;
		}

		// ================================================================================================================


		//	LOGIC -----------------------------------------------------------------------------------------------------
		internal bool Initialize( HEU_SessionBase session, HAPI_NodeId nodeID, ref HAPI_NodeInfo nodeInfo,
								  Dictionary< string, HEU_ParameterData > previousParamFolders,
								  Dictionary< string, HEU_InputNode > previousParamInputNodes,
								  HEU_HoudiniAsset parentAsset ) {
			_nodeID      = nodeID ;
			_parentAsset = parentAsset ;

			HAPI_ParmInfo[] parmInfos = new HAPI_ParmInfo[ nodeInfo.parmCount ] ;
			if ( !HEU_GeneralUtility.GetArray1Arg( nodeID, session.GetParams, parmInfos, 0, nodeInfo.parmCount ) )
				return false ;

			_rootParameters = new( ) ;
			_parameterList  = new( ) ;
			Dictionary< HAPI_NodeId, HEU_ParameterData > parameterMap = new( ) ;

			// Load in all the parameter values.
			_paramInts = new int[ nodeInfo.parmIntValueCount ] ;
			if ( !HEU_GeneralUtility.GetArray1Arg( nodeID, session.GetParamIntValues,
												   _paramInts, 0, nodeInfo.parmIntValueCount ) ) return false ;

			_paramFloats = new float[ nodeInfo.parmFloatValueCount ] ;
			if ( !HEU_GeneralUtility.GetArray1Arg( nodeID, session.GetParamFloatValues,
												   _paramFloats, 0, nodeInfo.parmFloatValueCount ) ) return false ;
			
			HAPI_StringHandle[ ] parmStringHandles = new HAPI_StringHandle[ nodeInfo.parmStringValueCount ] ;
			if ( !HEU_GeneralUtility.GetArray1Arg( nodeID, session.GetParamStringValues, parmStringHandles, 0,
												   nodeInfo.parmStringValueCount ) ) return false ;

			// Convert to actual strings
			_paramStrings = new string[ nodeInfo.parmStringValueCount ] ;
			for ( int s = 0; s < nodeInfo.parmStringValueCount; ++s )
				_paramStrings[ s ] = HEU_SessionManager.GetString( parmStringHandles[ s ], session ) ;

			_paramChoices = new HAPI_ParmChoiceInfo[ nodeInfo.parmChoiceCount ] ;
			if ( !HEU_GeneralUtility.GetArray1Arg( nodeID, session.GetParamChoiceValues, 
												   _paramChoices, 0, nodeInfo.parmChoiceCount ) ) return false ;

			// Store ramps temporarily to post-process them later
			Stack< HEU_ParameterData > folderListParameters = new( ) ;
			List< HEU_ParameterData > rampParameters = new( ) ;
			HEU_ParameterData? currentFolderList = null ;
			
			// Parse each param info and build up the local representation of the hierarchy.
			// Note that this assumes that parmInfos is ordered as specified in the docs.
			// Specifically, a child parameter will always be listed after the containing parent's folder.
			for ( int i = 0; i < nodeInfo.parmCount; ++i ) {
				HAPI_ParmInfo parmInfo = parmInfos[ i ] ;

				if ( currentFolderList != null ) {
					// We're in a folder list. Check if all its children have been processed. If not, increment children processed.

					while ( currentFolderList._folderListChildrenProcessed >= currentFolderList._parmInfo.size ) {
						// Already processed all folders in folder list, so move to previous folder list or nullify if none left
						if ( folderListParameters.Count > 0 )
							currentFolderList = folderListParameters.Pop( ) ;
						else {
							currentFolderList = null ;
							break ;
						}
					}
					
					if ( currentFolderList is not null ) {
						// This is part of a folder list, so mark as processed
						currentFolderList._folderListChildrenProcessed++ ;
						//HEU_Logger.LogFormat("Updating folder list children to {0} for {1}", currentFolderList._folderListChildrenProcessed, currentFolderList._name);

						// Sanity check because folders must come right after the folder list
						if ( parmInfo.type is not HAPI_ParmType.HAPI_PARMTYPE_FOLDER ) {
							HEU_Logger.LogErrorFormat( "Expected {0} type but got {1} for parameter {2}",
													   HAPI_ParmType.HAPI_PARMTYPE_FOLDER, parmInfo.type,
													   HEU_SessionManager.GetString( parmInfo.nameSH, session ) ) ;
						}
					}
				}
				if ( parmInfo is { id: < 0, } or { childIndex: < 0, } ) {
					HEU_Logger.LogWarningFormat( "Corrupt parameter detected with name {0}. Skipping it.",
												 HEU_SessionManager.GetString( parmInfo.nameSH, session ) ) ;
					continue ;
				}

				//HEU_Logger.LogFormat("Param: name={0}, type={1}, size={2}, invisible={3}, parentID={4}, instanceNum={5}, childIndex={6}", 
				//	HEU_SessionManager.GetString(parmInfo.nameSH, session), parmInfo.type, parmInfo.size, parmInfo.invisible, parmInfo.parentId,
				//	parmInfo.instanceNum, parmInfo.childIndex);

				if ( parmInfo.invisible ) continue ;
				
				// Skip this param if any of the parm's parent folders are invisible
				bool bSkipParam = false ;
				HAPI_ParmId parentID = parmInfo.parentId ;
				while ( parentID > 0 && !bSkipParam ) {
					int parentIndex = Array.FindIndex( parmInfos,
													   p => p.id == parentID
													 ) ;
					
					if ( parentIndex > -1 ) {
						if ( parmInfos[ parentIndex ].invisible &&
							 ( parmInfos[ parentIndex ].type is HAPI_ParmType.HAPI_PARMTYPE_FOLDER
							   || parmInfos[ parentIndex ].type is HAPI_ParmType.HAPI_PARMTYPE_FOLDERLIST ) )
							bSkipParam = true ;

						parentID = parmInfos[ parentIndex ].parentId ;
					}
					else {
						HEU_Logger.LogErrorFormat( "Parent of parameter {0} not found!", parmInfo.id ) ;
						bSkipParam = true ;
					}
				}

				if ( bSkipParam ) continue ;
				HEU_ParameterData newParameter = new( ) ;

				switch ( parmInfo.type ) {
					case HAPI_ParmType.HAPI_PARMTYPE_FOLDERLIST: {
						// Folder lists: Push current container folder list on stack, and make new one current ...
						if ( currentFolderList is not null )
							folderListParameters.Push( currentFolderList ) ;

						currentFolderList = newParameter ;
						break ;
					}
					
					case >= HAPI_ParmType.HAPI_PARMTYPE_CONTAINER_START 
						 and <= HAPI_ParmType.HAPI_PARMTYPE_CONTAINER_END:
						// Contains list of folders.
						// Do nothing for containers. We're just going to use the Folder to get the children (see next case).
						continue ;
					
					case HAPI_ParmType.HAPI_PARMTYPE_FOLDER:
					case HAPI_ParmType.HAPI_PARMTYPE_MULTIPARMLIST:
						// Contains other containers or regular parms. Handling below.
						break ;
				}

				//! Note: Removed null check since we literally just instantiated `newParameter` above ...
				// Regular params (not a container). Handling below.
				// Initialize with parm info
				newParameter._parmInfo  = parmInfo ;
				newParameter._name      = HEU_SessionManager.GetString( parmInfo.nameSH, session ) ;
				newParameter._labelName = HEU_SessionManager.GetString( parmInfo.labelSH, session ) ;
				newParameter._help      = HEU_SessionManager.GetString( parmInfo.helpSH, session ) ;

				// Set its value based on type
				switch ( parmInfo.type ) {
					case HAPI_ParmType.HAPI_PARMTYPE_INT: {
						newParameter._intValues = new int[ parmInfo.size ] ;
						Array.Copy( _paramInts, parmInfo.intValuesIndex, newParameter._intValues, 0,
									parmInfo.size ) ;

						switch ( parmInfo ) {
							case { choiceCount : > 0,
									 scriptType: not HAPI_PrmScriptType.HAPI_PRM_SCRIPT_TYPE_BUTTONSTRIP, }: {
								// Choice list for Int

								// We need to add the user labels and their corresponding int values
								newParameter._choiceLabels = new GUIContent[ parmInfo.choiceCount ] ;

								// This is the list of values that Unity Inspector requires for dropdowns
								newParameter._choiceIntValues = new int[ parmInfo.choiceCount ] ;
								HAPI_ChoiceListType choiceType = parmInfo.choiceListType ;

								for ( int c = 0; c < parmInfo.choiceCount; ++c ) {
									// Store the user friendly labels for each choice
									string? labelStr =
										HEU_SessionManager.GetString( _paramChoices[ parmInfo.choiceIndex + c ].labelSH,
																	  session ) ;
									newParameter._choiceLabels[ c ] = new( labelStr ) ;

									string? tokenStr =
										HEU_SessionManager.GetString( _paramChoices[ parmInfo.choiceIndex + c ].valueSH,
																	  session ) ;

									// This will be the index of the above string value for Unity
									newParameter._choiceIntValues[ c ] = c ;

									if ( parmInfo.useMenuItemTokenAsValue ) {
										try {
											int value = Int32.Parse( tokenStr ) ;
											newParameter._choiceIntValues[ c ] = value ;
										}
										catch ( Exception e ) {
											HEU_Logger
												.LogWarningFormat( "UseMenuItemTokenAsValue set but unable to parse token value: {0}",
																   e.ToString( ) ) ;
										}
									}

									// Store the current chosen value's index. This is to let Unity know which option to display.
									if ( _paramInts[ parmInfo.intValuesIndex ] == newParameter._choiceIntValues[ c ] ) {
										newParameter._choiceValue = newParameter._choiceIntValues[ c ] ;
									}
								}

								break ;
							}
							
							case { choiceCount: > 0, scriptType: HAPI_PrmScriptType.HAPI_PRM_SCRIPT_TYPE_BUTTONSTRIP, }: {
								newParameter._choiceLabels = new GUIContent[ parmInfo.choiceCount ] ;

								for ( int c = 0; c < parmInfo.choiceCount; ++c ) {
									// Store the user friendly labels for each choice
									string? labelStr =
										HEU_SessionManager.GetString( _paramChoices[ parmInfo.choiceIndex + c ].labelSH, session ) ;
									newParameter._choiceLabels[ c ] = new( labelStr ) ;
								}
								break ;
							}
						}

						break ;
					}
					
					case HAPI_ParmType.HAPI_PARMTYPE_FLOAT: {
						//HEU_Logger.LogFormat("Param: name:{0}, size:{1}", parmInfo.label, parmInfo.size);
						newParameter._floatValues = new float[ parmInfo.size ] ;
						
						Array.Copy( _paramFloats, parmInfo.floatValuesIndex,
									newParameter._floatValues, 0, parmInfo.size ) ;

						//HEU_Logger.LogFormat("Param float with name {0}. Value = {1}", newParameter._name, newParameter._floatValues[parmInfo.size - 1]);
						break ;
					}
					
					case HAPI_ParmType.HAPI_PARMTYPE_STRING: {
						newParameter._stringValues = new string[ parmInfo.size ] ;
						Array.Copy( _paramStrings, parmInfo.stringValuesIndex,
									newParameter._stringValues, 0, parmInfo.size ) ;

						if ( parmInfo.tagCount > 0 ) {
							bool bHasPathTag = false ;
							if ( session.ParmHasTag( nodeID, parmInfo.id, "heuassetpath", ref bHasPathTag ) )
								newParameter._hasAssetPathTag = bHasPathTag ;
						}

						if ( parmInfo.choiceCount > 0 ) {
							// Choice list
							// We need to add the user labels and their corresponding string values
							newParameter._choiceLabels = new GUIContent[ parmInfo.choiceCount ] ;
							// This is the list of values Houdini requires
							newParameter._choiceStringValues = new string[ parmInfo.choiceCount ] ;

							// This is the list of values that Unity Inspector requires.
							// The Inspector requires an int array so we give it one.
							newParameter._choiceIntValues = new int[ parmInfo.choiceCount ] ;

							for ( int c = 0; c < parmInfo.choiceCount; ++c ) {
								// Store the user friendly labels for each choice
								string? labelStr =
									HEU_SessionManager.GetString( _paramChoices[ parmInfo.choiceIndex + c ].labelSH,
																  session ) ;
								newParameter._choiceLabels[ c ] = new( labelStr ) ;

								// Store the string value that Houdini requires
								newParameter._choiceStringValues[ c ] =
									HEU_SessionManager.GetString( _paramChoices[ parmInfo.choiceIndex + c ].valueSH,
																  session ) ;

								// This will be the index of the above string value
								newParameter._choiceIntValues[ c ] = c ;

								// Store the current chosen value
								// We look up our list of stringValues, and set the index into _choiceStringValues where
								// the string values match.
								if ( _paramStrings[ parmInfo.stringValuesIndex ] == newParameter._choiceStringValues[ c ] )
									newParameter._choiceValue = newParameter._choiceIntValues[ c ] ;
							}
						}
						break ;
					}
					
					case HAPI_ParmType.HAPI_PARMTYPE_TOGGLE: {
						newParameter._toggle = Convert.ToBoolean( _paramInts[ parmInfo.intValuesIndex ] ) ;
						break ;
					}
					
					case HAPI_ParmType.HAPI_PARMTYPE_COLOR: {
						switch ( parmInfo.size ) {
							case 3:
								newParameter._color = new( _paramFloats[ parmInfo.floatValuesIndex ],
														   _paramFloats[ parmInfo.floatValuesIndex + 1 ],
														   _paramFloats[ parmInfo.floatValuesIndex + 2 ], 1f ) ;
								break ;
							case 4:
								newParameter._color = new( _paramFloats[ parmInfo.floatValuesIndex ],
														   _paramFloats[ parmInfo.floatValuesIndex + 1 ],
														   _paramFloats[ parmInfo.floatValuesIndex + 2 ],
														   _paramFloats[ parmInfo.floatValuesIndex + 3 ] ) ;
								break ;
							default:
								HEU_Logger.LogWarningFormat( "Unsupported color parameter with label {0} and size {1}.",
															 HEU_SessionManager.GetString( parmInfo.labelSH, session ),
															 parmInfo.size ) ;
								break ;
						}

						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_FOLDER:
					case HAPI_ParmType.HAPI_PARMTYPE_FOLDERLIST:
					case HAPI_ParmType.HAPI_PARMTYPE_FOLDERLIST_RADIO: {
						// Sync up the show/hide and tab index states
						if ( previousParamFolders is not null ) {
							if ( previousParamFolders.TryGetValue( newParameter._name,
																   out HEU_ParameterData oldFolderParameterData ) ) {
								newParameter._showChildren = oldFolderParameterData._showChildren ;
								newParameter._tabSelectedIndex = oldFolderParameterData._tabSelectedIndex ;
							}
						}
						break ;
					}
					
					case HAPI_ParmType.HAPI_PARMTYPE_MULTIPARMLIST: {
						// For multiparms, we treat them pretty similar to folder lists.
						// The difference is that the instances can be changed. This is handled in the UI drawing.

						// parmInfo.instanceLength - # of parameters per instance
						// parmInfo.instanceCount - total # of instances
						// parmInfo.instanceStartOffset - instance numbers' start
						// parmInfo.isChildOfMultiParm - flags whether this instance is a child of a multiparm (parent)
						// parmInfo.instanceNum - instance this child belongs to

						// Note: adding / removing multiparm instance requires a complete rebuild of parameters, and UI refresh

						//HEU_Logger.LogFormat("MultiParm: id: {5}, # param per instance: {0}, # instances: {1}, start offset: {2}, childOfMutli: {3}, instanceNum: {4}",
						//	parmInfo.instanceLength, parmInfo.instanceCount, parmInfo.instanceStartOffset, parmInfo.isChildOfMultiParm, parmInfo.instanceNum,
						//	HEU_SessionManager.GetString(parmInfo.nameSH, session));

						if ( parmInfo.rampType is > HAPI_RampType.HAPI_RAMPTYPE_INVALID
												  and < HAPI_RampType.HAPI_RAMPTYPE_MAX )
							rampParameters.Add( newParameter ) ;
						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_DIR:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_GEO:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_IMAGE: {
						newParameter._stringValues = new string[ parmInfo.size ] ;
						Array.Copy( _paramStrings, parmInfo.stringValuesIndex,
									newParameter._stringValues, 0, parmInfo.size ) ;
						
						// Cache the file type
						newParameter._fileTypeInfo = HEU_SessionManager.GetString( parmInfo.typeInfoSH, session ) ;
						break ;
					}
					
					case HAPI_ParmType.HAPI_PARMTYPE_BUTTON: {
						newParameter._intValues = new int[ parmInfo.size ] ;
						Array.Copy( _paramInts, parmInfo.intValuesIndex,
									newParameter._intValues, 0, parmInfo.size ) ;
						break ;
					}
					
					case HAPI_ParmType.HAPI_PARMTYPE_SEPARATOR: break ;
					
					case HAPI_ParmType.HAPI_PARMTYPE_LABEL: {
						// No need to do anything
						newParameter._stringValues = new string[ parmInfo.size ] ;
						Array.Copy( _paramStrings, parmInfo.stringValuesIndex,
									newParameter._stringValues, 0, parmInfo.size ) ;
						break ;
					}
					
					case HAPI_ParmType.HAPI_PARMTYPE_NODE: {
						if ( previousParamInputNodes is not null ) {
							previousParamInputNodes.TryGetValue( newParameter._name,
																 out HEU_InputNode foundInputNode ) ;

							if ( foundInputNode ) {
								// It should be okay to set the saved input node data as long as its valid, regardless of whether
								// it matches Houdini session. The idea being that the user's saved state is more accurate than
								// whats in the Houdini session, though the session should take the correct value on recook.
								newParameter._paramInputNode = foundInputNode ;

								// Input node might have been removed from parent asset when cleaning parameters,
								// so add it back in
								parentAsset.AddInputNode( foundInputNode ) ;
							}
						}

						if ( newParameter._paramInputNode == null ) {
							newParameter._paramInputNode =
								HEU_InputNode.CreateSetupInput( parentAsset.AssetInfo.nodeId, 0, newParameter._name,
																newParameter._labelName,
																HEU_InputNode.InputNodeType.PARAMETER,
																parentAsset ) ;
							if ( newParameter._paramInputNode != null ) {
								newParameter._paramInputNode._paramName = newParameter._name ;
								parentAsset.AddInputNode( newParameter._paramInputNode ) ;
							}
						}

						break ;
					}
					default:
					{
						HEU_Logger.Log( "Unsupported parameter type: " + parmInfo.type ) ;
						break ;
					}
				}

				// Add to serializable map
				parameterMap.Add( newParameter.ParmID, newParameter ) ;

				int listIndex = _parameterList.Count ;
				newParameter._unityIndex = listIndex ;
				_parameterList.Add( newParameter ) ;

				// Now add to parent list

				if ( currentFolderList != null &&
					 newParameter._parmInfo.type is HAPI_ParmType.HAPI_PARMTYPE_FOLDER ) {
						// Folder is part of a folder list, in which case we add to the folder list as its child
						currentFolderList._childParameterIDs.Add( listIndex ) ;
						//HEU_Logger.LogFormat("Adding child param {0} to folder list {1}", newParameter._name, currentFolderList._name);
					}
					else if ( newParameter.ParentID == HEU_Defines.HEU_INVALID_NODE_ID ) {
						// No parent: store the root level parm in list.
						_rootParameters.Add( listIndex ) ;
					}
					else {
						// For mutliparams, the ParentID will be valid so we will add to its parent multiparm container

						// Look up parent and add to it
						//HEU_Logger.LogFormat("Child with Parent: name={0}, instance num={1}", HEU_SessionManager.GetString(parmInfo.nameSH, session), parmInfo.instanceNum);
						if ( parameterMap.TryGetValue( newParameter.ParentID, out HEU_ParameterData parentParameter ) ) {
							// Store the list index of the current parameter into its parent's child list
							//HEU_Logger.LogFormat("Found parent id: {0}", HEU_SessionManager.GetString(parentParameter._parmInfo.nameSH, session));

							bool bInserted   = false ;
							int  numChildren = parentParameter._childParameterIDs.Count ;
							if ( parmInfo.isChildOfMultiParm && numChildren > 0 ) {
								// For multiparms, keep the list ordered based on instance number.
								// The ordered list allows us to draw Inspector UI quickly.
								for ( int j = 0; j < numChildren; ++j ) {
									HEU_ParameterData childParm =
										GetParameter( parentParameter._childParameterIDs[ j ] ) ;
									if ( childParm._parmInfo.instanceNum >= 0 &&
										 parmInfo.instanceNum < childParm._parmInfo.instanceNum ) {
										parentParameter._childParameterIDs.Insert( j, listIndex ) ;
										bInserted = true ;
										break ;
									}
								}
							}

							if ( !bInserted ) {
								parentParameter._childParameterIDs.Add( listIndex ) ;
								//HEU_Logger.LogFormat("Added child {0} to parent {1} with instance num {2} at index {3}",
								//	HEU_SessionManager.GetString(newParameter._parmInfo.nameSH, session),
								//	HEU_SessionManager.GetString(parentParameter._parmInfo.nameSH, session),
								//	newParameter._parmInfo.instanceNum, parentParameter._childParameterIDs.Count - 1);
							}
						}
						else {
							HEU_Logger
								.LogErrorFormat( "Unable to find parent parameter with id {0}. It should have already been added to list!\n"
												 + "Parameter with id {0} and name {1} will not be showing up on UI.",
												 newParameter.ParmID, newParameter._name ) ;
						}
					}
			}

			// Setup each ramp parameter for quicker drawing
			foreach ( HEU_ParameterData ramp in rampParameters ) {
				SetupRampParameter( ramp ) ;
			}

			//HEU_Logger.Log("Param regenerated!");
			RecacheUI = true ;

			_validParameters = true ;
			return true ;
		}

		void SetupRampParameter( HEU_ParameterData rampParameter ) {
			if ( rampParameter._parmInfo.rampType == HAPI_RampType.HAPI_RAMPTYPE_COLOR ) {
				// Get all children that are points, and use their info to set the gradient color keys

				// instanceCount is the # of points, and therefore # of color keys
				GradientColorKey[] colorKeys = new GradientColorKey[ rampParameter._parmInfo.instanceCount ] ;

				int pointStartOffset = rampParameter._parmInfo.instanceStartOffset ;

				// Unity only supports global GradientMode for the Gradient. Not per point.
				// Also there is only Fixed or Blend avaiable.
				// Therefore we use Blend unless all points in Houdini are Constant, in which case we use Fixed.
				GradientMode gradientMode = GradientMode.Fixed ;

				int numChildren = rampParameter._childParameterIDs.Count ;
				for ( int i = 0; i < numChildren; ++i ) {
					HEU_ParameterData childParam = GetParameter( rampParameter._childParameterIDs[ i ] ) ;
					Debug.Assert( childParam is { _parmInfo: { isChildOfMultiParm: true, }, },
								  "Expected valid child for MultiParm: " + rampParameter._labelName ) ;

					int pointIndex = childParam._parmInfo.instanceNum ;
					if ( pointIndex >= pointStartOffset && ( ( pointIndex - pointStartOffset ) < colorKeys.Length ) ) {
						if ( childParam._parmInfo.type is HAPI_ParmType.HAPI_PARMTYPE_FLOAT ) {
							// Point position
							Debug.Assert( childParam._floatValues is { Length: 1, },
										  "Only expecting a single float for ramp position." ) ;
							colorKeys[ pointIndex - pointStartOffset ].time = childParam._floatValues[ 0 ] ;
						}
						else if ( childParam._parmInfo.type is HAPI_ParmType.HAPI_PARMTYPE_COLOR ) {
							// Point color
							colorKeys[ pointIndex - pointStartOffset ].color = childParam._color ;
						}
						else if ( childParam._parmInfo.type is HAPI_ParmType.HAPI_PARMTYPE_INT ) {
							// Point interpolation
							if ( childParam._intValues[ 0 ] != 0 ) {
								gradientMode = GradientMode.Blend ;
							}
						}
					}
				}

				rampParameter._gradient           = new( ) ;
				rampParameter._gradient.colorKeys = colorKeys ;
				rampParameter._gradient.mode      = gradientMode ;
			}
			else if ( rampParameter._parmInfo.rampType == HAPI_RampType.HAPI_RAMPTYPE_FLOAT ) {
				int numPts           = rampParameter._parmInfo.instanceCount ;
				int pointStartOffset = rampParameter._parmInfo.instanceStartOffset ;

				List< int > interpolationValues = new( ) ;
				int         indexAdded          = 0 ;

				// First create the animation curve and set point positions and values
				rampParameter._animCurve = new( ) ;
				for ( int pt = 0; pt < numPts; ++pt ) {
					HEU_ParameterData posParamData   = GetParameter( rampParameter._childParameterIDs[ pt * 3 + 0 ] ) ;
					HEU_ParameterData valueParamData = GetParameter( rampParameter._childParameterIDs[ pt * 3 + 1 ] ) ;

					int pointIndex = posParamData._parmInfo.instanceNum ;
					if ( pointIndex >= pointStartOffset && ( ( pointIndex - pointStartOffset ) < numPts ) ) {
						float position = posParamData._floatValues[ 0 ] ;
						float value    = valueParamData._floatValues[ 0 ] ;

						// AddKey returns index that was added, or -1 if already have the value
						// Note that Unity's Animation Curve doesn't allow for duplicate position-value pairs
						indexAdded = rampParameter._animCurve.AddKey( position, value ) ;
						if ( indexAdded >= 0 ) {
							HEU_ParameterData interpParamData =
								GetParameter( rampParameter._childParameterIDs[ pt * 3 + 2 ] ) ;
							interpolationValues.Add( interpParamData._intValues[ 0 ] ) ;
						}
					}
				}

				// Setting tangent mode seems to work better after all points are added.
				HEU_HAPIUtility.SetAnimationCurveTangentModes( rampParameter._animCurve, interpolationValues ) ;
			}
		}

		internal bool UploadValuesToHoudini( HEU_SessionBase session,         HEU_HoudiniAsset parentAsset,
											 bool            bDoCheck = true, bool bForceUploadInputs = false ) {
			if ( !AreParametersValid( ) ) {
				return false ;
			}

			//HEU_Logger.LogFormat("UploadValuesToHAPI(bDoCheck = {0})", bDoCheck);

			// Check if parameters changed (unless bDoCheck is false).
			// Upload ints and floats are arrays.
			// Upload strings individually.

			// Get the node info
			HAPI_NodeInfo nodeInfo = new( ) ;
			bool          bResult  = session.GetNodeInfo( _nodeID, ref nodeInfo ) ;
			if ( !bResult ) {
				return false ;
			}

			// For each parameter, check changes and upload
			foreach ( HEU_ParameterData parameterData in _parameterList ) {
				switch ( parameterData._parmInfo.type ) {
					case HAPI_ParmType.HAPI_PARMTYPE_INT:
					case HAPI_ParmType.HAPI_PARMTYPE_BUTTON:
					{
						if ( !bDoCheck ||
							 !HEU_GeneralUtility.DoArrayElementsMatch( _paramInts,
																	   parameterData._parmInfo.intValuesIndex,
																	   parameterData._intValues, 0,
																	   parameterData.ParmSize ) ) {
							//HEU_Logger.LogFormat("Int changed from {0} to {1}", _paramInts[parameterData._parmInfo.intValuesIndex], parameterData._intValues[0]);

							if ( !session.SetParamIntValues( _nodeID, ref parameterData._intValues,
															 parameterData._parmInfo.intValuesIndex,
															 parameterData.ParmSize ) ) {
								return false ;
							}

							Array.Copy( parameterData._intValues, 0, _paramInts, parameterData._parmInfo.intValuesIndex,
										parameterData.ParmSize ) ;
						}

						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_FLOAT:
					{
						if ( !bDoCheck ||
							 !HEU_GeneralUtility.DoArrayElementsMatch( _paramFloats,
																	   parameterData._parmInfo.floatValuesIndex,
																	   parameterData._floatValues, 0,
																	   parameterData.ParmSize ) ) {
							//HEU_Logger.LogFormat("Float changed to from {0} to {1}", _paramFloats[parameterData._parmInfo.floatValuesIndex], parameterData._floatValues[0]);

							if ( !session.SetParamFloatValues( _nodeID, ref parameterData._floatValues,
															   parameterData._parmInfo.floatValuesIndex,
															   parameterData.ParmSize ) ) {
								return false ;
							}

							Array.Copy( parameterData._floatValues, 0, _paramFloats,
										parameterData._parmInfo.floatValuesIndex, parameterData.ParmSize ) ;
						}

						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_STRING:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_DIR:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_GEO:
					case HAPI_ParmType.HAPI_PARMTYPE_PATH_FILE_IMAGE:
					{
						if ( !bDoCheck ||
							 !HEU_GeneralUtility.DoArrayElementsMatch( _paramStrings,
																	   parameterData._parmInfo.stringValuesIndex,
																	   parameterData._stringValues, 0,
																	   parameterData.ParmSize ) ) {
							//HEU_Logger.LogFormat("Updating string at {0} with value {1}", parameterData._parmInfo.stringValuesIndex, parameterData._stringValue);

							// Update Houdini each string at a time
							int numStrings = parameterData.ParmSize ;
							for ( int i = 0; i < numStrings; ++i ) {
								if ( !session.SetParamStringValue( _nodeID, parameterData._stringValues[ i ],
																   parameterData.ParmID, i ) ) {
									return false ;
								}
							}

							Array.Copy( parameterData._stringValues, 0, _paramStrings,
										parameterData._parmInfo.stringValuesIndex, parameterData.ParmSize ) ;
						}

						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_TOGGLE:
					{
						int toggleInt = Convert.ToInt32( parameterData._toggle ) ;
						if ( !bDoCheck || _paramInts[ parameterData._parmInfo.intValuesIndex ] != toggleInt ) {
							if ( !session.SetParamIntValue( _nodeID, parameterData._name, 0, toggleInt ) ) {
								return false ;
							}

							_paramInts[ parameterData._parmInfo.intValuesIndex ] = toggleInt ;
						}

						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_COLOR:
					{
						if ( !bDoCheck
							 || _paramFloats[ parameterData._parmInfo.floatValuesIndex ] != parameterData._color[ 0 ]
							 || _paramFloats[ parameterData._parmInfo.floatValuesIndex + 1 ] !=
							 parameterData._color[ 1 ]
							 || _paramFloats[ parameterData._parmInfo.floatValuesIndex + 2 ] !=
							 parameterData._color[ 2 ]
							 || ( parameterData.ParmSize is 4 &&
								  _paramFloats[ parameterData._parmInfo.floatValuesIndex + 3 ] !=
								  parameterData._color[ 3 ] ) ) {
							float[] tempColor = new float[ parameterData.ParmSize ] ;

							tempColor[ 0 ] = parameterData._color[ 0 ] ;
							tempColor[ 1 ] = parameterData._color[ 1 ] ;
							tempColor[ 2 ] = parameterData._color[ 2 ] ;

							if ( parameterData.ParmSize is 4 ) {
								tempColor[ 3 ] = parameterData._color[ 3 ] ;
							}

							if ( !session.SetParamFloatValues( _nodeID, ref tempColor,
															   parameterData._parmInfo.floatValuesIndex,
															   parameterData.ParmSize ) ) {
								return false ;
							}

							Array.Copy( _paramFloats, parameterData._parmInfo.floatValuesIndex, tempColor, 0,
										parameterData.ParmSize ) ;
						}

						break ;
					}
					case HAPI_ParmType.HAPI_PARMTYPE_NODE:
					{
						if ( !bDoCheck || ( parameterData._paramInputNode.RequiresUpload ) || bForceUploadInputs ) {
							parameterData._paramInputNode.UploadInput( session ) ;
						}
						else if ( bDoCheck && parameterData._paramInputNode.HasInputNodeTransformChanged( ) ) {
							parameterData._paramInputNode.UploadInputObjectTransforms( session ) ;
						}

						break ;
					}
					// TODO: add support for rest of types
				}
			}

			return true ;
		}

		internal void InsertInstanceToMultiParm( int unityParamIndex, int instanceIndex, int numInstancesToAdd ) {
			_parameterModifiers.Add( HEU_ParameterModifier
										 .GetNewModifier( HEU_ParameterModifier.ModifierAction.MULTIPARM_INSERT,
														  unityParamIndex, instanceIndex, numInstancesToAdd ) ) ;
		}

		internal void RemoveInstancesFromMultiParm( int unityParamIndex, int instanceIndex, int numInstancesToRemove ) {
			_parameterModifiers.Add( HEU_ParameterModifier
										 .GetNewModifier( HEU_ParameterModifier.ModifierAction.MULTIPARM_REMOVE,
														  unityParamIndex, instanceIndex, numInstancesToRemove ) ) ;
		}

		internal void ClearInstancesFromMultiParm( int unityParamIndex ) {
			_parameterModifiers.Add( HEU_ParameterModifier
										 .GetNewModifier( HEU_ParameterModifier.ModifierAction.MULTIPARM_CLEAR,
														  unityParamIndex, 0, 0 ) ) ;
		}

		internal bool HasModifiersPending( ) {
			return _parameterModifiers.Count > 0 ;
		}

		/// <summary>
		/// Goes through all pending parameter modifiers and actions on them.
		/// Deferred way to modify the parameter list after UI drawing.
		/// </summary>
		internal void ProcessModifiers( HEU_SessionBase session ) {
			if ( !AreParametersValid( ) ) {
				return ;
			}

			foreach ( HEU_ParameterModifier paramModifier in _parameterModifiers ) {
				//HEU_Logger.LogFormat("Processing modifier {0}", paramModifier._action);

				HEU_ParameterData parameter = GetParameter( paramModifier.ParameterIndex ) ;
				if ( parameter == null ) {
					// Possibly removed already? Don't believe need to flag a warning here.
					continue ;
				}

				if ( paramModifier._action == HEU_ParameterModifier.ModifierAction.MULTIPARM_CLEAR ) {
					// Remove all instances one by one.
					for ( int i = 0; i < parameter._parmInfo.instanceCount; ++i ) {
						int lastIndex = parameter._parmInfo.instanceCount - i ;
						//HEU_Logger.Log("CLEARING instance index " + lastIndex);
						if ( !session.RemoveMultiParmInstance( _nodeID, parameter._parmInfo.id, lastIndex ) ) {
							HEU_Logger.LogWarningFormat( "Unable to clear instances from MultiParm {0}",
														 parameter._labelName ) ;
							break ;
						}
					}

					RequiresRegeneration = true ;
				}
				else if ( paramModifier._action == HEU_ParameterModifier.ModifierAction.MULTIPARM_INSERT ) {
					// Insert new parameter instances at the specified index
					// paramModifier._instanceIndex is the location to add at
					// paramModifier._modifierValue is the number of new parameter instances to add
					for ( int i = 0; i < paramModifier.ModifierValue; ++i ) {
						int insertIndex = paramModifier.InstanceIndex + i ;
						//HEU_Logger.Log("INSERTING instance index " + insertIndex);
						if ( !session.InsertMultiparmInstance( _nodeID, parameter._parmInfo.id, insertIndex ) ) {
							HEU_Logger.LogWarningFormat( "Unable to insert instance at {0} for MultiParm {1}",
														 insertIndex, parameter._labelName ) ;
							break ;
						}
					}

					RequiresRegeneration = true ;
				}
				else if ( paramModifier._action == HEU_ParameterModifier.ModifierAction.MULTIPARM_REMOVE ) {
					// Remove parameter instances at the specified index
					// paramModifier._modifierValue number of instances will be removed
					// paramModifier._instanceIndex is the starting index to remove from
					for ( int i = 0; i < paramModifier.ModifierValue; ++i ) {
						int removeIndex = paramModifier.InstanceIndex ;
						//HEU_Logger.Log("REMOVING instance index " + removeIndex);
						if ( !session.RemoveMultiParmInstance( _nodeID, parameter._parmInfo.id, removeIndex ) ) {
							HEU_Logger.LogWarningFormat( "Unable to remove instance at {0} for MultiParm {1}",
														 removeIndex, parameter._labelName ) ;
							break ;
						}
					}

					RequiresRegeneration = true ;
				}
				else if ( paramModifier._action == HEU_ParameterModifier.ModifierAction.SET_FLOAT ) {
					string paramName = parameter._name ;
					session.SetParamFloatValue( _nodeID, paramName, paramModifier.InstanceIndex,
												paramModifier.FloatValue ) ;

					RequiresRegeneration = true ;
				}
				else if ( paramModifier._action == HEU_ParameterModifier.ModifierAction.SET_INT ) {
					string? paramName = parameter._name ;
					session.SetParamIntValue( _nodeID, paramName, paramModifier.InstanceIndex,
											  paramModifier.IntValue ) ;

					RequiresRegeneration = true ;
				}
				else {
					HEU_Logger.LogWarningFormat( "Unsupported parameter modifier: {0}", paramModifier._action ) ;
				}
			}

			_parameterModifiers.Clear( ) ;
		}

		/// <summary>
		/// Populate folder and input node parameter data from current parameter list.
		/// </summary>
		/// <param name="folderParams">Map to populate folder parameters</param>
		/// <param name="inputNodeParams">Map to populate input node parameters</param>
		internal void GetParameterDataForUIRestore( Dictionary< string, HEU_ParameterData > folderParams,
													Dictionary< string, HEU_InputNode >     inputNodeParams ) {
			foreach ( HEU_ParameterData parmData in _parameterList ) {
				if ( parmData._parmInfo.type is HAPI_ParmType.HAPI_PARMTYPE_NODE ) {
					inputNodeParams[ parmData._name ] = parmData._paramInputNode ;
				}
				else if ( parmData._parmInfo.type is HAPI_ParmType.HAPI_PARMTYPE_FOLDER ||
						  parmData._parmInfo.type is HAPI_ParmType.HAPI_PARMTYPE_FOLDERLIST ) {
					folderParams[ parmData._name ] = parmData ;
				}
			}
		}

		/// <summary>
		/// Returns list of connected input node gameobjects.
		/// </summary>
		/// <param name="inputNodeObjects">List to populate</param>
		internal void GetInputNodeConnectionObjects( List< GameObject > inputNodeObjects ) {
			foreach ( HEU_ParameterData parmData in _parameterList ) {

				if ( parmData._parmInfo.type is HAPI_ParmType.HAPI_PARMTYPE_NODE && parmData._paramInputNode != null &&
					 parmData._paramInputNode.InputObjects != null ) {
					foreach ( HEU_InputObjectInfo input in parmData._paramInputNode.InputObjects ) {
						if ( input != null && input._gameObject != null ) {
							inputNodeObjects.Add( input._gameObject ) ;
						}
					}
				}
			}
		}

		internal void DownloadPresetData( HEU_SessionBase session ) {
			if ( session.GetPreset( _nodeID, out byte[] presetData ) ) {
				_presetData = presetData ;
			}
		}

		internal void UploadPresetData( HEU_SessionBase session ) {
			if ( _presetData is { Length: > 0, } ) {
				session.SetPreset( _nodeID, _presetData ) ;
			}
		}

		internal void DownloadAsDefaultPresetData( HEU_SessionBase session ) {
			if ( session.GetPreset( _nodeID, out byte[] presetData ) ) {
				_defaultPresetData = presetData ;
			}
		}

		internal void UploadParameterInputs( HEU_SessionBase session, HEU_HoudiniAsset parentAsset,
											 bool            bForceUpdate ) {
			foreach ( HEU_ParameterData parmData in _parameterList ) {
				if ( parmData._parmInfo.type is HAPI_ParmType.HAPI_PARMTYPE_NODE &&
					 ( bForceUpdate || parmData._paramInputNode.RequiresUpload ) ) {
					// Update the node ID as it might have changed (e.g. new session)
					parmData._paramInputNode.SetInputNodeID( parentAsset.AssetID ) ;

					if ( bForceUpdate ) {
						parmData._paramInputNode.ResetConnectionForForceUpdate( session ) ;
					}

					parmData._paramInputNode.UploadInput( session ) ;
				}
			}
		}

		internal void UpdateTransformParameters( HEU_SessionBase session, ref HAPI_TransformEuler HAPITransform ) {
			SyncParameterFromHoudini( session, "t" ) ;
			SyncParameterFromHoudini( session, "r" ) ;
			SyncParameterFromHoudini( session, "s" ) ;
		}

		internal void SyncParameterFromHoudini( HEU_SessionBase session, string? parameterName ) {
			HEU_ParameterData parameterData = GetParameter( parameterName ) ;
			if ( parameterData != null ) {
				if ( session.GetParamFloatValues( _nodeID, parameterData._floatValues,
												  parameterData._parmInfo.floatValuesIndex, parameterData.ParmSize ) ) {
					Array.Copy( parameterData._floatValues, 0, _paramFloats, parameterData._parmInfo.floatValuesIndex,
								parameterData.ParmSize ) ;
				}
			}
		}

		/// <summary>
		/// Get the current parameter values from Houdini and store in
		/// the internal parameter value set. This is used for doing
		/// comparision of which values had changed after an Undo.
		/// </summary>
		internal void SyncInternalParametersForUndoCompare( HEU_SessionBase session ) {
			HAPI_NodeInfo nodeInfo = new( ) ;
			if ( !session.GetNodeInfo( _nodeID, ref nodeInfo ) ) {
				return ;
			}

			HAPI_ParmInfo[] parmInfos = new HAPI_ParmInfo[ nodeInfo.parmCount ] ;
			if ( !HEU_GeneralUtility.GetArray1Arg( _nodeID, session.GetParams, parmInfos, 0, nodeInfo.parmCount ) ) {
				return ;
			}

			_paramInts = new int[ nodeInfo.parmIntValueCount ] ;
			if ( !HEU_GeneralUtility.GetArray1Arg( _nodeID, session.GetParamIntValues, _paramInts, 0,
												   nodeInfo.parmIntValueCount ) ) {
				return ;
			}

			_paramFloats = new float[ nodeInfo.parmFloatValueCount ] ;
			if ( !HEU_GeneralUtility.GetArray1Arg( _nodeID, session.GetParamFloatValues, _paramFloats, 0,
												   nodeInfo.parmFloatValueCount ) ) {
				return ;
			}

			HAPI_StringHandle[] parmStringHandles = new HAPI_StringHandle[ nodeInfo.parmStringValueCount ] ;
			if ( !HEU_GeneralUtility.GetArray1Arg( _nodeID, session.GetParamStringValues, parmStringHandles, 0,
												   nodeInfo.parmStringValueCount ) ) {
				return ;
			}

			// Convert to actual strings
			_paramStrings = new string[ nodeInfo.parmStringValueCount ] ;
			for ( int s = 0; s < nodeInfo.parmStringValueCount; ++s ) {
				_paramStrings[ s ] = HEU_SessionManager.GetString( parmStringHandles[ s ], session ) ;
			}

			_paramChoices = new HAPI_ParmChoiceInfo[ nodeInfo.parmChoiceCount ] ;
			if ( !HEU_GeneralUtility.GetArray1Arg( _nodeID, session.GetParamChoiceValues, _paramChoices, 0,
												   nodeInfo.parmChoiceCount ) ) { }
		}

		internal void CleanUp( ) {
			//HEU_Logger.Log("Cleaning up parameters!");

			// For input parameters, notify removal
			foreach ( HEU_ParameterData paramData in _parameterList ) {
				if ( paramData != null && paramData._paramInputNode != null ) {
					paramData._paramInputNode.NotifyParentRemovedInput( ) ;
				}
			}

			_validParameters      = false ;
			_regenerateParameters = false ;

			_rootParameters     = new( ) ;
			_parameterList      = new( ) ;
			_parameterModifiers = new( ) ;

			_paramInts    = null ;
			_paramFloats  = null ;
			_paramStrings = null ;
			_paramChoices = null ;

			_presetData = null ;
		}

		internal void ResetAllToDefault( HEU_SessionBase session ) {
			foreach ( HEU_ParameterData parameterData in _parameterList ) {
				if ( parameterData._parmInfo.type is HAPI_ParmType.HAPI_PARMTYPE_NODE ) {
					parameterData._paramInputNode.ResetInputNode( session ) ;
				}
			}

			_presetData = _defaultPresetData ;

			RequiresRegeneration = true ;
		}

		public bool IsEquivalentTo( HEU_Parameters other ) {
			bool bResult = true ;

			string header = "HEU_Parameters" ;

			if ( other == null ) {
				HEU_Logger.LogError( header + " Not equivalent" ) ;
				return false ;
			}

			HEU_TestHelpers.AssertTrueLogEquivalent( _uiLabel, other._uiLabel, ref bResult, header, "_uiLabel" ) ;

			// Do not test raw parameter values as there can be intermediate / unsupported / unequal values for the same object
			// The main parameter check will be done in parameterList
			//HEU_TestHelpers.AssertTrueLogEquivalent(this._paramInts, other._paramInts, ref bResult, header, "_paramInts");
			//HEU_TestHelpers.AssertTrueLogEquivalent(this._paramFloats, other._paramFloats, ref bResult, header, "_paramFloats");
			//HEU_TestHelpers.AssertTrueLogEquivalent(this._paramStrings, other._paramStrings, ref bResult, header, "_paramString");

			// Skip parmChoices

			HEU_TestHelpers.AssertTrueLogEquivalent( _parameterList, other._parameterList, ref bResult, header,
													 "_parameterList" ) ;

			HEU_TestHelpers.AssertTrueLogEquivalent( _parameterModifiers, other._parameterModifiers, ref bResult,
													 header, "_parameterModifiers" ) ;


			HEU_TestHelpers.AssertTrueLogEquivalent( _regenerateParameters, other._regenerateParameters, ref bResult,
													 header, "_regenerateParameters" ) ;


			// Preset data doesn't seem to be the same for different components
			// HEU_TestHelpers.AssertTrueLogEquivalent(this._presetData.IsEquivalentArray(other._presetData), ref bResult, header, "_presetData");

			HEU_TestHelpers.AssertTrueLogEquivalent( _defaultPresetData, other._defaultPresetData, ref bResult, header,
													 "_defaultPresetData" ) ;

			HEU_TestHelpers.AssertTrueLogEquivalent( _validParameters, other._validParameters, ref bResult, header,
													 "_validParameters" ) ;

			HEU_TestHelpers.AssertTrueLogEquivalent( _showParameters, other._showParameters, ref bResult, header,
													 "_showParameters" ) ;


			// Skip _materialKey

			return bResult ;
		}
	}

} // HoudiniEngineUnity