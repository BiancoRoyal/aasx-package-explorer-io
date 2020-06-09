﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using AdminShellNS;

namespace AasxPackageExplorer
{

    public class field
    {
        public string name;
        public string value;
        public string description;
    }

    public class UaNode
    {
        public string UAObjectTypeName;
        public string NodeId;
        public string ParentNodeId;
        public string BrowseName;
        public string NameSpace;
        public string SymbolicName;
        public string DataType;
        public string Description;
        public string Value;
        public string DisplayName;

        public object parent;
        public List<UaNode> children;
        public List<string> references;

        public string DefinitionName;
        public string DefinitionNameSpace;
        public List<field> fields;

        public UaNode()
        {
            children = new List<UaNode> { };
            references = new List<string> { };
            fields = new List<field> { };
        }
    }

    public static class OpcUaTools
    {
        static List<UaNode> roots;
        static List<UaNode> nodes;
        static Dictionary<string, UaNode> parentNodes;

        public static void ImportNodeSetToSubModel(string inputFn, AdminShell.AdministrationShellEnv env, AdminShell.Submodel sm, AdminShell.SubmodelRef smref)
        {
            XmlTextReader reader = new XmlTextReader(inputFn);
            StreamWriter sw = File.CreateText(inputFn + ".log.txt");

            string elementName = "";
            string lastElementName = "";
            bool tagDefinition = false;
            string referenceType = "";

            roots = new List<UaNode> { };
            nodes = new List<UaNode> { };
            parentNodes = new Dictionary<string, UaNode>();
            UaNode currentNode = null;

            // global model data
            string ModelUri = "";
            string ModelUriVersion = "";
            string ModelUriPublicationDate = "";
            string RequiredModelUri = "";
            string RequiredModelUriVersion = "";
            string RequiredModelUriPublicationDate = "";


            // scan nodeset and store node data in nodes
            // store also roots, i.e. no parent in node
            // store also new ParentNodeIds in parentNodes with value null
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        elementName = reader.Name;
                        switch (elementName)
                        {
                            case "Model":
                                ModelUri = reader.GetAttribute("ModelUri");
                                ModelUriVersion = reader.GetAttribute("Version");
                                ModelUriPublicationDate = reader.GetAttribute("PublicationDate");
                                break;
                            case "RequiredModel":
                                RequiredModelUri = reader.GetAttribute("ModelUri");
                                RequiredModelUriVersion = reader.GetAttribute("Version");
                                RequiredModelUriPublicationDate = reader.GetAttribute("PublicationDate");
                                break;
                            case "UADataType":
                            case "UAVariable":
                            case "UAObject":
                            case "UAMethod":
                            case "UAReferenceType":
                            case "UAObjectType":
                            case "UAVariableType":
                                string parentNodeId = reader.GetAttribute("ParentNodeId");
                                currentNode = new UaNode();
                                currentNode.UAObjectTypeName = elementName;
                                currentNode.NodeId = reader.GetAttribute("NodeId");
                                currentNode.ParentNodeId = parentNodeId;
                                currentNode.BrowseName = reader.GetAttribute("BrowseName");
                                var split = currentNode.BrowseName.Split(':');
                                if (split.Length > 1)
                                {
                                    currentNode.NameSpace = split[0];
                                    if (split.Length == 2)
                                        currentNode.BrowseName = split[1];
                                }
                                currentNode.SymbolicName = reader.GetAttribute("SymbolicName");
                                currentNode.DataType = reader.GetAttribute("DataType");
                                break;
                            case "Reference":
                                referenceType = reader.GetAttribute("ReferenceType");
                                break;
                            case "Definition":
                                tagDefinition = true;
                                currentNode.DefinitionName = reader.GetAttribute("Name");
                                var splitd = currentNode.DefinitionName.Split(':');
                                if (splitd.Length > 1)
                                {
                                    currentNode.DefinitionNameSpace = splitd[0];
                                    if (splitd.Length == 2)
                                        currentNode.DefinitionName = splitd[1];
                                }
                                break;
                            case "Field":
                                field f = new field();
                                f.name = reader.GetAttribute("Name");
                                f.value = reader.GetAttribute("Value");
                                currentNode.fields.Add(f);
                                break;
                            case "Description":
                                break;
                        }
                        break;
                    case XmlNodeType.Text:
                        switch (elementName)
                        {
                            case "String":
                            case "DateTime":
                            case "Boolean":
                            case "Int32":
                            case "ByteString":
                                currentNode.Value = reader.Value;
                                break;
                            case "Description":
                                if (tagDefinition)
                                {
                                    int count = currentNode.fields.Count;
                                    if (count > 0)
                                    {
                                        currentNode.fields[count - 1].description = reader.Value;
                                    }
                                }
                                else
                                {
                                    currentNode.Description = reader.Value;
                                }
                                break;
                            case "Reference":
                                string reference = referenceType + " " + reader.Value;
                                currentNode.references.Add(reference);
                                break;
                            case "DisplayName":
                                currentNode.DisplayName = reader.Value;
                                break;
                        }
                        break;
                    case XmlNodeType.EndElement: //Display the end of the element.
                        switch (reader.Name)
                        {
                            case "Definition":
                                tagDefinition = false;
                                break;
                        }
                        if (currentNode == null || currentNode.UAObjectTypeName == null)
                        {
                            break;
                        }
                        if (reader.Name == currentNode.UAObjectTypeName)
                        {
                            switch (currentNode.UAObjectTypeName)
                            {
                                case "UADataType":
                                case "UAVariable":
                                case "UAObject":
                                case "UAMethod":
                                case "UAReferenceType":
                                case "UAObjectType":
                                case "UAVariableType":
                                    nodes.Add(currentNode);
                                    if (currentNode.ParentNodeId == null || currentNode.ParentNodeId == "")
                                    {
                                        roots.Add(currentNode);
                                    }
                                    else
                                    {
                                        // collect different parentNodeIDs to set corresponding node in dictionary later
                                        if (!parentNodes.ContainsKey(currentNode.ParentNodeId))
                                            parentNodes.Add(currentNode.ParentNodeId, null);
                                    }
                                    break;
                            }
                        }
                        lastElementName = reader.Name;
                        break;
                }
            }

            sw.Close();

            // scan nodes and store parent node in parentNodes value
            foreach (UaNode n in nodes)
            {
                UaNode p = null;
                if (parentNodes.TryGetValue(n.NodeId, out p))
                {
                    parentNodes[n.NodeId] = n;
                }
            }

            // scan nodes and set parent and children for node
            foreach (UaNode n in nodes)
            {
                if (n.ParentNodeId != null && n.ParentNodeId != "")
                {
                    UaNode p = null;
                    if (parentNodes.TryGetValue(n.ParentNodeId, out p))
                    {
                        n.parent = p;
                        p.children.Add(n);
                    }
                }
            }

            // store models information
            /*
            ModelUri = reader.GetAttribute("ModelUri");
            ModelUriVersion = reader.GetAttribute("Version");
            ModelUriPublicationDate = reader.GetAttribute("PublicationDate");
            RequiredModelUri = reader.GetAttribute("ModelUri");
            RequiredModelUriVersion = reader.GetAttribute("Version");
            RequiredModelUriPublicationDate = reader.GetAttribute("PublicationDate");
            */
            var msemanticID = AdminShell.Key.CreateNew("GlobalReference", false, "IRI", ModelUri + "models");
            var msme = AdminShell.SubmodelElementCollection.CreateNew("Models", null, msemanticID);
            msme.semanticId.Keys.Add(AdminShell.Key.CreateNew("UATypeName", false, "OPC", "Models"));
            sm.Add(msme);
            // modeluri
            msemanticID = AdminShell.Key.CreateNew("GlobalReference", false, "IRI", ModelUri + "models/modeluri");
            var mp = AdminShell.Property.CreateNew("ModelUri", null, msemanticID);
            mp.valueType = "string";
            mp.value = ModelUri;
            msme.Add(mp);
            // modeluriversion
            msemanticID = AdminShell.Key.CreateNew("GlobalReference", false, "IRI", ModelUri + "models/modeluriversion");
            mp = AdminShell.Property.CreateNew("ModelUriVersion", null, msemanticID);
            mp.valueType = "string";
            mp.value = ModelUriVersion;
            msme.Add(mp);
            // modeluripublicationdate
            msemanticID = AdminShell.Key.CreateNew("GlobalReference", false, "IRI", ModelUri + "models/modeluripublicationdate");
            mp = AdminShell.Property.CreateNew("ModelUriPublicationDate", null, msemanticID);
            mp.valueType = "string";
            mp.value = ModelUriPublicationDate;
            msme.Add(mp);
            // requiredmodeluri
            msemanticID = AdminShell.Key.CreateNew("GlobalReference", false, "IRI", ModelUri + "models/requiredmodeluri");
            mp = AdminShell.Property.CreateNew("RequiredModelUri", null, msemanticID);
            mp.valueType = "string";
            mp.value = RequiredModelUri;
            msme.Add(mp);
            // modeluriversion
            msemanticID = AdminShell.Key.CreateNew("GlobalReference", false, "IRI", ModelUri + "models/requiredmodeluriversion");
            mp = AdminShell.Property.CreateNew("RequiredModelUriVersion", null, msemanticID);
            mp.valueType = "string";
            mp.value = RequiredModelUriVersion;
            msme.Add(mp);
            // modeluripublicationdate
            msemanticID = AdminShell.Key.CreateNew("GlobalReference", false, "IRI", ModelUri + "models/requiredmodeluripublicationdate");
            mp = AdminShell.Property.CreateNew("RequiredModelUriPublicationDate", null, msemanticID);
            mp.valueType = "string";
            mp.value = RequiredModelUriPublicationDate;
            msme.Add(mp);

            // iterate through independent root trees
            foreach (UaNode n in roots)
            {
                String name = n.BrowseName;
                if (n.SymbolicName != null && n.SymbolicName != "")
                {
                    name = n.SymbolicName;
                }
                var semanticID = AdminShell.Key.CreateNew("GlobalReference", false, "IRI", ModelUri + name);
                if (n.children != null && n.children.Count != 0)
                {
                    var sme = AdminShell.SubmodelElementCollection.CreateNew(name, null, semanticID);
                    sme.semanticId.Keys.Add(AdminShell.Key.CreateNew("UATypeName", false, "OPC", n.UAObjectTypeName));
                    sm.Add(sme);
                    if (n.Value != "")
                    {
                        var p = AdminShell.Property.CreateNew(name, null, semanticID);
                        storeProperty(n, ref p);
                        sme.Add(p);
                    }
                    foreach (UaNode c in n.children)
                    {
                        createSubmodelElements(c, env, sme, smref, ModelUri + name + "/");
                    }
                }
                else
                {
                    var p = AdminShell.Property.CreateNew(name, null, semanticID);
                    storeProperty(n, ref p);
                    sm.Add(p);
                }
            }
        }

        public static void createSubmodelElements(UaNode n, AdminShell.AdministrationShellEnv env, AdminShell.SubmodelElementCollection smec, AdminShell.SubmodelRef smref, string path)
        {
            String name = n.BrowseName;
            if (n.SymbolicName != null && n.SymbolicName != "")
            {
                name = n.SymbolicName;
            }
            var semanticID = AdminShell.Key.CreateNew("GlobalReference", false, "IRI", path + name);
            if (n.children != null && n.children.Count != 0)
            {
                var sme = AdminShell.SubmodelElementCollection.CreateNew(name, null, semanticID);
                sme.semanticId.Keys.Add(AdminShell.Key.CreateNew("UATypeName", false, "OPC", n.UAObjectTypeName));
                smec.Add(sme);
                if (n.Value != "")
                {
                    var p = AdminShell.Property.CreateNew(name, null, semanticID);
                    storeProperty(n, ref p);
                    sme.Add(p);
                }
                foreach (UaNode c in n.children)
                {
                    createSubmodelElements(c, env, sme, smref, path + name + "/");
                }
            }
            else
            {
                var p = AdminShell.Property.CreateNew(name, null, semanticID);
                storeProperty(n, ref p);
                smec.Add(p);
            }
        }

        public static void storeProperty(UaNode n, ref AdminShell.Property p)
        {
            p.valueType = "string";
            p.value = n.Value;
            p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UATypeName", false, "OPC", n.UAObjectTypeName));
            p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UANodeId", false, "OPC", n.NodeId));
            if (n.ParentNodeId != null && n.ParentNodeId != "")
                p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UAParentNodeId", false, "OPC", n.ParentNodeId));
            if (n.BrowseName != null && n.BrowseName != "")
                p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UABrowseName", false, "OPC", n.BrowseName));
            if (n.DisplayName != null && n.DisplayName != "")
                p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UADisplayName", false, "OPC", n.DisplayName));
            if (n.NameSpace != null && n.NameSpace != "")
                p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UANameSpace", false, "OPC", n.NameSpace));
            if (n.SymbolicName != null && n.SymbolicName != "")
                p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UASymbolicName", false, "OPC", n.SymbolicName));
            if (n.DataType != null && n.DataType != "")
                p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UADataType", false, "OPC", n.DataType));
            if (n.Description != null && n.Description != "")
                p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UADescription", false, "OPC", n.Description));
            foreach (string s in n.references)
            {
                p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UAReference", false, "OPC", s));
            }
            if (n.DefinitionName != null && n.DefinitionName != "")
                p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UADefinitionName", false, "OPC", n.DefinitionName));
            if (n.DefinitionNameSpace != null && n.DefinitionNameSpace != "")
                p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UADefinitionNameSpace", false, "OPC", n.DefinitionNameSpace));
            foreach (field f in n.fields)
            {
                p.semanticId.Keys.Add(AdminShell.Key.CreateNew("UAField", false, "OPC", f.name + " = " + f.value + " : " + f.description));
            }
        }
    }
}
