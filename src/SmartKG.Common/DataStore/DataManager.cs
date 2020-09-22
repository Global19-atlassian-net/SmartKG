﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using SmartKG.Common.Logger;
using SmartKG.Common.Data.KG;
using SmartKG.Common.Data.LU;
using SmartKG.Common.Data;

namespace SmartKG.Common.DataStore
{
    public class DataManager : KGBotLogHandler
    {
        private KnowledgeGraphStore kgStore;
        private NLUStore nluStore;

        private ILogger log;

        public DataManager()
        {        
            this.kgStore = KnowledgeGraphStore.GetInstance();
            this.nluStore = NLUStore.GetInstance();
            log = Log.Logger.ForContext<DataManager>();
        }

        public void LogInformation(ILogger log, string title, string content)
        {
            log.Information(title + " " + content);
        }

        public void LogError(ILogger log, Exception e)
        {
            log.Error(e.Message, e);
        }

        public int GetMaxOptions(string scenarioName)
        {
            
            return this.nluStore.GetSetting(scenarioName).maxOptions;
        }

        public SortSetting GetSortSetting(string scenarioName)
        {
            return this.nluStore.GetSetting(scenarioName).sortSetting;
        }

        public List<DialogSlot> GetConfiguredSlots(string scenarioName)
        {
            if (this.nluStore.GetSetting(scenarioName) != null)
                return this.nluStore.GetSetting(scenarioName).slots;
            else
                return null;
        }

        public List<Vertex> SearchGraphByLabel(string label, string scenarioName, List<AttributePair> attributes)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return null;
            }
            else
            {
                List<Vertex> vertexes = kgStore.GetVertexByLabel(label);
                return Filter(vertexes, scenarioName, attributes);
            }
        }

        public Vertex SearchGraph(string startVertexName, string scenarioName, List<AttributePair> attributes)
        {
            if (string.IsNullOrWhiteSpace(startVertexName) && string.IsNullOrWhiteSpace(scenarioName))
            {
                return null;
            }
            else if (string.IsNullOrWhiteSpace(startVertexName) && !string.IsNullOrWhiteSpace(scenarioName))
            {
                Vertex root = nluStore.GetRoot(scenarioName);
                if (root != null)
                {
                    startVertexName = root.name;
                }
                else
                {
                    return null;
                }
            }

            List<Vertex> vertexes = kgStore.GetVertexByName(startVertexName);

            /*

            if (vertexes != null)
            {
                List<Vertex> results = new List<Vertex>();
                foreach (Vertex vertex in vertexes)
                {
                    if (!string.IsNullOrWhiteSpace(scenarioName) && (vertex.scenarios != null && !vertex.scenarios.Contains(scenarioName)))
                    {
                        continue;
                    }


                    if (IsSelected(vertex, attributes))
                    {
                        results.Add(vertex);
                    }
                }

                if (results.Count > 0)
                {
                    return results[0];
                }
                else { return null; }
            }
            else
            {
                LogInformation(log.Here(), startVertexName, "doesn't filtered with attributes: " + JsonConvert.SerializeObject(attributes));
                return null;
            }
            */

            List<Vertex> results = Filter(vertexes, scenarioName, attributes);

            if (results != null && results.Count > 0)
            {
                return results[0];
            }
            else
            {
                return null;
            }
        }

        private List<Vertex> Filter(List<Vertex> vertexes, string scenarioName, List<AttributePair> attributes)
        {
            
            if (vertexes != null)
            {
                List<Vertex> results = new List<Vertex>();
                foreach (Vertex vertex in vertexes)
                {
                    if (!string.IsNullOrWhiteSpace(scenarioName) && (vertex.scenarios != null && !vertex.scenarios.Contains(scenarioName)))
                    {
                        continue;
                    }


                    if (IsSelected(vertex, attributes))
                    {
                        results.Add(vertex);
                    }
                }

                if (results.Count > 0)
                {
                    return results;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                LogInformation(log.Here(), "Vertexes", "doesn't filtered with attributes: " + JsonConvert.SerializeObject(attributes));
                return null;
            }

        }

        public Dictionary<string, List<Vertex>> FilterGraph(string startVertexName, string scenarioName, HashSet<string> relationTypeSet, List<AttributePair> attributes)
        {

            if (string.IsNullOrWhiteSpace(startVertexName) && string.IsNullOrWhiteSpace(scenarioName))
            {
                return null;
            }
            else if (string.IsNullOrWhiteSpace(startVertexName) && !string.IsNullOrWhiteSpace(scenarioName))
            {
                Vertex root = nluStore.GetRoot(scenarioName);
                if (root != null)
                {
                    startVertexName = root.name;
                }
                else
                {
                    return null;
                }
            }


            Dictionary<string, List<Vertex>> results = new Dictionary<string, List<Vertex>>();

            Vertex startVertex = SearchGraph(startVertexName, scenarioName, attributes);

            if (startVertex == null)
            {
                LogInformation(log.Here(), startVertexName, "doesn't exist");
                return null;
            }

            List<Vertex> tobeProcessedVertexes = new List<Vertex>();
            tobeProcessedVertexes.Add(startVertex);

            int index = 0;

            while (index < tobeProcessedVertexes.Count())
            {
                Vertex vertex = tobeProcessedVertexes[index];


                Dictionary<string, HashSet<string>> childrenIds = new Dictionary<string, HashSet<string>>();

                if (relationTypeSet == null)
                {
                    childrenIds = kgStore.GetChildrenIds(vertex.id, null, scenarioName);
                }
                else
                {
                    foreach (string relationType in relationTypeSet)
                    {
                        Dictionary<string, HashSet<string>> cIds = kgStore.GetChildrenIds(vertex.id, relationType, scenarioName);
                        if (cIds != null && cIds.Count > 0)
                        {                            
                            childrenIds.Add(relationType, cIds[relationType]);
                        }
                    }
                }

                if (childrenIds == null)
                    continue;

                foreach(string relationType in childrenIds.Keys)
                { 
                    foreach (string childId in childrenIds[relationType])
                    {
                        Vertex child = kgStore.GetVertexById(childId);
                        if (child != null)
                        {
                            if (child.isLeaf())
                            {
                                if (IsSelected(child, attributes))
                                {
                                    if (results.ContainsKey(relationType))
                                    { 
                                        results[relationType].Add(child);
                                    }
                                    else
                                    {
                                        results.Add(relationType, new List<Vertex> { vertex });
                                    }
                                }
                            }
                            else
                            {
                                tobeProcessedVertexes.Add(child);
                            }
                        }
                    }
                
                }
                index += 1;
            }

            return results;

        }

        private bool IsSelected(Vertex vertex, List<AttributePair> attributes)
        {

            bool isSelected = true;

            if (attributes == null || attributes.Count() == 0)
                return true;

            foreach (AttributePair attribute in attributes)
            {
                string attributeValue = attribute.attributeValue;
                try
                {
                    string value = vertex.GetPropertyValue(attribute.attributeName);
                    if ((value != null) && (value != "ALL") && (attributeValue != "ALL") && (attributeValue != value))
                    {
                        isSelected = false;
                        break;
                    }

                }
                catch (Exception e)
                {
                    LogError(log.Here(), e);
                }
            }

            return isSelected;
        }

        public Dictionary<string, List<Vertex>> GetChildren(Vertex vertex, HashSet<string> relationTypeSet, List<AttributePair> attributes, string scenarioName)
        {
            if (vertex.isLeaf())
            {
                LogInformation(log.Here(), vertex.name, "is not leaf");
                return null;
            }

            string id = vertex.id;
            
            Dictionary<string, HashSet<string>> childrenIds = new Dictionary<string, HashSet<string>>();

            if (relationTypeSet != null)
            {
                foreach (string relationType in relationTypeSet)
                {
                    Dictionary<string, HashSet<string>> cIds = kgStore.GetChildrenIds(vertex.id, relationType, scenarioName);//allTypeChildrenIds.SelectMany(x => x.Value).ToList();
                    if (cIds != null && cIds.Count > 0)
                    {
                        
                        childrenIds.Add(relationType, cIds[relationType]);
                    }
                }
            }
            else
            {
                childrenIds = kgStore.GetChildrenIds(vertex.id, null, scenarioName);
            }
            

            if (childrenIds == null ||  childrenIds.Count == 0)
            {
                LogInformation(log.Here(), vertex.name, "has no child");
                return null;
            }

            Dictionary<string, List<Vertex>> results = new Dictionary<string, List<Vertex>>();
            foreach (string relationType in childrenIds.Keys)
            {
                List<Vertex> vertexes = new List<Vertex>();

                foreach (string cId in childrenIds[relationType])
                {
                    Vertex child = kgStore.GetVertexById(cId);
                    if (child != null && IsSelected(child, attributes))
                        vertexes.Add(child);
                }

                if (vertexes.Count > 0)
                {
                    results.Add(relationType, vertexes);
                }
                
            }

            return results;
        }                        
    }
}