namespace RealmlyCompanion;

internal static class CompanionDistribution
{
#if REALMLY_STORE
    internal static bool UsesMicrosoftStore => true;
#else
    internal static bool UsesMicrosoftStore => false;
#endif
    internal static string Name => UsesMicrosoftStore ? "Microsoft Store" : "Portable development";
}
