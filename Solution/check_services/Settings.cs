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
        public static string[] Categories = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] WarnCategories = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };

        public static bool bDefaultIncludeList = true;
        public static bool bDefaultExcludeList = true;
        public static bool bDefaultStoppedList = true;
        public static bool bDefaultRunningList = true;
        public static bool bDefaultWarnCategoriesList = true;
        public static bool bDefaultCategoriesList = true;

        public static string[] services_in_system_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_essential_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_role_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_supporting_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_thirdparty_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };
        public static string[] services_in_ignored_category = new string[] { "thisshouldprobablyneverbeoverwrittenbysomething" };

        public static bool bDefaultSystemCategory = true;
        public static bool bDefaultEssentialCategory = true;
        public static bool bDefaultRoleCategory = true;
        public static bool bDefaultSupportingCategory = true;
        public static bool bDefaultThirdPartyCategory = true;
        public static bool bDefaultIgnoredCategory = true;
    }
}