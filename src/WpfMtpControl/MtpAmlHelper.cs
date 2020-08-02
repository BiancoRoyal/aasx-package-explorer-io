﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AasxUtils;
using Aml.Engine.CAEX;
using WpfMtpControl.DataSources;

namespace WpfMtpControl
{
    public class MtpAmlHelper
    {
        public static bool CheckForRole(CAEXSequence<SupportedRoleClassType> seq, string refRoleClassPath)
        {
            if (seq == null)
                return false;
            foreach (var src in seq)
                if (src.RefRoleClassPath != null && src.RefRoleClassPath.Trim() != "")
                    if (src.RefRoleClassPath.Trim().ToLower() == refRoleClassPath.Trim().ToLower())
                        return true;
            return false;
        }

        public static bool CheckForRole(CAEXSequence<RoleRequirementsType> seq, string refBaseRoleClassPath)
        {
            if (seq == null)
                return false;
            foreach (var src in seq)
                if (src.RefBaseRoleClassPath != null && src.RefBaseRoleClassPath.Trim() != "")
                    if (src.RefBaseRoleClassPath.Trim().ToLower() == refBaseRoleClassPath.Trim().ToLower())
                        return true;
            return false;
        }

        public static bool CheckForRoleClassOrRoleRequirements(SystemUnitClassType ie, string classPath)
        {
            // TODO MICHA+M. WIEGAND: I dont understand the determinism behind that!
            // WIEGAND: me, neither ;-)
            // Wiegand:  ich hab mir von Prof.Drath nochmal erklären lassen, wie SupportedRoleClass und RoleRequirement verwendet werden:
            // In CAEX2.15(aktuelle AML Version und unsere AAS Mapping Version):
            //   1.Eine SystemUnitClass hat eine oder mehrere SupportedRoleClasses, die ihre „mögliche Rolle beschreiben(Drucker / Fax / kopierer)
            //   2.Wird die SystemUnitClass als InternalElement instanziiert entscheidet man sich für eine Hauptrolle, die dann zum RoleRequirement wird 
            //     und evtl.Nebenklassen die dann SupportedRoleClasses sind(ist ein Workaround weil CAEX2.15 in der Norm nur ein RoleReuqirement erlaubt)
            // InCAEX3.0(nächste AMl Version):
            //   1.Wie bei CAEX2.15
            //   2.Wird die SystemUnitClass als Internal Elementinstanziiert werden die verwendeten Rollen jeweils als RoleRequirement zugewiesen (in CAEX3 
            //     sind mehrere RoleReuqirements nun erlaubt)

            // Remark: SystemUnitClassType is suitable for SysUnitClasses and InternalElements

            if (ie is InternalElementType)
                if (CheckForRole((ie as InternalElementType).RoleRequirements, classPath))
                    return true;

            return
                CheckForRole(ie.SupportedRoleClass, classPath);
        }

        public static bool CheckAttributeFoRefSemantic(AttributeType a, string correspondingAttributePath)
        {
            if (a.RefSemantic != null)
                foreach (var rf in a.RefSemantic)
                    if (rf.CorrespondingAttributePath != null && rf.CorrespondingAttributePath.Trim() != ""
                        && rf.CorrespondingAttributePath.Trim().ToLower() == correspondingAttributePath.Trim().ToLower())
                        // found!
                        return true;
            return false;
        }

        public static AttributeType FindAttributeByRefSemantic(AttributeSequence aseq, string correspondingAttributePath)
        {
            foreach (var a in aseq)
            {
                // check attribute itself
                if (CheckAttributeFoRefSemantic(a, correspondingAttributePath))
                    // found!
                    return a;

                // could be childs
                var x = FindAttributeByRefSemantic(a.Attribute, correspondingAttributePath);
                if (x != null)
                    return x;
            }
            return null;
        }

        public static string FindAttributeValueByRefSemantic(AttributeSequence aseq, string correspondingAttributePath)
        {
            var a = FindAttributeByRefSemantic(aseq, correspondingAttributePath);
            return a?.Value;
        }

        public static AttributeType FindAttributeByName(AttributeSequence aseq, string name)
        {
            if (aseq != null)
                foreach (var a in aseq)
                    if (a.Name.Trim() == name.Trim())
                        return a;
            return null;
        }

        public static string FindAttributeValueByName(AttributeSequence aseq, string name)
        {
            var a = FindAttributeByName(aseq, name);
            return a?.Value;
        }

        public static Nullable<int> FindAttributeValueByNameFromInt(AttributeSequence aseq, string name)
        {
            var astr = FindAttributeValueByName(aseq, name);
            if (astr == null)
                return (null);
            return Convert.ToInt32(astr);
        }

        public static Nullable<double> FindAttributeValueByNameFromDouble(AttributeSequence aseq, string name)
        {
            var astr = FindAttributeValueByName(aseq, name);
            if (astr == null)
                return (null);
            double res;
            if (!double.TryParse(astr, NumberStyles.Any, CultureInfo.InvariantCulture, out res))
                return null;
            return res;
        }

        public static List<Tuple<string, SystemUnitClassType>> FindAllMtpPictures(CAEXFileType aml)
        {
            // start
            var res = new List<Tuple<string, SystemUnitClassType>>();

            // assumption: all pictures are on the 1st level of a instance hierarchy ..
            foreach (var ih in aml.InstanceHierarchy)
                foreach (var ie in ih.InternalElement)
                    if (ie.RefBaseSystemUnitPath.Trim() == "MTPHMISUCLib/Picture")
                        res.Add(new Tuple<string, SystemUnitClassType>(ie.Name, ie));

            // ok
            return res;
        }

        public static Dictionary<string, SystemUnitClassType> FindAllDynamicInstances(CAEXFileType aml)
        {
            // start           
            var res = new Dictionary<string, SystemUnitClassType>(StringComparer.InvariantCultureIgnoreCase);
            if (aml == null)
                return res;

            // assumption: all instances are on a fixed level of a instance hierarchy ..
            foreach (var ih in aml.InstanceHierarchy)                   // e.g.: ModuleTypePackage
                foreach (var ie in ih.InternalElement)                  // e.g. Class = ModuleTypePackage
                    foreach (var ie2 in ie.InternalElement)             // e.g. CommunicationSet
                        foreach (var ie3 in ie2.InternalElement)        // e.g. InstanceList
                            if (ie3.RefBaseSystemUnitPath.Trim() == "MTPSUCLib/CommunicationSet/InstanceList")
                                foreach (var ie4 in ie3.InternalElement)     // now ALWAYS an dynamic instance
                                {
                                    var refID = MtpAmlHelper.FindAttributeValueByName(ie4.Attribute, "RefID");
                                    if (refID != null && refID.Length>0)
                                        res.Add(refID, ie4);
                                }

            // ok
            return res;
        }

        public static void CreateDataSources (IMtpDataSourceFactoryOpcUa dataSourceFactory, CAEXFileType aml)
        {
            // access
            if (dataSourceFactory == null || aml == null)
                return;

            // assumption: all instances are on a fixed level of a instance hierarchy ..
            foreach (var ih in aml.InstanceHierarchy)                               // e.g.: ModuleTypePackage
                foreach (var ie in ih.InternalElement)                              // e.g. Class = ModuleTypePackage
                    foreach (var ie2 in ie.InternalElement)                         // e.g. CommunicationSet
                        foreach (var ie3 in ie2.InternalElement)                    // e.g. InstanceList
                            if (ie3.RefBaseSystemUnitPath.Trim() == "MTPSUCLib/CommunicationSet/SourceList")
                                foreach (var server in ie3.InternalElement)     // now on server
                                {
                                    // check if server
                                    if (server.RefBaseSystemUnitPath.Trim() != "MTPCommunicationSUCLib/ServerAssembly/OPCUAServer")
                                        continue;

                                    // get attributes
                                    var ep = FindAttributeValueByName(server.Attribute, "Endpoint");
                                    if (ep == null || ep.Trim().Length < 1)
                                        continue;

                                    // make server
                                    var serv = dataSourceFactory.CreateOrUseUaServer(ep);
                                    if (serv == null)
                                        continue;

                                    // go into items
                                    foreach (var item in server.ExternalInterface)
                                    {
                                        // check fo item
                                        // TODO: spec/example files seem not to be in a final state
                                        if (!item.RefBaseClassPath.Trim().Contains("OPCUAItem"))
                                            continue;

                                        // get attributes
                                        var id = FindAttributeValueByName(item.Attribute, "Identifier");
                                        var ns = FindAttributeValueByName(item.Attribute, "Namespace");
                                        var ac = FindAttributeValueByName(item.Attribute, "Access");

                                        // create
                                        var it = dataSourceFactory.CreateOrUseItem(serv, id, ns, ac, item.ID);
                                    }
                                }

        }

        public static double[] ConvertStringToDoubleArray(string input, char[] separator)
        {
            var pieces = input.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            if (pieces == null)
                return null;
            var res = new List<double>();
            foreach (var p in pieces)
            {
                double num;
                if (!double.TryParse(p.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out num))
                    // fail immediately
                    return null;
                res.Add(num);
            }
            return res.ToArray();
        }

        /// <summary>
        /// Edges delimited by ';', coordinates by ','. Example: "1,2;3,4;5,6".
        /// </summary>
        public static PointCollection PointCollectionFromString(string edgepath)
        {
            var edges = edgepath.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (edges == null || edges.Length < 2)
                return null;

            var pc = new PointCollection();
            foreach (var e in edges)
            {
                var coord = ConvertStringToDoubleArray(e, new char[] { ',' });
                if (coord != null && coord.Length == 2)
                    pc.Add(new Point(coord[0], coord[1]));
            }
            return pc;
        }

    }
}
