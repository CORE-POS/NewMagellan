//-------------------------------------------------------------
// <copyright file="Discover.cs" company="Whole Foods Co-op">
//  Released under GPL2 license
// </copyright>
//-------------------------------------------------------------

namespace Discover
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Convert stings to System.Types
    /// </summary>
    public class Discover
    {
        /// <summary>
        /// Cache of current types
        /// </summary>
        private List<Type> cache;

        /// <summary>
        /// Initializes a new instance of the <see cref="Discover"/> class. 
        /// </summary>
        public Discover()
        {
            this.cache = new List<Type>();
            this.BuildCache();
        }

        /// <summary>
        /// Get all subclasses
        /// </summary>
        /// <param name="parent">Parent type</param>
        /// <returns>Subclass types</returns>
        public List<Type> GetSubClasses(Type parent)
        {
            return this.cache.Where(t => t.IsSubclassOf(parent)).ToList();
        }

        /// <summary>
        /// Get all subclasses by string
        /// </summary>
        /// <param name="parent">parent type</param>
        /// <returns>subclass types</returns>
        public List<Type> GetSubClasses(string parent)
        {
            return this.GetSubClasses(this.GetType(parent));
        }

        /// <summary>
        /// Get a type by name
        /// </summary>
        /// <param name="name">type name</param>
        /// <returns>the type</returns>
        public Type GetType(string name)
        {
            return this.cache.Where(c => c.FullName == name).First();
        }

        /// <summary>
        /// Build cache of all current types
        /// </summary>
        /// <returns>number of additions</returns>
        private int BuildCache()
        {
            var query = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(t => t.GetTypes())
                        .Where(t => t.IsClass && t.Namespace != null && !t.Namespace.StartsWith("System"));

            int ret = 0;
            foreach (var t in query.ToList())
            {
                if (!this.cache.Any(c => c.FullName == t.FullName))
                {
                    this.cache.Add(t);
                    ret++;
                }
            }

            return ret;
        }
    }
}
