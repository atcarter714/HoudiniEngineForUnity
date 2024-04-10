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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HoudiniEngineUnity
{
	/// <summary>
	/// Generic serializable Dictionary.
	/// </summary>
	/// <typeparam name="TKey"></typeparam>
	/// <typeparam name="TValue"></typeparam>
	[System.Serializable]
	public class HEU_SerializableDictionary< TKey, TValue >:
		IDictionary< TKey, TValue >, ISerializationCallbackReceiver {
		[System.NonSerialized] Dictionary< TKey, TValue > _dictionary ;
		[SerializeField] TKey[ ] _keys ;
		[SerializeField] TValue[ ] _values ;
		
		
		public TValue this[ TKey key ] {
			get {
				if ( _dictionary is null )
					throw new KeyNotFoundException( ) ;
				return _dictionary[ key ] ;
			}
			set {
				_dictionary        ??= new( ) ;
				_dictionary[ key ] =   value ;
			}
		}

		public ICollection< TKey > Keys {
			get {
				_dictionary ??= new( ) ;
				return _dictionary.Keys ;
			}
		}

		public ICollection< TValue > Values {
			get {
				_dictionary ??= new( ) ;
				return _dictionary.Values ;
			}
		}

		public int Count => _dictionary?.Count ?? 0 ;

		public bool IsReadOnly => false ;

		public void Add( TKey key, TValue value ) {
			_dictionary ??= new( ) ;
			_dictionary.Add( key, value ) ;
		}

		public void Add( KeyValuePair< TKey, TValue > item ) {
			_dictionary ??= new( ) ;
			_dictionary.TryAdd( item.Key, item.Value ) ;
			//( _dictionary as ICollection< KeyValuePair< TKey, TValue > > ).Add( item ) ;
		}

		public void Clear( ) => _dictionary?.Clear( ) ;

		public bool Contains( KeyValuePair< TKey, TValue > item ) =>
			_dictionary.TryGetValue( item.Key, out TValue value ) && item.Value.Equals( value ) ;

		public bool ContainsKey( TKey key ) => _dictionary?.ContainsKey( key ) ?? false ;

		public void CopyTo( KeyValuePair< TKey, TValue >[] array, int arrayIndex ) =>
			( _dictionary as ICollection< KeyValuePair< TKey, TValue > > )?.CopyTo( array, arrayIndex ) ;

		public IEnumerator< KeyValuePair< TKey, TValue > > GetEnumerator( ) =>
			_dictionary?.GetEnumerator( ) ?? default ;

		public bool Remove( TKey key ) => _dictionary?.Remove( key ) ?? false ;

		public bool Remove( KeyValuePair< TKey, TValue > item ) => _dictionary?.Remove( item.Key ) ?? false ;

		public bool TryGetValue( TKey key, out TValue value ) {
			value = default ;
			return _dictionary?.TryGetValue( key, out value ) ?? false ;
		}

		IEnumerator IEnumerable.GetEnumerator( ) {
			_dictionary ??= new( ) ;
			return _dictionary.GetEnumerator( ) ;
		}

		public void OnAfterDeserialize( ) {
			if ( _keys is not null && _values is not null ) {
				_dictionary ??= new( _keys.Length ) ;
				_dictionary.Clear( ) ;
				for ( int i = 0; i < _keys.Length; ++i ) {
					if ( i < _values.Length )
						_dictionary[ _keys[ i ] ]  = _values[ i ] ;
					else _dictionary[ _keys[ i ] ] = default ;
				}
			}

			_keys   = null ;
			_values = null ;
		}

		public void OnBeforeSerialize( ) {
			if ( _dictionary is not { Count: > 0 } ) {
				_keys   = null ;
				_values = null ;
			}
			else {
				// Copy dictionary into keys and values array
				int itemCount = _dictionary.Count ;
				_values = new TValue[ itemCount ] ;
				_keys   = new TKey[ itemCount ] ;
				int index = 0 ;

				using var enumerator = _dictionary.GetEnumerator( ) ;
				while ( enumerator.MoveNext( ) ) {
					_keys[ index ]   = enumerator.Current.Key ;
					_values[ index ] = enumerator.Current.Value ;
					++index ;
				}
			}
		}
	}

} // HoudiniEngineUnity