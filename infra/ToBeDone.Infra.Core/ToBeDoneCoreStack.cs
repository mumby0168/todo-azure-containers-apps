using System;
using Pulumi;
using Pulumi.AzureAD;
using Pulumi.AzureAD.Inputs;
using Pulumi.AzureNative.AzureActiveDirectory;
using Pulumi.AzureNative.AzureActiveDirectory.Inputs;
using Pulumi.AzureNative.Insights;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Web.V20210301;
using Pulumi.AzureNative.Web.V20210301.Inputs;
using Pulumi.AzureNative.OperationalInsights;
using Pulumi.AzureNative.OperationalInsights.Inputs;
using ToBeDone.Infra.Shared.Helpers;
using Config = Pulumi.Config;

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
        
        var current = Output.Create(GetClientConfig.InvokeAsync());

        const string serverAccessScopeName = "ToBeDoneServer.Access";

        var serverApp = new Application(ResourceNameHelper.AppendEnvWithDash("app-tobedone-server"),
            new ApplicationArgs()
            {
                DisplayName = ResourceNameHelper.AppendEnvWithDash("app-tobedone-web-server"),
                Web = new ApplicationWebArgs()
                {
                },
                IdentifierUris = new InputList<string>()
                {
                    $"api://{ResourceNameHelper.AppendEnvWithDash("app-tobedone-server")}.net"
                },
                Api = new ApplicationApiArgs()
                {
                    Oauth2PermissionScopes = new InputList<ApplicationApiOauth2PermissionScopeArgs>()
                    {
                        new ApplicationApiOauth2PermissionScopeArgs()
                        {
                            Id = Guid.NewGuid().ToString(),
                            Value = serverAccessScopeName,
                            AdminConsentDisplayName = serverAccessScopeName,
                            AdminConsentDescription = "Allow access to the server app",
                            Enabled = true,
                        }
                    }
                }
            });
        
        var serverServicePrincipal = new ServicePrincipal("spn-tobedone-server", new ServicePrincipalArgs
        {
            ApplicationId = serverApp.ApplicationId,
            AppRoleAssignmentRequired = false,
            Owners = 
            {
                current.Apply(c => c.ObjectId),
            },
        });

        var clientApp = new Application(ResourceNameHelper.AppendEnvWithDash("app-tobedone-spa"),
            new ApplicationArgs()
            {
                DisplayName = ResourceNameHelper.AppendEnvWithDash("app-tobedone-web-spa"),
                SinglePageApplication = new ApplicationSinglePageApplicationArgs()
                {
                    RedirectUris = new InputList<string>()
                    {
                        "https://localhost:7051/authentication/login-callback",
                    }
                },
                IdentifierUris = new InputList<string>()
                {
                    $"api://{ResourceNameHelper.AppendEnvWithDash("app-tobedone-spa")}.net"
                },
                AppRoles = new InputList<ApplicationAppRoleArgs>()
                {
                    // define roles in the app here - none so far
                },
                RequiredResourceAccesses = new InputList<ApplicationRequiredResourceAccessArgs>()
                {
                    new ApplicationRequiredResourceAccessArgs()
                    {
                        ResourceAppId = "00000003-0000-0000-c000-000000000000", //Microsoft Graph ID
                        ResourceAccesses = new InputList<ApplicationRequiredResourceAccessResourceAccessArgs>()
                        {
                            new ApplicationRequiredResourceAccessResourceAccessArgs()
                            {
                                Id = "e1fe6dd8-ba31-4d61-89e7-88639da4683d", //User.Read delegate permissions id
                                Type = "Scope"
                            }
                        }
                    },
                    new ApplicationRequiredResourceAccessArgs()
                    {
                        ResourceAppId = serverApp.ApplicationId,
                        ResourceAccesses = new InputList<ApplicationRequiredResourceAccessResourceAccessArgs>()
                        {
                            new ApplicationRequiredResourceAccessResourceAccessArgs()
                            {
                                Id = serverApp.Oauth2PermissionScopeIds.Apply(x => x[serverAccessScopeName]),
                                Type = "Scope"
                            }
                        }
                    }
                }
            });
        
        var spaServicePrincipal = new ServicePrincipal("spn-tobedone-spa", new ServicePrincipalArgs
        {
            ApplicationId = clientApp.ApplicationId,
            AppRoleAssignmentRequired = false,
            Owners = 
            {
                current.Apply(c => c.ObjectId),
            },
        });
    }

    [Output("kubeEnvId")] public Output<string> KubeEnvId { get; set; }

    [Output("instrumentationKey")] public Output<string> InstrumentationKey { get; set; }

    [Output("resourceGroupName")] public Output<string> ResourceGroupName { get; set; }
}