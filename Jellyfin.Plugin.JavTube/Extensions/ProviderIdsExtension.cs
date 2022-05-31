using Jellyfin.Plugin.JavTube.Models;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JavTube.Extensions;

internal static class ProviderIdsExtension
{
    public static ProviderIdModel GetProviderIdModel(this IHasProviderIds instance, string name)
    {
        return !instance.ProviderIds.Any()
            ? new ProviderIdModel()
            : ProviderIdModel.Deserialize(instance.GetProviderId(name));
    }

    public static void SetProviderIdModel(this IHasProviderIds instance, string name, ProviderIdModel pid)
    {
        instance.SetProviderId(name, pid.Serialize());
    }
}