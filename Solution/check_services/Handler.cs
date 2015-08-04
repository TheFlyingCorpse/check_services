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
        public static int HandleArguments(int returncode, List<string> temp_excluded_services, List<string> temp_included_services, List<string> temp_stopped_services, List<string> temp_running_services,
            List<string> temp_categories, List<string> temp_warn_categories, List<string> temp_services_in_system_category, List<string> temp_services_in_essential_category,
            List<string> temp_services_in_role_category, List<string> temp_services_in_supporting_category, List<string> temp_services_in_thirdparty_category, List<string> temp_services_in_ignored_category,
            string split_by)
        {
            if (temp_excluded_services.Count > 0)
            {
                Settings.ExcludedServices = SplitList(temp_excluded_services, split_by);
                PrintArray("ExcludedServices", Settings.ExcludedServices);
                Settings.bDefaultExcludeList = false;
            }
            if (temp_included_services.Count > 0)
            {
                Settings.IncludedServices = SplitList(temp_included_services, split_by);
                PrintArray("IncludedServices", Settings.IncludedServices);
                Settings.bDefaultIncludeList = false;
            }
            if (temp_stopped_services.Count > 0)
            {
                Settings.StoppedServices = SplitList(temp_stopped_services, split_by);
                PrintArray("StoppedServices", Settings.StoppedServices);
                Settings.bDefaultStoppedList = false;
            }
            if (temp_running_services.Count > 0)
            {
                Settings.RunningServices = SplitList(temp_running_services, split_by);
                PrintArray("RunningServices", Settings.RunningServices);
                Settings.bDefaultRunningList = false;
            }
            if (temp_categories.Count > 0)
            {
                Settings.Categories = SplitList(temp_categories, split_by);
                PrintArray("Categories", Settings.Categories);
            }
            if (temp_warn_categories.Count > 0)
            {
                Settings.WarnCategories = SplitList(temp_warn_categories, split_by);
                PrintArray("WarnCategories", Settings.WarnCategories);
                Settings.bDefaultWarnCategoriesList = false;
            }
            if (temp_services_in_system_category.Count > 0)
            {
                Settings.services_in_system_category = SplitList(temp_services_in_system_category, split_by);
                PrintArray("services_in_system_category", Settings.services_in_system_category);
                Settings.bDefaultSystemCategory = false;
            }
            if (temp_services_in_essential_category.Count > 0)
            {
                Settings.services_in_essential_category = SplitList(temp_services_in_essential_category, split_by);
                PrintArray("services_in_essential_category", Settings.services_in_essential_category);
                Settings.bDefaultEssentialCategory = false;
            }
            if (temp_services_in_role_category.Count > 0)
            {
                Settings.services_in_role_category = SplitList(temp_services_in_role_category, split_by);
                PrintArray("services_in_role_category", Settings.services_in_role_category);
                Settings.bDefaultRoleCategory = false;
            }
            if (temp_services_in_supporting_category.Count > 0)
            {
                Settings.services_in_supporting_category = SplitList(temp_services_in_supporting_category, split_by);
                PrintArray("services_in_supporting_category", Settings.services_in_supporting_category);
                Settings.bDefaultSupportingCategory = false;
            }
            if (temp_services_in_thirdparty_category.Count > 0)
            {
                Settings.services_in_thirdparty_category = SplitList(temp_services_in_thirdparty_category, split_by);
                PrintArray("services_in_thirdparty_category", Settings.services_in_thirdparty_category);
                Settings.bDefaultThirdPartyCategory = false;
            }
            if (temp_services_in_ignored_category.Count > 0)
            {
                Settings.services_in_ignored_category = SplitList(temp_services_in_ignored_category, split_by);
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
                Settings.bDefaultCategoriesList = true;
                Settings.Categories = new string[] { "ThirdParty" };
            }
            else if (Settings.Categories.Contains("Basic"))
            {
                Settings.Categories = new string[] { "Essential", "System", "Supporting", "Role" };
            }

            if (Settings.WarnCategories.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (Settings.bVerbose == true)
                    Console.WriteLine("INFO: Default warn_categories list.");
                Settings.bDefaultWarnCategoriesList = true;
                Settings.WarnCategories = new string[] { "Supporting" };
            }

            return returncode;
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
            Console.WriteLine("Usage: " + System.AppDomain.CurrentDomain.FriendlyName + " [OPTIONS]");
            Console.WriteLine("This plugin checks the State of one or more services on the local machine.");
            Console.WriteLine("You can filter for your services by using category or a combination of category and include/exclude filters. Use the same switch multiple times if you want to");
            Console.WriteLine();
            Console.WriteLine("Options:");
            p.WriteOptionDescriptions(Console.Out);
        }
    }
}