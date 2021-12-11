using Pulumi;

namespace ToBeDone.Infra.Shared.Helpers;

public class ResourceNameHelper
{
    public static string AppendEnvWithDash(string name) =>
        $"{name}-{Deployment.Instance.StackName}";
    
    public static string AppendEnv(string name) =>
        $"{name}-{Deployment.Instance.StackName}";
}