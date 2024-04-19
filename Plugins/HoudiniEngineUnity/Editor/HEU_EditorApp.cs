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


using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace HoudiniEngineUnity {

	/// <summary>
	/// Manages Editor callbacks and events.
	/// </summary>
	[InitializeOnLoad]
	public static class HEU_EditorApp {
		/// <summary>Executed after script (re)load. Sets up plugin callbacks.</summary>
		static HEU_EditorApp( ) {
			EditorApplication.hierarchyWindowItemOnGUI += HierarchyWindowItemOnGUI ;

#if UNITY_2018_1_OR_NEWER
			EditorApplication.quitting += EditorQuit ;
#endif

#if UNITY_2019_1_OR_NEWER
			SceneView.duringSceneGui += OnSceneGUIDelegate ;
#else
			SceneView.onSceneGUIDelegate += OnSceneGUIDelegate;
#endif
		}

		static void HierarchyWindowItemOnGUI( int instanceID, Rect selectionRect ) => ProcessDragEvent( Event.current, null ) ;

		static void OnSceneGUIDelegate( SceneView sceneView ) => ProcessDragEvent( Event.current, sceneView ) ;

		static void ProcessDragEvent( Event dragEvent, SceneView sceneView ) {
			if ( dragEvent is not { type: EventType.DragUpdated or EventType.DragPerform } ) return ;
			
			bool dragHDAs = false ;
			List< string > hdaList  = new( ) ;
			foreach ( string file in DragAndDrop.paths ) {
				if ( !HEU_HAPIUtility.IsHoudiniAssetFile(file) ) continue ;
				
				dragHDAs = true ;
				DragAndDrop.visualMode = DragAndDropVisualMode.Move ;
				hdaList.Add( file ) ;
				break ;
			}

			if ( !dragHDAs ) return ;
			if ( dragEvent.type is EventType.DragPerform ) {
				if ( HEU_SessionManager.ValidatePluginSession( ) ) {
					Vector3 dropPos = Vector3.zero ;
					if ( sceneView ) {
						Camera  camera   = sceneView.camera ;
						Vector3 mousePos = HEU_EditorUI.GetMousePosition( ref dragEvent, camera ) ;

						Ray ray = camera.ScreenPointToRay( mousePos ) ;
						ray.origin = camera.transform.position ;
						Plane plane = new( ) ;
						plane.SetNormalAndPosition( Vector3.up, Vector3.zero ) ;
						plane.Raycast( ray, out float enter ) ;
						enter   = Mathf.Clamp( enter, camera.nearClipPlane, camera.farClipPlane ) ;
						dropPos = ray.origin + ray.direction * enter ;
					}

					List< GameObject > createdGOs = new( ) ;
					foreach ( string file in hdaList ) {
						GameObject go =
							HEU_HAPIUtility.InstantiateHDA( file, dropPos,
															HEU_SessionManager.GetOrCreateDefaultSession( ),
															true ) ;
								
						if ( go ) createdGOs.Add( go ) ;
					}

					// Select the created assets
					HEU_EditorUtility.SelectObjects( createdGOs.ToArray( ) ) ;
				}
			}
			dragEvent.Use( ) ;
		}

		static void EditorQuit( ) {
			HEU_Logger.Log( "Houdini Engine: Editor is closing. Closing all sessions and clearing cache." ) ;
			HEU_SessionManager.CloseAllSessions( ) ;
			HEU_PluginStorage.DeleteAllSavedSessionData( ) ;
		}
	} ;
} // HoudiniEngineUnity