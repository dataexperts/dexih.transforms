﻿using dexih.connections.test;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace dexih.connections.flatfile
{
    public class ConnectionAzureFlatFileTests
    {
        public ConnectionFlatFileLocal GetLocalConnection()
        {
            var ServerName = Convert.ToString(Configuration.AppSettings["FlatFileLocal:ServerName"]);
            if (ServerName == "")
                return null;

            var connection  = new ConnectionFlatFileLocal()
            {
                Name = "Test Connection",
                ServerName = ServerName,
            };
            return connection;
        }

        [Fact]
        public async Task FlatFileLocal_Basic()
        {
            string database = "Test-" + Guid.NewGuid().ToString();

            await new UnitTests().Unit(GetLocalConnection(), database);
        }

    }
}