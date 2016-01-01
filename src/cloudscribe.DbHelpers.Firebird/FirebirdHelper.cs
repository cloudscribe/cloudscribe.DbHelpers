// Copyright (c) Source Tree Solutions, LLC. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
// Author:					Joe Audette
// Created:					2016-01-01
// Last Modified:			2016-01-01
// 

using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace cloudscribe.DbHelpers
{
    public class FirebirdHelper : AdoHelper
    {
        public FirebirdHelper(DbProviderFactory factory) : base(factory)
        {
            // not sure we should throw this, 
            // what if the DbProviderFactory is a wrapper around FirebirdClientFactory 
            // with decorator pattern, ie for glimpse tracking queries
            //if(!(factory is FirebirdSql.Data.FirebirdClient.FirebirdClientFactory))
            //{ throw new ArgumentException("Expected FirebirdClientFactory"); }
        }

        

        public string GetParamString(int count)
        {
            if (count <= 1) { return count < 1 ? "" : "?"; }
            return "?," + GetParamString(count - 1);
        }
    }
}
