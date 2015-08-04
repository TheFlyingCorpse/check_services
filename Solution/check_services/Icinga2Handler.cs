using Icinga;
using Mono.Options;
using System.Collections.Generic;

namespace check_services
{
    internal class Icinga2Handler
    {
        public static CheckResult ParseArgs(CheckResult I2CheckResult, string[] args)
        {
            int temp = 3;
            int returncode = 0;

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
                I2CheckResult.Output = "Error occured during parsing the arguments: " + e;
                I2CheckResult.State = ServiceState.ServiceUnknown;
                return I2CheckResult;
            }

            // Return unknown if we do not check services or inventory.
            if (do_services == false && do_inventory == false)
            {
                Handler.ShowHelp(p);
                I2CheckResult.State = ServiceState.ServiceUnknown;
                return I2CheckResult;
            }

            // Handle Arguments
            returncode = Handler.HandleArguments(returncode, temp_excluded_services, temp_included_services, temp_stopped_services, temp_running_services, temp_categories, temp_warn_categories, temp_services_in_system_category,
                temp_services_in_essential_category, temp_services_in_role_category, temp_services_in_supporting_category, temp_services_in_thirdparty_category, temp_services_in_ignored_category, split_by);

            // Return if something is not ok.
            if (returncode > (int)ServiceState.ServiceOK)
            {
                I2CheckResult.State = ServiceState.ServiceUnknown;
                return I2CheckResult;
            }

            // Translate if need be the expected_state
            expected_state = Inventory.CleanStatus(expected_state);

            // Inventory is blocked from running at the same time as other checks, thus it is run first if specified.
            if (do_inventory == true && inventory_format == "i2conf")
            {
                temp = Inventory.OutputI2Conf(inventory_level, do_all_running_only, do_hide_empty_vars);
                I2CheckResult.State = ServiceState.ServiceOK;
                return I2CheckResult;
            }
            else if (do_inventory == true)
            {
                I2CheckResult.Output = "Unknown inventory, format: '" + inventory_format + "', level: '" + inventory_level + "'";
                I2CheckResult.State = ServiceState.ServiceUnknown;
                return I2CheckResult;
            }

            if (do_services == true)
            {
                returncode = Checks.Services(inventory_level, returncode, do_all_running_only, do_all_starttypes, delayed_grace_duration, do_hide_category_from_output, do_single_check, expected_state);
            }

            I2CheckResult.State = (ServiceState)returncode;

            I2CheckResult = HandleIcinga2Text(I2CheckResult, returncode, do_hide_long_output);

            return I2CheckResult;
        }

        public static CheckResult HandleIcinga2Text(CheckResult I2CheckResult, int returncode, bool do_hide_long_output)
        {
            // ORDER the output
            string output = "";
            output = Checks.outputServices;

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
            if (PerfData.iNumberOfServices > 1 && do_hide_long_output == false)
            {
                int x = 1;
                foreach (string outputS in Checks.listServiceOutput)
                {
                    if (x < Checks.listServiceOutput.Count)
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
            foreach (string outputP in Checks.listPerfData)
            {
                perfdata = perfdata + outputP;
            }

            if (do_hide_long_output)
            {
                I2CheckResult.Output = output;
            }
            else
            {
                if (PerfData.iNumberOfServices > 1)
                    output = output + outputLong;
                I2CheckResult.Output = output;
            }

            I2CheckResult.PerformanceData = perfdata;

            return I2CheckResult;
        }
    }
}