// Copyright (c) 2017 Colectica. All rights reserved
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Cogs.Model
{
    /// <summary>
    /// Base class for objects in a connected COGS model graph. Objects are mutable while a
    /// model is being assembled and become permanently read-only when a successful
    /// <see cref="CogsBuildResult"/> is created.
    /// </summary>
    public abstract class CogsModelNode
    {
        /// <summary>Gets whether this node belongs to a completed, read-only model graph.</summary>
        public bool IsReadOnly { get; private set; }

        internal void MakeReadOnly()
        {
            if (IsReadOnly)
            {
                return;
            }

            // Mark first so cycles through datatype, property, inheritance, and relationship
            // links terminate while the complete reachable graph is frozen.
            IsReadOnly = true;
            MakeReadOnlyCore();
        }

        protected virtual void MakeReadOnlyCore()
        {
        }

        protected void SetValue<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            ThrowIfReadOnly(propertyName);
            storage = value;
        }

        protected void ThrowIfReadOnly(string memberName = null)
        {
            if (IsReadOnly)
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(memberName)
                        ? "The completed COGS model is read-only."
                        : $"The completed COGS model is read-only; '{memberName}' cannot be changed.");
            }
        }

        protected static IList<T> ReadOnlyCopy<T>(IEnumerable<T> values) =>
            new ReadOnlyCollection<T>((values ?? Enumerable.Empty<T>()).ToArray());
    }
}
