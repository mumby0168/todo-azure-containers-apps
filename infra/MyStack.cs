using Pulumi;
using Pulumi.AzureNative.ContainerRegistry;
using Pulumi.AzureNative.DocumentDB;
using Pulumi.AzureNative.DocumentDB.Inputs;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web.V20210301;
using Pulumi.AzureNative.Web.V20210301.Inputs;
using ToBeDone.Helpers;
using Pulumi.AzureNative.OperationalInsights;
using Pulumi.AzureNative.OperationalInsights.Inputs;
using SkuArgs = Pulumi.AzureNative.ContainerRegistry.Inputs.SkuArgs;

class MyStack : Stack
{
    public MyStack()
    {
        var rg = new ResourceGroup(ResourceNameHelper.AppendEnvWithDash("rg-tobedone"), new ResourceGroupArgs
        {
            ResourceGroupName = ResourceNameHelper.AppendEnvWithDash("rg-tobedone")
        });

        var workspace = new Workspace(ResourceNameHelper.AppendEnvWithDash("law-tobedone"), new WorkspaceArgs
        {
            WorkspaceName = ResourceNameHelper.AppendEnvWithDash("law-tobedone"),
            ResourceGroupName = rg.Name,
            Sku = new WorkspaceSkuArgs {Name = "PerGB2018"},
            RetentionInDays = 30,
        });

        var workspaceSharedKeys = Output.Tuple(rg.Name, workspace.Name).Apply(items =>
            GetSharedKeys.InvokeAsync(new GetSharedKeysArgs
            {
                ResourceGroupName = items.Item1,
                WorkspaceName = items.Item2,
            }));
        
        
        var acaEnv = new KubeEnvironment(ResourceNameHelper.AppendEnvWithDash("ace-tobedone"), new KubeEnvironmentArgs
        {
            Name = ResourceNameHelper.AppendEnvWithDash("ace-tobedone"),
            ResourceGroupName = rg.Name,
            Type = "Managed",
            AppLogsConfiguration = new AppLogsConfigurationArgs
            {
                Destination = "log-analytics",
                LogAnalyticsConfiguration = new LogAnalyticsConfigurationArgs
                {
                    CustomerId = workspace.CustomerId,
                    SharedKey = workspaceSharedKeys.Apply(r => r.PrimarySharedKey)
                }
            }
        });
        
        var workspaceServiceApp = CreateApp("work-service", rg, acaEnv, "ghcr.io/mumby0168/to-be-done/work-service:latest");
        var workServiceApp = CreateApp("workspaces-service", rg, acaEnv, "ghcr.io/mumby0168/to-be-done/workspaces-service:latest");
        
        WorkspaceAppUrl = Output.Format($"https://{workspaceServiceApp.Configuration.Apply(c => c.Ingress).Apply(i => i.Fqdn)}");
        WorkAppUrl = Output.Format($"https://{workServiceApp.Configuration.Apply(c => c.Ingress).Apply(i => i.Fqdn)}");
    }

    private static ContainerApp CreateApp(string appName, ResourceGroup rg, KubeEnvironment acaEnv, string imageName)
    {
        var workspaceServiceApp = new ContainerApp(appName, new ContainerAppArgs
        {
            Name = appName,
            ResourceGroupName = rg.Name,
            KubeEnvironmentId = acaEnv.Id,
            Configuration = new ConfigurationArgs
            {
                Ingress = new IngressArgs
                {
                    External = true,
                    TargetPort = 80
                },
            },
            Template = new TemplateArgs
            {
                Containers =
                {
                    new ContainerArgs
                    {
                        Name = ResourceNameHelper.AppendEnvWithDash(appName),
                        Image = imageName,
                        Resources = new ContainerResourcesArgs()
                        {
                            Cpu = 0.5,
                            Memory = "1.0Gi"
                        }
                    },
                },
                Scale = new ScaleArgs()
                {
                    MinReplicas = 0,
                    MaxReplicas = 1
                },
            }
        });
        return workspaceServiceApp;
    }

    public Output<string> WorkAppUrl { get; set; }
    
    public Output<string> WorkspaceAppUrl { get; set; }
}