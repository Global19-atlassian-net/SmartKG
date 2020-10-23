﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using SmartKG.Common.Data;
using MongoDB.Driver;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using SmartKG.Common.Logger;
using MongoDB.Bson;

namespace SmartKG.Common.ContextStore
{
    public class ContextDBAccessor : IContextAccessor
    {
        private ILogger log;
        
        private IMongoCollection<DialogContext> collection;

        int MAX_DURATION_INVALID_INPUT = 3;

        public ContextDBAccessor(string connectionString, string dbName)         {
            log = Log.Logger.ForContext<ContextDBAccessor>();

                MongoClient client = new MongoClient(connectionString);

                IMongoDatabase db = client.GetDatabase(dbName);
                this.collection = db.GetCollection<DialogContext>("Contexts");

            log.Here().Information("Context in MongoDB. connectionString: " + connectionString + ", ContextDatabaseName: " + dbName);                
        }

        public (bool, DialogContext) GetContext(string userId, string sessionId)
        {
            bool newlyCreated = true;

            DialogContext context = null;
            try
            {
                var results = collection.FindAsync(x => x.userId == userId && x.sessionId == sessionId).Result;
                List<DialogContext> contexts = null;

                if (results != null)
                {
                    contexts = results.ToList<DialogContext>();
                }

                if (contexts != null && contexts.Count() > 0)
                {
                    context = contexts[0];
                    newlyCreated = false;
                }
                else
                {
                    context = new DialogContext(userId, sessionId, MAX_DURATION_INVALID_INPUT);
                    this.collection.InsertOneAsync(context);
                }               
            }
            catch (Exception e)
            {
                log.Error(e, e.Message);
                throw (e);
            }

            return (newlyCreated, context);
        }

        public bool UpdateContext(string userId, string sessionId, DialogContext context)
        {
            try
            {
                this.collection.ReplaceOneAsync<DialogContext>(x => x.userId == userId && x.sessionId == sessionId, context);                
            }
            catch (Exception e)
            {
                Log.Error(e, e.Message);
                throw (e);
            }

            return true;
        }

        public void CleanContext()
        {
            BsonDocument allFilter = new BsonDocument();
            this.collection.DeleteMany(allFilter);
            this.collection.InsertOne(new DialogContext("000", "000", 3));
        }
    }
}
