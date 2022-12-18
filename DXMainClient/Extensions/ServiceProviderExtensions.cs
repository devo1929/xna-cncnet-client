using Microsoft.Extensions.DependencyInjection;
using Rampastring.XNAUI.XNAControls;

namespace DTAClient.Extensions;

public static class ServiceProviderExtensions
{
    public static T GetControl<T>(this ServiceProvider serviceProvider) where T : XNAControl
        => serviceProvider.GetService<T>();
}