﻿/*
 * MindTouch Dream - a distributed REST framework 
 * Copyright (C) 2006-2011 MindTouch, Inc.
 * www.mindtouch.com  oss@mindtouch.com
 *
 * For community documentation and downloads visit wiki.developer.mindtouch.com;
 * please review the licensing section.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;

using MindTouch.Dream;
using MindTouch.Xml;

namespace MindTouch.PageGrants {

    /// <summary>
    /// Page class to keep track of new restrictions
    /// </summary>
    internal class Page {
        public string path;
        public string cascade;
        public string restriction;
        public List<XDoc> grants;

        public Page(string path, string cascade, string restriction, List<XDoc> grants) {
            this.path = path;
            this.cascade = cascade;
            this.restriction = restriction;
            this.grants = grants;
        }

        public override string ToString() {
            var sw = new StringWriter();
            sw.WriteLine(string.Format("Page path: {0}\nRestriction: {1}\nCascade: {2}", path, restriction, cascade));
            foreach(var grant in grants) {
                sw.WriteLine(string.Format("\n{0}\n", grant));
            }
            return sw.ToString();
        }
    };

    internal class Program {
        static int Main(string[] args) {
            string site = "", username = "", password = "";
            bool verbose = false, dryrun = false;
            bool showHelp = false;
            string configFile = "";

            var options = new Options() {
                { "s=|site=", "Site address", s => site = s },
                { "u=|username", "Username", u => username = u },
                { "p=|password", "Password", p => password = p },
                { "v|verbose", "Enable verbose output", v => {verbose = true;} },
                { "d|dryrun", "Only perform a dry run, do not change actual data", d => {dryrun = true;} },
            };

            // Validate arguments
            if(args == null || args.Length == 0) {
                showHelp = true;
            } else {
                try {
                    var trailingOptions = options.Parse(args).ToArray();

                    if(trailingOptions.Length < 1) {
                        showHelp = true;
                    } else {
                        configFile = Path.GetFullPath(trailingOptions.First());
                    }
                } catch(InvalidOperationException) {
                    showHelp = true;
                }
                CheckArg(site, "No sitename was specified");
                CheckArg(username, "No username was specified");
                CheckArg(password, "No password was specified");
            }
            if(showHelp) {
                ShowHelp(options);
                return -1;
            }

            // Read config File
            XDoc config;
            try {
                config = XDocFactory.LoadFrom(configFile, MimeType.XML);
            } catch(FileNotFoundException) {
                Console.WriteLine(string.Format("Could not find file: {0}", configFile));
                return -1;
            }

            // Create page listing from config file
            var pageList = new List<Page>();
            foreach(var pageXml in config["//page"]) {
                var pagePath = pageXml["./path"].AsText;
                if(string.IsNullOrEmpty(pagePath)) {
                    Console.WriteLine(String.Format("WARNING: page path was not specified: \n\n{0}", pageXml));
                    continue;
                }
                var cascade = pageXml["./@cascade"].AsText ?? "none";
                var restriction = pageXml["./restriction"].AsText ?? "";
                var grants = new List<XDoc>();
                foreach(var grant in pageXml[".//grant"]) {
                    grants.Add(grant);
                }
                var p = new Page(pagePath, cascade, restriction, grants);
                pageList.Add(p);
                if(verbose) {
                    Console.WriteLine(p.ToString());
                }
            }
            if(pageList.Count == 0) {
                Console.WriteLine("No page configurations were parsed from the XML config file.");
                return -1;
            }

            // Connect to MindTouch
            if(!site.StartsWith("http://") && !site.StartsWith("https://")) {
                site = "http://" + site;
            }
            var siteUri = new XUri(site);
            var plug = Plug.New(siteUri).At("@api", "deki").WithCredentials(username, password);
            DreamMessage msg;

            // Check connection with MindTouch
            try {
                msg = plug.At("site", "status").Get();
            } catch(Exception ex) {
                Console.WriteLine(string.Format("Cannot connect to {0} with the provided credentials", site));
                return -1;
            }

            // Update pages
            foreach(var page in pageList) {
                if(!dryrun) {
                    UpdatePage(plug, page, verbose);
                }
            }

            return 0;
        }

        private static void UpdatePage(Plug plug, Page page, bool verbose) {
            if(verbose) {
                Console.WriteLine("Processing page: " + page.path);
            }
            string encodedPath = XUri.EncodeSegment(XUri.EncodeSegment(page.path));
            DreamMessage msg;
            try {
                msg = plug.At("pages", "=" + encodedPath, "security").Get();
            } catch(Exception ex) {
                Console.WriteLine(string.Format("WARNING: processing of page {0} failed", page.path));
                if(verbose) {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
                return;
            }
            var securityDoc = msg.ToDocument();
            if(!string.IsNullOrEmpty(page.restriction)) {
                securityDoc["./permissions.page/restriction"].ReplaceValue(page.restriction);
            }
            var grants = new XDoc("grants.added");
            foreach(var grant in page.grants) {
                grants.Add(grant);
            }
            securityDoc["/security"].Add(grants);
            try {
                msg = plug.At("pages", "=" + encodedPath, "security").With("cascade", page.cascade).Post(securityDoc);
            } catch(Exception ex) {
                Console.WriteLine(string.Format("WARNING: processing of page {0} failed", page.path));
                if(verbose) {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
                return;
            }
        }

        private static void ShowHelp(Options p) {
            var sw = new StringWriter();
            sw.WriteLine("Usage: mindtouch.pagegrants.exe -s site.mindtouch.us -u admin -p password config.xml");
            p.WriteOptionDescriptions(sw);
            Console.WriteLine(sw.ToString());
        }

        private static void CheckArg(string arg, string message) {
            if(string.IsNullOrEmpty(arg)) {
                throw new ArgumentException(message);
            }
        }
    }
}
