﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Serilog;
using SmartKG.Common.Data.KG;
using SmartKG.Common.Data.Visulization;
using SmartKG.Common.DataStoreMgmt;
using SmartKG.Common.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartKG.KGManagement.GraphSearch
{
    public class GraphExecutor
    {
        private KnowledgeGraphDataFrame kgDF;
        private ILogger log;

        public GraphExecutor(string datastoreName)
        {
            DataStoreFrame dsFrame = DataStoreManager.GetInstance().GetDataStore(datastoreName);
            this.kgDF = dsFrame.GetKG();

            log = Log.Logger.ForContext<GraphExecutor>();
        }

        public void LogInformation(ILogger log, string title, string content)
        {
            log.Information(title + " " + content);
        }

        public void LogError(ILogger log, Exception e)
        {
            log.Error(e.Message, e);
        }

        public VisulizedVertex GetVertexById(string vId)
        {
            Vertex vertex =  this.kgDF.GetVertexById(vId);

            if (vertex == null)
                return null;
            else
                return ConvertVertex(vertex);
        }
        
        public List<VisulizedVertex> SearchVertexesByName(string keyword)
        {
            List<Vertex> searchedVertexes =  this.kgDF.GetVertexByKeyword(keyword);

            if (searchedVertexes == null || searchedVertexes.Count == 0)
            {
                return null;
            }

            List<VisulizedVertex> results = new List<VisulizedVertex>();
            
            foreach(Vertex vertex in searchedVertexes)
            {
                results.Add(ConvertVertex(vertex));
            }

            return results;
        }

        public List<VisulizedVertex> FilterVertexesByProperty(string propertyName, string propertyValue)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                Exception e = new Exception("Invalid input: propertyName is empty.");
                LogError(this.log, e);
                throw (e);
            }

            if (string.IsNullOrEmpty(propertyValue))
            {
                Exception e = new Exception("Invalid input: propertyValue is empty.");
                LogError(this.log, e);
                throw (e);
            }

            List<Vertex> allVertexes = this.kgDF.GetAllVertexes();

            List<VisulizedVertex> results = new List<VisulizedVertex>();

            foreach(Vertex vertex in allVertexes)
            {
                string value = vertex.GetPropertyValue(propertyName);

                if (value == null || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }
                else
                {
                    if (value == propertyValue)
                    {
                        results.Add(ConvertVertex(vertex));
                    }
                }
            }

            return results;
        }

        public List<string> GetScenarioNames()
        {
            HashSet<string> names = this.kgDF.GetScenarioNames();
            if (names == null || names.Count == 0)
            {
                return null;
            }
            else
            { 
                return this.kgDF.GetScenarioNames().ToList();
            }
        }

        public List<ColorConfig> GetColorConfigs(string scenarioName)
        {
            Dictionary<string, List<ColorConfig>> colorConfigs = this.kgDF.GetVertexLabelColorMap();

            if (colorConfigs == null || colorConfigs.Count == 0)
            {
                return null;
            }
            else if (string.IsNullOrWhiteSpace(scenarioName))
            {
                List<ColorConfig> configs = new List<ColorConfig>();
                foreach (string scenario in colorConfigs.Keys)
                {
                    configs.AddRange(colorConfigs[scenario]);
                }

                return configs;
            }
            else
            {
                if (!colorConfigs.ContainsKey(scenarioName))
                {
                    return null;
                }
                else
                {
                    return colorConfigs[scenarioName];
                }
            }
        }

        public (List<VisulizedVertex>, List<VisulizedEdge>) GetVertexesAndEdgesByScenarios(List<string> scenarios)
        {
            List<Vertex> catchedVertexes = this.kgDF.GetVertexesByScenarios(scenarios);

            List<VisulizedVertex> vvs = new List<VisulizedVertex>();

            foreach(Vertex vertex in catchedVertexes)
            {
                vvs.Add(ConvertVertex(vertex));
            }

            List<Edge> catchedEdges = this.kgDF.GetRelationsByScenarios(scenarios);

            List<VisulizedEdge> ves = new List<VisulizedEdge>();

            foreach(Edge edge in catchedEdges)
            {
                ves.Add(ConvertEdge(edge));
            }
            
            return (vvs, ves);
        }

        public (List<VisulizedVertex>, List<VisulizedEdge>) GetFirstLevelRelationships(string vId)
        {
            Vertex vertex = this.kgDF.GetVertexById(vId);

            if (vertex == null)
            {
                return (null, null);
            }

            List<VisulizedVertex> rVVs = new List<VisulizedVertex>();
            List<VisulizedEdge> rVEs = new List<VisulizedEdge>();

            if (vertex.properties != null && vertex.properties.Count > 0)
            {
                foreach(VertexProperty property in vertex.properties)
                {
                    VisulizedVertex vP = KGUtility.GeneratePropertyVVertex(vertex.label, property.name, property.value);
                    rVVs.Add(vP);

                    VisulizedEdge vEdge = new VisulizedEdge();
                    vEdge.value = vP.displayName;
                    vEdge.sourceId = vertex.id;
                    vEdge.targetId = vP.id;

                    rVEs.Add(vEdge);
                }
            }

            List<VisulizedVertex> tmpRVVs;
            List<VisulizedEdge> tmpRVEs;

            Dictionary <RelationLink, List<string>> childrenLinkDict = this.kgDF.GetChildrenLinkDict(vId);

            if (childrenLinkDict != null)
            {
                (tmpRVVs, tmpRVEs) = GetConnectedVertexesAndEdges(vId, childrenLinkDict, true);

                rVVs.AddRange(tmpRVVs);
                rVEs.AddRange(tmpRVEs);
            }

            Dictionary<RelationLink, List<string>> parentLinkDict = this.kgDF.GetParentLinkDict(vId);

            if (parentLinkDict != null)
            {
                (tmpRVVs, tmpRVEs) = GetConnectedVertexesAndEdges(vId, parentLinkDict, false);

                rVVs.AddRange(tmpRVVs);
                rVEs.AddRange(tmpRVEs);
            }

            return (rVVs, rVEs);
        }

        private (List<VisulizedVertex>, List<VisulizedEdge>) GetConnectedVertexesAndEdges(string vId, Dictionary<RelationLink, List<string>> relationDict, bool vIsSrc)
        {
            List<VisulizedVertex> rVVs = new List<VisulizedVertex>();
            List<VisulizedEdge> rVEs = new List<VisulizedEdge>();

            HashSet<string> cIdSet = new HashSet<string>();

            if (relationDict != null && relationDict.Count > 0)
            {
                foreach (RelationLink link in relationDict.Keys)
                {
                    string relationType = link.relationType;

                    foreach (string cId in relationDict[link])
                    {
                        if (cIdSet.Contains(cId))
                        {
                            continue;
                        }
                        
                        cIdSet.Add(cId);
                        
                        VisulizedVertex vRV = ConvertVertex(this.kgDF.GetVertexById(cId));

                        VisulizedEdge vRE = new VisulizedEdge();
                        vRE.value = relationType;

                        if (vIsSrc)
                        { 
                            vRE.sourceId = vId;
                            vRE.targetId = vRV.id;
                        }
                        else
                        {
                            vRE.targetId = vId;
                            vRE.sourceId = vRV.id;
                        }

                        rVVs.Add(vRV);
                        rVEs.Add(vRE);
                    }
                }
            }

            return (rVVs, rVEs);
        }
        

        private VisulizedVertex ConvertVertex(Vertex vertex)
        {
            if (vertex == null)
            {
                return null;
            }

            VisulizedVertex vv = new VisulizedVertex();
            vv.id = vertex.id;
            vv.name = vertex.name;
            vv.displayName = vertex.name;
            vv.label = vertex.label;

            return vv;
        }

        private VisulizedEdge ConvertEdge(Edge edge)
        {
            if (edge == null)
            {
                return null;
            }

            VisulizedEdge ve = new VisulizedEdge();
            ve.sourceId = edge.headVertexId;
            ve.targetId = edge.tailVertexId;
            ve.value = edge.relationType;

            return ve;
        }
    }
}
