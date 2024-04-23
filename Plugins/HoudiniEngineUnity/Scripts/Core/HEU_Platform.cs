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
using System.IO ;
using System.Text ;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace HoudiniEngineUnity {

	/// <summary>
	/// Base class for platform-specific functionaltiy.
	/// </summary>
#if UNITY_EDITOR && HOUDINIENGINEUNITY_ENABLED
	[InitializeOnLoad]
#endif
	public class HEU_Platform {
		/// <summary>Returns path separator character.</summary>
		/// <remarks>
		/// Instead of returning Path.DirectorySeparator, we'll use /
		/// since all our platforms support it and to keep it consistent.
		/// This way any saved paths in the project will work on all platforms.
		/// </remarks>
		public const char DirectorySeparator = '/' ;
		
		/// <summary>Returns path separator string.</summary>
		/// <remarks>
		/// Instead of returning Path.DirectorySeparator, we'll use /
		/// since all our platforms support it and to keep it consistent.
		/// This way any saved paths in the project will work on all platforms.
		/// </remarks>
		public const string DirectorySeparatorStr = "/" ;
		
		static readonly string[ ] houdiniAppNames = { "Houdini Engine", "Houdini", } ;
		
		
#pragma warning disable 0414
		static string? _lastErrorMsg ;
#pragma warning restore 0414

		public static string? LibPath { get ; private set ; }
		public static bool IsPathSet { get ; private set ; }
		

		static HEU_Platform( ) {
			// This gets set whenever Unity initializes or there is a code refresh.
			SetHapiClientName( ) ;
			SetHoudiniEnginePath( ) ;
		}
		
		
		/// <summary>Returns the path to the Houdini Engine plugin installation.</summary>
		/// <returns>Path to the Houdini Engine plugin installation.</returns>
		public static string? GetHoudiniEnginePath( ) {
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
			// Limiting only to Windows since unable to dynamically load HAPI libs
			// with relative custom paths for now.

			// Use plugin setting path unless its not set
			string? HAPIPath = GetSavedHoudiniPath( ) ;
			return !string.IsNullOrEmpty( HAPIPath ) 
										   ? HAPIPath
											: GetHoudiniEngineDefaultPath( ) ;
#endif
		}

		/// <summary>Returns the default installation path of Houdini that this plugin was built to use.</summary>
		public static string? GetHoudiniEngineDefaultPath( ) {
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)

			// Look up in environment variable
			string? HAPIPath = Environment.GetEnvironmentVariable( HEU_Defines.HAPI_PATH,
																   EnvironmentVariableTarget.Machine ) ;
			if ( string.IsNullOrEmpty( HAPIPath ) )
				HAPIPath = Environment.GetEnvironmentVariable( HEU_Defines.HAPI_PATH,
															   EnvironmentVariableTarget.User ) ;

			if ( string.IsNullOrEmpty( HAPIPath ) )
				HAPIPath = Environment.GetEnvironmentVariable( HEU_Defines.HAPI_PATH,
															   EnvironmentVariableTarget.Process ) ;

			if ( !string.IsNullOrEmpty( HAPIPath ) ) return HAPIPath ;
			
			// HAPI_PATH not set. Look in registry.
			foreach ( string appName in houdiniAppNames ) {
				try {
					HAPIPath = HEU_PlatformWin.GetApplicationPath( appName ) ;
					break ;
				}
				catch ( HEU_HoudiniEngineError error ) {
					_lastErrorMsg = error.ToString( ) ;
				}
			}

#elif (UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)))
	    HAPIPath = System.Environment.GetEnvironmentVariable(HEU_Defines.HAPI_PATH);
	    if (string.IsNullOrEmpty(HAPIPath))
	    {
		HAPIPath = HEU_HoudiniVersion.HOUDINI_INSTALL_PATH;
	    }
#else
	    _lastErrorMsg = "Unable to find Houdini installation because this is an unsupported platform!";
#endif

			return HAPIPath ;
		}

		/// <summary>
		/// Return the saved Houdini install path.
		/// Checks if the plugin has been updated, and if so, asks
		/// user whether they want to switch to new version.
		/// If user switches, then this returns null to allow installed version
		/// to be used.
		/// </summary>
		/// <returns>The saved Houdini install path or null if it doesn't 
		/// exist or user wants to use installed version</returns>
		public static string? GetSavedHoudiniPath( ) {
			string? HAPIPath = HEU_PluginSettings.HoudiniInstallPath ;
			if ( string.IsNullOrEmpty( HAPIPath ) ) return HAPIPath ;
			
			// First check if the last stored installed Houdini version matches current installed version
			string? lastHoudiniVersion = HEU_PluginSettings.LastHoudiniVersion ;
			if ( string.IsNullOrEmpty( lastHoudiniVersion ) ) return HAPIPath ;
			if ( lastHoudiniVersion?.Equals( HEU_HoudiniVersion.HOUDINI_VERSION_STRING ) is true )
				return HAPIPath ;
			
			// Mismatch means different version of the plugin has been installed.
			// Ask user if they want to update their HAPIPath.
			// Confirmation means to clear out the saved HAPI path and use
			// the default one specified by the plugin.
			string title = "Updated Houdini Engine Plugin Detected" ;
			string? msg = string.Format( "You have overriden the plugin's default Houdini version with your own, but the plugin has been updated.\n" +
										 "Would you like to use the updated plugin's default Houdini version?." ) ;
			if ( HEU_EditorUtility.DisplayDialog( title, msg, "Yes", "No" ) ) {
				HEU_PluginSettings.HoudiniInstallPath = string.Empty ;
				HAPIPath = null ;
			}

			// Always update LastHoudiniVersion so this doesn't keep asking
			HEU_PluginSettings.LastHoudiniVersion = HEU_HoudiniVersion.HOUDINI_VERSION_STRING ;

			return HAPIPath ;
		}

		/// <summary>Sets the HAPI_CLIENT_NAME environment variable.</summary>
		public static void SetHapiClientName( ) {
			Environment.SetEnvironmentVariable(
													  HEU_HAPIConstants.HAPI_ENV_CLIENT_NAME, "unity" ) ;
		}

		/// <summary>
		/// Find the Houdini Engine libraries, and add the Houdini Engine path to the system path.
		/// </summary>
		public static void SetHoudiniEnginePath( ) {
#if HOUDINIENGINEUNITY_ENABLED
			if ( IsPathSet ) return ;

			// Get path to Houdini Engine
			string? appPath = GetHoudiniEnginePath( ) ;
			string binPath = appPath + HEU_HoudiniVersion.HAPI_BIN_PATH ;

			IsPathSet = false ;

#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
			bool bFoundLib = false ;

			// Add path to system path if not already in there
			string? systemPath =
				Environment.GetEnvironmentVariable( "PATH", EnvironmentVariableTarget.Machine ) ;
			if ( !string.IsNullOrEmpty(systemPath)
				 && systemPath?.Contains(binPath) is false ) {
				if ( systemPath.Length is 0 ) systemPath = binPath ;
				else systemPath = binPath + ";" + systemPath ;
				
				Environment.SetEnvironmentVariable( "PATH", systemPath,
														   EnvironmentVariableTarget.Process ) ;
			}

			// Look for the HAPI library DLL using system path
			if( string.IsNullOrEmpty(systemPath) ) {
				HEU_Logger.LogError("Could not find Houdini Engine library because system path is empty!");
				_lastErrorMsg = "Could not find Houdini Engine library because system path is empty!" ;
				return ;
			}
			foreach ( string path in systemPath!.Split( ';' ) ) {
				if ( !Directory.Exists(path) ) continue ;

				string? libPath = $"{path}/{HEU_HoudiniVersion.HAPI_LIBRARY}.dll" ;
				if ( !DoesFileExist( libPath ) ) continue ;
				
				LibPath = libPath.Replace( "\\", "/" ) ;
				bFoundLib = true ;
				break ;
				//HEU_Logger.Log("Houdini Engine DLL found at: " + LibPath);
			}

			if ( !bFoundLib ) {
				_lastErrorMsg = appPath != string.Empty
										? $"Could not find {HEU_HoudiniVersion.HAPI_LIBRARY} in PATH or at {appPath}."
											: $"Could not find {HEU_HoudiniVersion.HAPI_LIBRARY} in PATH." ;
				
				return ;
			}

			IsPathSet = true ;

#elif (UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_STANDALONE_LINUX)))
	    if(!System.IO.Directory.Exists(appPath))
	    {
		    _lastErrorMsg = string.Format("Could not find Houdini Engine library at {0}", appPath);
		    HEU_Logger.LogError(_lastErrorMsg);
		    return;
	    }

	    _libPath = appPath + HEU_HoudiniVersion.HAPI_LIBRARY_PATH;

	    // Set HARS bin path to environment path so that we can start Thrift server
	    string systemPath = System.Environment.GetEnvironmentVariable("PATH", System.EnvironmentVariableTarget.Process);
	    if (string.IsNullOrEmpty(systemPath) || !systemPath.Contains(binPath))
	    {
		    if (string.IsNullOrEmpty(systemPath))
		    {
			    systemPath = binPath;
		    }
		    else
		    {
			    systemPath = binPath + ":" + systemPath;
		    }
	    }
	    System.Environment.SetEnvironmentVariable("PATH", systemPath, System.EnvironmentVariableTarget.Process);
			
	    _pathSet = true;
#endif

#endif
		}

		/// <summary>
		/// Return all folders (their full paths) in given path as semicolon delimited string.
		/// </summary>
		/// <param name="path">Path to parse.</param>
		/// <returns>Paths of all folders under given path.</returns>
		public static string GetAllFoldersInPath( string path ) {
			if ( !Directory.Exists( path ) ) {
				return "" ;
			}

			// Using StringBuilder as its much more memory efficient than regular strings for concatenation.
			StringBuilder pathBuilder = new( ) ;
			GetAllFoldersInPathHelper( path, pathBuilder ) ;
			return pathBuilder.ToString( ) ;
		}

		/// <summary>
		/// Helper that uses StringBuilder to build up the paths of all folders in given path.
		/// </summary>
		/// <param name="inPath">Path to parse.</param>
		/// <param name="pathBuilder">StringBuilder to add results to.</param>
		static void GetAllFoldersInPathHelper( string inPath, StringBuilder pathBuilder ) {
			if ( Directory.Exists( inPath ) ) {
				pathBuilder.Append( inPath ) ;

				DirectoryInfo dirInfo = new( inPath ) ;
				foreach ( DirectoryInfo childDir in dirInfo.GetDirectories( ) ) {
					pathBuilder.Append( ";" ) ;
					pathBuilder.Append( GetAllFoldersInPath( childDir.FullName ) ) ;
				}
			}
		}

		/// <summary>
		/// Returns all files (with their paths) in a given folder, with or without pattern, either recursively or just the first.
		/// </summary>
		/// <param name="folderPath">Path to folder</param>
		/// <param name="searchPattern">File name pattern to search for</param>
		/// <param name="bRecursive">Search all directories or just the top</param>
		/// <returns>Array of file paths found or null if error</returns>
		public static string[ ]? GetFilesInFolder( string? folderPath, string searchPattern, bool bRecursive ) {
			if ( string.IsNullOrEmpty(folderPath) ) {
				HEU_Logger.LogWarning( "Failed to get files in folder. Folder path is null or empty!" ) ;
				return null ;
			}
			
			try {
				return Directory.GetFiles( folderPath,
										   searchPattern,
										   bRecursive
											   ? SearchOption.TopDirectoryOnly
													: SearchOption.AllDirectories
										 ) ;
			}
			catch ( Exception ex ) {
				HEU_Logger.LogErrorFormat( "Getting files in directory {0} threw exception: {1}", folderPath, ex ) ;
				return null ;
			}
		}

		public static string? GetFileName( string? path ) => Path.GetFileName( path ) ;

		public static string? GetFileNameWithoutExtension( string? path ) => 
															string.IsNullOrEmpty( path )
																? string.Empty
																	: Path.GetFileNameWithoutExtension( path ) ;

		/// <summary>Removes file name and returns the path containing just the folders.</summary>
		/// <param name="path"></param>
		/// <param name="bRemoveDirectorySeparatorAtEnd"></param>
		/// <returns></returns>
		public static string? GetFolderPath( string? path, bool bRemoveDirectorySeparatorAtEnd = false ) {
			if ( string.IsNullOrEmpty(path) ) {
				HEU_Logger.LogWarning( "Failed to get folder path. Path is null or empty!" ) ;
				return null ;
			}
			
			string? resultPath = path ;
			string fileName = Path.GetFileName( path ) ;
			
			//! TODO: WTF is this? If `fileName` is null/empty then replace with empty string?
			if ( !string.IsNullOrEmpty(fileName) )
				resultPath = path?.Replace( fileName, string.Empty ) ;
			
			if ( bRemoveDirectorySeparatorAtEnd )
				resultPath = resultPath?.TrimEnd( '\\', '/' ) ;
			
			return resultPath ;
		}

		
		/// <summary>
		/// Given a list of folders, builds a platform-compatible
		/// path, using a separator in between the arguments.
		/// Assumes folder arguments are given in order from left to right.
		/// eg. folder1/folder2/args[0]/args[1]/...
		/// </summary>
		/// <param name="folder1"></param>
		/// <param name="folder2"></param>
		/// <param name="args"></param>
		/// <returns>Returns platform-compatible path of given folders</returns>
		public static string? BuildPath( string? folder1, string? folder2, params object?[] args ) {
			char separator = DirectorySeparator ;
			StringBuilder sb = new( ) ;
			sb.Append( folder1 ) ;
			sb.Append( separator ) ;
			sb.Append( folder2 ) ;

			for ( int i = 0; i < args.Length; ++i ) {
				sb.Append( separator ) ;
				sb.Append( args[ i ] ) ;
			}
			
			return sb.ToString( ) ;
		}

		/// <summary>Removes and returns the last directory separator character from given string.</summary>
		/// <param name="inPath">Path to parse</param>
		/// <returns>Returns the last directory separator character from given string</returns>
		public static string? TrimLastDirectorySeparator( string? inPath ) => inPath?.TrimEnd( DirectorySeparator ) ;
		public static bool DoesPathExist( string? inPath ) => File.Exists( inPath ) || Directory.Exists( inPath ) ;
		public static bool DoesFileExist( string? inPath ) => File.Exists( inPath ) ;
		public static bool DoesDirectoryExist( string? inPath ) => Directory.Exists( inPath ) ;
		public static bool CreateDirectory( string inPath ) => Directory.CreateDirectory( inPath ).Exists ;
		public static string? GetParentDirectory( string inPath ) => Directory.GetParent( inPath )?.FullName ;
		public static string? GetFullPath( string inPath ) => Path.GetFullPath( inPath ) ;
		public static bool IsPathRooted( string? inPath ) => Path.IsPathRooted( inPath ) ;
		public static void WriteBytes( string path, byte[ ] bytes ) => File.WriteAllBytes( path, bytes ) ;
		
		public static bool WriteAllText( string? path, string? text ) {
			if ( string.IsNullOrEmpty(path) || string.IsNullOrEmpty(text) ) return false ;
			
			try {
				File.WriteAllText( path, text ) ;
				return true ;
			}
			catch ( Exception ex ) {
				HEU_Logger.LogErrorFormat( "Unable to save session to file: {0}. Exception: {1}", text,
										   ex.ToString( ) ) ;
			}

			return false ;
		}

		public static string? ReadAllText( string? path ) {
			if ( string.IsNullOrEmpty(path) ) {
				HEU_Logger.LogWarning( "Failed to load from file. Path is null or empty!" ) ;
				return null ;
			}
			
			try {
				if ( File.Exists(path) )
					return File.ReadAllText( path ) ;
			}
			catch ( Exception ex ) {
				HEU_Logger.LogErrorFormat( "Unable to load from file: {0}. Exception: {1}", path, ex.ToString( ) ) ;
			}

			return string.Empty ;
		}

		/// <summary>Returns environment value of given key, if found.</summary>
		/// <param name="key">Key to get the environment value for</param>
		/// <returns>Environment value as string, or empty if none found</returns>
		public static string? GetEnvironmentValue( string? key ) {
			if ( string.IsNullOrEmpty(key) ) {
				HEU_Logger.LogWarning( "Failed to get environment value. Key is null or empty!" ) ;
				return null ;
			}
			
			string? value = Environment.GetEnvironmentVariable( key, EnvironmentVariableTarget.Machine ) ;
			
			if ( string.IsNullOrEmpty(value) )
				value = Environment.GetEnvironmentVariable( key, EnvironmentVariableTarget.User ) ;
			if ( string.IsNullOrEmpty(value) )
				value = Environment.GetEnvironmentVariable( key, EnvironmentVariableTarget.Process ) ;

			return value ;
		}

		public static string? GetHoudiniEngineEnvironmentFilePathFull( ) {
			string? envPath = HEU_PluginSettings.HoudiniEngineEnvFilePath ;

			if ( !IsPathRooted(envPath) ) 
				envPath = HEU_AssetDatabase.GetAssetFullPath( envPath ) ;

			return DoesFileExist( envPath )
								   ? envPath
										: string.Empty ;
		}
		
		public static bool LoadFileIntoMemory( string? path, out byte[ ]? buffer ) {
			if ( string.IsNullOrEmpty(path) ) {
				HEU_Logger.LogWarning( "Failed to open file. Path is null or empty!" ) ;
				buffer = null ;
				return false ;
			}
			
			buffer = null ;
			try {
				if ( File.Exists( path ) ) buffer = File.ReadAllBytes( path ) ;
				else HEU_Logger.LogErrorFormat( "Failed to open (0}. File doesn't exist!", path ) ;
			}
			catch ( Exception ex ) {
				HEU_Logger.LogErrorFormat( "Failed to open (0}. Exception: {1}",
										   path, ex.ToString( ) ) ;
			}

			return buffer is not null ;
		}
	} ;
	
} // HoudiniEngineUnity
