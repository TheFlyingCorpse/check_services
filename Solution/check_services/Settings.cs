namespace check_services
{
    public class Settings
    {
        // Program Settings
        public static bool bDebug;

        public static bool bVerbose;
        public static bool bDoShowHelp;
        public static bool bDoInventory;
        public static bool bDoCheckServices;
        public static bool bDoInvAllRunningOnly;
        public static bool bDoCheckAllStartTypes;
        public static bool bDoHideLongOutput;
        public static bool bDoHideCategoryFromOuput;
        public static bool bDoHideEmptyVars;
        public static bool bDoSingleCheck;

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

        public static int iDelayedGraceDuration = 60;
        public static string strInventoryFormat = "readable";
        public static string strInventoryLevel = "normal";
        public static string strExpectedState = "Running";
        public static string strSplitBy = ",";

        public static string strCategoryFilePath = "unspecified";
        public static string strCategoryFileFormat = "CSV";
    }
}