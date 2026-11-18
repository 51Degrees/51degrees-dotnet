/* *********************************************************************
 * This Original Work is copyright of 51 Degrees Mobile Experts Limited.
 * Copyright 2026 51 Degrees Mobile Experts Limited, Davidson House,
 * Forbury Square, Reading, Berkshire, United Kingdom RG1 3EU.
 *
 * This Original Work is licensed under the European Union Public Licence
 * (EUPL) v.1.2 and is subject to its terms as set out below.
 *
 * If a copy of the EUPL was not distributed with this file, You can obtain
 * one at https://opensource.org/licenses/EUPL-1.2.
 *
 * The 'Compatible Licences' set out in the Appendix to the EUPL (as may be
 * amended by the European Commission) shall be deemed incompatible for
 * the purposes of the Work and the provisions of the compatibility
 * clause in Article 5 of the EUPL shall not apply.
 *
 * If using the Work as, or as part of, a network application, by
 * including the attribution notice(s) required under Article 5 of the EUPL
 * in the end user terms of the application under an appropriate heading,
 * such notice(s) shall fulfill the requirements of that article.
 * ********************************************************************* */

using FiftyOne.Pipeline.Core.Exceptions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace FiftyOne.Pipeline.Core.TypedMap
{
    /// <summary>
    /// A collection that stores data of multiple different types.
    /// Data is accessed using an <see cref="ITypedKey{T}"/> that specifies
    /// the unique 'name' to store the data under and the type of the data
    /// being stored.
    /// </summary>
    /// <remarks>
    /// The entries are a linked list, newest first, as the specification
    /// requires for the Element Data of a Flow Data. A node never changes
    /// once it is in the list, so readers follow the links with no lock.
    /// A new node points at the current head. In a thread-safe map it is
    /// put in place with one compare and swap, which tries again if another
    /// thread added first. In any other map it is a plain write. Replacing
    /// or removing an entry, which processing never does, copies the nodes
    /// in front of it. A Flow Data holds one entry for each Flow Element,
    /// so the list is short and a search along it is fast.
    /// See the <see href="https://github.com/51Degrees/specifications/blob/main/pipeline-specification/features/thread-safety.md#flow-data">Specification</see>
    /// </remarks>
    internal class TypedKeyMap : ITypedKeyMap
    {
        /// <summary>
        /// One entry. Never changed once it is in the list.
        /// </summary>
        private sealed class Node
        {
            internal readonly string Key;
            internal readonly object Value;
            internal readonly Node Next;

            internal Node(string key, object value, Node next)
            {
                Key = key;
                Value = value;
                Next = next;
            }
        }

        /// <summary>
        /// The newest entry, or null when the map is empty. Read with
        /// <see cref="Volatile"/> so that a reader sees each node in full.
        /// </summary>
        private Node _head;

        /// <summary>
        /// True if several threads can add at the same time.
        /// </summary>
        private readonly bool _threadSafe;

        /// <summary>
        /// The live view that <see cref="AsStringKeyDictionary"/> returns.
        /// </summary>
        private readonly StringKeyView _view;

        /// <summary>
        /// Default constructor.
        /// Creates a non-thread safe <see cref="TypedKeyMap"/>
        /// </summary>
        public TypedKeyMap() : this(false)
        { }

        /// <summary>
        /// Constructor.
        /// Creates a thread-safe or non-thread safe <see cref="TypedKeyMap"/>
        /// </summary>
        /// <param name="threadSafe">
        /// If true then several threads can add at the same time, each with
        /// one compare and swap. If false then an add is a plain write.
        /// </param>
        public TypedKeyMap(bool threadSafe)
        {
            _threadSafe = threadSafe;
            _view = new StringKeyView(this);
        }

        private Node Head => Volatile.Read(ref _head);

        private static Node Find(Node head, string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            for (var node = head; node != null; node = node.Next)
            {
                if (string.Equals(
                    node.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return node;
                }
            }
            return null;
        }

        /// <summary>
        /// Put the replacement in place of the head, if the head is still
        /// the one that was read.
        /// </summary>
        private bool Swap(Node read, Node replacement)
        {
            if (_threadSafe)
            {
                return ReferenceEquals(
                    Interlocked.CompareExchange(ref _head, replacement, read),
                    read);
            }
            Volatile.Write(ref _head, replacement);
            return true;
        }

        /// <summary>
        /// A copy of the nodes in front of target, followed by a node that
        /// holds value in place of target when replace is true, or by the
        /// nodes after target when it is false.
        /// </summary>
        private static Node Copy(
            Node head, Node target, bool replace, object value)
        {
            if (ReferenceEquals(head, target))
            {
                return replace
                    ? new Node(target.Key, value, target.Next)
                    : target.Next;
            }
            return new Node(
                head.Key,
                head.Value,
                Copy(head.Next, target, replace, value));
        }

        /// <summary>
        /// Add the value unless the key is already there.
        /// </summary>
        /// <param name="key">The key to add.</param>
        /// <param name="value">The value to add.</param>
        /// <param name="held">The value the map then holds for the key.</param>
        /// <returns>True if this call added the value.</returns>
        internal bool TryAdd(string key, object value, out object held)
        {
            while (true)
            {
                var head = Head;
                var existing = Find(head, key);
                if (existing != null)
                {
                    held = existing.Value;
                    return false;
                }
                if (Swap(head, new Node(key, value, head)))
                {
                    held = value;
                    return true;
                }
            }
        }

        /// <summary>
        /// Add the value, replacing any value already held for the key.
        /// </summary>
        private void Set(string key, object value)
        {
            while (true)
            {
                var head = Head;
                var existing = Find(head, key);
                var replacement = existing == null
                    ? new Node(key, value, head)
                    : Copy(head, existing, true, value);
                if (Swap(head, replacement))
                {
                    return;
                }
            }
        }

        /// <summary>
        /// Remove the entry for the key.
        /// </summary>
        /// <returns>True if there was an entry to remove.</returns>
        private bool Remove(string key)
        {
            while (true)
            {
                var head = Head;
                var existing = Find(head, key);
                if (existing == null)
                {
                    return false;
                }
                if (Swap(head, Copy(head, existing, false, null)))
                {
                    return true;
                }
            }
        }

        private bool TryGet(string key, out object value)
        {
            var node = Find(Head, key);
            value = node?.Value;
            return node != null;
        }

        /// <summary>
        /// The entries in the order they were added, oldest first.
        /// </summary>
        private List<Node> Entries()
        {
            var entries = new List<Node>();
            for (var node = Head; node != null; node = node.Next)
            {
                entries.Add(node);
            }
            entries.Reverse();
            return entries;
        }

        /// <summary>
        /// Add the specified data to the collection using the specified key.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the data being stored.
        /// </typeparam>
        /// <param name="key">
        /// The key used to identify the data.
        /// </param>
        /// <param name="data">
        /// The data to store.
        /// </param>
        public void Add<T>(ITypedKey<T> key, T data)
        {
            Set(key.Name, data);
        }

        /// <summary>
        /// Get the data associated with the specified key.
        /// If the key is not present or the data value is null then the
        /// return value will be <code>default(T)</code>.
        /// </summary>
        /// <typeparam name="T">
        /// The type of the data to return.
        /// </typeparam>
        /// <param name="key">
        /// The key used to access the data.
        /// </param>
        /// <returns>
        /// The data.
        /// </returns>
        /// <exception cref="InvalidCastException">
        /// Thrown if the data object stored under the name of the key
        /// cannot be cast to the expected type T.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// Thrown if the key argument is null
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown if the Name property of the key argument is null
        /// </exception>
        /// <exception cref="KeyNotFoundException">
        /// Thrown if the map does not contain an entry for the key
        /// </exception>
        public T Get<T>(ITypedKey<T> key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            if (key.Name == null)
            {
                throw new ArgumentException(Messages.ExceptionKeyNameNull,
                    nameof(key));
            }

            T result = default(T);
            if (TryGet(key.Name, out object obj))
            {
                if (obj != null)
                {
                    result = (T)obj;
                }
            }
            else
            {
                throw new KeyNotFoundException(GetKeyMissingMessage(key.Name));
            }
            return result;
        }

        public T Get<T>()
        {
            // Look once along the list for entries that are assignable to
            // the requested type.
            object match = null;
            bool found = false;
            for (var node = Head; node != null; node = node.Next)
            {
                if (node.Value is T)
                {
                    if (found)
                    {
                        throw new PipelineDataException($"This map contains " +
                            $"multiple data instances matching type '{typeof(T).Name}'");
                    }
                    match = node.Value;
                    found = true;
                }
            }

            if (found == false)
            {
                throw new PipelineDataException(
                    $"This map contains no data matching type " +
                    $"'{typeof(T).Name}'");
            }
            return (T)match;
        }

        private String GetKeyMissingMessage(string key)
        {
            return "There is no data for '" + key + "' against this " +
                "instance of '" + this.GetType().Name +
                "'. Available keys are: " +
                string.Join(", ", GetKeys());
        }

        /// <summary>
        /// Return string values of the keys in the map, in the order they
        /// were added.
        /// </summary>
        /// <returns>
        /// Key strings
        /// </returns>
        public ICollection<string> GetKeys()
        {
            var keys = new List<string>();
            foreach (var node in Entries())
            {
                keys.Add(node.Key);
            }
            return keys;
        }

        /// <summary>
        /// Return the entire collection as a
        /// <see cref="IDictionary{TKey, TValue}"/> object.
        /// This is a live view, so any changes to it are made to the
        /// <see cref="TypedKeyMap"/>. It lists the entries in the order they
        /// were added.
        /// </summary>
        /// <returns>
        /// The data as a <see cref="IDictionary{TKey, TValue}"/>.
        /// </returns>
        public IDictionary<string, object> AsStringKeyDictionary()
        {
            return _view;
        }

        /// <summary>
        /// Check if the map contains an item with the specified
        /// key name and type. If it does exist, retrieve it.
        /// </summary>
        /// <param name="key">
        /// The key to check for.
        /// </param>
        /// <param name="value">
        /// The value associated with the key.
        /// </param>
        /// <returns>
        /// True if an entry matching the key exists in the map.
        /// False otherwise.
        /// </returns>
        public bool TryGetValue<T>(ITypedKey<T> key, out T value)
        {
            bool result = false;
            value = default(T);

            object valueObj = null;
            if(TryGet(key.Name, out valueObj))
            {
                if(typeof(T).IsAssignableFrom(valueObj.GetType()))
                {
                    value = (T)valueObj;
                    result = true;
                }
            }

            return result;
        }

        /// <summary>
        /// A dictionary over the list. It keeps no entries of its own, and
        /// its keys ignore case.
        /// </summary>
        private sealed class StringKeyView : IDictionary<string, object>
        {
            private readonly TypedKeyMap _map;

            internal StringKeyView(TypedKeyMap map)
            {
                _map = map;
            }

            public object this[string key]
            {
                get
                {
                    if (_map.TryGet(key, out var value))
                    {
                        return value;
                    }
                    throw new KeyNotFoundException(
                        _map.GetKeyMissingMessage(key));
                }
                set => _map.Set(key, value);
            }

            public ICollection<string> Keys => _map.GetKeys();

            public ICollection<object> Values
            {
                get
                {
                    var values = new List<object>();
                    foreach (var node in _map.Entries())
                    {
                        values.Add(node.Value);
                    }
                    return values;
                }
            }

            public int Count
            {
                get
                {
                    var count = 0;
                    for (var node = _map.Head; node != null; node = node.Next)
                    {
                        count++;
                    }
                    return count;
                }
            }

            public bool IsReadOnly => false;

            public void Add(string key, object value)
            {
                if (_map.TryAdd(key, value, out _) == false)
                {
                    throw new ArgumentException(
                        $"An entry for '{key}' is already present.",
                        nameof(key));
                }
            }

            public void Add(KeyValuePair<string, object> item) =>
                Add(item.Key, item.Value);

            public void Clear() => Volatile.Write(ref _map._head, null);

            public bool Contains(KeyValuePair<string, object> item) =>
                _map.TryGet(item.Key, out var value) &&
                Equals(value, item.Value);

            public bool ContainsKey(string key) => _map.TryGet(key, out _);

            public void CopyTo(
                KeyValuePair<string, object>[] array,
                int arrayIndex)
            {
                if (array == null)
                {
                    throw new ArgumentNullException(nameof(array));
                }
                foreach (var entry in this)
                {
                    array[arrayIndex++] = entry;
                }
            }

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                foreach (var node in _map.Entries())
                {
                    yield return new KeyValuePair<string, object>(
                        node.Key, node.Value);
                }
            }

            public bool Remove(string key) => _map.Remove(key);

            public bool Remove(KeyValuePair<string, object> item) =>
                Contains(item) && _map.Remove(item.Key);

            public bool TryGetValue(string key, out object value) =>
                _map.TryGet(key, out value);

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
