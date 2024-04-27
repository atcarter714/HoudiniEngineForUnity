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


// Expose internal classes/functions
#if UNITY_EDITOR
using System ;
using System.Collections.Generic ;
using System.IO ;
using System.Linq ;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization ;
using System.Runtime.Serialization.Formatters.Binary ;
using UnityEngine ;
using Object = System.Object ;

[assembly: InternalsVisibleTo("HoudiniEngineUnityEditor")]
[assembly: InternalsVisibleTo("HoudiniEngineUnityEditorTests")]
[assembly: InternalsVisibleTo("HoudiniEngineUnityPlayModeTests")]
#endif

namespace HoudiniEngineUnity {
    

    /// <summary>
    /// Serializable class for a HDA's preset data.
    /// </summary>
    [Serializable] public class HEU_AssetPreset {
        // File identifier
        public char[]? _identifier ;

        // Version entry
        public int _version ;

        // Asset's full OP name
        public string? _assetOPName ;

        // Main parameter preset
        public byte[]? _parameterPreset ;

        // List of curve names and their parameter presets
        public List< string? > _curveNames   = new( ) ;
        public List< byte[] >  _curvePresets = new( ) ;

        [OptionalField( VersionAdded = 2 )] public List< HEU_InputPreset > inputPresets = new( ) ;

        [OptionalField( VersionAdded = 3 )] public List< HEU_VolumeCachePreset > volumeCachePresets = new( ) ;

        // NOTE: If adding a new field, add attribute: [OptionalField(VersionAdded=2)]
        // See: https://docs.microsoft.com/en-us/dotnet/standard/serialization/version-tolerant-serialization
        // Also increment HEU_AssetPreset.PRESET_VERSION
    }

    /// <summary>
    /// Container for input parms preset.
    /// </summary>
    [Serializable] public class HEU_InputPreset {
        [SerializeField] internal HEU_InputNode.InputObjectType _inputObjectType ;

        public HEU_InputObjectTypeWrapper InputObjectType {
            get { return HEU_InputNode.InputObjectType_InternalToWrapper( _inputObjectType ) ; }
            set { _inputObjectType = HEU_InputNode.InputObjectType_WrapperToInternal( value ) ; }
        }

        public List< HEU_InputObjectPreset > _inputObjectPresets = new( ) ;

        // Deprecated and replaced with _inputAssetPresets. Leaving it in for backwards compatibility.
        public string? _inputAssetName ;
        public int _inputIndex ;
        public string? _inputName ;
        public bool _keepWorldTransform ;
        public bool _packGeometryBeforeMerging ;

        [OptionalField( VersionAdded = 4 )]
        public List< HEU_InputAssetPreset > _inputAssetPresets = new( ) ;
    }

    /// <summary>
    /// Container for HEU_InputObject preset which represents an input object parm for objects.
    /// </summary>
    [Serializable]
    public class HEU_InputObjectPreset {
        public string? _gameObjectName ;
        public bool    _isSceneObject ;

        // When rebuilding an HDA we need to store the gameObject, but not serialize it for presets.
        [NonSerialized] public GameObject? _gameObject ;

        public bool _useTransformOffset ;

        public Vector3 _translateOffset = Vector3.zero ;
        public Vector3 _rotateOffset    = Vector3.zero ;
        public Vector3 _scaleOffset     = Vector3.one ;
    }

    /// <summary>
    /// Container for HEU_InputAsset preset which represents an input object parm for assets.
    /// </summary>
    [Serializable] public class HEU_InputAssetPreset { public string? _gameObjectName ; }
    
    /// <summary>
    /// Container for HEU_VolumeLayer preset which represents a Unity Terrain layer (or Heightfield layer).
    /// </summary>
    [Serializable]
    public class HEU_VolumeLayerPreset {
        public string? _layerName ;
        public float _strength ;
        public bool _uiExpanded ;

        // Tile index of HF volume
        [OptionalField( VersionAdded = 5 )] public int _tile ;
    }

    /// <summary>
    /// Container for HEU_VolumeCache presets which represents a Unity Terrain (or Heightfield with layers).
    /// </summary>
    [Serializable]
    public class HEU_VolumeCachePreset {
        public string? _objName ;
        public string? _geoName ;
        public bool _uiExpanded ;

        public List< HEU_VolumeLayerPreset > _volumeLayersPresets = new( ) ;

        // Path to TerrainData object
        [OptionalField( VersionAdded = 6 )] public string? _terrainDataPath ;

        // Tile index of HF volume
        [OptionalField( VersionAdded = 6 )] public int _tile ;
    }

    /// <summary>
    /// Pending presets to apply on Asset Recook.
    /// In some cases, on Rebuild, after the first cook the asset doesn't have all geo 
    /// nodes due to relying on inputs. On rebuild, the inputs are applied and the asset
    /// is cooked again. The geo nodes are created on the second cook, and so the
    /// presets must be applied after the second cook. This structure allows to store
    /// these second set of presets to apply.
    /// </summary>
    [Serializable]
    internal class HEU_RecookPreset {
        public List< HEU_VolumeCachePreset > _volumeCachePresets = new( ) ;
        public List< HEU_InputPreset > _inputPresets = new( ) ;
    } ;
    
    /// <summary>
    /// Helper to serialize and deserialize a HDA's preset data.
    /// </summary>
    public static class HEU_AssetPresetUtility {
        // Preset file identifier
        public static char[ ] PRESET_IDENTIFIER = {
            'H', 'D', 'A', 'P', 'R', 'E', 'S', 'E', 'T',
        } ;

        // Preset version for debugging (increment if added fields to HEU_AssetPreset)
        public static int PRESET_VERSION = 6 ;

        /// <summary>
        /// Save the specified asset's preset data to file at specified path.
        /// </summary>
        /// <param name="asset">The asset's preset data will be saved</param>
        /// <param name="filePath">The file to save to</param>
        public static void SaveAssetPresetToFile( HEU_HoudiniAsset asset, string filePath ) {
            // This should return an object filled with preset data, and which we can serialize directly
            HEU_AssetPreset? assetPreset = asset.GetAssetPreset( ) ;

            if ( assetPreset is not null ) {
                try {
                    int len = PRESET_IDENTIFIER.Length ;

                    assetPreset._identifier = PRESET_IDENTIFIER ;
                    assetPreset._version    = PRESET_VERSION ;

                    using FileStream fs        = new( filePath, FileMode.Create, FileAccess.Write ) ;
                    IFormatter       formatter = new BinaryFormatter( ) ;

                    HEU_Vector3SerializationSurrogate vector3S = new( ) ;
                    HEU_Vector2SerializationSurrogate vector2S = new( ) ;

                    SurrogateSelector surrogateSelector = new( ) ;
                    surrogateSelector.AddSurrogate( typeof( Vector3 ),
                                                    new StreamingContext( StreamingContextStates.All ), vector3S ) ;
                    surrogateSelector.AddSurrogate( typeof( Vector2 ),
                                                    new StreamingContext( StreamingContextStates.All ), vector2S ) ;
                    formatter.SurrogateSelector = surrogateSelector ;

                    formatter.Serialize( fs, assetPreset ) ;
                }
                catch ( Exception ex ) {
                    HEU_Logger.LogErrorFormat( "Failed to save preset due to exception: " + ex ) ;
                }
            }
            else {
                HEU_Logger.LogErrorFormat( "Failed to save preset due to unable to retrieve the preset buffer!" ) ;
            }
        }

        /// <summary>
        /// Load the preset file at the specified path into the specified asset and cook it.
        /// </summary>
        /// <param name="asset">Asset to load preset into</param>
        /// <param name="filePath">Full path to file containing preset. File must have been written out by SaveAssetPresetToFile.</param>
        public static void LoadPresetFileIntoAssetAndCook( HEU_HoudiniAsset asset, string? filePath ) {
            if ( filePath is null ) {
                HEU_Logger.LogError( "No preset file path specified!" ) ;
                return ;
            }

            try {
                using FileStream fs        = new( filePath, FileMode.Open, FileAccess.Read ) ;
                BinaryFormatter  formatter = new( ) ;

                HEU_Vector3SerializationSurrogate vector3S = new( ) ;
                HEU_Vector2SerializationSurrogate vector2S = new( ) ;

                SurrogateSelector surrogateSelector = new( ) ;
                surrogateSelector.AddSurrogate( typeof( Vector3 ), new StreamingContext( StreamingContextStates.All ),
                                                vector3S ) ;
                surrogateSelector.AddSurrogate( typeof( Vector2 ), new StreamingContext( StreamingContextStates.All ),
                                                vector2S ) ;
                formatter.SurrogateSelector = surrogateSelector ;

                HEU_AssetPreset? assetPreset = (HEU_AssetPreset)formatter.Deserialize( fs ) ;

                if ( assetPreset != null ) {
                    if ( PRESET_IDENTIFIER.SequenceEqual( assetPreset._identifier ) ) {
                        asset.LoadAssetPresetAndCook( assetPreset ) ;
                    }
                    else {
                        HEU_Logger
                            .LogErrorFormat( "Unable to load preset. Specified file is not a saved HDA preset: {0}",
                                             filePath ) ;
                    }
                }
                else {
                    HEU_Logger.LogErrorFormat( "Failed to load preset file {0}.", filePath ) ;
                }
            }
            catch ( Exception ex ) {
                HEU_Logger.LogErrorFormat( "Failed to load preset due to exception: " + ex ) ;
            }
        }
    }

    /// <summary>
    /// Helper to serialize Vector3 using BinaryFormatter
    /// </summary>
    public class HEU_Vector3SerializationSurrogate: ISerializationSurrogate {
        void ISerializationSurrogate.GetObjectData( object obj,
                                                    SerializationInfo info,
                                                    StreamingContext  context ) {
            Vector3 v3 = (Vector3)obj ;
            info.AddValue( "x", v3.x ) ;
            info.AddValue( "y", v3.y ) ;
            info.AddValue( "z", v3.z ) ;
        }

        Object ISerializationSurrogate.SetObjectData( object obj,
                                                      SerializationInfo info,
                                                      StreamingContext context,
                                                      ISurrogateSelector selector ) {
            Vector3 v3 = (Vector3)obj ;
            v3.x = (float)info.GetValue( "x", typeof( float ) ) ;
            v3.y = (float)info.GetValue( "y", typeof( float ) ) ;
            v3.z = (float)info.GetValue( "z", typeof( float ) ) ;
            obj  = v3 ;
            return obj ;
        }
    }

    /// <summary>
    /// Helper to serialize Vector3 using BinaryFormatter
    /// </summary>
    public class HEU_Vector2SerializationSurrogate: ISerializationSurrogate {
        void ISerializationSurrogate.GetObjectData( object obj, SerializationInfo info, StreamingContext context ) {
            Vector2 v2 = (Vector2)obj ;
            info.AddValue( "x", v2.x ) ;
            info.AddValue( "y", v2.y ) ;
        }

        Object ISerializationSurrogate.SetObjectData( object obj, SerializationInfo info, StreamingContext context,
                                                      ISurrogateSelector selector ) {
            Vector2 v2 = (Vector2)obj ;
            v2.x = (float)info.GetValue( "x", typeof( float ) ) ;
            v2.y = (float)info.GetValue( "y", typeof( float ) ) ;
            obj  = v2 ;
            return obj ;
        }
    }
    
} // HoudiniEngineUnity