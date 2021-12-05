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
        var rg = new ResourceGroup(ResourceNameHelper.AppendEnvWithDash("rg-tobedone"));

        var cosmosAccount = new DatabaseAccount(ResourceNameHelper.AppendEnvWithDash("cosmos-tobedone"),
            new DatabaseAccountArgs()
            {
                ResourceGroupName = rg.Name,
                DatabaseAccountOfferType = DatabaseAccountOfferType.Standard,
                Locations =
                {
                    new LocationArgs
                    {
                        LocationName = rg.Location,
                        FailoverPriority = 0,
                    },
                },
                ConsistencyPolicy = new ConsistencyPolicyArgs
                {
                    DefaultConsistencyLevel = DefaultConsistencyLevel.ConsistentPrefix
                }
            });

        var workServiceDb = new SqlResourceSqlDatabase("work-service-db", new SqlResourceSqlDatabaseArgs
        {
            ResourceGroupName = rg.Name,
            AccountName = cosmosAccount.Name,
            Resource = new SqlDatabaseResourceArgs()
            {
                Id = "work-service-db"
            }
        });

        var workspaceServiceDb = new SqlResourceSqlDatabase("workspace-service-db", new SqlResourceSqlDatabaseArgs
        {
            ResourceGroupName = rg.Name,
            AccountName = cosmosAccount.Name,
            Resource = new SqlDatabaseResourceArgs()
            {
                Id = "workspace-service-db"
            }
        });

        var workspace = new Workspace(ResourceNameHelper.AppendEnvWithDash("lwsp-tobedone"), new WorkspaceArgs
        {
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
        ;
        var acaEnv = new KubeEnvironment(ResourceNameHelper.AppendEnvWithDash("ace-tobedone"), new KubeEnvironmentArgs
        {
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

        var acr = new Registry(ResourceNameHelper.AppendEnv("acrtobedone"), new RegistryArgs()
        {
            ResourceGroupName = rg.Name,
            Sku = new SkuArgs {Name = "Basic"},
            AdminUserEnabled = true
        });

        var credentials = Output.Tuple(rg.Name, acr.Name).Apply(items =>
            ListRegistryCredentials.InvokeAsync(new ListRegistryCredentialsArgs
            {
                ResourceGroupName = items.Item1,
                RegistryName = items.Item2
            }));

        var acrUsername = credentials.Apply(c => c.Username);
        var acrPassword = credentials.Apply(c => c.Passwords[0].Value);

        
        //TODO: Need to define the container image here
        // var workspaceServiceApp = CreateApp("workspace-service", rg, acaEnv, acr, acrUsername, acrPassword);
        // var workServiceApp = CreateApp("work-service", rg, acaEnv, acr, acrUsername, acrPassword);
        //
        // WorkspaceAppUrl = Output.Format($"https://{workspaceServiceApp.Configuration.Apply(c => c.Ingress).Apply(i => i.Fqdn)}");
        // WorkAppUrl = Output.Format($"https://{workServiceApp.Configuration.Apply(c => c.Ingress).Apply(i => i.Fqdn)}");

        WorkDbName = workServiceDb.Name;
        WorkspaceDbName = workspaceServiceDb.Name;
    }

    private static ContainerApp CreateApp(string appName, ResourceGroup rg, KubeEnvironment acaEnv, Registry acr,
        Output<string?> acrUsername,
        Output<string?> acrPassword)
    {
        var workspaceServiceApp = new ContainerApp(appName, new ContainerAppArgs
        {
            ResourceGroupName = rg.Name,
            KubeEnvironmentId = acaEnv.Id,
            Configuration = new ConfigurationArgs
            {
                Ingress = new IngressArgs
                {
                    External = true,
                    TargetPort = 80
                },
                Registries =
                {
                    new RegistryCredentialsArgs
                    {
                        Server = acr.LoginServer,
                        Username = acrUsername!,
                        PasswordSecretRef = "pwd"
                    }
                },
                Secrets =
                {
                    new SecretArgs
                    {
                        Name = "pwd",
                        Value = acrPassword!
                    }
                },
            },
            Template = new TemplateArgs
            {
                Containers =
                {
                    new ContainerArgs
                    {
                        Name = appName,
                    }
                }
            }
        });
        return workspaceServiceApp;
    }

    public Output<string> WorkDbName { get; set; }

    public Output<string> WorkAppUrl { get; set; }

    public Output<string> WorkspaceDbName { get; set; }

    public Output<string> WorkspaceAppUrl { get; set; }
}