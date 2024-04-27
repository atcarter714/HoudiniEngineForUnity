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
// Typedefs (copy these from HEU_Common.cs)
using HAPI_NodeId = System.Int32 ;
using HAPI_PDG_WorkitemId = System.Int32 ;


namespace HoudiniEngineUnity {
    
    /// <summary>
    /// Meta data container for filtering when displaying TOP networks and nodes
    /// </summary>
    public class TOPNodeTags {
        // True if should show the TOP node in dropdown
        public bool _show = false ;

        // True if TOP node's generated results should be loaded into scene
        public bool _autoload = false ;

        // Whether or not TOP node should show, according to henginedata param
        // Note that if this is false, it will not show in HEU_TOPNetworkData at all
        public bool _showHEngineData = false ;

        // Whether or not TOP node will be auto loaded, according to henginedata param
        public bool _autoloadHEngineData = false ;
    } ;

    /// <summary>
    /// Container for TOP network data
    /// </summary>
    [Serializable] public class HEU_TOPNetworkData {
        public HAPI_NodeId _nodeID ;
        public string? _nodeName ;
        public List< HEU_TOPNodeData > _topNodes = new( ) ;
        public string?[] _topNodeNames = Array.Empty< string >( ) ;
        public int _selectedTOPIndex ;
        public string? _parentName ;
        public TOPNodeTags _tags = new( ) ;
    } ;

    /// <summary>
    /// Container for TOP node data, status, results, etc
    /// </summary>
    [Serializable] public class HEU_TOPNodeData {
        public enum PDGState {
            NONE,
            DIRTIED,
            DIRTYING,
            COOKING,
            COOK_COMPLETE,
            COOK_FAILED
        } ;
        
        public HAPI_NodeId _nodeID ;
        public string? _nodeName,
                       _parentName ;
        public GameObject? _workResultParentGO ;
        public List< HEU_TOPWorkResult > _workResults = new( ) ;
        public TOPNodeTags _tags = new( ) ;
        public bool _showResults = true ;
        public PDGState _pdgState ;
        
        [SerializeField]
        internal HEU_WorkItemTally _workItemTally = new( ) ;

        
        public void Reset( ) {
            _pdgState = PDGState.NONE ;
            _workItemTally.ZeroAll( ) ;
        }

        public bool AreAllWorkItemsComplete( ) {
            return _workItemTally.AreAllWorkItemsComplete( ) ;
        }

        public bool AnyWorkItemsFailed( ) {
            return _workItemTally.AnyWorkItemsFailed( ) ;
        }

        public bool AnyWorkItemsPending( ) {
            return _workItemTally.AnyWorkItemsPending( ) ;
        }
    } ;

    /// <summary>
    /// Container of work item's results (e.g. loaded geometry / gameobject)
    /// </summary>
    [Serializable] public class HEU_TOPWorkResult {
        public int _workItemIndex = -1;
        public HAPI_PDG_WorkitemId _workItemID = -1;
        public List< GameObject >? _generatedGOs  = new( ) ;
    } ;

    /// <summary>
    /// Work item status tally for UI.
    /// Allows to show number of work items cooking, waiting, errored, etc.
    /// </summary>
    [Serializable] internal class HEU_WorkItemTally {
        public int _totalWorkItems,
                   _waitingWorkItems,
                   _scheduledWorkItems,
                   _cookingWorkItems,
                   _cookedWorkItems,
                   _erroredWorkItems ;

        public void ZeroAll( ) {
            _totalWorkItems     = 0 ;
            _waitingWorkItems   = 0 ;
            _scheduledWorkItems = 0 ;
            _cookingWorkItems   = 0 ;
            _cookedWorkItems    = 0 ;
            _erroredWorkItems   = 0 ;
        }

        public bool AreAllWorkItemsComplete( ) {
            return ( _waitingWorkItems == 0 && _cookingWorkItems == 0 && _scheduledWorkItems == 0 &&
                     ( _totalWorkItems == ( _cookedWorkItems + _erroredWorkItems ) ) ) ;
        }

        public bool AnyWorkItemsFailed( ) {
            return _erroredWorkItems > 0 ;
        }

        public bool AnyWorkItemsPending( ) {
            return ( _totalWorkItems > 0 &&
                     ( _waitingWorkItems > 0 || _cookingWorkItems > 0 || _scheduledWorkItems > 0 ) ) ;
        }

        public string ProgressRatio( ) {
            float cooked = _cookedWorkItems ;
            float total  = _totalWorkItems ;
            float ratio  = _totalWorkItems > 0 ? Mathf.Min( ( cooked / total ) * 100f, 100f ) : 0 ;
            return $"{ratio:0}%" ;
        }
    } ;
    
} // HoudiniEngineUnity