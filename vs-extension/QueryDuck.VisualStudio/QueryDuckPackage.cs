using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace QueryDuck.VisualStudio;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[Guid(QueryDuckPackage.PackageGuidString)]
// Docked as a tab alongside Solution Explorer (its well-known window GUID).
[ProvideToolWindow(typeof(QueryDuckToolWindow), Style = VsDockStyle.Tabbed, Window = "3ae79031-e1bc-11d0-8f78-00a0c9110057")]
public sealed class QueryDuckPackage : AsyncPackage
{
    public const string PackageGuidString = "8f3c2a1b-4d5e-6f70-8192-a3b4c5d6e7f8";

    protected override Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress) =>
        Task.CompletedTask;
}
