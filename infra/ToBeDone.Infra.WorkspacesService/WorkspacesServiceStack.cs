using System;
using Pulumi;
using Pulumi.AzureNative.Web.V20210301;
using Pulumi.AzureNative.Web.V20210301.Inputs;
using ToBeDone.Infra.Shared.Helpers;

class WorkspacesServiceStack : Stack
{
    public WorkspacesServiceStack()
    {
        var config = new Config();

        var coreStack = new StackReference(config.Require("coreStackRef"));

        string appName = ResourceNameHelper.AppendEnvWithDash("workspaces-service");

        var app = new ContainerApp(appName, new ContainerAppArgs
        {
            Name = appName,
            ResourceGroupName = coreStack.RequireOutput("resourceGroupName").Apply(x => x.ToString()!),
            KubeEnvironmentId = coreStack.RequireOutput("kubeEnvId").Apply(x => x.ToString()!),
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
                        Image = config.Require("image"),
                        Resources = new ContainerResourcesArgs()
                        {
                            Cpu = 0.5,
                            Memory = "1.0Gi"
                        },
                        Env = new InputList<EnvironmentVarArgs>()
                        {
                            new EnvironmentVarArgs()
                            {
                                Name = "ApplicationInsights__InstrumentationKey",
                                Value = coreStack.RequireOutput("instrumentationKey").Apply(x => x.ToString()!),
                            }
                        }
                    },
                },
                Scale = new ScaleArgs()
                {
                    MinReplicas = 0,
                    MaxReplicas = 1,
                },
            }
        });
    }
}