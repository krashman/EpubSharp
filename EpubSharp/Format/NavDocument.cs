﻿using System.Collections.Generic;
using System.Xml.Linq;

namespace EpubSharp.Format
{
    internal static class NavElements
    {
        public static readonly XName Html = Constants.XhtmlNamespace + "html";

        public static readonly XName Head = "head";
        public static readonly XName Title = "title";
        public static readonly XName Link = "link";
        public static readonly XName Meta = "meta";

        public static readonly XName Body = "body";
    }

    public class NavDocument
    {
        public NavHead Head { get; internal set; }
        public NavBody Body { get; internal set; }
    }

    public class NavHead
    {
        /// <summary>
        /// Instantiated only when the EPUB was read.
        /// </summary>
        internal XElement Dom { get; set; }

        public string Title { get; internal set; }
        public ICollection<NavHeadLink> Links { get; internal set; } = new List<NavHeadLink>();
        public ICollection<NavMeta> Metas { get; internal set; } = new List<NavMeta>();
    }

    public class NavHeadLink
    {
        internal static class Attributes
        {
            public static readonly XName Href = "href";
            public static readonly XName Rel = "rel";
            public static readonly XName Type = "type";
            public static readonly XName Class = "class";
            public static readonly XName Title = "title";
        }

        public string Href { get; internal set; }
        public string Rel { get; internal set; }
        public string Type { get; internal set; }
        public string Class { get; internal set; }
        public string Title { get; internal set; }
    }

    public class NavMeta
    {
        internal static class Attributes
        {
            public static readonly XName Name = "name";
            public static readonly XName Content = "content";
            public static readonly XName Charset = "charset";
        }

        public string Name { get; internal set; }
        public string Content { get; internal set; }
        public string Charset { get; internal set; }
    }

    public class NavBody
    {
        internal static class Attributes
        {
            public static readonly XName Id = "id";
            public static readonly XName Type = Constants.OpsNamespace + "type";
            public static readonly XName Hidden = Constants.OpsNamespace + "hidden";
        }

        /// <summary>
        /// Instantiated only when the EPUB was read.
        /// </summary>
        internal XElement Dom { get; set; }

        public string Type { get; internal set; }
        public string Id { get; internal set; }
        public string Hidden { get; internal set; }
        public XElement Element { get; internal set; }
    }
}
