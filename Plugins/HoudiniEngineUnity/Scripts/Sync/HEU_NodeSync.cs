#nullable enable
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

#if (UNITY_EDITOR_WIN || UNITY_EDITOR_OSX || UNITY_STANDALONE_LINUX)
#define HOUDINIENGINEUNITY_ENABLED
#endif

using UnityEngine;

namespace HoudiniEngineUnity
{
	/////////////////////////////////////////////////////////////////////////////////////////////////////////////////
	// Typedefs (copy these from HEU_Common.cs)
	using HAPI_NodeId = System.Int32 ;

		#region SETUP
	//[ExecuteInEditMode] // Needed to get OnDestroy callback when deleted in Editor
	public class HEU_NodeSync: HEU_BaseSync {
		#region FUNCTIONS

		#region SETUP

		void OnEnable( ) {
#if HOUDINIENGINEUNITY_ENABLED
			// Adding in OnEnable as its called after a code recompile (Awake is not).
			HEU_AssetUpdater.AddNodeSyncForUpdate( this ) ;
#endif
		}

		void OnDestroy( ) {
			// Need to remove the NodySync from AssetUpdater.
			// Parent's OnDestroy doesn't get called so
			// do session deletion here as well.

#if HOUDINIENGINEUNITY_ENABLED
			HEU_AssetUpdater.RemoveNodeSync( this ) ;
#endif

			DeleteSessionData( ) ;
		}

		public void InitializeFromHoudini( HEU_SessionBase session,  HAPI_NodeId nodeID,
										   string          nodeName, string      filePath ) {
			Initialize( ) ;
			_sessionID        = session.GetSessionData( ).SessionID ;
			_cookNodeID       = nodeID ;
			_nodeName         = nodeName ;
			_nodeSaveFilePath = filePath ;
			StartSync( ) ;
		}

		protected override void SetupLoadTask( HEU_SessionBase session ) {
			_loadTask ??= new( ) ;
			_loadTask.SetupLoadNode( session, this, _cookNodeID, _nodeName ) ;
			_loadTask.Start( ) ;
		}

		#endregion

		#region UTILITY

		public bool SaveNodeToFile( string filePath ) {
			HEU_SessionBase? session = GetHoudiniSession( false ) ;
			if ( session is null )
				return false ;

			HEU_Logger.Log( $"Saving to {filePath}" ) ;
			_nodeSaveFilePath = filePath ;

			return session.SaveNodeToFile( _cookNodeID, filePath ) ;
		}

		public static void CreateNodeSync( HEU_SessionBase session, string opName, string nodeNabel ) {
			const HAPI_NodeId parentNodeId = -1 ;
			session ??= HEU_SessionManager.GetDefaultSession( ) ;
			if ( !session.IsSessionValid( ) ) return ;


			if ( !session.CreateNode( parentNodeId, opName, nodeNabel, true, out HAPI_NodeId newNodeID ) ) {
				HEU_Logger.LogErrorFormat( "Unable to create merge SOP node for connecting input assets." ) ;
				return ;
			}

			// When creating a node without a parent, for SOP nodes, a container
			// geometry object will have been created by HAPI.
			// In all cases we want to use the node ID of that object container
			// so the below code sets the parent's node ID.

			// But for SOP/subnet we actually do want the subnet SOP node ID
			// hence the useSOPNodeID argument here is to override it.
			bool          useSopNodeID = opName.Equals( "SOP/subnet" ) ;
			HAPI_NodeInfo nodeInfo     = new( ) ;
			if ( !session.GetNodeInfo( newNodeID, ref nodeInfo ) )
				return ;

			switch ( nodeInfo.type ) {
				case HAPI_NodeType.HAPI_NODETYPE_SOP:
				{
					if ( !useSopNodeID ) newNodeID = nodeInfo.parentId ;
					break ;
				}
				case HAPI_NodeType.HAPI_NODETYPE_ANY:
					break ;
				case HAPI_NodeType.HAPI_NODETYPE_NONE:
					break ;
				case HAPI_NodeType.HAPI_NODETYPE_OBJ:
					break ;
				case HAPI_NodeType.HAPI_NODETYPE_CHOP:
					break ;
				case HAPI_NodeType.HAPI_NODETYPE_ROP:
					break ;
				case HAPI_NodeType.HAPI_NODETYPE_SHOP:
					break ;
				case HAPI_NodeType.HAPI_NODETYPE_COP:
					break ;
				case HAPI_NodeType.HAPI_NODETYPE_VOP:
					break ;
				case HAPI_NodeType.HAPI_NODETYPE_DOP:
					break ;
				case HAPI_NodeType.HAPI_NODETYPE_TOP:
					break ;
				default:
				{
					if ( nodeInfo.type is not HAPI_NodeType.HAPI_NODETYPE_OBJ ) {
						HEU_Logger.LogErrorFormat( "Unsupported node type {0}", nodeInfo.type ) ;
						return ;
					}

					break ;
				}
			}

			var          newGo    = HEU_GeneralUtility.CreateNewGameObject( nodeNabel ) ;
			HEU_NodeSync nodeSync = newGo.AddComponent< HEU_NodeSync >( ) ;
			nodeSync.InitializeFromHoudini( session, newNodeID, nodeNabel, "" ) ;
		}

		#endregion

		#region SYNC

		public override void Resync( ) {
			if ( _syncing )
				return ;

			// Not unloading, but rather just generating local geometry
			DestroyGeneratedData( ) ;
			StartSync( ) ;
		}

		#endregion
		#region UPDATE
		public override void SyncUpdate( ) {
			if ( _syncing || _cookNodeID is -1 || !_firstSyncComplete ) return ;
			if ( !HEU_PluginSettings.SessionSyncAutoCook || !_sessionSyncAutoCook ) return ;
			HEU_SessionBase? session = GetHoudiniSession( false ) ;
			if ( session is null || !session.IsSessionValid( ) || !session.IsSessionSync( ) )
				return ;

			// TODO: should check parent obj, or turn off recurse?
			// TODO: instead of cook count, how about cook time? but how to handle hierarchy cook change?
			HAPI_NodeId oldCount = _totalCookCount ;
			session.GetTotalCookCount(
									  _cookNodeID,
									  (HAPI_NodeId)( HAPI_NodeType.HAPI_NODETYPE_OBJ |
													 HAPI_NodeType.HAPI_NODETYPE_SOP ),
									  (HAPI_NodeId)( HAPI_NodeFlags.HAPI_NODEFLAGS_OBJ_GEOMETRY |
													 HAPI_NodeFlags.HAPI_NODEFLAGS_DISPLAY |
													 HAPI_NodeFlags.HAPI_NODEFLAGS_RENDER ),
									  true, out _totalCookCount ) ;
			if ( oldCount == _totalCookCount ) return ;

			//HEU_Logger.LogFormat("Resyncing due to cook count (old={0}, new={1})", oldCount, _totalCookCount);
			_loadTask?.Stop( ) ;
			DestroyGeneratedData( ) ;
			StartSync( ) ;
		}

		#endregion


		#region DATA

		public string _nodeSaveFilePath ;

		#endregion
	}

} // HoudiniEngineUnity