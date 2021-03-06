

using System.Runtime.Serialization;

namespace dexih.transforms
{
    public class ConnectionBase
    {
           /// <summary>
        /// Category of the connection
        /// </summary>
        [DataMember(Order = 0)]
        public Connection.EConnectionCategory ConnectionCategory { get; set; }

        /// <summary>
        /// Connection Name
        /// </summary>
        [DataMember(Order = 0)]
        public string Name { get; set; }

        /// <summary>
        /// Description for the connection
        /// </summary>
        [DataMember(Order = 1)]
        public string Description { get; set; }

        /// <summary>
        /// Description for the database property (such as database, directory etc.)
        /// </summary>
        [DataMember(Order = 2)]
        public string DatabaseDescription { get; set; }

        /// <summary>
        /// Description for the server property (such as server name, web address etc.)
        /// </summary>
        [DataMember(Order = 3)]
        public string ServerDescription { get; set; }

        /// <summary>
        /// Allows for a connection string to use for credentials
        /// </summary>
        [DataMember(Order = 4)]
        public bool AllowsConnectionString { get; set; }

        /// <summary>
        /// Allows Sql Entry
        /// </summary>
        [DataMember(Order = 5)]
        public bool AllowsSql { get; set; }

        /// <summary>
        /// Uses files which can be managed (such as moving from incoming/processed directories.
        /// </summary>
        [DataMember(Order = 6)]
        public bool AllowsFlatFiles { get; set; }

        /// <summary>
        /// Can be used as a managed connection, supporting read/write and table create functions.
        /// </summary>
        [DataMember(Order = 7)]
        public bool AllowsManagedConnection { get; set; }

        /// <summary>
        /// Can be used a s source connection
        /// </summary>
        [DataMember(Order = 8)]
        public bool AllowsSourceConnection { get; set; }

        /// <summary>
        /// Can be used as a target connection
        /// </summary>
        [DataMember(Order = 9)]
        public bool AllowsTargetConnection { get; set; }

        /// <summary>
        /// Can use a username/password combination.
        /// </summary>
        [DataMember(Order = 10)]
        public bool AllowsUserPassword { get; set; }

        /// <summary>
        /// Can use windows authentication
        /// </summary>
        [DataMember(Order = 11)]
        public bool AllowsWindowsAuth { get; set; }

        /// <summary>
        /// Requires a database to be specified
        /// </summary>
        [DataMember(Order = 12)]
        public bool RequiresDatabase { get; set; }

        /// <summary>
        /// Requires access tothe local file system.
        /// </summary>
        [DataMember(Order = 13)]
        public bool RequiresLocalStorage { get; set; }
    }
}