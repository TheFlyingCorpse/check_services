using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Icinga;
using System.Text;

namespace check_services
{
    public class Settings
    {
        public static bool bDebug;
        public static bool bVerbose;

        public static string strCategoryFilePath = "unspecified";
        public static string strCategoryFileFormat = "CSV";

        public static string[] ExcludedServices = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] IncludedServices = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] StoppedServices = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] RunningServices = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] categories = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] warn_categories = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };

        public static bool bDefaultIncludeList;
        public static bool bDefaultExcludeList;
        public static bool bDefaultStoppedList;
        public static bool bDefaultRunningList;
        public static bool bDefaultWarnCategoriesList;
        public static bool bDefaultCategoriesList;

        public static string[] services_in_system_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_essential_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_role_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_supporting_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_thirdparty_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_ignored_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };

        public static bool bDefaultSystemCategory;
        public static bool bDefaultEssentialCategory;
        public static bool bDefaultRoleCategory;
        public static bool bDefaultSupportingCategory;
        public static bool bDefaultThirdPartyCategory;
        public static bool bDefaultIgnoredCategory;

        public static int HandleArguments(int returncode, List<string> temp_excluded_services, List<string> temp_included_services, List<string> temp_stopped_services, List<string> temp_running_services,
            List<string> temp_categories, List<string> temp_warn_categories, List<string> temp_services_in_system_category, List<string> temp_services_in_essential_category,
            List<string> temp_services_in_role_category, List<string> temp_services_in_supporting_category, List<string> temp_services_in_thirdparty_category, List<string> temp_services_in_ignored_category,
            string split_by)
        {
            string temp;
            if (temp_excluded_services.Count > 0)
            {
                ExcludedServices = SplitList(temp_excluded_services, split_by);
                PrintArray("excluded_services", ExcludedServices);
            }
            if (temp_included_services.Count > 0)
            {
                IncludedServices = SplitList(temp_included_services, split_by);
                PrintArray("included_services", IncludedServices);
            }
            if (temp_stopped_services.Count > 0)
            {
                StoppedServices = SplitList(temp_stopped_services, split_by);
                PrintArray("stopped_services", StoppedServices);
            }
            if (temp_running_services.Count > 0)
            {
                RunningServices = SplitList(temp_running_services, split_by);
                PrintArray("RunningServices", RunningServices);
            }
            if (temp_categories.Count > 0)
            {
                categories = SplitList(temp_categories, split_by);
                PrintArray("categories", categories);
            }
            if (temp_warn_categories.Count > 0)
            {
                warn_categories = SplitList(temp_warn_categories, split_by);
                PrintArray("warn_categories", warn_categories);
            }
            if (temp_services_in_system_category.Count > 0)
            {
                services_in_system_category = SplitList(temp_services_in_system_category, split_by);
                PrintArray("services_in_system_category", services_in_system_category);
            }
            if (temp_services_in_essential_category.Count > 0)
            {
                services_in_essential_category = SplitList(temp_services_in_essential_category, split_by);
                PrintArray("services_in_essential_category", services_in_essential_category);
            }
            if (temp_services_in_role_category.Count > 0)
            {
                services_in_role_category = SplitList(temp_services_in_role_category, split_by);
                PrintArray("services_in_role_category", services_in_role_category);
            }
            if (temp_services_in_supporting_category.Count > 0)
            {
                services_in_supporting_category = SplitList(temp_services_in_supporting_category, split_by);
                PrintArray("services_in_supporting_category", services_in_supporting_category);
            }
            if (temp_services_in_thirdparty_category.Count > 0)
            {
                services_in_thirdparty_category = SplitList(temp_services_in_thirdparty_category, split_by);
                PrintArray("services_in_thirdparty_category", services_in_thirdparty_category);
            }
            if (temp_services_in_ignored_category.Count > 0)
            {
                services_in_ignored_category = SplitList(temp_services_in_ignored_category, split_by);
                PrintArray("services_in_ignored_category", services_in_ignored_category);
            }

            if (strCategoryFilePath != "unspecified")
            {
                if (File.Exists(strCategoryFilePath) == false)
                {
                    Console.WriteLine("Error: Specified csv_file not found: " + strCategoryFilePath);
                    return (int)ServiceState.ServiceUnknown;
                }
            }

            if (ExcludedServices.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (bVerbose == true)
                    Console.WriteLine("INFO: Default excluded_services list.");
                bDefaultExcludeList = true;
            }

            if (IncludedServices.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (bVerbose == true)
                    Console.WriteLine("INFO: Default included_services list.");
                bDefaultIncludeList = true;
            }

            if (categories.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (bVerbose == true)
                    Console.WriteLine("INFO: Default categories list, setting it to ThirdParty.");
                bDefaultCategoriesList = true;
                categories = new string[] { "ThirdParty" };
            }
            else if (categories.Contains("Basic"))
            {
                categories = new string[] { "Essential", "System", "Supporting", "Role" };
            }

            if (warn_categories.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (bVerbose == true)
                    Console.WriteLine("INFO: Default warn_categories list.");
                bDefaultWarnCategoriesList = true;
                warn_categories = new string[] { "Supporting" };
            }

            if (services_in_system_category.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (bVerbose == true)
                    Console.WriteLine("INFO: Default services_in_system_category list.");
                bDefaultSystemCategory = true;
            }
            if (services_in_essential_category.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (bVerbose == true)
                    Console.WriteLine("INFO: Default services_in_essential_category list.");
                bDefaultEssentialCategory = true;
            }
            if (services_in_role_category.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (bVerbose == true)
                    Console.WriteLine("INFO: Default services_in_role_category list.");
                bDefaultRoleCategory = true;
            }
            if (services_in_supporting_category.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (bVerbose == true)
                    Console.WriteLine("INFO: Default services_in_supporting_category list.");
                bDefaultSupportingCategory = true;
            }
            if (services_in_ignored_category.Contains("thisshouldprobablyneverbeoverwrittenbysomething"))
            {
                if (bVerbose == true)
                    Console.WriteLine("INFO: Default services_in_ignored_category list.");
                bDefaultIgnoredCategory = true;
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

    }
}
