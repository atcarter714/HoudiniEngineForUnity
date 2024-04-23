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

using System ;
using System.Collections.Generic ;
using System.Diagnostics ;
using System.IO ;
using System.Runtime.InteropServices ;
using UnityEditor ;
using UnityEngine ;
using Object = UnityEngine.Object ;

#region Type Aliases
// Typedefs (copy these from HEU_Common.cs)
using HAPI_Int64 = System.Int64;
using HAPI_StringHandle = System.Int32;
using HAPI_NodeId = System.Int32;
using HAPI_NodeTypeBits = System.Int32;
using HAPI_NodeFlagsBits = System.Int32;
using HAPI_PartId = System.Int32;
#endregion


namespace HoudiniEngineUnity {
	
	/// <summary>
	/// Manages a session for Houdini Engine. Supports all types of sessions.
	/// </summary>
	public static class HEU_SessionManager {
		static HEU_HoudiniAsset[ ]? _assets ;
		
		// Default session
		static HEU_SessionBase? _defaultSession ;

		// Registry map for retrieval via session ID, and guaranteed persistence across code refresh/compile
		static readonly Dictionary< HAPI_Int64, HEU_SessionBase > _sessionMap = new( ) ;

		// Delegate for creating custom HEU_SessionBase objects
		public delegate HEU_SessionBase CreateSessionFromTypeDelegate( Type type ) ;
		
		// Custom HEU_SessionBase classes can register with this delegate in order to be re-created after
		// code refresh/compile, or when loading session data from storage.
		public static CreateSessionFromTypeDelegate? _createSessionFromTypeDelegate ;


		// SESSION ----------------------------------------------------------------------------------------------------

		/// <summary>
		/// Create new session if specified.
		/// </summary>
		/// <returns>A new session object</returns>
		public static HEU_SessionBase CreateSessionObject( ) {
#if HOUDINIENGINEUNITY_ENABLED
			HEU_SessionHAPI sessionBase = new( ) ;
#else
            
			HEU_SessionBase sessionBase = new( ) ;
#endif
			return sessionBase ;
		}

		public static HEU_SessionBase CreateSessionFromType( Type? type ) {
#if !UNITY_5_6_OR_NEWER
            HEU_Logger.LogError("Houdini Engine for Unity only supports Unity version 5.6.0 and newer!");
#elif HOUDINIENGINEUNITY_ENABLED
			// By default, we use HAPI if Houdini Engine is enabled
			if ( (type is null || type == typeof(HEU_SessionHAPI))
					|| _createSessionFromTypeDelegate is null ) return new( ) ;
			
			// For custom HEU_SessionBase classes
			Delegate[ ] delegates = _createSessionFromTypeDelegate.GetInvocationList( ) ;
			foreach ( var del in delegates ) {
				CreateSessionFromTypeDelegate? createDelegate = 
					del as CreateSessionFromTypeDelegate ;
				
				HEU_SessionBase? newSession = createDelegate?.Invoke( type ) ;
				if ( newSession is not null ) return newSession ;
			}
#endif
			
			return new( ) ; //! Fallback to empty session
		}

		/// <summary>
		/// Returns the default session in use. Tries to reconnect to a stored session.
		/// Does not create a new session.
		/// </summary>
		/// <returns>default session object or null if none found</returns>
		public static HEU_SessionBase? GetDefaultSession( ) {
			if ( _defaultSession is null )
				LoadStoredDefaultSession( ) ;
			
			return _defaultSession ;
		}

		/// <summary>
		/// Register the given session so that it can be retrieved via its session ID.
		/// This guarantees persistence reconnectiong across code refresh/compiles.
		/// </summary>
		/// <param name="sessionID"></param>
		/// <param name="session"></param>
		public static void RegisterSession( HAPI_Int64 sessionID, HEU_SessionBase session ) {
			_sessionMap.Add( sessionID, session ) ;
			SaveAllSessionData( ) ;
		}

		/// <summary>
		/// Unregister the session (used when closing).
		/// </summary>
		/// <param name="sessionID">Session to remove from registry</param>
		public static void UnregisterSession( HAPI_Int64 sessionID ) {
			_sessionMap.Remove( sessionID ) ;
			SaveAllSessionData( ) ;
		}

		/// <summary>
		/// Get the session associated with this session ID.
		/// As long as the session was registered, this will return it.
		/// </summary>
		/// <param name="sessionID">Session ID to use for matching to session</param>
		/// <returns>Session object if found</returns>
		public static HEU_SessionBase? GetSessionWithID( HAPI_Int64 sessionID ) {
			_sessionMap.TryGetValue( sessionID, out HEU_SessionBase session ) ;
			return session ;
		}

		/// <summary>
		/// Save given list of sessions (HEU_SessionData) into storage for retrieval later.
		/// A way to persist current session information through code refresh/compiles.
		/// </summary>
		public static void SaveAllSessionData( ) => HEU_PluginStorage.SaveAllSessionData( _sessionMap.Values ) ;
			//List< HEU_SessionBase? > sessions = new( _sessionMap.Values ) ;

		/// <summary>
		/// Load stored session data and recreate the session objects.
		/// </summary>
		public static void LoadAllSessionData( ) {
			// Clear existing sessions, and load session data from storage.
			// Then create session for each session data.
			_sessionMap.Clear( ) ;

			List< HEU_SessionData > sessionDatas = HEU_PluginStorage.LoadAllSessionData( ) ;
			foreach ( HEU_SessionData sessionData in sessionDatas ) {
				if ( sessionData is null ) continue ;
				try {
					// Create session based on type
					HEU_SessionBase? sessionBase = CreateSessionFromType( sessionData.SessionClassType ) ;
					if ( sessionBase is null ) continue ;
					
					sessionBase.SetSessionData( sessionData ) ;
					_sessionMap.Add( sessionData.SessionID, sessionBase ) ;
					if ( sessionData.IsDefaultSession ) _defaultSession = sessionBase ;
					// Find assets in scene with session ID. Check if valid and reset those that aren't.
				}
				catch ( Exception ex ) {
					HEU_Logger.LogWarningFormat( "Loading session with ID {0} failed with {1}. Ignoring the session.",
												 sessionData.SessionID, ex.ToString() ) ;
				}
			}
			
			InternalValidateSceneAssets( ) ;
		}

		/// <summary>
		/// Goes through all assets in scene, and re-registers them with the scene
		/// they were originally created in. If their session is not valid, the asset
		/// is invalidated so it will be created again in the default session.
		/// </summary>
		static void InternalValidateSceneAssets( ) {
			// Go through each asset, and validate in session
			_assets = Object.FindObjectsByType< HEU_HoudiniAsset >( FindObjectsSortMode.None ) ;
			foreach ( HEU_HoudiniAsset asset in _assets ) {
				if ( asset is not { SessionID: not HEU_Defines.HEU_INVALID_NODE_ID } ) 
					continue ;
				
				if ( _sessionMap.TryGetValue( asset.SessionID, out HEU_SessionBase session ) )
					session.RegisterAsset( asset ) ;
				else 
					asset.InvalidateAsset( ) ;
			}
		}

		/// <summary>
		/// Returns a valid session by either reconnecting to an existing session, or creating a new session.
		/// Note that this will display error (once) if unable to get a valid session.
		/// </summary>
		/// <returns>
		/// A session object (new or existing). Session might not actually have connected successfully.
		/// Check IsSessionValid() and error message.
		/// </returns>
		public static HEU_SessionBase? GetOrCreateDefaultSession( bool bNotifyUserError = true ) {
			// After a code refresh, _defaultSession might be null.
			// So try loading stored plugin data to see if we can get it back.
			if ( _defaultSession is null )
				HEU_PluginStorage.InstantiateAndLoad( ) ;
			if ( _defaultSession?.IsSessionValid() ?? false )
				return _defaultSession ;
			
			if ( _defaultSession is null or { ConnectionState: SessionConnectionState.NOT_CONNECTED, } ) {
				HEU_Logger.Log( HEU_Defines.HEU_NAME + ": No valid session found. Creating new session." ) ;
				// Try creating it if we haven't tried yet
				bNotifyUserError &= !CreateThriftPipeSession( HEU_PluginSettings.Session_PipeName,
															  HEU_PluginSettings.Session_AutoClose,
															  HEU_PluginSettings.Session_Timeout,
															  bNotifyUserError ) ;
			}

			if ( _defaultSession is not null ) {
				if ( !bNotifyUserError || _defaultSession.UserNotifiedSessionInvalid ) 
					return _defaultSession ;
				_defaultSession.UserNotifiedSessionInvalid = true ;
			}
			
			HEU_EditorUtility.DisplayErrorDialog( HEU_Defines.HEU_ERROR_TITLE,
												  GetLastSessionError( ), "OK" ) ;
			HEU_EditorUtility.DisplayDialog( HEU_Defines.HEU_INSTALL_INFO,
											 HEU_HAPIUtility.GetHoudiniEngineInstallationInfo( ), "OK" ) ;
			
			return _defaultSession ;
		}

		/// <summary>Create in-process Houdini Engine session.</summary>
		/// <returns>True if session creation succeeded.</returns>
		public static bool CreateInProcessSession( ) {
			CheckAndCloseExistingSession( ) ;

			_defaultSession = CreateSessionObject( ) ;
			return _defaultSession.CreateInProcessSession( true ) ;
		}

		/// <summary>Create socket session for Houdini Engine.</summary>
		/// <param name="hostName">Network name of the host.</param>
		/// <param name="serverPort">Network port of the host.</param>
		/// <param name="autoClose"></param>
		/// <param name="timeout"></param>
		/// <param name="logError"></param>
		/// <returns>True if successfully created session.</returns>
		public static bool CreateThriftSocketSession( string? hostName,  int   serverPort, 
													  bool    autoClose, float timeout,
													  bool    logError ) {
			CheckAndCloseExistingSession( ) ;
			_defaultSession = CreateSessionObject( ) ;
			return _defaultSession.CreateThriftSocketSession( true, hostName, serverPort, autoClose, timeout,
															  logError ) ;
		}

		/// <summary>Create pipe session for Houdini Engine.</summary>
		/// <param name="pipeName"></param>
		/// <param name="autoClose"></param>
		/// <param name="timeout"></param>
		/// <param name="logError"></param>
		/// <returns>True if successfully created session.</returns>
		public static bool CreateThriftPipeSession( string? pipeName, bool autoClose, float timeout, bool logError ) {
			CheckAndCloseExistingSession( ) ;

			_defaultSession = CreateSessionObject( ) ;
			return _defaultSession.CreateThriftPipeSession( true, pipeName, autoClose, timeout, logError ) ;
		}

		/// <summary>Create custom Houdini Engine session.</summary>
		/// <returns>True if session was created successfully.</returns>
		public static bool CreateCustomSession( ) {
			CheckAndCloseExistingSession( ) ;
			_defaultSession = CreateSessionObject( ) ;
			return _defaultSession.CreateCustomSession( true ) ;
		}

		public static bool ConnectThriftSocketSession( string? hostName,  int   serverPort, 
													   bool    autoClose, float timeout ) {
			CheckAndCloseExistingSession( ) ;
			_defaultSession = CreateSessionObject( ) ;
			return _defaultSession.ConnectThriftSocketSession( true, hostName, serverPort, autoClose, timeout ) ;
		}

		public static bool ConnectThriftPipeSession( string? pipeName, bool autoClose, float timeout ) {
			CheckAndCloseExistingSession( ) ;
			_defaultSession = CreateSessionObject( ) ;
			return _defaultSession.ConnectThriftPipeSession( true, pipeName, autoClose, timeout ) ;
		}

		public static void RecreateDefaultSessionData( ) {
			CheckAndCloseExistingSession( ) ;
			_defaultSession = CreateSessionObject( ) ;
		}

		public static bool ConnectSessionSyncUsingThriftSocket( string? hostName, int  serverPort, bool autoClose, 
																float   timeout,  bool logError ) {
			if ( _defaultSession is null ) 
				RecreateDefaultSessionData( ) ;
			
			return _defaultSession?.ConnectThriftSocketSession(
															  true, hostName, serverPort, autoClose, timeout,
															  logError, false ) 
																?? false ;
		}

		public static bool ConnectSessionSyncUsingThriftPipe( string? pipeName, bool autoClose,
															  float   timeout,  bool logError ) {
			if ( _defaultSession is null ) 
				RecreateDefaultSessionData( ) ;

			return _defaultSession?.ConnectThriftPipeSession(
															true, pipeName, autoClose, timeout,
															logError, false )
																?? false ;
		}

		public static bool InitializeDefaultSession( ) => 
								_defaultSession is not null 
									&& _defaultSession.InitializeSession( _defaultSession.GetSessionData() ) ;

		/// <summary>Close the default session.</summary>
		/// <returns>True if successfully closed session.</returns>
		public static bool CloseDefaultSession( ) {
			// Try to reconnect to session if _sessionObj is null.
			if ( _defaultSession is null || !LoadStoredDefaultSession( ) ) 
				return true ;
			
			bool bResult = _defaultSession?.CloseSession( ) ?? false ;
			_defaultSession = null ;
			return bResult ;
		}

		/// <summary>Close all sessions</summary>
		public static void CloseAllSessions( ) {
			List< HEU_SessionBase > sessions = new( _sessionMap.Values ) ;
			foreach ( HEU_SessionBase sessionEntry in sessions ) {
				if ( sessionEntry.GetSessionData( ) is not null ) {
					HEU_Logger.Log( HEU_Defines.HEU_NAME + ": Closing session: " +
									(sessionEntry.GetSessionData( )?.SessionID ?? -1) ) ;
				}

				sessionEntry.CloseSession( ) ;
			}

			// Clear out the default session
			_sessionMap.Clear( ) ;
			_defaultSession = null ;
		}

		/// <summary>
		/// Closes session if one exists and is valid.
		/// Trying to close invalid session might throw error so this bypasses it.
		/// </summary>
		static void CheckAndCloseExistingSession( ) {
			// Try to reconnect to session if _sessionObj is null.
			if ( _defaultSession is not null 
				 && !LoadStoredDefaultSession( ) ) return ;
			
			_defaultSession?.CloseSession( ) ;
			_defaultSession = null ;
		}

		/// <summary>Return the existing session data.</summary>
		/// <returns></returns>
		public static HEU_SessionData? GetSessionData( ) {
			HEU_SessionBase? sessionBase = GetDefaultSession( ) ;
			return sessionBase?.GetSessionData( ) ;
		}

		/// <summary>Return the session info.</summary>
		/// <returns>The session information as a formatted string.</returns>
		public static string? GetSessionInfo( ) {
			HEU_SessionBase? sessionBase = GetDefaultSession( ) ;
			return sessionBase is not null
					   ? sessionBase.GetSessionInfo( )
						: HEU_Defines.NO_EXISTING_SESSION ;
		}

		/// <summary>
		/// Tries to load a stored default session. This would be after a code refresh
		/// or if the Houdini session is still running but Unity hasn't connected to it.
		/// </summary>
		/// <returns>True if successfully reconnected to a stored session.</returns>		
		public static bool LoadStoredDefaultSession( ) {
			// By forcing our plugin and session data to be loaded here, it will
			// result in all stored sessions to be recreated, including _defaultSession
			// being initialized if found in storage.
			HEU_PluginStorage.InstantiateAndLoad( ) ;
			return _defaultSession?.IsSessionValid( ) ?? false ;
		}

		/// <summary>Close default (if valid) and open a new session.</summary>
		/// <returns>True if created a new session.</returns>
		public static bool RestartSession( ) {
			// Close and reconnect if session object exists.
			// Otherwise try reconnecting based on last session info.
			// If both fail, just create new.
			if ( _defaultSession is not null )
				return _defaultSession.RestartSession( ) ;
			if ( LoadStoredDefaultSession() ) return true ;
			
			HEU_SessionBase sessionBase = CreateSessionObject( ) ;

			if ( !sessionBase.CreateThriftPipeSession( true ) ) 
				return false ;
			
			_defaultSession = sessionBase ;
			return true ;
		}

		/// <summary>
		/// Returns true if the plugin is installed properly, and that a session (new or existing) can be established.
		/// Notifies user if either fails.
		/// This can be called before each operation into Houdini Engine to establish or reconnect to session.
		/// </summary>
		/// <returns>True if plugin is installed and session is valid.</returns>
		public static bool ValidatePluginSession( HEU_SessionBase? session = null ) {
			session ??= GetOrCreateDefaultSession( ) ;
			return session?.IsSessionValid( ) is true ;
		}

		/// <summary>Returns last session error.</summary>
		/// <returns>The last session error.</returns>
		public static string? GetLastSessionError( ) {
			HEU_SessionBase? sessionBase = GetDefaultSession( ) ;
			return sessionBase is not null 
					   ? sessionBase.GetLastSessionError( )
						: HEU_Defines.NO_EXISTING_SESSION ;
		}

		/// <summary>
		/// Check that the Unity plugin's Houdini Engine version
		/// matches with the linked Houdini Engine API version.
		/// </summary>
		/// <returns>True if the versions match.</returns>
		public static bool CheckVersionMatch( ) {
			HEU_SessionBase? sessionBase = GetOrCreateDefaultSession( ) ;
			return sessionBase?.CheckVersionMatch( ) ?? false ;
		}

		public static bool ClearConnectionError( ) {
#if HOUDINIENGINEUNITY_ENABLED
			HAPI_Result result = HEU_HAPIFunctions.HAPI_ClearConnectionError( ) ;
			return ( result is HAPI_Result.HAPI_RESULT_SUCCESS ) ;
#else
	    return true ;
#endif
		}

		public static string? GetConnectionError( bool clear ) {
#if HOUDINIENGINEUNITY_ENABLED
			HAPI_Result result = 
				HEU_HAPIFunctions.HAPI_GetConnectionErrorLength( out int bufferLength ) ;
			
			if ( result is not HAPI_Result.HAPI_RESULT_SUCCESS ) 
				return "Failed to get connection error" ;
			
			if ( bufferLength > 0 ) {
				var bytes = new byte[ bufferLength ] ;
				result = HEU_HAPIFunctions.HAPI_GetConnectionError( bytes, bufferLength, clear ) ;
				if ( result is HAPI_Result.HAPI_RESULT_SUCCESS )
					return bytes.AsString( ) ;
			}
			
			else return string.Empty ; // Empty string (no error)
#endif
			return "Failed to get connection error" ;
		}

		public static bool IsHARSProcessRunning( int processID ) {
			if ( processID < 1 ) return false ;
			try {
				Process serverProcess = 
					Process.GetProcessById( processID ) ;
				return serverProcess is { HasExited: false, ProcessName: "HARS" } ;
				//!serverProcess.HasExited && serverProcess.ProcessName.Equals( "HARS" ) ;
			}
			catch ( Exception ) { return false ; }
		}

		// SESSION DEBUG ----------------------------------------------------------------------------------------------

		/// <summary>
		/// Load a HIP file into given session.
		/// The user will be prompted to choose a HIP file via Unity's file dialog.
		/// </summary>
		/// <param name="bCookNodes">True if nodes should be cooked on load</param>
		/// <param name="session">Session to load into. If null, will use default session</param>
		/// <returns>True if successfully loaded the HIP file</returns>
		public static bool LoadSessionFromHIP( bool bCookNodes, HEU_SessionBase? session = null ) {
			if ( session?.IsSessionValid( ) != true ) {
				session = GetOrCreateDefaultSession( ) ;
				if ( session?.IsSessionValid( ) != true ) {
					session?.SetSessionErrorMsg( "No valid session found. Unable to load session!", true ) ;
					return false ;
				}
			}
			
#if UNITY_EDITOR
			const string fileExt  = "hip;*.hiplc;*.hipnc" ;
			string?      lastPath = HEU_PluginSettings.LastLoadHIPPath ;
			string?      filePath = EditorUtility.OpenFilePanel( "Open Houdini HIP", lastPath, fileExt ) ;
			if ( string.IsNullOrEmpty( filePath ) ) return false ;
			
			HEU_PluginSettings.LastLoadHIPPath = filePath ;
			bool bResult = session.LoadHIPFile( filePath, bCookNodes ) ;
			if ( bResult ) {
				// TODO
				HEU_Logger.LogWarning( "This is currently not supported in the plugin!" ) ;
			}
#else
            session.SetSessionErrorMsg("Load session only supported in Unity Editor!", true);
#endif
			return false ;
		}

		/// <summary>
		/// Save given session to a HIP file.
		/// The user will be prompted with Unity's file dialog to choose HIP file location.
		/// </summary>
		/// <param name="bLockNodes">Whether to lock nodes in HIP file so as not to recook them on load</param>
		/// <param name="session">Session to save out. If null, uses default session.</param>
		/// <returns>True if successfully saved session</returns>
		public static bool SaveSessionToHIP( bool bLockNodes, HEU_SessionBase? session = null ) {
			if ( session?.IsSessionValid( ) != true ) {
				session = GetOrCreateDefaultSession( ) ;
				if ( session?.IsSessionValid( ) != true ) {
					session?.SetSessionErrorMsg( "No valid session found. Unable to save session!", true ) ;
					return false ;
				}
			}

#if UNITY_EDITOR
			string fileExt = "hip" ;
			HAPI_License license = GetCurrentLicense( false ) ;
			if ( license is HAPI_License.HAPI_LICENSE_HOUDINI_INDIE ||
				 license is HAPI_License.HAPI_LICENSE_HOUDINI_ENGINE_INDIE ) 
				fileExt = "hiplc" ;

			string filePath = EditorUtility.SaveFilePanel( "Save HIP File",
														   "",
														   "hscene",
														   fileExt ) ;
			if ( string.IsNullOrEmpty( filePath ) ) return true ;
			
			bool bResult = session.SaveHIPFile( filePath, bLockNodes ) ;
			if ( bResult ) 
				HEU_EditorUtility.RevealInFinder( filePath ) ;

			return bResult ;

#else
            session.SetSessionErrorMsg("Save session only supported in Unity Editor!", true);
            return false;
#endif
		}

		public static string GetHoudiniPathOnMacOS( string houdiniPath ) {
#if UNITY_EDITOR_OSX
	    // On macOS, need to find the actual executable, which is within one of .app folders
	    // that Houdini ships with, depending on the installation type.
	    // HoudiniPath should by default be pointing to the HFS (Houdini install) folder.
	    // Or user should have selected one of the .app folders within.

	    // If not set to .app, then set it based on what app is available
	    if (!houdiniPath.EndsWith(".app", System.StringComparison.InvariantCulture))
	    {
		string[] appNames = { "FX", "Core", "Indie", "Apprentice", "Indie", "Indie Steam Edition" };
		string tryPath;
		foreach (string name in appNames)
		{
		    tryPath = HEU_Platform.BuildPath(houdiniPath, string.Format("Houdini {0} {1}.app",
			    name,
			    HEU_HoudiniVersion.HOUDINI_VERSION_STRING));
		    if (HEU_Platform.DoesPathExist(tryPath))
		    {
			houdiniPath = tryPath;
			break;
		    }
		}
	    }

	    if (houdiniPath.EndsWith(".app", System.StringComparison.InvariantCulture))
	    {
		// Get the executable name inside the .app, but the executable
		// name is based on the license type, so need to query the 
		// license type and map it to executable name:
		// 	Houdini Apprenctice 18.0.100
		// 	Houdini Core 18.0.100
		// 	Houdini FX 18.0.100
		// 	Houdini Indie 18.0.100
		// 	Houdini Indie Steam Edition 18.0.100
		//houdiniPath = "/Applications/Houdini/Houdini18.0.100/Houdini Indie 18.0.100.app";
		string hexecutable = "";
		string pattern = @"(.*)/Houdini (.*) (.*).app$";
		Regex reg = new Regex(pattern);
		Match match = reg.Match(houdiniPath);
		if (match.Success && match.Groups.Count > 2)
		{
		    switch (match.Groups[2].Value)
		    {
			case "Apprentice": hexecutable = "happrentice"; break;
			case "Core": hexecutable = "houdinicore"; break;
			case "FX": hexecutable = "houdini"; break;
			case "Indie": hexecutable = "hindie"; break;
			case "Indie Steam Edition": hexecutable = "hindie.steam"; break;
			default: break;
		    }
		}

		houdiniPath += "/Contents/MacOS/" + hexecutable;
	    }
#endif
			return houdiniPath ;
		}

		public static bool OpenHoudini( string args ) {
			string? houdiniPath = HEU_PluginSettings.HoudiniDebugLaunchPath ;

#if UNITY_EDITOR_OSX
	    houdiniPath = GetHoudiniPathOnMacOS(houdiniPath);
#endif
			
			var HoudiniProcess = new Process( ) ;
			HoudiniProcess.StartInfo.FileName  = houdiniPath ;
			HoudiniProcess.StartInfo.Arguments = args ;
			return HoudiniProcess.Start( ) ;
		}

		/// <summary>Open given session in a new Houdini instance.</summary>
		/// <param name="session">Session to open. If null, will use default session.</param>
		/// <returns>True if successfully loaded session</returns>
		public static bool OpenSessionInHoudini( HEU_SessionBase? session = null ) {
			if ( session is null || !session.IsSessionValid( ) ) {
				session = GetOrCreateDefaultSession( ) ;
				if ( session is null || !session.IsSessionValid( ) ) {
					session?.SetSessionErrorMsg( "No valid session found. Unable to open session in Houdini!", true ) ;
					return false ;
				}
			}

			string HIPName = string.Format( "hscene_{0}.hip", Path.GetRandomFileName( ).Replace( ".", "" ) ) ;
			string HIPPath = Application.temporaryCachePath + HEU_Platform.DirectorySeparatorStr + HIPName ;

			if ( !session.SaveHIPFile( HIPPath, false ) ) {
				session.SetSessionErrorMsg( "Unable to save session to .hip file at: " + HIPPath, true ) ;
				return false ;
			}

			HEU_Logger.Log( "Saved session to " + HIPPath ) ;

			string? HoudiniPath = HEU_PluginSettings.HoudiniDebugLaunchPath ;

#if UNITY_EDITOR_OSX
	    HoudiniPath = GetHoudiniPathOnMacOS(HoudiniPath);
#endif

			var HoudiniProcess = new Process( ) ;
			HoudiniProcess.StartInfo.FileName  = HoudiniPath ;
			HoudiniProcess.StartInfo.Arguments = string.Format( "\"{0}\"", HIPPath ) ;
			if ( !HoudiniProcess.Start( ) ) {
				session.SetSessionErrorMsg( "Unable to start Houdini. Check that the Houdini Debug Exectable path is valid in Plugin Settings.",
											true ) ;
				HEU_EditorUtility.RevealInFinder( HIPPath ) ;
				return false ;
			}

			return true ;
		}


		// ENVIRONMENT ------------------------------------------------------------------------------------------------

		/// <summary>
		/// Returns the current license value.
		/// </summary>
		/// <returns>The current license</returns>
		public static HAPI_License GetCurrentLicense( bool bLogError ) {
			HEU_SessionBase? sessionBase = GetOrCreateDefaultSession( bLogError ) ;
			if ( sessionBase is null || !sessionBase.IsSessionValid( ) )
				return HAPI_License.HAPI_LICENSE_NONE ;
			
			int result =
				sessionBase.GetSessionEnvInt( HAPI_SessionEnvIntType.HAPI_SESSIONENVINT_LICENSE, bLogError ) ;
			return (HAPI_License)result ;
		}

		/// <summary>Get the string value of the given string handle.</summary>
		/// <param name="stringHandle">String handle to query.</param>
		/// <param name="session"></param>
		/// <returns>String value of the given string handle.</returns>
		public static string? GetString( int stringHandle, HEU_SessionBase? session = null ) {
			if ( stringHandle < 1 ) return string.Empty ;
			
			session ??= GetOrCreateDefaultSession( ) ;
			if ( session is null ) return string.Empty ;
			
			int length = session.GetStringBufferLength( stringHandle ) ;
			if ( length < 1 ) return string.Empty ;
			
			string? str = string.Empty ;
			session.GetString( stringHandle, ref str, length ) ;
			return str ;
		}

		public static string[ ]? GetStringValuesFromStringIndices( int[ ] strIndices ) {
			if ( strIndices is not { Length: > 0, } ) return null ;

			HEU_SessionBase? sessionBase = GetOrCreateDefaultSession( ) ;
			if ( sessionBase is null ) return null ;
			
			int       numLength = strIndices.Length ;
			string?[] strValues = new string[ numLength ] ;
			
			for ( int i = 0; i < numLength; ++i ) {
				int strIndex = strIndices[ i ] ;
				
				int length = strIndex >= 0 
								 ? sessionBase.GetStringBufferLength( strIndex )
									: 0 ;
				
				if ( length < 1 ) continue ;
				strValues[ i ] = string.Empty ;
				
				sessionBase.GetString( strIndex, ref strValues[ i ], length ) ;
			}

			return strValues ;
		}

		/// <summary>Gets the group names for given group type.</summary>
		/// <param name="session"></param>
		/// <param name="nodeID">The node ID</param>
		/// <param name="partID"></param>
		/// <param name="groupType">The group type to query</param>
		/// <param name="isInstanced"></param>
		/// <returns>Populated array of string names, or null if failed</returns>
		public static string[ ]? GetGroupNames( HEU_SessionBase session, 
												HAPI_NodeId nodeID, HAPI_PartId partID,
												HAPI_GroupType groupType, bool isInstanced ) {
			bool bSuccess ;
			int groupCount = 0 ;
			HAPI_GeoInfo geoInfo = new( ) ;
			if ( !session?.GetGeoInfo(nodeID, ref geoInfo) ?? true ) {
				HEU_Logger.LogError( $"Failed to get geo info for node: {nodeID}" ) ;
				return null ;
			}
			
			if ( !isInstanced )
				groupCount = geoInfo.getGroupCountByType( groupType ) ;
			
			else {
				if ( session.GetGroupCountOnPackedInstancePart( nodeID, partID, out int pointGroupCount,
																out int primGroupCount ) ) {
					groupCount = ( groupType is HAPI_GroupType.HAPI_GROUPTYPE_POINT )
									 ? pointGroupCount
										: primGroupCount ;
				}
			}

			if ( groupCount < 1 ) return null ;
			int[ ] groupNames = new int[ groupCount ] ;
			if ( !isInstanced )
				bSuccess = session.GetGroupNames( nodeID, groupType, ref groupNames, groupCount ) ;
			else
				bSuccess = session.GetGroupNamesOnPackedInstancePart( nodeID, partID, groupType, ref groupNames,
																	  groupCount ) ;

			if ( !bSuccess ) return null ;
			string?[] nameStrings = new string[ groupCount ] ;
			for ( int i = 0; i < groupCount; ++i )
				nameStrings[ i ] = GetString( groupNames[ i ], session ) ;
			
			return nameStrings ;
		}

		/// <summary>Get group membership</summary>
		/// <param name="session"></param>
		/// <param name="nodeID"></param>
		/// <param name="partID"></param>
		/// <param name="groupType"></param>
		/// <param name="groupName"></param>
		/// <param name="membership">Array of ints representing the membership of this group</param>
		/// <param name="isInstanced"></param>
		/// <returns>True if successfully queried the group membership</returns>
		public static bool GetGroupMembership( HEU_SessionBase session, 
											   HAPI_NodeId nodeID, HAPI_PartId partID,
											   HAPI_GroupType groupType, string groupName,
											   ref int[ ] membership, bool isInstanced ) {
			HAPI_PartInfo partInfo = new( ) ;
			bool bResult  = session.GetPartInfo( nodeID, partID, ref partInfo ) ;
			if ( !bResult ) return false ;
			
			int count = partInfo.getElementCountByGroupType( groupType ) ;
			if ( count < 1 ) {
				membership = Array.Empty< int >( ) ;
				return true ;
			}
				
			membership = new int[ count ] ;
			bool membershipArrayAllEqual = false ;
			
			if ( !isInstanced ) {
				session.GetGroupMembership( nodeID, partID, groupType, groupName, ref membershipArrayAllEqual,
											membership, 0, count ) ;
			}
			else {
				session.GetGroupMembershipOnPackedInstancePart( nodeID, partID,
																groupType, groupName,
																ref membershipArrayAllEqual,
																membership, 0, count
															  ) ;
			}
			
			return true ;
		}

		/// <summary>
		/// Returns the name of the specified node.
		/// </summary>
		/// <param name="nodeID">Node ID of the node to find the name of</param>
		/// <param name="session">Session that the node should be in</param>
		/// <returns>Name of node or empty string if not valid</returns>
		public static string? GetNodeName( HAPI_NodeId nodeID, HEU_SessionBase? session = null ) {
			session ??= GetOrCreateDefaultSession( ) ;
				if ( session is null ) return null ;
			
			HAPI_NodeInfo nodeInfo = new( ) ;
			return session.GetNodeInfo( nodeID, ref nodeInfo, false ) 
					   ? GetString( nodeInfo.nameSH, session )
						: string.Empty ;
		}

		// ASSETS -----------------------------------------------------------------------------------------------------

		/// <summary>
		/// Get the name of the given node's input.
		/// </summary>
		/// <param name="nodeID">Node's ID</param>
		/// <param name="inputIndex">Index of the input</param>
		/// <param name="inputName">Input name string</param>
		/// <returns>True if successfully queried the node</returns>
		public static bool GetNodeInputName( HAPI_NodeId nodeID, int inputIndex, out string? inputName ) {
			inputName = string.Empty ;
			HEU_SessionBase? sessionBase = GetOrCreateDefaultSession( ) ;
			if ( sessionBase is null ) return false ;
			
			bool bResult = sessionBase.GetNodeInputName( nodeID, inputIndex,
														 out HAPI_StringHandle nodeNameIndex
													   ) ;
			if ( !bResult ) return false ;
			
			inputName = GetString( nodeNameIndex ) ;
			return true ;
		}

		/// <summary>
		/// Get the composed list of child node IDs. 
		/// </summary>
		/// <param name="session"></param>
		/// <param name="parentNodeID">Parent node ID</param>
		/// <param name="nodeTypeFilter">Node type by which to filter the children</param>
		/// <param name="nodeFlagFilter">Node flags by which to filter the children</param>
		/// <param name="bRecursive">Whether or not to compose the list recursively</param>
		/// <param name="childNodeIDs">Array to store the child node IDs.</param>
		/// <returns>True if successfully retrieved the child node list</returns>
		/// <param name="bLogIfError"></param>
		public static bool GetComposedChildNodeList( HEU_SessionBase session,
													 HAPI_NodeId parentNodeID,
													 HAPI_NodeTypeBits nodeTypeFilter,
													 HAPI_NodeFlagsBits nodeFlagFilter, bool bRecursive,
													 out HAPI_NodeId[ ]? childNodeIDs, bool bLogIfError = true ) {
			childNodeIDs = default ;
			
			// First compose the internal list and get the count, then get the actual list.
			int count = -1 ;
			bool bResult = session.ComposeChildNodeList( parentNodeID,
														 nodeTypeFilter, nodeFlagFilter,
														 bRecursive, ref count, bLogIfError
													   ) ;
			if ( !bResult ) return false ;
			
			childNodeIDs = new HAPI_NodeId[ count ] ;
			return count <= 0 || session.GetComposedChildNodeList( parentNodeID, childNodeIDs, count, bLogIfError ) ;
		}

		// OBJECTS ----------------------------------------------------------------------------------------------------

		/// <summary>
		/// Fill an array of HAPI_ObjectInfo list. Use for large arrays where marshalling is done in chunks.
		/// </summary>
		/// <param name="session"></param>
		/// <param name="nodeID">The parent node ID</param>
		/// <param name="objectInfos">Array to fill. Should atleast be size of length</param>
		/// <param name="start">At least 0 and at most object count returned by ComposeObjectList</param>
		/// <param name="length">Object count returned by ComposeObjectList. Should be at least 0 and at most object count - start</param>
		/// <returns>True if successfully queuried the object list</returns>
		public static bool GetComposedObjectListMemorySafe( HEU_SessionBase session,
															HAPI_NodeId nodeID,
															[Out] HAPI_ObjectInfo[ ] objectInfos,
															int start, int length ) =>
			HEU_GeneralUtility.GetArray1Arg( nodeID, session.GetComposedObjectList, objectInfos, start, length ) ;

		/// <summary>
		/// Fill in array of HAPI_Transform list. Use for large arrays where marshalling is done in chunks.
		/// </summary>
		/// <param name="session"></param>
		/// <param name="nodeID">The parent node ID</param>
		/// <param name="rstOrder">Transform order</param>
		/// <param name="transforms">Array to fill. Should at least be size of length</param>
		/// <param name="start">At least 0 and at most object count returned by ComposeObjectList</param>
		/// <param name="length">Object count returned by ComposeObjectList. Should be at least 0 and at most object count - start</param>
		/// <returns>True if successfully queuried the transform list</returns>
		public static bool GetComposedObjectTransformsMemorySafe( HEU_SessionBase session,
																  HAPI_NodeId nodeID, HAPI_RSTOrder rstOrder,
																  [Out] HAPI_Transform[ ] transforms,
																  int start, int length ) =>
			HEU_GeneralUtility.GetArray2Arg( nodeID, rstOrder,
											 session.GetComposedObjectTransforms,
											 transforms, start, length
										   ) ;
		
		
		// GEOMETRY GETTERS -------------------------------------------------------------------------------------------

		public static string? GetUniqueMaterialShopName( HAPI_NodeId assetID, HAPI_NodeId materialID ) {
			HEU_SessionBase? sessionBase = GetOrCreateDefaultSession( ) ;
			if ( sessionBase is null ) return string.Empty ;

			HAPI_AssetInfo assetInfo = new( ) ;
			if ( !sessionBase.GetAssetInfo( assetID, ref assetInfo ) )
				return string.Empty ;

			HAPI_MaterialInfo materialInfo = new( ) ;
			if ( !sessionBase.GetMaterialInfo( materialID, ref materialInfo ) )
				return string.Empty ;

			HAPI_NodeInfo assetNodeInfo = new( ) ;
			if ( !sessionBase.GetNodeInfo( assetID, ref assetNodeInfo ) )
				return string.Empty ;

			HAPI_NodeInfo materialNodeInfo = new( ) ;
			if ( !sessionBase.GetNodeInfo( materialInfo.nodeId, ref materialNodeInfo ) )
				return string.Empty ;

			string? assetNodeName    = GetString( assetNodeInfo.internalNodePathSH, sessionBase ) ;
			string? materialNodeName = GetString( materialNodeInfo.internalNodePathSH, sessionBase ) ;
			if ( assetNodeName.Length <= 0 || materialNodeName.Length <= 0 ) return string.Empty ;

			// This throws exceptions because assetNodeName is not necessarily even part of the string ...
			// Remove assetNodeName from materialNodeName. Extra position is for separator.
			// string materialName = materialNodeName.Substring( assetNodeName.Length + 1 ) ;
			
			string materialName = materialNodeName.Replace( assetNodeName, string.Empty ) ;
			return materialName.Replace( "/", "_" ) ;
		}
		
	} ;
}   // HoudiniEngineUnity