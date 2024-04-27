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

namespace HoudiniEngineUnity {
    
    /// <summary>
    /// Base class for all Houdini Engine Unity tasks.
    /// </summary>
    public abstract class HEU_Task {
        // -----------------------------------------------------------------------
        public delegate void TaskCallback( HEU_Task task ) ;
        
        public enum TaskStatus
        {
            NONE,
            PENDING_START,
            STARTED,
            REQUIRE_UPDATE,
            PENDING_COMPLETE,
            COMPLETED,
            UNUSED
        }
        // -----------------------------------------------------------------------
        
        public TaskStatus _status ;

        public enum TaskResult
        {
            NONE,
            SUCCESS,
            FAILED,
            KILLED
        }

        public TaskResult _result ;

        public Guid TaskGuid { get ; } = Guid.NewGuid( ) ;
        
        public TaskCallback? _taskCompletedDelegate ;

        public abstract void DoTask( ) ;

        public virtual void UpdateTask( ) { }

        public abstract void KillTask( ) ;

        public abstract void CompleteTask( TaskResult result ) ;
    } ;


    /// <summary>Asset-specific class for Houdini Engine Unity tasks.</summary>
    public class HEU_AssetTask: HEU_Task {
        // -----------------------------------------------------------------------
        public enum BuildType { NONE, LOAD, COOK, RELOAD, } ;
        // -----------------------------------------------------------------------
        
        public BuildType _buildType ;
        public HEU_HoudiniAsset? _asset ;
        public string? _assetPath ;
        public Vector3 _position = Vector3.zero ;
        public bool _buildResult ;
        public long _forceSessionID = HEU_SessionData.INVALID_SESSION_ID ;
        
        public HEU_SessionBase? GetTaskSession( ) =>
            _forceSessionID is HEU_SessionData.INVALID_SESSION_ID 
                ? HEU_SessionManager.GetOrCreateDefaultSession( )
                : HEU_SessionManager.GetSessionWithID( _forceSessionID ) ;
        
        public override void DoTask( ) {
            if( !_asset ) {
                HEU_Logger.LogError( $"{nameof(HEU_AssetTask)} :: " +
                                     $"Failed to get the {nameof(HEU_HoudiniAsset)}!" ) ;
                HEU_TaskManager.CompleteTask( this, TaskResult.FAILED ) ;
                return ;
            }
            
            switch ( _buildType ) {
                case BuildType.LOAD when string.IsNullOrEmpty( _assetPath ):
                    // Bad path so fail		
                    HEU_TaskManager.CompleteTask( this, TaskResult.FAILED ) ;
                    break ;
                case BuildType.LOAD: {
                    // File-based HDA
                    GameObject? newGO =
                        HEU_HAPIUtility.InstantiateHDA( _assetPath, _position, 
                                                        GetTaskSession( ), true ) ;
                    
                    if ( newGO && newGO!.GetComponent< HEU_HoudiniAssetRoot >() ) {
                        // Add to post-load callback
                        _asset = newGO.GetComponent< HEU_HoudiniAssetRoot >( )._houdiniAsset ;
                        _asset!.ReloadDataEvent?.AddListener( CookCompletedCallback ) ;
                    }
                    else HEU_TaskManager.CompleteTask( this, TaskResult.FAILED ) ;
                    break ;
                }
                case BuildType.COOK:
                    _asset!.CookedDataEvent?.RemoveListener( CookCompletedCallback ) ;
                    _asset.CookedDataEvent?.AddListener( CookCompletedCallback ) ;
                    _asset.RequestCook( true, true, false ) ;
                    break ;
                
                case BuildType.RELOAD:
                    _asset!.ReloadDataEvent?.RemoveListener( CookCompletedCallback ) ;
                    _asset.ReloadDataEvent?.AddListener( CookCompletedCallback ) ;
                    _asset.RequestReload( true ) ;
                    break ;
                
                case BuildType.NONE:
                default:
                    throw new ArgumentOutOfRangeException( ) ;
            }
        }

        public override void KillTask( ) {
            if ( !_asset ) return ;
            _asset!.ReloadDataEvent?.RemoveListener( CookCompletedCallback ) ;
            _asset.CookedDataEvent?.RemoveListener( CookCompletedCallback ) ;
        }

        public override void CompleteTask( TaskResult result ) {
            if ( !_asset ) return ;
            _asset!.ReloadDataEvent?.RemoveListener( CookCompletedCallback ) ;
            _asset.CookedDataEvent?.RemoveListener( CookCompletedCallback ) ;
        }
        
        // -----------------------------------------------------------------------
        void CookCompletedCallback( HEU_HoudiniAsset asset, bool bSuccess, List< GameObject > outputs ) {
            if ( _status is not TaskStatus.STARTED ) return ;
            HEU_TaskManager.CompleteTask( this, bSuccess
                                                    ? TaskResult.SUCCESS
                                                        : TaskResult.FAILED ) ;
        }
        
        void CookCompletedCallback( HEU_CookedEventData? cookedEventData ) {
            if ( cookedEventData is null ) return ;
            CookCompletedCallback( cookedEventData.Asset,
                                   cookedEventData.CookSuccess,
                                   cookedEventData.OutputObjects ) ;
        }

        void CookCompletedCallback( HEU_ReloadEventData? reloadEventData ) {
            if ( reloadEventData is null ) return ;
            CookCompletedCallback( reloadEventData.Asset,
                                   reloadEventData.CookSuccess,
                                   reloadEventData.OutputObjects ) ;
        }
        // ========================================================================================
    } ;
    
} // HoudiniEngineUnity