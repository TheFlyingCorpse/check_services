﻿using Mono.Options;
using Icinga;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.ServiceProcess;
using MonitoringPluginsForWindows;

namespace check_services
{
    public class Program
    {
        private enum StartType : int
        {
            Boot = 0,
            System = 1,
            Automatic = 2,
            Manual = 3,
            Disabled = 4
        }

        private static List<string> listPerfData = new List<string>();
        private static List<string> listServiceOutput = new List<string>();
        private static List<WinServiceDefined> listWinServicesFromDefinition = new List<WinServiceDefined>();
        private static List<WinServiceActual> listWinServicesOnComputer = new List<WinServiceActual>();
        private static List<string> listServicePerfCounters = new List<string>();

        private static int iRegKeyStart = 0;
        private static int iRegKeyDelayedAutoStart = 0;
        private static int iRegKeyWOW64 = 0;
        private static bool bRegKeyDelayedAutoStart = false;
        private static bool bRegKeyWOW64 = false;

        private static string strObjectName = "";
        private static string strFileOwner = "";
        private static string strImagePath = "";
        private static string strResolvedImagePath = "";

        private static bool errorServices = false;
        private static bool do_i2 = false;

        private static int iNumberOfServices = 0;
        private static int iNumberOfRunningServices = 0;
        private static int iNumberOfStoppedServices = 0;
        private static int iNumberOfPendingServices = 0;
        private static int iNumberOfPausedServices = 0;
        private static int iNumberOfUnknownServices = 0;
        private static int iNumberOfCorrectServices = 0;
        private static int iNumberOfWrongServices = 0;

        private static string outputServices = "";

        private static int Main(string[] args)
        {
            int returncode = 0;
            int temp = 3;

            bool do_inventory = false;
            bool do_services = false;
            bool do_show_help = false;
            bool do_all_running_only = false;
            bool do_all_starttypes = false;
            bool do_hide_long_output = false;
            bool do_hide_category_from_output = false;
            bool do_hide_empty_vars = false;
            bool do_single_check = false;

            string inventory_format = "readable";
            string inventory_level = "normal";
            string expected_state = "Running";
            string split_by = ",";

            int delayed_grace_duration = 60;

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
                    v => { do_inventory = (v != null); } },
                { "c|check-services", "Check the health status of the local services",
                    v => { do_services = (v != null); } },
                { "category=", "Category to check, default is ThirdParty",
                    v => temp_categories.Add (v)},
                { "excluded-svc=", "Exclude this service",
                    v => temp_excluded_services.Add (v)},
                { "included-svc=", "Excplicity include this service",
                    v => temp_included_services.Add (v)},
                { "stopped-svc=", "This service should be stopped",
                    v => temp_stopped_services.Add (v)},
                { "running-svc=", "Override CSV, this service should be running",
                    v => temp_running_services.Add (v)},
                { "warn-on-category=", "Warn on the specified category. Default is Supporting",
                    v => temp_warn_categories.Add (v)},
                { "inv-format=", "Inventory output format, default is readable, available are csv,readable,i2conf",
                    v => inventory_format = v },
                { "inv-level=", "Inventory level, normal or full",
                    v => inventory_level = v },
                { "inv-all-running", "Inventory only the running services",
                    v => { do_all_running_only = (v != null); } },
                { "inv-hide-empty", "Hide empty vars from inventory output.",
                    v => { do_hide_empty_vars = (v != null); } },
                { "single-check", "Specifies that only one Service is to be checked, simplifies output of perfdata and perfcounters",
                    v => { do_single_check = (v != null); } },
                { "expected-state=", "Set the expected state for the service, used primarly with --single-service option",
                    v => expected_state = v },
                { "split-by=", "Alternative character to split input options VALUES with",
                    v => split_by = v },
                { "check-all-starttypes", "Check all StartTypes against specified Category, not only Automatic",
                    v => { do_all_starttypes = (v != null); } },
                { "perfcounter", "Extra performance counters, use with caution",
                    v => { Settings.bVerbose = (v != null); } },
                { "delayed-grace=", "Set gracetime for Automatic (Delayed) services after bootup before they must be started",
                    (int v) => delayed_grace_duration = v },
                { "hide-long-output", "Hide verbose output from the --check-service command, simple output",
                    v => { do_hide_long_output = (v != null); } },
                { "hide-category", "Hide category from the normal output from the --check-service command",
                    v => { do_hide_category_from_output = (v != null); } },
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
                { "svc-in-ign-category=", "Set category of specified service to Ingored",
                    v => temp_services_in_ignored_category.Add (v)},
                { "icinga2", "Set mode to be from icinga2 (WIP)",
                    v => { do_i2 = (v != null); } },
                { "category-file=", "Path to a file which contains an alternative list of Service to Category definitions",
                    v => Settings.strCategoryFilePath = v },
                { "file-format=", "Specify format of the file path given in category-file, default CSV",
                    v => Settings.strCategoryFileFormat = v },
                { "v|verbose", "Verbose output",
                    v => { Settings.bVerbose = (v != null); } },
                { "d|debug", "Debug output",
                    v => { Settings.bDebug = (v != null); } },
                { "h|help", "Show this help",
                    v => { do_show_help = (v != null); } }
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

            // Return unknown if we do not check services or inventory.
            if (do_services == false && do_inventory == false)
            {
                ShowHelp(p);
                return (int)ServiceState.ServiceUnknown;
            }

            // Handle Arguments
            returncode = HandleArguments(returncode, temp_excluded_services, temp_included_services, temp_stopped_services, temp_running_services, temp_categories, temp_warn_categories, temp_services_in_system_category,
                temp_services_in_essential_category, temp_services_in_role_category, temp_services_in_supporting_category, temp_services_in_thirdparty_category, temp_services_in_ignored_category, split_by);

            // Return if something is not ok.
            if (returncode > (int)ServiceState.ServiceOK)
                return returncode;

            // Translate if need be the expected_state
            expected_state = CleanStatus(expected_state);

            // Inventory is blocked from running at the same time as other checks, thus it is run first if specified.
            if (do_inventory == true && inventory_format == "readable")
            {
                temp = ServicesInventoryReadable(inventory_level, do_all_running_only, do_hide_empty_vars);
                return (int)ServiceState.ServiceUnknown;
            }
            else if (do_inventory == true && inventory_format == "csv")
            {
                temp = ServicesInventoryCSV(inventory_level, do_all_running_only);
                return (int)ServiceState.ServiceUnknown;
            }
            else if (do_inventory == true && inventory_format == "i2conf")
            {
                temp = ServicesInventoryI2Conf(inventory_level, do_all_running_only, do_hide_empty_vars);
                return (int)ServiceState.ServiceUnknown;
            }
            else if (do_inventory == true && inventory_format == "json")
            {
                temp = ServicesInventoryJSON(inventory_level, do_all_running_only);
                return (int)ServiceState.ServiceUnknown;
            }
            else if (do_inventory == true)
            {
                Console.WriteLine("Unknown inventory, format: '" + inventory_format + "', level: '" + inventory_level + "'");
                return (int)ServiceState.ServiceUnknown;
            }

            if (do_services == true)
            {
                returncode = CheckAllServicesDeux(inventory_level, returncode, do_all_running_only, do_all_starttypes, delayed_grace_duration, do_hide_category_from_output, do_single_check, expected_state);
            }

            returncode = HandleExitText(returncode, do_hide_long_output);

            return (int)returncode;
        }

        private static void ShowHelp(OptionSet p)
        {
            Console.WriteLine("Usage: " + System.AppDomain.CurrentDomain.FriendlyName + " [OPTIONS]");
            Console.WriteLine("This plugin checks the State of one or more services on the local machine.");
            Console.WriteLine("You can filter for your services by using category or a combination of category and include/exclude filters. Use the same switch multiple times if you want to");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }

        private static string[] SplitList(List<string> items, string split_by)
        {
            if (split_by == null)
            {
                throw new ArgumentNullException("split_by");
            }
            else if (split_by == " ")
            {
                return items.ToArray();
            }
            else
            {
                return items.Select(item => item.Split(split_by.ToCharArray()))
                            .SelectMany(str => str)
                            .ToArray();
            }
        }

        private static int HandleArguments(int returncode, List<string> temp_excluded_services, List<string> temp_included_services, List<string> temp_stopped_services, List<string> temp_running_services,
            List<string> temp_categories, List<string> temp_warn_categories, List<string> temp_services_in_system_category, List<string> temp_services_in_essential_category,
            List<string> temp_services_in_role_category, List<string> temp_services_in_supporting_category, List<string> temp_services_in_thirdparty_category, List<string> temp_services_in_ignored_category,
            string split_by)
        {
            string temp;
            if (temp_excluded_services.Count > 0)
            {
                Settings.excluded_services = SplitList(temp_excluded_services, split_by);
                PrintArray("Settings.excluded_services", Settings.excluded_services);
            }
            if (temp_included_services.Count > 0)
            {
                Settings.included_services = SplitList(temp_included_services, split_by);
                PrintArray("Settings.included_services", Settings.included_services);
            }
            if (temp_stopped_services.Count > 0)
            {
                Settings.stopped_services = SplitList(temp_stopped_services, split_by);
                PrintArray("Settings.stopped_services", Settings.stopped_services);
            }
            if (temp_running_services.Count > 0)
            {
                Settings.running_services = SplitList(temp_running_services, split_by);
                PrintArray("Settings.running_services", Settings.running_services);
            }
            if (temp_categories.Count > 0)
            {
                Settings.categories = SplitList(temp_categories, split_by);
                PrintArray("Settings.categories", Settings.categories);
            }
            if (temp_warn_categories.Count > 0)
            {
                Settings.warn_categories = SplitList(temp_warn_categories, split_by);
                PrintArray("Settings.warn_categories", Settings.warn_categories);
            }
            if (temp_services_in_system_category.Count > 0)
            {
                Settings.services_in_system_category = SplitList(temp_services_in_system_category, split_by);
                PrintArray("Settings.services_in_system_category", Settings.services_in_system_category);
            }
            if (temp_services_in_essential_category.Count > 0)
            {
                Settings.services_in_essential_category = SplitList(temp_services_in_essential_category, split_by);
                PrintArray("Settings.services_in_essential_category", Settings.services_in_essential_category);
            }
            if (temp_services_in_role_category.Count > 0)
            {
                Settings.services_in_role_category = SplitList(temp_services_in_role_category, split_by);
                PrintArray("Settings.services_in_role_category", Settings.services_in_role_category);
            }
            if (temp_services_in_supporting_category.Count > 0)
            {
                Settings.services_in_supporting_category = SplitList(temp_services_in_supporting_category, split_by);
                PrintArray("Settings.services_in_supporting_category", Settings.services_in_supporting_category);
            }
            if (temp_services_in_thirdparty_category.Count > 0)
            {
                Settings.services_in_thirdparty_category = SplitList(temp_services_in_thirdparty_category, split_by);
                PrintArray("Settings.services_in_thirdparty_category", Settings.services_in_thirdparty_category);
            }
            if (temp_services_in_ignored_category.Count > 0)
            {
                Settings.services_in_ignored_category = SplitList(temp_services_in_ignored_category, split_by);
                PrintArray("Settings.services_in_ignored_category", Settings.services_in_ignored_category);
            }

            if (Settings.strCategoryFilePath != "unspecified")
            {
                if (File.Exists(Settings.strCategoryFilePath) == false)
                {
                    Console.WriteLine("Error: Specified csv_file not found: " + Settings.strCategoryFilePath);
                    return (int)ServiceState.ServiceUnknown;
                }
            }

            if (Settings.excluded_services.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (Settings.bVerbose == true)
                    Console.WriteLine("INFO: Default Settings.excluded_services list.");
                Settings.bDefaultExcludeList = true;
            }

            if (Settings.included_services.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (Settings.bVerbose == true)
                    Console.WriteLine("INFO: Default Settings.included_services list.");
                Settings.bDefaultIncludeList = true;
            }

            if (Settings.categories.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (Settings.bVerbose == true)
                    Console.WriteLine("INFO: Default Settings.categories list, setting it to ThirdParty.");
                Settings.bDefaultCategoriesList = true;
                Settings.categories = new string[] { "ThirdParty" };
            }
            else if (Settings.categories.Contains("Basic"))
            {
                Settings.categories = new string[] { "Essential", "System", "Supporting", "Role" };
            }

            if (Settings.warn_categories.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (Settings.bVerbose == true)
                    Console.WriteLine("INFO: Default Settings.warn_categories list.");
                Settings.bDefaultWarnCategoriesList = true;
                Settings.warn_categories = new string[] { "Supporting" };
            }

            if (Settings.services_in_system_category.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (Settings.bVerbose == true)
                    Console.WriteLine("INFO: Default Settings.services_in_system_category list.");
                Settings.bDefaultSystemCategory = true;
            }
            if (Settings.services_in_essential_category.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (Settings.bVerbose == true)
                    Console.WriteLine("INFO: Default Settings.services_in_essential_category list.");
                Settings.bDefaultEssentialCategory = true;
            }
            if (Settings.services_in_role_category.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (Settings.bVerbose == true)
                    Console.WriteLine("INFO: Default Settings.services_in_role_category list.");
                Settings.bDefaultRoleCategory = true;
            }
            if (Settings.services_in_supporting_category.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (Settings.bVerbose == true)
                    Console.WriteLine("INFO: Default Settings.services_in_supporting_category list.");
                Settings.bDefaultSupportingCategory = true;
            }
            if (Settings.services_in_ignored_category.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (Settings.bVerbose == true)
                    Console.WriteLine("INFO: Default Settings.services_in_ignored_category list.");
                Settings.bDefaultIgnoredCategory = true;
            }
            return returncode;
        }

        private static int HandleExitText(int returncode, bool do_hide_long_output)
        {
            // ORDER the output
            string output = "";
            output = outputServices;

            // Handle returncode and exit with proper messages.
            if (returncode == (int)ServiceState.ServiceOK)
            {
                output = "OK: " + output;
            }
            else if (returncode == (int)ServiceState.ServiceWarning)
            {
                output = "WARNING: " + output;
            }
            else if (returncode == (int)ServiceState.ServiceCritical)
            {
                output = "CRITICAL: " + output;
            }
            else if (returncode == (int)ServiceState.ServiceUnknown)
            {
                output = "UNKNOWN: " + output;
            }
            else
            {
                output = "UNHANDLED: " + output;
            }

            string outputLong = "";
            if (iNumberOfServices > 1 && do_hide_long_output == false)
            {
                int x = 1;
                foreach (string outputS in listServiceOutput)
                {
                    if (x < listServiceOutput.Count)
                    {
                        outputLong = outputLong + outputS + "\n";
                    }
                    else
                    {
                        outputLong = outputLong + outputS;
                    }
                    x++;
                }
            }

            string perfdata = "";
            foreach (string outputP in listPerfData)
            {
                perfdata = perfdata + outputP;
            }

            if (do_i2 == true)
            {
                CheckResult IcingaOutput = new CheckResult();
                IcingaOutput.State = (ServiceState)returncode;

                if (do_hide_long_output == true)
                {
                    IcingaOutput.Output = output;
                }
                else
                {
                    IcingaOutput.Output = output + outputLong;
                }

                IcingaOutput.PerformanceData = perfdata;
            }
            else
            {
                Console.Write(output);
                if (iNumberOfServices > 1 && do_hide_long_output == false)
                    Console.Write("\n" + outputLong);

                Console.Write(" | " + perfdata);
            }

            return returncode;
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

        private static int RegReadIntFromHKLMService(string service, string key)
        {
            int value = 0;
            try
            {
                using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services\" + service))
                {
                    value = (int)regKey.GetValue(key);
                }
            }
            catch (Exception e)
            {
                if (Settings.bDebug == true && Settings.bVerbose == true)
                {
                    Console.WriteLine("Did not read " + key + " from Registry, error: " + e);
                }
            }
            return value;
        }

        private static bool RegReadBoolFromHKLMService(string service, string key)
        {
            bool value = false;
            try
            {
                using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services\" + service))
                {
                    value = (bool)regKey.GetValue(key);
                }
            }
            catch (Exception e)
            {
                if (Settings.bDebug == true && Settings.bVerbose == true)
                {
                    Console.WriteLine("Did not read " + key + " from Registry, error: " + e);
                }
            }
            return value;
        }

        private static string RegReadStringFromHKLMService(string service, string key)
        {
            string value = "missing value";
            try
            {
                using (RegistryKey regKey = Registry.LocalMachine.OpenSubKey(@"System\CurrentControlSet\Services\" + service))
                {
                    value = (string)regKey.GetValue(key);
                }
            }
            catch (Exception e)
            {
                if (Settings.bDebug == true)
                {
                    Console.WriteLine("Did not read " + key + " from Registry, error: " + e);
                }
            }
            return value;
        }

        private static int ServicesInventoryReadable(string inventory_level, bool do_all_running_only, bool do_hide_empty_vars)
        {
            bool temp = true;

            // Import service definitions
            temp = ImportServiceDefinitions();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            // Import all services
            temp = ImportServicesOnMachine(inventory_level, do_all_running_only, true);
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            Dictionary<String, WinServiceActual> listOverActualServices = listWinServicesOnComputer.ToDictionary(o => o.ServiceName, o => o);
            foreach (var ActualService in listOverActualServices)
            {
                var LocalService = ActualService.Value;
                string readable = ReadableSerializer.Serialize(LocalService, do_hide_empty_vars);
                Console.WriteLine("Service: " + LocalService.ServiceName + ":\n" + readable);
                Console.WriteLine("");
            }

            return (int)ServiceState.ServiceOK;
        }

        private static int ServicesInventoryCSV(string inventory_level, bool do_all_running_only)
        {
            bool temp = true;

            // Import service definitions
            temp = ImportServiceDefinitions();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            // Import all services
            temp = ImportServicesOnMachine(inventory_level, do_all_running_only, true);
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            Console.WriteLine("ServiceName,DisplayName,StartType,DelayedAutostart,ObjectName,ExpectedStatus,ServiceCategory,WOW64,FileOwner,ImagePath,ResolvedImagePath");

            Dictionary<String, WinServiceActual> listOverActualServices = listWinServicesOnComputer.ToDictionary(o => o.ServiceName, o => o);
            foreach (var ActualService in listOverActualServices)
            {
                var LocalService = ActualService.Value;

                Console.WriteLine("\"" + LocalService.ServiceName + "\",\"" + LocalService.DisplayName + "\"," + LocalService.StartType + "," +
                    LocalService.DelayedAutostart + ",\"" + LocalService.ObjectName + "\"," + LocalService.CurrentStatus + "," + LocalService.ServiceCategory +
                    "," + LocalService.WOW64 + ",\"" + LocalService.FileOwner + "\",\"" + LocalService.ImagePath + "\",\"" + LocalService.ResolvedImagePath + "\"");
            }

            return (int)ServiceState.ServiceOK;
        }

        private static int ServicesInventoryJSON(string inventory_level, bool do_all_running_only)
        {
            bool temp = true;

            // Import service definitions
            temp = ImportServiceDefinitions();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            // Import all services
            temp = ImportServicesOnMachine(inventory_level, do_all_running_only, true);
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            Dictionary<String, WinServiceActual> listOverActualServices = listWinServicesOnComputer.ToDictionary(o => o.ServiceName, o => o);
            string json = JsonConvert.SerializeObject(listWinServicesOnComputer, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            Console.WriteLine("Json: " + json);

            return (int)ServiceState.ServiceOK;
        }

        private static int ServicesInventoryI2Conf(string inventory_level, bool do_all_running_only, bool do_hide_empty_vars)
        {
            bool temp = true;

            // Import service definitions
            temp = ImportServiceDefinitions();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            // Import all services
            temp = ImportServicesOnMachine(inventory_level, do_all_running_only, true);
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            Dictionary<String, WinServiceActual> listOverActualServices = listWinServicesOnComputer.ToDictionary(o => o.ServiceName, o => o);
            foreach (var ActualService in listOverActualServices)
            {
                var LocalService = ActualService.Value;
                string i2conf = IcingaSerializer.Serialize(LocalService, do_hide_empty_vars);
                Console.WriteLine("vars.inv.windows.service[\"" + LocalService.ServiceName + "\"] = " + i2conf);
                Console.WriteLine("");
            }

            return (int)ServiceState.ServiceOK;
        }

        public static int ServiceStartupMode(string inventory_level, string service)
        {
            string key;

            //string strImagePath = "";
            strObjectName = "";
            iRegKeyStart = 0;
            iRegKeyDelayedAutoStart = 0;

            if (inventory_level == "full")
            {
                iRegKeyWOW64 = 0;
                strFileOwner = "";
                strImagePath = "";
                strResolvedImagePath = "";
            }

            // Read Start value
            key = "Start";
            iRegKeyStart = RegReadIntFromHKLMService(service, key);

            key = "DelayedAutoStart";
            bRegKeyDelayedAutoStart = RegReadBoolFromHKLMService(service, key);

            key = "ObjectName";
            strObjectName = RegReadStringFromHKLMService(service, key);

            if (inventory_level == "full")
            {
                // Read WOW64 value
                key = "WOW64";
                iRegKeyWOW64 = RegReadIntFromHKLMService(service, key);
                bRegKeyWOW64 = RegReadBoolFromHKLMService(service, key);

                // Read ImagePath value
                key = "ImagePath";
                strImagePath = RegReadStringFromHKLMService(service, key);

                // Try to find the correct path to the service, so we can find the owner of the file.
                try
                {
                    // Clean up any arguments from the ImagePath
                    strResolvedImagePath = strImagePath.Trim('"');
                    strResolvedImagePath = strResolvedImagePath.Substring(0, strResolvedImagePath.IndexOf(".exe") + 4);
                    strResolvedImagePath = strResolvedImagePath.Trim();

                    if (!File.Exists(@strResolvedImagePath))
                    {
                        // If WOW64 flag set, this is usually only in 64bit environments, if its 32bit it "should" have been found in the previous test already.
                        if (iRegKeyWOW64 == 1)
                        {
                            // Trying to guess if it is inside syswow64 due to 32bit flag set (64bit system expected).
                            string WinDir = Environment.ExpandEnvironmentVariables("%WinDir%");
                            string SYSWOW64Path = WinDir + @"\syswow64";
                            string SYSTEM32Path = WinDir + @"\system32";
                            string resolvedpath = strResolvedImagePath.Replace(SYSTEM32Path, SYSWOW64Path);

                            if (!File.Exists(@resolvedpath))
                            {
                                strResolvedImagePath = "Unable to locate file" + @strResolvedImagePath;
                                return (int)ServiceState.ServiceUnknown;
                            }
                            else
                            {
                                strResolvedImagePath = @resolvedpath;
                            }
                        }
                        else
                        {
                            // Console.WriteLine("Unable to find file:" + strImagePath + "!");
                            strResolvedImagePath = "Unable to locate file" + @strResolvedImagePath;
                            return (int)ServiceState.ServiceUnknown;
                        }
                    }

                    //Console.WriteLine("\tResolvedImagePath:\t" + strImagePath);
                    var fs = File.GetAccessControl(@strResolvedImagePath);

                    var sid = fs.GetOwner(typeof(SecurityIdentifier));

                    var ntAccount = sid.Translate(typeof(NTAccount));
                    strFileOwner = ntAccount.ToString();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error while finding the executable or owner:" + e);
                }
            }

            return (int)ServiceState.ServiceOK;
        }

        public static int GetUpTime()
        {
            PerformanceCounter pc = new PerformanceCounter("System", "System Up Time");
            pc.NextValue();
            return (int)pc.NextValue();
        }

        public static int CheckAllServicesDeux(string inventory_level, int returncode, bool do_all_running_only, bool do_all_starttypes, int delayed_grace_duration, bool do_hide_category_from_output, bool do_single_check, string expected_state)
        {
            bool temp;
            outputServices = "";

            bool bDelayedGracePeriod = false;
            bool bMatchedService = false;
            bool bIncludeCategoryInOutput = false;
            bool bWarningForServiceCategory = false;

            temp = ImportServiceDefinitions();
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            temp = ImportServicesOnMachine(inventory_level, do_all_running_only, false);
            if (temp == false)
                return (int)ServiceState.ServiceUnknown;

            // Time since bootup
            if (GetUpTime() < delayed_grace_duration)
                bDelayedGracePeriod = true;

            if (do_hide_category_from_output == false && Settings.categories.Length >= 2)
                bIncludeCategoryInOutput = true;

            // Find Services that we have that is in the definition.
            Dictionary<String, WinServiceDefined> listOverDefinedServices = listWinServicesFromDefinition.ToDictionary(o => o.ServiceName, o => o);
            Dictionary<String, WinServiceActual> listOverActualServices = listWinServicesOnComputer.ToDictionary(o => o.ServiceName, o => o);
            foreach (var Actualservices in listOverActualServices)
            {
                bMatchedService = false;
                bWarningForServiceCategory = false;

                WinServiceActual ActualService = Actualservices.Value;

                

                if (Settings.warn_categories.Contains(ActualService.ServiceCategory))
                    bWarningForServiceCategory = true;

                // Single check services should bypass do_all_starttypes check further down.
                if (do_single_check == true)
                {
                    returncode = CheckExpectedService(returncode, ActualService, expected_state, bWarningForServiceCategory, bDelayedGracePeriod);
                    PerfDataCounters(ActualService.CurrentStatus);
                    listServicePerfCounters.Add(ActualService.ServiceName);
                    iNumberOfServices++;
                    bMatchedService = true;
                    break;
                }

                // Skip past this service if we only check for services with Automatic StartMode regardless of anything else.
                if (do_all_starttypes == false && ActualService.StartType != (string)ServiceStartMode.Automatic.ToString())
                {
                    if (Settings.bVerbose == true)
                        Console.WriteLine("Skipping, Service is not 'Automatic': " + ActualService.ServiceName);
                    continue;
                }

                // If match for stopped service
                if (Settings.stopped_services.Contains(ActualService.ServiceName))
                {
                    returncode = CheckStoppedService(returncode, ActualService, bIncludeCategoryInOutput);
                    PerfDataCounters(ActualService.CurrentStatus);
                    listServicePerfCounters.Add(ActualService.ServiceName);
                    iNumberOfServices++;
                    bMatchedService = true;
                    continue;
                }
                // If match for started service
                else if (Settings.running_services.Contains(ActualService.ServiceName))
                {
                    returncode = CheckRunningService(returncode, ActualService, bIncludeCategoryInOutput);
                    PerfDataCounters(ActualService.CurrentStatus);
                    listServicePerfCounters.Add(ActualService.ServiceName);
                    iNumberOfServices++;
                    bMatchedService = true;
                    continue;
                }

                // Match for Defined service.
                foreach (var Definedservices in listOverDefinedServices)
                {
                    WinServiceDefined DefinedService = Definedservices.Value;

                    // If we have a match for a defined service.
                    if (ActualService.ServiceName == DefinedService.ServiceName)
                    {
                        returncode = CheckDefinedServices(returncode, ActualService, DefinedService, bDelayedGracePeriod, bIncludeCategoryInOutput, bWarningForServiceCategory);
                        PerfDataCounters(ActualService.CurrentStatus);
                        listServicePerfCounters.Add(ActualService.ServiceName);
                        iNumberOfServices++;
                        bMatchedService = true;
                        break;
                    }

                    // Did not match, trying until end of list, will continue until match found (break) or no found (match Settings.categories)
                }

                // If match for the Category and it is a service that starts Automatically.
                if (Settings.categories.Contains(ActualService.ServiceCategory) && ActualService.StartType == ServiceStartMode.Automatic.ToString() && bMatchedService == false)
                {
                    returncode = CheckCategories(returncode, ActualService, bDelayedGracePeriod, bIncludeCategoryInOutput, bWarningForServiceCategory);
                    PerfDataCounters(ActualService.CurrentStatus);
                    listServicePerfCounters.Add(ActualService.ServiceName);
                    iNumberOfServices++;
                    continue;
                }
            }

            if (errorServices == false)
            {
                if (iNumberOfServices == 0)
                {
                    outputServices = "No Services matched the filters given, or none exist on this server.";
                    returncode = (int)ServiceState.ServiceUnknown;
                }
                else if (iNumberOfServices == 1)
                {
                    string tempOutput = string.Join(",", listServiceOutput.ToArray());
                    outputServices = tempOutput;
                }
                else
                {
                    outputServices = "All Services are in their correct states.";
                }
            }

            // Add perfdata to global PerfData list, we are done with checking what we must check.
            listPerfData.Add(" 'NumberOfServices'=" + iNumberOfServices);
            listPerfData.Add(" 'NumberOfRunningServices'=" + iNumberOfRunningServices + ";;;0;" + iNumberOfServices);
            listPerfData.Add(" 'NumberOfStoppedServices'=" + iNumberOfStoppedServices + ";;;0;" + iNumberOfServices);
            listPerfData.Add(" 'NumberOfPendingServices'=" + iNumberOfPendingServices + ";;;0;" + iNumberOfServices);
            listPerfData.Add(" 'NumberOfPausedServices'=" + iNumberOfPausedServices + ";;;0;" + iNumberOfServices);
            listPerfData.Add(" 'NumberOfUnknownServices'=" + iNumberOfUnknownServices + ";;;0;" + iNumberOfServices);
            listPerfData.Add(" 'NumberOfCorrectServices'=" + iNumberOfCorrectServices + ";;;0;" + iNumberOfServices);
            listPerfData.Add(" 'NumberOfWrongServices'=" + iNumberOfWrongServices + ";;;0;" + iNumberOfServices);

            return returncode;
        }

        private static int CheckExpectedService(int returncode, WinServiceActual ActualService, string expected_state, bool bWarningForServiceCategory, bool bDelayedGracePeriod)
        {
            if (ActualService.CurrentStatus == expected_state)
            {
                listServiceOutput.Add("Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is in the expected state '" + ActualService.CurrentStatus.ToString() + "'");
                iNumberOfCorrectServices++;
            }
            else if (ActualService.StartType == ServiceStartMode.Automatic.ToString() && ActualService.DelayedAutostart == true && bDelayedGracePeriod == true)
            {
                listServiceOutput.Add("Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not yet in the expected state of '" + ActualService.CurrentStatus.ToString() + "', it is currently in '" + expected_state + "', it is within its grace period to start.");
                iNumberOfPendingServices++;
            }
            else if (bWarningForServiceCategory == true)
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add("Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + expected_state + "'");
                returncode = (int)ServiceState.ServiceWarning;
                iNumberOfWrongServices++;
                errorServices = true;
            }
            else
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add("Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + expected_state + "'");
                returncode = (int)ServiceState.ServiceCritical;
                iNumberOfWrongServices++;
                errorServices = true;
            }

            return returncode;
        }

        private static int CheckDefinedServices(int returncode, WinServiceActual ActualService, WinServiceDefined DefinedService, bool bDelayedGracePeriod, bool bIncludeCategoryInOutput, bool bWarningForServiceCategory)
        {
            string strCategoryIncl = "";
            if (bIncludeCategoryInOutput == true)
                strCategoryIncl = ActualService.ServiceCategory + " - ";

            if (DefinedService.ExpectedStatus == ActualService.CurrentStatus)
            {
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is in the expected state '" + ActualService.CurrentStatus.ToString() + "'");
                iNumberOfCorrectServices++;
            }
            else if (ActualService.StartType == ServiceStartMode.Automatic.ToString() && ActualService.DelayedAutostart == true && bDelayedGracePeriod == true)
            {
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not yet in the expected state '" + DefinedService.ExpectedStatus + "', it is within its grace period to start.");
                iNumberOfPendingServices++;
            }
            else if (bWarningForServiceCategory == true)
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + DefinedService.ExpectedStatus + "'");
                returncode = (int)ServiceState.ServiceWarning;
                iNumberOfWrongServices++;
                errorServices = true;
            }
            else
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + DefinedService.ExpectedStatus + "'");
                returncode = (int)ServiceState.ServiceCritical;
                iNumberOfWrongServices++;
                errorServices = true;
            }

            return returncode;
        }

        private static int CheckCategories(int returncode, WinServiceActual ActualService, bool bDelayedGracePeriod, bool bIncludeCategoryInOutput, bool bWarningForSupportingService)
        {
            string strCategoryIncl = "";
            if (bIncludeCategoryInOutput == true)
                strCategoryIncl = ActualService.ServiceCategory + " - ";

            if (ActualService.StartType == ServiceStartMode.Automatic.ToString() && ActualService.DelayedAutostart == true && bDelayedGracePeriod == true)
            {
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + ServiceControllerStatus.Running.ToString() + "', it is currently '" + ActualService.CurrentStatus + "', it is within its grace period to start.");
                iNumberOfPendingServices++;
            }
            else if (ActualService.CurrentStatus == ServiceControllerStatus.Running.ToString())
            {
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is '" + ActualService.CurrentStatus.ToString() + "'");
                iNumberOfCorrectServices++;
            }
            else if (bWarningForSupportingService == true)
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + ServiceControllerStatus.Running.ToString() + "', it is currently '" + ActualService.CurrentStatus + "'");
                returncode = (int)ServiceState.ServiceWarning;
                iNumberOfWrongServices++;
                errorServices = true;
            }
            else
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + ServiceControllerStatus.Running.ToString() + "', it is currently '" + ActualService.CurrentStatus + "'");
                returncode = (int)ServiceState.ServiceCritical;
                iNumberOfWrongServices++;
                errorServices = true;
            }
            return returncode;
        }

        private static int CheckRunningService(int returncode, WinServiceActual ActualService, bool bIncludeCategoryInOutput)
        {
            string strCategoryIncl = "";
            if (bIncludeCategoryInOutput == true)
                strCategoryIncl = ActualService.ServiceCategory + " - ";

            if (ActualService.CurrentStatus == ServiceControllerStatus.Running.ToString())
            {
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is in the expected state '" + ActualService.CurrentStatus.ToString() + "'");
                iNumberOfCorrectServices++;
            }
            else
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + ServiceControllerStatus.Running.ToString() + "'");
                returncode = (int)ServiceState.ServiceCritical;
                iNumberOfWrongServices++;
                errorServices = true;
            }
            return returncode;
        }

        private static int CheckStoppedService(int returncode, WinServiceActual ActualService, bool bIncludeCategoryInOutput)
        {
            string strCategoryIncl = "";
            if (bIncludeCategoryInOutput == true)
                strCategoryIncl = ActualService.ServiceCategory + " - ";

            if (ActualService.CurrentStatus == ServiceControllerStatus.Stopped.ToString())
            {
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is in the expected state '" + ActualService.CurrentStatus.ToString() + "'");
                iNumberOfCorrectServices++;
            }
            else
            {
                outputServices = outputServices + "Service '" + ActualService.ServiceName + "' is in the wrong state '" + ActualService.CurrentStatus.ToString() + "' ";
                listServiceOutput.Add(strCategoryIncl + "Service '" + ActualService.ServiceName + "' (" + ActualService.DisplayName + ") is not in the expected state '" + ServiceControllerStatus.Stopped.ToString() + "'");
                returncode = (int)ServiceState.ServiceCritical;
                iNumberOfWrongServices++;
                errorServices = true;
            }
            return returncode;
        }

        public static bool InsertDefaultServiceDefinitions()
        {
            // Adds default services

            // System
            listWinServicesFromDefinition.Add(new WinServiceDefined("BFE", "Base Filtering Engine", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("BrokerInfrastructure", "Background Tasks Infrastructure Service", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DcomLaunch", "DCOM Server Process Launcher", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("LSM", "Local Session Manager", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Power", "Power", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SamSs", "Security Accounts Manager", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Schedule", "Task Scheduler", "System", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WinDefend", "Windows Defender Service", "System", "Automatic", "Running"));

            // Essential
            listWinServicesFromDefinition.Add(new WinServiceDefined("BITS", "Background Intelligent Transfer Service", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("COMSysApp", "COM+ System Application", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Dnscache", "DNS Client", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DPS", "Diagnostic Policy Service", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("EventLog", "Windows Event Log", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("EventSystem", "COM+ Event System", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("iphlpsvc", "IP Helper", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("LanmanServer", "Server", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("LanmanWorkstation", "Workstation", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MpsSvc", "Windows Firewall", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Netlogon", "Netlogon", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("pla", "Performance Logs & Alerts", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ProfSvc", "User Profile Service", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RpcEptMapper", "RPC Endpoint Mapper", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RpcSs", "Remote Procedure Call (RPC)", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("UALSVC", "User Access Logging Service", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmicheartbeat", "Hyper-V Heartbeat Service", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmicshutdown", "Hyper-V Guest Shutdown Service", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmictimesync", "Hyper-V Time Synchronization Service", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("VMTools", "VMware Tools", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Winmgmt", "Windows Management Instrumentation", "Essential", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WinRM", "Windows Remote Management (WS-Management)", "Essential", "Automatic", "Running"));

            // Role
            listWinServicesFromDefinition.Add(new WinServiceDefined("adfssrv", "Active Directory Federation Services", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ADWS", "Active Directory Web Services", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("BITSCompactServer", "BITS Compact Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("c2wts", "Claims to Windows Token Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CertSvc", "Active Directory Certificate Services", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ClusSvc", "Cluster Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ddpsvc", "Data Deduplication Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ddpvssvc", "Data Deduplication Volume Shadow Copy Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Dfs", "DFS Namespace", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DFSR", "DFS Replication", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DNS", "DNS Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("drs", "Device Registration Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Eaphost", "Extensible Authentication Protocol", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Fax", "Fax", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("fdPHost", "Function Discovery Provider Host", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("FDResPub", "Function Discovery Resource Publication", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("fssagent", "Microsoft File Server Shadow Copy Agent Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("hkmsvc", "Health Key and Certificate Management", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("IAS", "Network Policy Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("IISADMIN", "IIS Admin Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("IKEEXT", "IKE and AuthIP IPsec Keying Modules", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("IsmServ", "Intersite Messaging", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Kdc", "Kerberos Key Distribution Center", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("KdsSvc", "Microsoft Key Distribution Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("KeyIso", "CNG Key Isolation", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("KPSSVC", "KDC Proxy Server service (KPS)", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("KtmRm", "KtmRm for Distributed Transaction Coordinator", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("LPDSVC", "LPD Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MMCSS", "Multimedia Class Scheduler", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSDTC", "Distributed Transaction Coordinator", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSiSCSI", "Microsoft iSCSI Initiator Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSiSNS", "Microsoft iSNS Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSMQ", "Message Queuing", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSMQTriggers", "Message Queuing Triggers", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSSQL$MICROSOFT##WID", "Windows Internal Database", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("MSStrgSvc", "Windows Standards-Based Storage Management", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetMsmqActivator", "Net.Msmq Listener Adapter", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetPipeActivator", "Net.Pipe Listener Adapter", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetTcpActivator", "Net.Tcp Listener Adapter", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NetTcpPortSharing", "Net.Tcp Port Sharing Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NfsService", "Server for NFS", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NtFrs", "File Replication", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("OcspSvc", "Online Responder Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PeerDistSvc", "BranchCache", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PNRPAutoReg", "PNRP Machine Name Publication Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PNRPsvc", "Peer Name Resolution Protocol", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PrintNotify", "Printer Extensions and Notifications", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RaMgmtSvc", "Remote Access Management service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RasAuto", "Remote Access Auto Connection Manager", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RasMan", "Remote Access Connection Manager", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RDMS", "Remote Desktop Management", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RemoteAccess", "Routing and Remote Access", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RPCHTTPLBS", "RPC/HTTP Load Balancing Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RpcLocator", "Remote Procedure Call (RPC) Locator", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("rqs", "Remote Access Quarantine Agent", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SessionEnv", "Remote Desktop Configuration", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("simptcp", "Simple TCP/IP Services", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SmbHash", "SMB Hash Generation Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SmbWitness", "SMB Witness", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("smphost", "Microsoft Storage Spaces SMP", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SMTPSVC", "Simple Mail Transfer Protocol (SMTP)", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SNMP", "SNMP Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SrmReports", "File Server Storage Reports Manager", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SrmSvc", "File Server Resource Manager", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SstpSvc", "Secure Socket Tunneling Protocol Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("stisvc", "Windows Image Acquisition (WIA)", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("StorSvc", "Storage Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("svsvc", "Spot Verifier", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SyncShareSvc", "Windows Sync Share", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SyncShareTTSvc", "Sync Share Token Translation Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TapiSrv", "Telephony", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TermService", "Remote Desktop Services", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TermServLicensing", "Remote Desktop Licensing", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("THREADORDER", "Thread Ordering Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TieringEngineService", "Storage Tiers Management", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TimeBroker", "Time Broker", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TlntSvr", "Telnet", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TPAutoConnSvc", "TP AutoConnect Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TPVCGateway", "TP VC Gateway Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TrkWks", "Distributed Link Tracking Client", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TScPubRPC", "RemoteApp and Desktop Connection Management", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TSGateway", "Remote Desktop Gateway", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Tssdis", "Remote Desktop Connection Broker", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("UI0Detect", "Interactive Services Detection", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("UmRdpService", "Remote Desktop Services UserMode Port Redirector", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("w3logsvc", "W3C Logging Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("W3SVC", "World Wide Web Publishing Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WAS", "Windows Process Activation Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("wbengine", "Block Level Backup Engine Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WbioSrvc", "Windows Biometric Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WDSServer", "Windows Deployment Services Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WEPHOSTSVC", "Windows Encryption Provider Host Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("wercplsupport", "Problem Reports and Solutions Control Panel Support", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WerSvc", "Windows Error Reporting Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WFFSvc", "Windows Feedback Forwarder Service", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WiaRpc", "Still Image Acquisition Events", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WIDWriter", "Windows Internal Database VSS Writer", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WinTarget", "Microsoft iSCSI Software Target", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WSusCertServer", "WSUS Certificate Server", "Role", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WsusService", "WSUS Service", "Role", "Automatic", "Running"));

            // Supporting
            listWinServicesFromDefinition.Add(new WinServiceDefined("AeLookupSvc", "Application Experience", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AppHostSvc", "Application Host Helper Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AppIDSvc", "Application Identity", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Appinfo", "Application Information", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AppMgmt", "Application Management", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("aspnet_state", "ASP.NET State Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AudioEndpointBuilder", "Windows Audio Endpoint Builder", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Audiosrv", "Windows Audio", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AxInstSV", "ActiveX Installer (AxInstSV)", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("BDESVC", "BitLocker Drive Encryption Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Browser", "Computer Browser", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CertPropSvc", "Certificate Propagation", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CryptSvc", "Cryptographic Services", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("CscService", "Offline Files", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("defragsvc", "Optimize drives", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DeviceAssociationService", "Device Association Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DeviceInstall", "Device Install Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Dhcp", "DHCP Client", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DiagTrack", "Diagnostics Tracking Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("FontCache", "Windows Font Cache Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("gpsvc", "Group Policy Client", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("IEEtwCollectorService", "Internet Explorer ETW Collector Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("lmhosts", "TCP/IP NetBIOS Helper", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("napagent", "Network Access Protection Agent", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NcaSvc", "Network Connectivity Assistant", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NcbService", "Network Connection Broker", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Netman", "Network Connections", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("netprofm", "Network List Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("NlaSvc", "Network Location Awareness", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("nsi", "Network Store Interface Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("p2pimsvc", "Peer Networking Identity Manager", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PerfHost", "Performance Counter DLL Host", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PlugPlay", "Plug and Play", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("PolicyAgent", "IPsec Policy Agent", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("QWAVE", "Quality Windows Audio Video Experience", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RemoteRegistry", "Remote Registry", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("RSoPProv", "Resultant Set of Policy Provider", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("sacsvr", "Special Administration Console Helper", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SCardSvr", "Smart Card", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ScDeviceEnum", "Smart Card Device Enumeration Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SCPolicySvc", "Smart Card Removal Policy", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("seclogon", "Secondary Logon", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SENS", "System Event Notification Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SharedAccess", "Internet Connection Sharing (ICS)", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("ShellHWDetection", "Shell Hardware Detection", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Spooler", "Print Spooler", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("sppsvc", "Software Protection", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SSDPSRV", "SSDP Discovery", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("swprv", "Microsoft Software Shadow Copy Provider", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SystemEventsBroker", "System Events Broker", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Themes", "Themes", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("VaultSvc", "Credential Manager", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vds", "Virtual Disk", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmicguestinterface", "Hyper-V Guest Service Interface", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmickvpexchange", "Hyper-V Data Exchange Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmicrdv", "Hyper-V Remote Desktop Virtualization Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("W32Time", "Windows Time", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Wcmsvc", "Windows Connection Manager", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WcsPlugInService", "Windows Color System", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WdiServiceHost", "Diagnostic Service Host", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WdiSystemHost", "Diagnostic System Host", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WebClient", "WebClient", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WinHttpAutoProxySvc", "WinHTTP Web Proxy Auto-Discovery Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("wlidsvc", "Microsoft Account Sign-in Assistant", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("wmiApSrv", "WMI Performance Adapter", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WPDBusEnum", "Portable Device Enumerator Service", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WSearch", "Windows Search", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("WSService", "Windows Store Service (WSService)", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("wuauserv", "Windows Update", "Supporting", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("wudfsvc", "Windows Driver Foundation - User-mode Driver Framework", "Supporting", "Automatic", "Running"));

            // Ignored services
            listWinServicesFromDefinition.Add(new WinServiceDefined("ALG", "Application Layer Gateway Service", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AppReadiness", "App Readiness", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("AppXSvc", "AppX Deployment Service (AppXSVC)", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("dot3svc", "Wired AutoConfig", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("DsmSvc", "Device Setup Manager", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("EFS", "Encrypting File System (EFS)", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("hidserv", "Human Interface Device Service", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("lltdsvc", "Link-Layer Topology Discovery Mapper", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("msiserver", "Windows Installer", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SNMPTRAP", "SNMP Trap", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("SysMain", "Superfetch", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TabletInputService", "Touch Keyboard and Handwriting Panel Service", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("TrustedInstaller", "Windows Modules Installer", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("upnphost", "UPnP Device Host", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmicvss", "Hyper-V Volume Shadow Copy Requestor", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("vmvss", "VMware Snapshot Provider", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("VSS", "Volume Shadow Copy", "Ignored", "Automatic", "Running"));
            listWinServicesFromDefinition.Add(new WinServiceDefined("Wecsvc", "Windows Event Collector", "Ignored", "Automatic", "Running"));

            return true;
        }

        public static bool ImportServiceDefinitions()
        {
            if (Settings.strCategoryFilePath == "unspecified")
            {
                bool temp = InsertDefaultServiceDefinitions();
                return temp;
            }

            if (Settings.strCategoryFileFormat == "CSV")
                return ImportServiceDefinitionsCSV();

            return false;
        }

        public static bool ImportServiceDefinitionsCSV()
        {
            try
            {
                // Import the definition file.
                DataTable csvTable = GetDataTabletFromCSVFile(Settings.strCategoryFilePath);
                foreach (DataRow csvRow in csvTable.Rows)
                {
                    string sServiceNameCSV = (string)csvRow["ServiceName"];
                    string sDisplayNameCSV = (string)csvRow["DisplayName"];
                    string sStartTypeCSV = (string)csvRow["StartType"];
                    string sExpectedStatusCSV = (string)csvRow["ExpectedStatus"];
                    string sServiceCategoryCSV = (string)csvRow["ServiceCategory"];
                    string sExpectedStatus = CleanStatus(sExpectedStatusCSV);

                    listWinServicesFromDefinition.Add(new WinServiceDefined(sServiceNameCSV, sDisplayNameCSV, sServiceCategoryCSV, sStartTypeCSV, sExpectedStatus));
                }
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception occured: " + e);
                return false;
            }
        }

        public static bool ImportServicesOnMachine(string inventory_level, bool do_all_running_only, bool do_inventory)
        {
            ServiceController[] scServices;
            scServices = ServiceController.GetServices();

            int z = 1;
            int y = 1;
            int test = 0;

            string strCategory = "ThirdParty";

            // Read the actual state of things.
            foreach (ServiceController scService in scServices)
            {
                try
                {
                    string sServiceName = scService.ServiceName.ToString();
                    string sDisplayName = scService.DisplayName.ToString();

                    // Skip service for inventory if we only scare about running services in the inventory output.
                    if (do_all_running_only == true && do_inventory == true && scService.Status.ToString() != ServiceControllerStatus.Running.ToString())
                    {
                        if (Settings.bVerbose == true)
                            Console.WriteLine("INFO: Service is not running, skipping: " + sServiceName);
                        continue;
                    }

                    // Skip all services that match excluderules
                    if (Settings.excluded_services.Contains(sServiceName) && Settings.bDefaultExcludeList == false)
                    {
                        if (Settings.bVerbose == true)
                            Console.WriteLine("INFO: Service in exclude list, skipping: " + sServiceName);

                        continue;
                    }

                    // Skip all services not set to include
                    if (Settings.bDefaultIncludeList == true)
                    {
                        if (Settings.bDebug == true)
                            Console.WriteLine("DEBUG: Included service: " + sServiceName);
                    }
                    else if (!Settings.included_services.Contains(sServiceName) && Settings.bDefaultIncludeList == false)
                    {
                        if (Settings.bDebug == true)
                            Console.WriteLine("INFO: Service not in Settings.included_services: " + sServiceName);
                        continue;
                    }
                    else if (Settings.included_services.Contains(sServiceName) && Settings.bDefaultIncludeList == false)
                    {
                        if (Settings.bVerbose == true)
                            Console.WriteLine("INFO: Included service: " + sServiceName);
                    }

                    strCategory = ServiceCategoryLookup(sServiceName);

                    // Match if the returned category matches the service, if it does not then skip
                    if (Settings.categories.Contains(strCategory))
                    {
                        if (Settings.bDebug)
                        {
                            Console.WriteLine("DEBUG: Service matching category");
                        }
                    }
                    else if (!Settings.categories.Contains(strCategory) && Settings.bDefaultCategoriesList == false)
                    {
                        if (Settings.bVerbose)
                            Console.WriteLine("INFO: Skipping service due to category not matched: " + sServiceName);
                        continue;
                    }

                    List<string> listDependentServices = new List<string>();
                    List<string> listServicesDependedOn = new List<string>();

                    foreach (var DependentService in scService.DependentServices)
                    {
                        try
                        {
                            listDependentServices.Add(DependentService.ServiceName.ToString());
                        }
                        catch (Exception e)
                        {
                            if (Settings.bVerbose || Settings.bDebug)
                                Console.WriteLine("ERROR: Looking up DependentService for '" + sServiceName + "' resulted in an exception, it is likely not installed:" + e);
                        }
                    }

                    foreach (var ServiceDependedOn in scService.ServicesDependedOn)
                    {
                        try
                        {
                            listServicesDependedOn.Add(ServiceDependedOn.ServiceName.ToString());
                        }
                        catch (Exception e)
                        {
                            if (Settings.bVerbose || Settings.bDebug)
                                Console.WriteLine("ERROR: Looking up DepdendendOn for '" + sServiceName + "' resulted in an exception, it is likely not installed:" + e);
                        }
                    }

                    Array dependentServices = listDependentServices.ToArray();
                    Array servicesDependedOn = listServicesDependedOn.ToArray();
                    test = ServiceStartupMode(inventory_level, sServiceName);
                    ServiceControllerStatus serviceStatus = scService.Status;

                    // Store the service to the list.
                    listWinServicesOnComputer.Add(new WinServiceActual(sServiceName, sDisplayName, Enum.GetName(typeof(StartType), iRegKeyStart), bRegKeyDelayedAutoStart, bRegKeyWOW64, strObjectName, serviceStatus.ToString(), strCategory, strFileOwner, strImagePath, strResolvedImagePath, dependentServices, servicesDependedOn));
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception occured: " + e);
                    return false;
                }
            }
            return true;
        }

        public static string ServiceCategoryLookup(string serviceName)
        {
            if (serviceName != "")
            {
                if (Settings.bDefaultSystemCategory == false && Settings.services_in_system_category.Contains(serviceName))
                    return "System";

                if (Settings.bDefaultEssentialCategory == false && Settings.services_in_essential_category.Contains(serviceName))
                    return "Essential";

                if (Settings.bDefaultRoleCategory == false && Settings.services_in_role_category.Contains(serviceName))
                    return "Role";

                if (Settings.bDefaultSupportingCategory == false && Settings.services_in_supporting_category.Contains(serviceName))
                    return "Supporting";

                if (Settings.bDefaultThirdPartyCategory == false && Settings.services_in_thirdparty_category.Contains(serviceName))
                    return "ThirdParty";

                if (Settings.bDefaultIgnoredCategory == false && Settings.services_in_ignored_category.Contains(serviceName))
                    return "Ignored";

                Dictionary<String, WinServiceDefined> listOverDefinedServices = listWinServicesFromDefinition.ToDictionary(o => o.ServiceName, o => o);
                foreach (var DefinedService in listOverDefinedServices)
                {
                    var DefinedServiceValue = DefinedService.Value;
                    if (DefinedServiceValue.ServiceName == serviceName)
                    {
                        return DefinedServiceValue.ServiceCategory.ToString();
                    }
                }

                return "ThirdParty";
            }
            return "errorInServiceCategoryLookup";
        }

        public static void PerfDataCounters(string status)
        {
            // Calculate perfdata only for matches
            if (status == ServiceControllerStatus.Running.ToString())
            {
                iNumberOfRunningServices++;
            }
            else if (status == ServiceControllerStatus.Stopped.ToString())
            {
                iNumberOfStoppedServices++;
            }
            else if (status == ServiceControllerStatus.Paused.ToString())
            {
                iNumberOfPausedServices++;
            }
            else if (status == ServiceControllerStatus.StartPending.ToString() ||
                status == ServiceControllerStatus.StopPending.ToString() ||
                status == ServiceControllerStatus.PausePending.ToString() ||
                status == ServiceControllerStatus.ContinuePending.ToString())
            {
                iNumberOfPendingServices++;
            }
        }

        public static string CleanStatus(string status)
        {
            // First test one way
            if (ServiceControllerStatus.Stopped.ToString() == status || ServiceControllerStatus.ContinuePending.ToString() == status
                || ServiceControllerStatus.Paused.ToString() == status || ServiceControllerStatus.PausePending.ToString() == status
                || ServiceControllerStatus.StartPending.ToString() == status || ServiceControllerStatus.StopPending.ToString() == status
                || ServiceControllerStatus.Running.ToString() == status)
            {
                return status;
            }
            // We need to try to guess the status and set the proper String for the OS language.
            else if (status == "Running")
                return ServiceControllerStatus.Running.ToString();
            else if (status == "Stopped")
                return ServiceControllerStatus.Stopped.ToString();
            else if (status == "ContinuePending")
                return ServiceControllerStatus.ContinuePending.ToString();
            else if (status == "Paused")
                return ServiceControllerStatus.Paused.ToString();
            else if (status == "PausedPending")
                return ServiceControllerStatus.PausePending.ToString();
            else if (status == "StartPending")
                return ServiceControllerStatus.StartPending.ToString();
            else if (status == "StopPending")
                return ServiceControllerStatus.StopPending.ToString();
            else
                Console.WriteLine("Unable to match Status '" + status + "' to ServiceControllerStatus");
            return "Unknown";
        }

        private static DataTable GetDataTabletFromCSVFile(string strCSVFilePath)
        {
            DataTable csvData = new DataTable();
            try
            {
                using (TextFieldParser csvReader = new TextFieldParser(strCSVFilePath))
                {
                    csvReader.SetDelimiters(new string[] { "," });
                    csvReader.HasFieldsEnclosedInQuotes = true;
                    //read column names
                    string[] colFields = csvReader.ReadFields();
                    foreach (string column in colFields)
                    {
                        DataColumn datecolumn = new DataColumn(column);
                        datecolumn.AllowDBNull = true;
                        csvData.Columns.Add(datecolumn);
                    }
                    while (!csvReader.EndOfData)
                    {
                        string[] fieldData = csvReader.ReadFields();
                        //Making empty value as null
                        for (int i = 0; i < fieldData.Length; i++)
                        {
                            if (fieldData[i] == "")
                            {
                                fieldData[i] = null;
                            }
                        }
                        csvData.Rows.Add(fieldData);
                    }
                }
            }
            catch (Exception ex)
            {
            }
            return csvData;
        }
    }
}