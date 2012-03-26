using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;

using MindTouch.Dream;
using MindTouch.Xml;

namespace MindTouch.PageGrants {

    internal class Page {
        public string path;
        public bool cascade = false;

        public Page(string path, bool cascade) {
            this.path = path;
            this.cascade = cascade;
        }

        public override string ToString() {
            return string.Format("Page path: {0}\nCascate: {1}", path, cascade ? "yes" : "no");
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
                var cascade = pageXml["./@cascade"].AsBool ?? false;
                
                var p = new Page(pagePath, cascade);
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

            return 0;
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
