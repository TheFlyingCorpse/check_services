using Icinga;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace check_services
{
    internal class Handler
    {
        public static int ParseArgs(int returncode, string[] args)
        {
            List<string> temp_excluded_services = new List<string>();
            List<string> temp_included_services = new List<string>();
            List<string> temp_stopped_services = new List<string>();
            List<string> temp_running_services = new List<string>();
            List<string> temp_categories = new List<string>();
            List<string> temp_warn_categories = new List<string>();

            List<string> temp_services_in_system_category = new List<string>();
            List<string> temp_services_in_essential_category = new List<string>();
            List<string> temp_services_in_role_category = new List<string>();
            List<string> temp_services_in_supporting_category = new List<string>();
            List<string> temp_services_in_thirdparty_category = new List<string>();
            List<string> temp_services_in_ignored_category = new List<string>();

            var p = new OptionSet()
            {
                { "i|inventory", "Provide the inventory",
                    v => { Settings.bDoInventory = (v != null); } },
                { "c|check-service", "Check the health status of the local services",
                    v => { Settings.bDoCheckServices = (v != null); } },
                { "category=", "Category to check or provide inventory from, default is ThirdParty",
                    v => temp_categories.Add (v)},
                { "excluded-svc=", "Exclude this service",
                    v => temp_excluded_services.Add (v)},
                { "included-svc=", "Explicitly include this service",
                    v => temp_included_services.Add (v)},
                { "stopped-svc=", "This service should be stopped",
                    v => temp_stopped_services.Add (v)},
                { "running-svc=", "Override CSV, this service should be running",
                    v => temp_running_services.Add (v)},
                { "warn-on-category=", "Warn on the specified category. Default is Supporting",
                    v => temp_warn_categories.Add (v)},
                { "inv-format=", "Inventory output format, default is readable, available are csv,readable,i2conf",
                    v => Settings.strInventoryFormat = v },
                { "inv-level=", "Inventory level, normal or full",
                    v => Settings.strInventoryLevel = v },
                { "inv-all-running", "Inventory only the running services",
                    v => { Settings.bDoInvAllRunningOnly = (v != null); } },
                { "inv-hide-empty", "Hide empty vars from inventory output.",
                    v => { Settings.bDoHideEmptyVars = (v != null); } },
                { "single-service", "Specifies that only one Service is to be checked, simplifies output of perfdata and optional perfcounters",
                    v => { Settings.bDoSingleCheck = (v != null); } },
                { "expected-state=", "Set the expected state for the service, used primarily with --single-service option",
                    v => Settings.strExpectedState = v },
                { "split-by=", "Alternative character to split input options VALUE with. Default is ','",
                    v => Settings.strSplitBy = v },
                { "check-all-starttypes", "Check all StartTypes against specified Category, not only Automatic",
                    v => { Settings.bDoCheckAllStartTypes = (v != null); } },
                { "perfcounter", "Extra performance counters, use with caution",
                    v => { Settings.bVerbose = (v != null); } },
                { "delayed-grace=", "Set grace time for Automatic (Delayed) services after boot-up before they must be started",
                    (int v) => Settings.iDelayedGraceDuration = v },
                { "hide-long-output", "Hide verbose output from the --check-service command, simple output",
                    v => { Settings.bDoHideLongOutput = (v != null); } },
                { "hide-category", "Hide category from the normal output from the --check-service command",
                    v => { Settings.bDoHideCategoryFromOuput = (v != null); } },
                { "svc-in-sys-category=", "Set category of specified service to System",
                    v => temp_services_in_system_category.Add (v)},
                { "svc-in-ess-category=", "Set category of specified service to Essential",
                    v => temp_services_in_essential_category.Add (v)},
                { "svc-in-role-category=", "Set category of specified service to Role",
                    v => temp_services_in_role_category.Add (v)},
                { "svc-in-3rd-category=", "Set category of specified service to ThirdParty",
                    v => temp_services_in_thirdparty_category.Add (v)},
                { "svc-in-sup-category=", "Set category of specified service to Supporting",
                    v => temp_services_in_supporting_category.Add (v)},
                { "svc-in-ign-category=", "Set category of specified service to Ignored",
                    v => temp_services_in_ignored_category.Add (v)},
                { "category-file=", "Path to a file which contains an alternative list of Service to Category definitions",
                    v => Settings.strCategoryFilePath = v },
                { "file-format=", "Specify format of the file path given in category-file, default CSV",
                    v => Settings.strCategoryFileFormat = v },
                { "v|verbose", "Verbose output",
                    v => { Settings.bVerbose = (v != null); } },
                { "d|debug", "Debug output",
                    v => { Settings.bDebug = (v != null); } },
                { "h|help", "Show this help",
                    v => { Settings.bDoShowHelp = (v != null); } }
            };

            List<string> extra;
            try
            {
                extra = p.Parse(args);
            }
            catch (OptionException e)
            {
                Console.WriteLine("Error occured during parsing the arguments: " + e);
                return (int)ServiceState.ServiceCritical;
            }
            if (Settings.bDoShowHelp)
            {
                Handler.ShowHelp(p);
                return (int)ServiceState.ServiceUnknown;
            }
            // Return unknown if we do not check services or inventory.
            if (Settings.bDoCheckServices == false && Settings.bDoInventory == false)
            {
                Handler.ShowHelp(p);
                return (int)ServiceState.ServiceUnknown;
            }

            // Translate if need be the Settings.strExpectedState
            Settings.strExpectedState = Inventory.CleanStatus(Settings.strExpectedState);

            // Handle Arguments
            returncode = Handler.HandleArguments(returncode, temp_excluded_services, temp_included_services, temp_stopped_services, temp_running_services, temp_categories, temp_warn_categories, temp_services_in_system_category,
                temp_services_in_essential_category, temp_services_in_role_category, temp_services_in_supporting_category, temp_services_in_thirdparty_category, temp_services_in_ignored_category);

            return returncode;

        }
        public static int HandleArguments(int returncode, List<string> temp_excluded_services, List<string> temp_included_services, List<string> temp_stopped_services, List<string> temp_running_services,
            List<string> temp_categories, List<string> temp_warn_categories, List<string> temp_services_in_system_category, List<string> temp_services_in_essential_category,
            List<string> temp_services_in_role_category, List<string> temp_services_in_supporting_category, List<string> temp_services_in_thirdparty_category, List<string> temp_services_in_ignored_category)
        {
            if (temp_excluded_services.Count > 0)
            {
                if (Settings.bDebug)
                    Console.WriteLine("temp excluded services count: " + temp_excluded_services.Count.ToString());

                Settings.ExcludedServices = SplitList(temp_excluded_services);
                PrintArray("ExcludedServices", Settings.ExcludedServices);
                Settings.bDefaultExcludeList = false;
            }
            if (temp_included_services.Count > 0)
            {
                if (Settings.bDebug)
                    Console.WriteLine("temp included services count: " + temp_included_services.Count.ToString());

                Settings.IncludedServices = SplitList(temp_included_services);
                PrintArray("IncludedServices", Settings.IncludedServices);
                Settings.bDefaultIncludeList = false;
            }
            if (temp_stopped_services.Count > 0)
            {
                if (Settings.bDebug)
                    Console.WriteLine("temp stopped services count: " + temp_stopped_services.Count.ToString());

                Settings.StoppedServices = SplitList(temp_stopped_services);
                PrintArray("StoppedServices", Settings.StoppedServices);
                Settings.bDefaultStoppedList = false;
            }
            if (temp_running_services.Count > 0)
            {
                if (Settings.bDebug)
                    Console.WriteLine("temp running services count: " + temp_running_services.Count.ToString());

                Settings.RunningServices = SplitList(temp_running_services);
                PrintArray("RunningServices", Settings.RunningServices);
                Settings.bDefaultRunningList = false;
            }
            if (temp_categories.Count > 0)
            {
                if (Settings.bDebug)
                    Console.WriteLine("temp categories count: " + temp_categories.Count.ToString());

                Settings.Categories = SplitList(temp_categories);
                PrintArray("Categories", Settings.Categories);
                Settings.bDefaultCategoriesList = false;
            }
            if (temp_warn_categories.Count > 0)
            {
                Settings.WarnCategories = SplitList(temp_warn_categories);
                PrintArray("WarnCategories", Settings.WarnCategories);
                Settings.bDefaultWarnCategoriesList = false;
            }
            if (temp_services_in_system_category.Count > 0)
            {
                Settings.services_in_system_category = SplitList(temp_services_in_system_category);
                PrintArray("services_in_system_category", Settings.services_in_system_category);
                Settings.bDefaultSystemCategory = false;
            }
            if (temp_services_in_essential_category.Count > 0)
            {
                Settings.services_in_essential_category = SplitList(temp_services_in_essential_category);
                PrintArray("services_in_essential_category", Settings.services_in_essential_category);
                Settings.bDefaultEssentialCategory = false;
            }
            if (temp_services_in_role_category.Count > 0)
            {
                Settings.services_in_role_category = SplitList(temp_services_in_role_category);
                PrintArray("services_in_role_category", Settings.services_in_role_category);
                Settings.bDefaultRoleCategory = false;
            }
            if (temp_services_in_supporting_category.Count > 0)
            {
                Settings.services_in_supporting_category = SplitList(temp_services_in_supporting_category);
                PrintArray("services_in_supporting_category", Settings.services_in_supporting_category);
                Settings.bDefaultSupportingCategory = false;
            }
            if (temp_services_in_thirdparty_category.Count > 0)
            {
                Settings.services_in_thirdparty_category = SplitList(temp_services_in_thirdparty_category);
                PrintArray("services_in_thirdparty_category", Settings.services_in_thirdparty_category);
                Settings.bDefaultThirdPartyCategory = false;
            }
            if (temp_services_in_ignored_category.Count > 0)
            {
                Settings.services_in_ignored_category = SplitList(temp_services_in_ignored_category);
                PrintArray("services_in_ignored_category", Settings.services_in_ignored_category);
                Settings.bDefaultIgnoredCategory = false;
            }

            if (Settings.strCategoryFilePath != "unspecified")
            {
                if (File.Exists(Settings.strCategoryFilePath) == false)
                {
                    Console.WriteLine("Error: Specified csv_file not found: " + Settings.strCategoryFilePath);
                    return (int)ServiceState.ServiceUnknown;
                }
            }

            if (Settings.Categories.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (Settings.bVerbose)
                    Console.WriteLine("INFO: Default Categories, setting Category to ThirdParty");
                Settings.bDefaultCategoriesList = true;
                Settings.Categories = new string[] { "ThirdParty" };
            }
            else if (Settings.Categories.Contains("Basic"))
            {
                if (Settings.bVerbose)
                    Console.WriteLine("INFO: Categories set to Basic, inserting System, Essential, Supporting and Role categories to check for.");
                Settings.Categories = new string[] { "Essential", "System", "Supporting", "Role" };
            }
            else
            {
                if (Settings.bVerbose)
                    Console.WriteLine("INFO: The --category flag has been used");
            }

            if (Settings.WarnCategories.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (Settings.bVerbose == true)
                    Console.WriteLine("INFO: Default warn_categories list.");
                Settings.bDefaultWarnCategoriesList = true;
                Settings.WarnCategories = new string[] { "Supporting" };
            }
            else
            {
                if (Settings.bVerbose)
                    Console.WriteLine("INFO: The --warn-on-category flag has been used");
            }

            return returncode;
        }

        private static string[] SplitList(List<string> items)
        {
            if (Settings.strSplitBy == " ")
            {
                return items.ToArray();
            }
            else
            {
                return items.Select(item => item.Split(Settings.strSplitBy.ToCharArray()))
                            .SelectMany(str => str)
                            .ToArray();
            }
        }

        private static void PrintArray(string arrayname, Array array)
        {
            if (Settings.bVerbose)
            {
                Console.WriteLine("DEBUG - Array: " + arrayname);
                foreach (var row in array)
                {
                    Console.WriteLine("DEBUG - row: " + row);
                }
                Console.WriteLine("DEBUG: End of Array: " + arrayname);
            }
        }

        public static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: " + AppDomain.CurrentDomain.FriendlyName + " [OPTIONS] ");
            Console.WriteLine("Version: " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("This plugin checks the State of one or more services on the local machine.");
            Console.WriteLine("You can filter for your services by using category or a combination of category and include/exclude filters.");
            Console.WriteLine("Use the same switch multiple times if you want to combine or exclude multiple categories or services");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}