using System;
using Pulumi;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web.V20210301;
using Pulumi.AzureNative.Web.V20210301.Inputs;
using Pulumi.AzureNative.OperationalInsights;
using Pulumi.AzureNative.OperationalInsights.Inputs;
using ToBeDone.Infra.Shared.Helpers;

class ToBeDoneCoreStack : Stack
{
    public ToBeDoneCoreStack()
    {
        var config = new Config();
        
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

        var insights = new Component(ResourceNameHelper.AppendEnvWithDash("ai-tobedone"),
            new ComponentArgs()
            {
                ResourceName = ResourceNameHelper.AppendEnvWithDash("ai-tobedone"),
                Kind = "web",
                ResourceGroupName = rg.Name,
            });

        var instrumentationKey = insights.InstrumentationKey.Apply(x => x);

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
                    SharedKey = workspaceSharedKeys.Apply(r => r.PrimarySharedKey!)
                }
            }
        });

        ResourceGroupName = rg.Name;
        KubeEnvId = acaEnv.Id;
        InstrumentationKey = instrumentationKey;
    }

    [Output("kubeEnvId")]
    public Output<string> KubeEnvId { get; set; }
    
    [Output("instrumentationKey")]
    public Output<string> InstrumentationKey { get; set; }
    
    [Output("resourceGroupName")]
    public Output<string> ResourceGroupName { get; set; }
}