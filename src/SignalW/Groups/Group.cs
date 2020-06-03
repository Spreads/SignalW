using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Spreads.Collections.Generic;
using Spreads.SignalW.Connections;

namespace Spreads.SignalW.Groups
{
    public class Group
    {
        public long Id { get; }

        /// <summary>
        /// Client could request subscription to a group by name.
        /// They must have read access to be a part of the group.
        /// </summary>
        public string Name { get; }

        public ConnectionList Connections { get; }

    }

    public abstract class GroupManager
    {
        // TODO per hub - WeakReference or a small list of  to 

        private ConcurrentDictionary<string, Group> _groupsByName = new ConcurrentDictionary<string, Group>(StringComparer.InvariantCultureIgnoreCase);
        private ConcurrentDictionary<long, Group> _groupsById = new ConcurrentDictionary<long, Group>();

        public abstract bool TryFindGroup(string name, out Group group);
        public abstract bool TryFindGroup(long id, out Group group);

        /// <summary>
        /// True if connection is authorized to be added to receivers.
        /// </summary>
        public virtual bool CouldRead(Group group, Connection connection) => false;

        /// <summary>
        /// True if connection is authorized to be a writer for a group.
        /// </summary> 
        public virtual bool CouldWrite(Group group, Connection connection) => false;

        public virtual bool AllowMultiWriters(Group group) => false;

    }
}
