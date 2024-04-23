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

using System;
using UnityEngine;

namespace HoudiniEngineUnity
{
	[Serializable]
	public enum SessionMode { Socket, Pipe, } ;
	[Serializable]
	public enum SessionConnectionState { NOT_CONNECTED, CONNECTED, FAILED_TO_CONNECT, } ;

	

	/// <summary>
	/// Container for session-specific data.
	/// Note that this is sealed for serialization purposes.
	/// </summary>
	[Serializable]
	public sealed class HEU_SessionData {
		public const long INVALID_SESSION_ID = -1 ;

		// Actual HAPI session data
		public HAPI_Session _HAPISession ;

#pragma warning disable 0414
		// Process ID for Thrift pipe session
		[SerializeField] int _serverProcessID = -1 ;

		// Whether the session has been initialized
		[SerializeField] bool _initialized ;

		// Name of pipe (for pipe session)
		[SerializeField] string? _pipeName ;

		[SerializeField] int _port ;
#pragma warning restore 0414

		// ID for the HEU_SessionBase class type
		[SerializeField] string _sessionClassType ;

		// Whether this is the default session
		[SerializeField] bool _isDefaultSession ;

		[SerializeField] HEU_SessionSyncData _sessionSync ;

		public HEU_SessionSyncData GetOrCreateSessionSync( ) =>
			_sessionSync ??= new( ) {
				_timeLastUpdate      = 0,
				_timeStartConnection = 0,
				SyncStatus           = HEU_SessionSyncData.Status.Stopped,
				_newNodeName         = null,
				_nodeTypeIndex       = 0,
				_validForConnection  = false,
				_viewportHAPI        = default,
				_viewportLocal       = default,
				_viewportJustUpdated = false,
				_syncInfo            = default
			} ;

		public HEU_SessionSyncData GetSessionSync( ) => _sessionSync ;

		public void SetSessionSync( HEU_SessionSyncData syncData ) => _sessionSync = syncData ;

		public long SessionID {
			get {
#if HOUDINIENGINEUNITY_ENABLED
				return _HAPISession.id ;
#else
		return INVALID_SESSION_ID;
#endif
			}

			set { _HAPISession.id = value ; }
		}

		public int ProcessID {
			get {
#if HOUDINIENGINEUNITY_ENABLED
				return _serverProcessID ;
#else
		return -1;
#endif
			}

			set { _serverProcessID = value ; }
		}

		public HAPI_SessionType SessionType {
			get {
#if HOUDINIENGINEUNITY_ENABLED
				return _HAPISession.type ;
#else
		return 0;
#endif
			}

			set { _HAPISession.type = value ; }
		}

		public bool IsInitialized {
			get {
#if HOUDINIENGINEUNITY_ENABLED
				return _initialized ;
#else
		return false;
#endif
			}

			set { _initialized = value ; }
		}

		public bool IsValidSessionID {
			get {
#if HOUDINIENGINEUNITY_ENABLED
				return SessionID > 0 ;
#else
		return false;
#endif
			}
		}

		public string? PipeName {
			get {
#if HOUDINIENGINEUNITY_ENABLED
				return _pipeName ;
#else
		return string.Empty ;
#endif
			}

			set { _pipeName = value ; }
		}

		public int Port {
			get => _port ;
			set => _port = value ;
		}

		public Type SessionClassType {
			get => string.IsNullOrEmpty( _sessionClassType ) 
					   ? null : Type.GetType( _sessionClassType ) ;
			set => _sessionClassType = value.ToString( ) ;
		}

		public bool IsDefaultSession {
			get => _isDefaultSession ;
			set => _isDefaultSession = value ;
		}

		public bool IsSessionSync => _sessionSync != null ;

		[SerializeField] SessionConnectionState _connectionState ;

		public SessionConnectionState ThisConnectionMode {
			get => _connectionState ;
			set => _connectionState = value ;
		}

		[SerializeField] SessionMode _sessionMode ;

		public SessionMode ThisSessionMode {
			get => _sessionMode ;
			set => _sessionMode = value ;
		}
	}

} // HoudiniEngineUnity
