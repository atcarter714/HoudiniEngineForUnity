﻿/*
 * Copyright (c) <2023> Side Effects Software Inc.
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
using System.Linq ;
using UnityEditor.Callbacks ;
using UnityEngine ;
#if UNITY_SPLINES_INSTALLED

using UnityEngine.Splines;
using Unity.Mathematics;

#endif


namespace HoudiniEngineUnity
{
    /////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // Typedefs (copy these from HEU_Common.cs)
    using HAPI_NodeId = Int32 ;

    [Serializable]
    public class HEU_InputInterfaceSplineSettings
    {
        public float SamplingResolution {
            get { return _samplingResolution ; }
            set { _samplingResolution = value ; }
        }

        [SerializeField] float _samplingResolution = 25.0f ;
    } ;

#if UNITY_SPLINES_INSTALLED
    /// <summary>
    /// This class provides functionality for uploading Unity spline data from gameobjects
    /// into Houdini through an input node.
    /// It derives from the HEU_InputInterface and registers with HEU_InputUtility so that it
    /// can be used automatically when uploading mesh data.
    /// </summary>
    public class HEU_InputInterfaceSpline: HEU_InputInterface {
#if UNITY_EDITOR
        /// <summary>
        /// Registers this input inteface for Unity splines on
        /// the callback after scripts are reloaded in Unity.
        /// </summary>
        [DidReloadScripts]
        static void OnScriptsReloaded( ) {
            HEU_InputInterfaceSpline inputInterface = new( ) ;
            HEU_InputUtility.RegisterInputInterface( inputInterface ) ;
        }
#endif

        HEU_InputInterfaceSplineSettings _settings ;

        HEU_InputInterfaceSpline( ): base( priority: DEFAULT_PRIORITY ) { }

        public void Initialize( HEU_InputInterfaceSplineSettings settings ) {
            settings ??= new( ) ;
            this._settings = settings ;
        }

        /// <summary>
        /// Return true if this interface supports uploading the given inputObject's data.
        /// Should check the components on the inputObject and children.
        /// </summary>
        /// <param name="inputObject">The gameobject whose components will be checked</param>
        /// <returns>True if this interface supports uploading this input object's data</returns>
        public override bool IsThisInputObjectSupported( GameObject inputObject ) => 
                                !inputObject ? false : inputObject.GetComponent< SplineContainer >( ) ;

        /// <summary>Create the input node and upload data based on the given inputObject.</summary>
        /// <param name="session">Session to create the node in</param>
        /// <param name="connectNodeID">The node to connect the input node to. Usually the SOP/merge node.</param>
        /// <param name="inputObject">The gameobject containing the components with data for upload</param>
        /// <param name="inputNodeID">The newly created input node's ID</param>
        /// <returns>Returns true if sucessfully created the input node and uploaded data.</returns>
        public override bool CreateInputNodeWithDataUpload( HEU_SessionBase session, HAPI_NodeId connectNodeID,
                                                            GameObject inputObject, out HAPI_NodeId inputNodeID ) {
            inputNodeID = HEU_Defines.HEU_INVALID_NODE_ID ;

            if ( !HEU_HAPIUtility.IsNodeValidInHoudini( session, connectNodeID ) ) {
                HEU_Logger.LogError( "Connection node is invalid." ) ;
                return false ;
            }

            // Get spline data from the input object
            HEU_InputDataSplineContainer inputSplines = GenerateSplineDataFromGameObject( inputObject ) ;
            if ( inputSplines?._inputSplines is not { Count: > 0 } ) {
                HEU_Logger.LogError( "No valid splines found on input objects." ) ;
                return false ;
            }

            string? inputName = inputObject.name + "_0" ;
            session.CreateInputCurveNode( out HAPI_NodeId newNodeID, inputName ) ;
            if ( newNodeID is HEU_Defines.HEU_INVALID_NODE_ID ||
                 !HEU_HAPIUtility.IsNodeValidInHoudini( session, newNodeID ) ) {
                HEU_Logger.LogError( "Failed to create new input cruve node in Houdini session!" ) ;
                return false ;
            }

            inputNodeID = newNodeID ;
            HEU_InputDataSpline inputSpline = inputSplines._inputSplines[ 0 ] ;
            
            if ( !UploadData( session, inputNodeID, inputSpline, Matrix4x4.identity ) ) {
                if ( session.CookNode( inputNodeID, false ) ) 
                    return false ;
                
                HEU_Logger.LogError( "New input curve node failed to cook!" ) ;
                return false ;
            }

            // The spline is made up of branching sub-splines.
            // Create an input node for each branching spline and object-merge it to the root spline.
            bool createMergeNode = inputSplines._inputSplines.Count( ) > 1 ;
            if ( !createMergeNode )
                return true ;

            HAPI_NodeId parentId = HEU_HAPIUtility.GetParentNodeID( session, inputNodeID ) ;

            if ( !session.CreateNode( parentId, "merge", null, false, out HAPI_NodeId mergeNodeId ) ) {
                HEU_Logger.LogErrorFormat( "Unable to create merge SOP node for connecting input assets." ) ;
                return false ;
            }

            if ( !session.ConnectNodeInput( mergeNodeId, 0, newNodeID ) ) {
                HEU_Logger.LogErrorFormat( "Unable to connect to input node!" ) ;
                return false ;
            }

            if ( !session.SetNodeDisplay( mergeNodeId, 1 ) ) {
                HEU_Logger.LogWarningFormat( "Unable to set display flag!" ) ;
            }

            inputNodeID = mergeNodeId ;

            Matrix4x4 localToWorld = inputSplines._transform.localToWorldMatrix ;
            for ( int i = 1; i < inputSplines._inputSplines.Count( ); i++ ) {
                session.CreateInputCurveNode( out HAPI_NodeId branchNodeID, 
                                              inputObject.name + "_" + i 
                                              ) ;
                
                if ( branchNodeID is HEU_Defines.HEU_INVALID_NODE_ID ||
                     !HEU_HAPIUtility.IsNodeValidInHoudini( session, branchNodeID ) ) {
                    HEU_Logger.LogError( "Failed to create new input curve node in Houdini session!" ) ;
                    return false ;
                }

                HEU_InputDataSpline branchSpline = inputSplines._inputSplines[ i ] ;
                if ( !UploadData( session, branchNodeID, branchSpline, localToWorld ) ) {
                    if ( session.CookNode( branchNodeID, false ) ) 
                        return false ;
                    
                    HEU_Logger.LogError( "New input curve node failed to cook!" ) ;
                    return false ;
                }

                if ( session.ConnectNodeInput( mergeNodeId, i, branchNodeID ) ) 
                    continue ;
                
                HEU_Logger.LogErrorFormat( "Unable to connect to input node!" ) ;
                return false ;
            }

            if ( session.CookNode( inputNodeID, false ) ) 
                return true ;
            
            HEU_Logger.LogError( "New input node failed to cook!" ) ;
            return false ;
        }

        /// <summary>Contains input geometry for a single spline.</summary>
        public class HEU_InputDataSpline {
            public Spline _spline ;
            public bool _closed ;
            public int _count ;
            public float _length ;
            public BezierKnot[ ] _knots ;
        }

        /// <summary>Contains input geometry for multiple splines.</summary>
        public class HEU_InputDataSplineContainer: HEU_InputData {
            public readonly List< HEU_InputDataSpline > _inputSplines = new( ) ;
            public Transform _transform ;
        }

        /// <summary>
        /// Return an input data structure containing spline data that needs to be
        /// uploaded from the given inputObject.
        /// </summary>
        /// <param name="inputObject">GameObject containing a Spline component</param>
        /// <returns>A valid input data strcuture containing spline data</returns>
        public HEU_InputDataSplineContainer GenerateSplineDataFromGameObject( GameObject inputObject ) {
            SplineContainer splineContainer = inputObject.GetComponent< SplineContainer >( ) ;
            IReadOnlyList< Spline > splines = splineContainer.Splines ;

            HEU_InputDataSplineContainer splineContainerData = new( ) ;
            foreach ( Spline spline in splines ) {
                HEU_InputDataSpline splineData = new( ) {
                    _spline = spline,
                    _closed = spline.Closed,
                    _count  = spline.Count,
                    _length = spline.GetLength( ),
                    _knots  = spline.Knots.ToArray( ),
                } ;

                splineContainerData._inputSplines.Add( splineData ) ;
            }

            splineContainerData._transform = inputObject.transform ;

            return splineContainerData ;
        }

        /// <summary>Upload the inputData into the input curve node with inputNodeID.</summary>
        /// <param name="session">Session that the input node exists in</param>
        /// <param name="inputNodeID">ID of the input node</param>
        /// <param name="inputSpline"></param>
        /// <param name="localToWorld"></param>
        /// <returns>True if successfully uploaded data</returns>
        public bool UploadData( HEU_SessionBase session, HAPI_NodeId inputNodeID,
                                HEU_InputDataSpline inputSpline, Matrix4x4 localToWorld ) {
            // Set the input curve info of the newly created input curve
            HAPI_InputCurveInfo inputCurveInfo = new( ) {
                order                      = 4,
                reverse                    = false,
                closed                     = inputSpline._closed,
                curveType                  = HAPI_CurveType.HAPI_CURVETYPE_BEZIER,
                inputMethod                = HAPI_InputCurveMethod.HAPI_CURVEMETHOD_BREAKPOINTS,
                breakpointParameterization = HAPI_InputCurveParameterization.HAPI_CURVEPARAMETERIZATION_UNIFORM,
            } ;
            
            if ( !session.SetInputCurveInfo( inputNodeID, 0, ref inputCurveInfo ) ) {
                HEU_Logger.LogError( "Failed to initialize input curve info." ) ;
                return false ;
            }

            // Calculate the number of refined points we want
            int   numControlPoints = inputSpline._knots.Count( ) ;
            float splineLength     = inputSpline._length ;
            float splineResolution = _settings?.SamplingResolution ?? 0.0f ;
            int numRefinedSplinePoints = splineResolution > 0.0f
                                             ? Mathf.CeilToInt( splineLength / splineResolution ) + 1
                                             : numControlPoints ;

            float[] posArr ;
            float[] rotArr ;
            float[] scaleArr ;
            if ( numRefinedSplinePoints <= numControlPoints ) {
                // There's not enough refined points, so we'll use the control points instead
                posArr   = new float[ numControlPoints * 3 ] ;
                rotArr   = new float[ numControlPoints * 4 ] ;
                scaleArr = new float[ numControlPoints * 3 ] ;
                for ( int i = 0; i < numControlPoints; i++ ) {
                    BezierKnot knot = inputSpline._knots[ i ] ;

                    // For branching sub-splines, apply local transform on vertices to get the merged spline
                    float3 pos = localToWorld.MultiplyPoint( knot.Position ) ;

                    HEU_HAPIUtility.ConvertPositionUnityToHoudini( pos, out posArr[ i * 3 + 0 ],
                                                                   out posArr[ i * 3 + 1 ], out posArr[ i * 3 + 2 ] ) ;
                    HEU_HAPIUtility.ConvertRotationUnityToHoudini( knot.Rotation, out rotArr[ i * 4 + 0 ],
                                                                   out rotArr[ i * 4 + 1 ], out rotArr[ i * 4 + 2 ],
                                                                   out rotArr[ i * 4 + 3 ] ) ;
                }
            }
            else {
                // Calculate the refined spline component
                posArr   = new float[ numRefinedSplinePoints * 3 ] ;
                rotArr   = new float[ numRefinedSplinePoints * 4 ] ;
                scaleArr = new float[ numRefinedSplinePoints * 3 ] ;
                float currentDistance = 0.0f ;
                for ( int i = 0; i < numRefinedSplinePoints; i++ ) {
                    float3 pos =
                        inputSpline._spline.EvaluatePosition( currentDistance / splineLength ) ;

                    // For branching sub-splines, apply local transform on vertices to get the merged spline
                    pos = localToWorld.MultiplyPoint( pos ) ;
                    HEU_HAPIUtility.ConvertPositionUnityToHoudini( pos, out posArr[ i * 3 + 0 ],
                                                                   out posArr[ i * 3 + 1 ], out posArr[ i * 3 + 2 ] ) ;
                    currentDistance += splineResolution ;
                }
            }

            bool hasRotations = rotArr.Length == posArr.Length ;
            bool hasScales = scaleArr.Length == posArr.Length ;
            bool hapi_result ;
            
            if ( !hasRotations && !hasScales ) {
                hapi_result = session.SetInputCurvePositions( inputNodeID, 0, posArr, 0, posArr.Length ) ;
            }
            else {
                hapi_result = session.SetInputCurvePositionsRotationsScales(
                                                                            inputNodeID, 0,
                                                                            posArr, 0, posArr.Length,
                                                                            rotArr, 0, rotArr.Length,
                                                                            scaleArr, 0, 0
                                                                           ) ;
            }

            if ( hapi_result ) return session.CommitGeo( inputNodeID ) ;
            HEU_Logger.LogError( "Failed to set input curve positions." ) ;
            return false ;
        }
    }
#endif

} // HoudiniEngineUnity