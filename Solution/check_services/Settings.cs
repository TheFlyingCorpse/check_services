using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace check_services
{
    public class Settings
    {
        public static bool bDebug = false;
        public static bool bVerbose = false;

        public static string strCategoryFilePath = "unspecified";
        public static string strCategoryFileFormat = "CSV";

        public static string[] excluded_services = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] included_services = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] stopped_services = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] running_services = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] categories = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] warn_categories = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };

        public static bool bDefaultIncludeList = false;
        public static bool bDefaultExcludeList = false;
        public static bool bDefaultStoppedList = false;
        public static bool bDefaultRunningList = false;
        public static bool bDefaultWarnCategoriesList = false;
        public static bool bDefaultCategoriesList = false;

        public static string[] services_in_system_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_essential_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_role_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_supporting_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_thirdparty_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_ignored_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };

        public static bool bDefaultSystemCategory = false;
        public static bool bDefaultEssentialCategory = false;
        public static bool bDefaultRoleCategory = false;
        public static bool bDefaultSupportingCategory = false;
        public static bool bDefaultThirdPartyCategory = false;
        public static bool bDefaultIgnoredCategory = false;
    }
}
