﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml;

namespace EpubSharp.Format.Readers
{
    internal static class PackageReader
    {
        public static PackageDocument Read(XmlDocument xml)
        {
            var xmlNamespaceManager = new XmlNamespaceManager(xml.NameTable);
            xmlNamespaceManager.AddNamespace("opf", "http://www.idpf.org/2007/opf");
            var packageNode = xml.DocumentElement.SelectSingleNode("/opf:package", xmlNamespaceManager);
            var package = new PackageDocument();

            var epubVersionValue = packageNode.Attributes["version"].Value;
            if (epubVersionValue == "2.0")
            {
                package.EpubVersion = EpubVersion.Epub2;
            }
            else if (epubVersionValue == "3.0" || epubVersionValue == "3.0.1" || epubVersionValue == "3.1")
            {
                package.EpubVersion = EpubVersion.Epub3;
            }
            else
            {
                throw new Exception($"Unsupported EPUB version: {epubVersionValue}.");
            }

            var metadataNode = packageNode.SelectSingleNode("opf:metadata", xmlNamespaceManager);
            if (metadataNode == null)
                throw new Exception("EPUB parsing error: metadata not found in the package.");
            var metadata = ReadMetadata(metadataNode, package.EpubVersion);
            package.Metadata = metadata;
            XmlNode manifestNode = packageNode.SelectSingleNode("opf:manifest", xmlNamespaceManager);
            if (manifestNode == null)
                throw new Exception("EPUB parsing error: manifest not found in the package.");
            package.Manifest = new PackageManifest();
            package.Manifest.Items = ReadManifestItems(manifestNode);
            XmlNode spineNode = packageNode.SelectSingleNode("opf:spine", xmlNamespaceManager);
            if (spineNode == null)
                throw new Exception("EPUB parsing error: spine not found in the package.");
            PackageSpine spine = ReadSpine(spineNode);
            package.Spine = spine;
            XmlNode guideNode = packageNode.SelectSingleNode("opf:guide", xmlNamespaceManager);
            if (guideNode != null)
            {
                PackageGuide guide = ReadGuide(guideNode);
                package.Guide = guide;
            }

            package.NavPath = FindNavPath(package);
            package.NcxPath = FindNcxPath(package);
            package.CoverPath = FindCoverPath(package);

            return package;
        }

        private static string FindCoverPath(PackageDocument package)
        {
            string coverId = null;

            var coverMetaItem = package.Metadata.MetaItems
                .FirstOrDefault(metaItem => string.Compare(metaItem.Name, "cover", StringComparison.OrdinalIgnoreCase) == 0);
            if (coverMetaItem != null)
            {
                coverId = coverMetaItem.Content;
            }
            else
            {
                var item = package.Manifest.Items.FirstOrDefault(e => e.Properties.Contains("cover-image"));
                if (item != null)
                {
                    coverId = item.Href;
                }
            }

            if (coverId == null)
            {
                return null;
            }

            var coverItem = package.Manifest.Items.FirstOrDefault(item => item.Id == coverId);
            return coverItem?.Href;
        }

        private static string FindNcxPath(PackageDocument package)
        {
            var ncxItem = package.Manifest.Items.FirstOrDefault(e => e.MediaType == "application/x-dtbncx+xml");
            if (ncxItem != null)
            {
                package.NcxPath = ncxItem.Href;
            }
            else
            {
                // If we can't find the toc by media-type then try to look for id of the item in the spine attributes as
                // according to http://www.idpf.org/epub/20/spec/OPF_2.0.1_draft.htm#Section2.4.1.2,
                // "The item that describes the NCX must be referenced by the spine toc attribute."

                if (!string.IsNullOrWhiteSpace(package.Spine.Toc))
                {
                    var tocItem = package.Manifest.Items.FirstOrDefault(e => e.Id == package.Spine.Toc);
                    if (tocItem != null)
                    {
                        return tocItem.Href;
                    }
                }
            }

            return null;
        }

        private static string FindNavPath(PackageDocument package)
        {
            var navItem = package.Manifest.Items.FirstOrDefault(e => e.Properties.Contains("nav"));
            return navItem?.Href;
        }

        private static PackageMetadata ReadMetadata(XmlNode metadataNode, EpubVersion epubVersion)
        {
            var titles = new List<string>();
            var creators = new List<PackageMetadataCreator>();
            var subjects = new List<string>();
            var publishers = new List<string>();
            var contributors = new List<PackageMetadataCreator>();
            var date = "";
            var types = new List<string>();
            var formats = new List<string>();
            var identifiers = new List<PackageMetadataIdentifier>();
            var sources = new List<string>();
            var languages = new List<string>();
            var relations = new List<string>();
            var coverages = new List<string>();
            var rights = new List<string>();
            var metaItems = new List<PackageMetadataMeta>();
            var description = "";

            foreach (XmlNode metadataItemNode in metadataNode.ChildNodes)
            {
                var innerText = metadataItemNode.InnerText;
                switch (metadataItemNode.LocalName.ToLowerInvariant())
                {
                    case "title":
                        titles.Add(innerText);
                        break;
                    case "creator":
                        var creator = ReadMetadataCreator(metadataItemNode);
                        creators.Add(creator);
                        break;
                    case "subject":
                        subjects.Add(innerText);
                        break;
                    case "description":
                        description = innerText;
                        break;
                    case "publisher":
                        publishers.Add(innerText);
                        break;
                    case "contributor":
                        var contributor = ReadMetadataContributor(metadataItemNode);
                        contributors.Add(contributor);
                        break;
                    case "date":
                        date = metadataItemNode.InnerText;
                        break;
                    case "type":
                        types.Add(innerText);
                        break;
                    case "format":
                        formats.Add(innerText);
                        break;
                    case "identifier":
                        var identifier = ReadMetadataIdentifier(metadataItemNode);
                        identifiers.Add(identifier);
                        break;
                    case "source":
                        sources.Add(innerText);
                        break;
                    case "language":
                        languages.Add(innerText);
                        break;
                    case "relation":
                        relations.Add(innerText);
                        break;
                    case "coverage":
                        coverages.Add(innerText);
                        break;
                    case "rights":
                        rights.Add(innerText);
                        break;
                    case "meta":
                        if (epubVersion == EpubVersion.Epub2)
                        {
                            var meta = ReadMetadataMetaVersion2(metadataItemNode);
                            metaItems.Add(meta);
                        }
                        else if (epubVersion == EpubVersion.Epub3)
                        {
                            var meta = ReadMetadataMetaVersion3(metadataItemNode);
                            metaItems.Add(meta);
                        }
                        break;
                }
            }
            
            return new PackageMetadata
            {
                Titles = titles,
                Creators = creators,
                Subjects = subjects,
                Publishers = publishers,
                Contributors = contributors,
                Date = date,
                Types = types,
                Formats = formats,
                Identifiers = identifiers,
                Sources = sources,
                Languages = languages,
                Relations = relations,
                Coverages = coverages,
                Rights = rights,
                MetaItems = metaItems,
                Description = description
            };
        }

        private static PackageMetadataCreator ReadMetadataCreator(XmlNode metadataCreatorNode)
        {
            var result = new PackageMetadataCreator();
            foreach (XmlAttribute metadataCreatorNodeAttribute in metadataCreatorNode.Attributes)
            {
                var attributeValue = metadataCreatorNodeAttribute.Value;
                switch (metadataCreatorNodeAttribute.Name.ToLowerInvariant())
                {
                    case "opf:role":
                        result.Role = attributeValue;
                        break;
                    case "opf:file-as":
                        result.FileAs = attributeValue;
                        break;
                    case "opf:alternate-script":
                        result.AlternateScript = attributeValue;
                        break;
                }
            }
            result.Text = metadataCreatorNode.InnerText;
            return result;
        }

        private static PackageMetadataCreator ReadMetadataContributor(XmlNode metadataContributorNode)
        {
            var result = new PackageMetadataCreator();
            foreach (XmlAttribute metadataContributorNodeAttribute in metadataContributorNode.Attributes)
            {
                var attributeValue = metadataContributorNodeAttribute.Value;
                switch (metadataContributorNodeAttribute.Name.ToLowerInvariant())
                {
                    case "opf:role":
                        result.Role = attributeValue;
                        break;
                    case "opf:file-as":
                        result.FileAs = attributeValue;
                        break;
                    case "opf:alternate-script":
                        result.AlternateScript = attributeValue;
                        break;
                }
            }
            result.Text = metadataContributorNode.InnerText;
            return result;
        }

        private static PackageMetadataIdentifier ReadMetadataIdentifier(XmlNode metadataIdentifierNode)
        {
            PackageMetadataIdentifier result = new PackageMetadataIdentifier();
            foreach (XmlAttribute metadataIdentifierNodeAttribute in metadataIdentifierNode.Attributes)
            {
                string attributeValue = metadataIdentifierNodeAttribute.Value;
                switch (metadataIdentifierNodeAttribute.Name.ToLowerInvariant())
                {
                    case "id":
                        result.Id = attributeValue;
                        break;
                    case "opf:scheme":
                        result.Scheme = attributeValue;
                        break;
                }
            }
            result.Text = metadataIdentifierNode.InnerText;
            return result;
        }

        private static PackageMetadataMeta ReadMetadataMetaVersion2(XmlNode metadataMetaNode)
        {
            PackageMetadataMeta result = new PackageMetadataMeta();
            foreach (XmlAttribute metadataMetaNodeAttribute in metadataMetaNode.Attributes)
            {
                string attributeValue = metadataMetaNodeAttribute.Value;
                switch (metadataMetaNodeAttribute.Name.ToLowerInvariant())
                {
                    case "name":
                        result.Name = attributeValue;
                        break;
                    case "content":
                        result.Content = attributeValue;
                        break;
                }
            }
            return result;
        }

        private static PackageMetadataMeta ReadMetadataMetaVersion3(XmlNode metadataMetaNode)
        {
            var result = new PackageMetadataMeta();
            foreach (XmlAttribute metadataMetaNodeAttribute in metadataMetaNode.Attributes)
            {
                var attributeValue = metadataMetaNodeAttribute.Value;
                switch (metadataMetaNodeAttribute.Name.ToLowerInvariant())
                {
                    case "id":
                        result.Id = attributeValue;
                        break;
                    case "refines":
                        result.Refines = attributeValue;
                        break;
                    case "property":
                        result.Property = attributeValue;
                        break;
                    case "scheme":
                        result.Scheme = attributeValue;
                        break;
                }
            }
            result.Content = metadataMetaNode.InnerText;
            return result;
        }

        private static IReadOnlyCollection<PackageManifestItem> ReadManifestItems(XmlNode manifestNode)
        {
            var result = new List<PackageManifestItem>();
            foreach (XmlNode manifestItemNode in manifestNode.ChildNodes)
                if (string.Compare(manifestItemNode.LocalName, "item", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var manifestItem = new PackageManifestItem();
                    foreach (XmlAttribute manifestItemNodeAttribute in manifestItemNode.Attributes)
                    {
                        string attributeValue = manifestItemNodeAttribute.Value;
                        switch (manifestItemNodeAttribute.Name.ToLowerInvariant())
                        {
                            case "id":
                                manifestItem.Id = attributeValue;
                                break;
                            case "href":
                                manifestItem.Href = attributeValue;
                                break;
                            case "properties":
                                manifestItem.Properties = attributeValue.Split(' ');
                                break;
                            case "media-type":
                                manifestItem.MediaType = attributeValue;
                                break;
                            case "required-namespace":
                                manifestItem.RequiredNamespace = attributeValue;
                                break;
                            case "required-modules":
                                manifestItem.RequiredModules = attributeValue;
                                break;
                            case "fallback":
                                manifestItem.Fallback = attributeValue;
                                break;
                            case "fallback-style":
                                manifestItem.FallbackStyle = attributeValue;
                                break;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(manifestItem.Id))
                        throw new Exception("Incorrect EPUB manifest: item ID is missing");
                    if (string.IsNullOrWhiteSpace(manifestItem.Href))
                        throw new Exception("Incorrect EPUB manifest: item href is missing");
                    if (string.IsNullOrWhiteSpace(manifestItem.MediaType))
                        throw new Exception("Incorrect EPUB manifest: item media type is missing");
                    result.Add(manifestItem);
                }
            return result.AsReadOnly();
        }

        private static PackageSpine ReadSpine(XmlNode spineNode)
        {
            var result = new PackageSpine();
            var tocAttribute = spineNode.Attributes["toc"];
            if (!string.IsNullOrWhiteSpace(tocAttribute?.Value))
            {
                result.Toc = tocAttribute.Value;
            }
            
            var itemRefs = new List<PackageSpineItemRef>();
            foreach (XmlNode spineItemNode in spineNode.ChildNodes)
            {
                if (string.Compare(spineItemNode.LocalName, "itemref", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var spineItemRef = new PackageSpineItemRef();
                    var idRefAttribute = spineItemNode.Attributes["idref"];
                    if (string.IsNullOrWhiteSpace(idRefAttribute?.Value))
                        throw new Exception("Incorrect EPUB spine: item ID ref is missing");
                    spineItemRef.IdRef = idRefAttribute.Value;
                    XmlAttribute linearAttribute = spineItemNode.Attributes["linear"];
                    spineItemRef.IsLinear = linearAttribute == null || string.Compare(linearAttribute.Value, "no", StringComparison.OrdinalIgnoreCase) != 0;
                    itemRefs.Add(spineItemRef);
                }
            }
            result.ItemRefs = itemRefs.AsReadOnly();
            return result;
        }

        private static PackageGuide ReadGuide(XmlNode guideNode)
        {
            var references = new List<PackageGuideReference>();

            foreach (XmlNode guideReferenceNode in guideNode.ChildNodes)
            {
                if (string.Compare(guideReferenceNode.LocalName, "reference", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    PackageGuideReference guideReference = new PackageGuideReference();
                    foreach (XmlAttribute guideReferenceNodeAttribute in guideReferenceNode.Attributes)
                    {
                        string attributeValue = guideReferenceNodeAttribute.Value;
                        switch (guideReferenceNodeAttribute.Name.ToLowerInvariant())
                        {
                            case "type":
                                guideReference.Type = attributeValue;
                                break;
                            case "title":
                                guideReference.Title = attributeValue;
                                break;
                            case "href":
                                guideReference.Href = attributeValue;
                                break;
                        }
                    }
                    if (string.IsNullOrWhiteSpace(guideReference.Type))
                        throw new Exception("Incorrect EPUB guide: item type is missing");
                    if (string.IsNullOrWhiteSpace(guideReference.Href))
                        throw new Exception("Incorrect EPUB guide: item href is missing");
                    references.Add(guideReference);
                }
            }

            return new PackageGuide { References = references.AsReadOnly() };
        }
    }
}